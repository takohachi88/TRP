using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Experimental.Rendering;

namespace Trp
{
	/// <summary>
	/// DepthTextureとNormalsTextureの生成。
	/// </summary>
	internal class DepthNormalsPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.DepthNormals);
		private static readonly ShaderTagId ShaderTagId = new("DepthNormalsOnly");
		private class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams)
		{
			if (!passParams.UseDepthNormalsTexture) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.RendererListHandle = renderGraph.CreateRendererList(
			new RendererListDesc(ShaderTagId, passParams.CullingResults, passParams.Camera)
			{
				sortingCriteria = SortingCriteria.CommonOpaque,
				rendererConfiguration = PerObjectData.None,
				renderQueueRange = RenderQueueRange.all,
				renderingLayerMask = (uint)passParams.RenderingLayerMask,
			});

			TextureHandle normals = renderGraph.CreateTexture(new TextureDesc(passParams.AttachmentSize.x, passParams.AttachmentSize.y)
			{
				colorFormat = GraphicsFormat.R8G8B8A8_SNorm,
				name = "CameraNormalsTexture",
			});
			passParams.CameraTextures.TextureNormals = normals;
			builder.SetRenderAttachment(normals, 0, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(normals, TrpConstants.ShaderIds.CameraNormalsTexture);

			TextureHandle depth = renderGraph.CreateTexture(new TextureDesc(passParams.AttachmentSize.x, passParams.AttachmentSize.y)
			{
				colorFormat = GraphicsFormat.D32_SFloat,
				name = "CameraDepthTexture",
			});
			passParams.CameraTextures.TextureDepth = depth;
			builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(depth, TrpConstants.ShaderIds.CameraDepthTexture);

			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}