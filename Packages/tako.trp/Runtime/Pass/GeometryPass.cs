using Unity.Profiling.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

namespace Trp
{
	/// <summary>
	/// ジオメトリの描画。
	/// </summary>
	internal class GeometryPass
	{
		private static readonly ProfilingSampler SamplerOpaque = ProfilingSampler.Create(nameof(GeometryPass) + ".Opaque", MarkerFlags.Default);
		private static readonly ProfilingSampler SamplerTransparent = ProfilingSampler.Create(nameof(GeometryPass) + ".Transparent", MarkerFlags.Default);

		private static readonly ShaderTagId[] ShaderTagIds =
		{
			new (TrpConstants.PassNames.SRP_DEFAULT_UNLIT),
			new (TrpConstants.PassNames.LIT),
		};

		private class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams, bool isOpaque)
		{
			ProfilingSampler sampler = isOpaque ? SamplerOpaque : SamplerTransparent;
			string passName = isOpaque ? SamplerOpaque.name : SamplerTransparent.name;

			CameraTextures cameraTextures = passParams.CameraTextures;
			RenderGraph renderGraph = passParams.RenderGraph;
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out PassData passData, sampler);
			LightingResources lightingResources = passParams.LightingResources;

			// Renderer-list shaders can consume textures through global bindings. Declaring this
			// dependency keeps SetGlobalTextureAfterPass resources alive until this draw.
			builder.UseAllGlobalTextures(true);
			if (lightingResources.DirectionalShadowMap.IsValid()) builder.UseTexture(lightingResources.DirectionalShadowMap, AccessFlags.Read);
			if (lightingResources.PunctualShadowMap.IsValid()) builder.UseTexture(lightingResources.PunctualShadowMap, AccessFlags.Read);
			if (lightingResources.DirectionalLightDataBuffer.IsValid()) builder.UseBuffer(lightingResources.DirectionalLightDataBuffer, AccessFlags.Read);
			if (lightingResources.PunctualLightDataBuffer.IsValid()) builder.UseBuffer(lightingResources.PunctualLightDataBuffer, AccessFlags.Read);
			if (lightingResources.ForwardPlusTileBuffer.IsValid()) builder.UseBuffer(lightingResources.ForwardPlusTileBuffer, AccessFlags.Read);
			if (lightingResources.PunctualShadowDataBuffer.IsValid()) builder.UseBuffer(lightingResources.PunctualShadowDataBuffer, AccessFlags.Read);

			passData.RendererListHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIds, passParams.CullingResults, passParams.Camera)
				{
					layerMask = isOpaque ? passParams.CommonSettings.OpaqueLayerMask : passParams.CommonSettings.TransparentLayerMask,

					sortingCriteria = isOpaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,

					rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask,

					renderQueueRange = isOpaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,

					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowPassCulling(false);
			builder.SetRenderAttachment(cameraTextures.AttachmentColor, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(cameraTextures.AttachmentDepth, AccessFlags.Write);

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}
