using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace Trp
{
	/// <summary>
	/// 中間バッファからゲーム画面へ描画をする。
	/// </summary>
	internal class FinalBlitPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.FinalBlit);

		private class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public BlendMode BlendSrc;
			public BlendMode BlendDst;
		}

		public void RecordRenderGraph(ref PassParams passParams, TextureHandle src, BlendMode blendSrc, BlendMode blendDst)
		{
			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			TextureHandle dst = passParams.CameraTextures.TargetColor;

			passData.Src = src;
			passData.Dst = dst;
			passData.Camera = passParams.Camera;
			passData.BlendSrc = blendSrc;
			passData.BlendDst= blendDst;
			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				RenderingUtils.BlitToCamera(context.cmd, passData.Src, passData.Dst, passData.Camera, passData.BlendSrc, passData.BlendDst, passData.Camera.pixelRect);
			});
		}
	}
}