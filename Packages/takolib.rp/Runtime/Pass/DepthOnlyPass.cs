using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

namespace TakoLib.Rp
{
	/// <summary>
	/// DepthOnlyパスの描画。
	/// </summary>
	internal class DepthOnlyPass
	{
		private static readonly int IdCameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.DepthOnly);
		private static readonly ShaderTagId ShaderTagId = new("DepthOnly");
		private class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams, TextureHandle dst)
		{
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
			
			builder.SetRenderAttachmentDepth(dst, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(dst, IdCameraDepthTexture);
			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}