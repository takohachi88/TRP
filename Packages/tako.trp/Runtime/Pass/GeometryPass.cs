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
		private static readonly  ProfilingSampler SamplerOpaque = ProfilingSampler.Get(TrpProfileId.OpaqueGeometry);
		private static readonly ProfilingSampler SamplerTransparent = ProfilingSampler.Get(TrpProfileId.TransparentGeometry);

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

			CameraTextures cameraTextures = passParams.CameraTextures;
			RenderGraph renderGraph = passParams.RenderGraph;
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(sampler.name, out PassData passData, sampler);

			passData.RendererListHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIds, passParams.CullingResults, passParams.Camera)
				{
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

			//Transparent描画時、OpaqueTexture等を使うならRead。
			if (!isOpaque)
			{
				if (cameraTextures.TextureOpaque.IsValid()) builder.UseTexture(cameraTextures.TextureOpaque, AccessFlags.Read);
				if (cameraTextures.TextureDepth.IsValid()) builder.UseTexture(cameraTextures.TextureDepth, AccessFlags.Read);
			}


			//TODO: ライティング関係のテクスチャの登録・確保。

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}