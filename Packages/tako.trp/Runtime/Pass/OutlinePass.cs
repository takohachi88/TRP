using Unity.Profiling.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine;

namespace Trp
{
	/// <summary>
	/// ジオメトリの描画。
	/// </summary>
	internal class OutlinePass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Create(nameof(OutlinePass), MarkerFlags.Default);
		private static readonly ShaderTagId ShaderTagId = new(TrpConstants.PassNames.OUTLINE);
		private static readonly int IdTanFov = Shader.PropertyToID("_TanFov");

		private class PassData
		{
			public float TanFov;
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(nameof(OutlinePass), out PassData passData, Sampler);
			LightingResources lightingResources = passParams.LightingResources;

			builder.UseAllGlobalTextures(true);
			if (lightingResources.DirectionalShadowMap.IsValid()) builder.UseTexture(lightingResources.DirectionalShadowMap, AccessFlags.Read);
			if (lightingResources.PunctualShadowMap.IsValid()) builder.UseTexture(lightingResources.PunctualShadowMap, AccessFlags.Read);
			if (lightingResources.DirectionalLightDataBuffer.IsValid()) builder.UseBuffer(lightingResources.DirectionalLightDataBuffer, AccessFlags.Read);
			if (lightingResources.PunctualLightDataBuffer.IsValid()) builder.UseBuffer(lightingResources.PunctualLightDataBuffer, AccessFlags.Read);
			if (lightingResources.ForwardPlusTileBuffer.IsValid()) builder.UseBuffer(lightingResources.ForwardPlusTileBuffer, AccessFlags.Read);
			if (lightingResources.PunctualShadowDataBuffer.IsValid()) builder.UseBuffer(lightingResources.PunctualShadowDataBuffer, AccessFlags.Read);

			passData.TanFov = Mathf.Tan(passParams.Camera.fieldOfView / 180f);
			passData.RendererListHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagId, passParams.CullingResults, passParams.Camera)
				{
					layerMask = passParams.CommonSettings.OpaqueLayerMask,

					sortingCriteria = SortingCriteria.CommonOpaque,

					//rendererConfiguration =
					//PerObjectData.ReflectionProbes, //TODO: 必要か検討。

					renderQueueRange = RenderQueueRange.opaque,

					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderAttachment(passParams.CameraTextures.AttachmentColor, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(passParams.CameraTextures.AttachmentDepth, AccessFlags.Write);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				IRasterCommandBuffer cmd = context.cmd;
				cmd.SetGlobalFloat(IdTanFov, passData.TanFov);
				cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}
