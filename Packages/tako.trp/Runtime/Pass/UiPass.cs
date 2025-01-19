using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	public class UiPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Ui);

		private class PassData
		{
			public RendererListHandle RendererList;
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			using (IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler))
			{
				passData.RendererList = passParams.RenderGraph.CreateUIOverlayRendererList(passParams.Camera);

				builder.UseAllGlobalTextures(true);
				builder.SetRenderAttachment(passParams.CameraTextures.TargetColor, 0, AccessFlags.Write);
				builder.SetRenderAttachmentDepth(passParams.CameraTextures.TargetDepth, AccessFlags.ReadWrite);
				builder.UseRendererList(passData.RendererList);
				builder.SetRenderFunc<PassData>(static (passData, context) => context.cmd.DrawRendererList(passData.RendererList));
			}

			//TODO: IMGUIの描画もRenderGraphでカバーする？（URPのDrawScreenSpaceUIPassによればIMGUIではカバーしておいた方が安全ではあるらしい。）
		}
	}
}
