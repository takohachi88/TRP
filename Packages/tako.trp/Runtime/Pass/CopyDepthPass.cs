using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using TakoLib.Common.Extensions;

namespace Trp
{
	/// <summary>
	/// Depthをコピーする。
	/// </summary>
	internal class CopyDepthPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.CopyDepth);

		private readonly Material _copyDepthMaterial;

		private static readonly GlobalKeyword KeywordOutputDepth = GlobalKeyword.Create("_OUTPUT_DEPTH");

		public enum CopyDepthMode
		{
			ToDepthTexture,
			ToTarget,
		}

		internal CopyDepthPass(Material coreBlitMaterial)
		{
			_copyDepthMaterial = coreBlitMaterial;
		}

		public class PassData
		{
			public TextureHandle Src;
			public Material Material;
			public Camera Camera;
			public CopyDepthMode Mode;
		}

		internal void RecordRenderGraph(ref PassParams passParams, CopyDepthMode mode)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			CameraTextures cameraTextures = passParams.CameraTextures;

			passData.Mode = mode;

			switch (mode)
			{
				//DepthTextureへのコピー。
				case CopyDepthMode.ToDepthTexture:
					passData.Src = cameraTextures.AttachmentDepth;

					if (passParams.IsSceneViewOrPreview) builder.SetRenderAttachmentDepth(cameraTextures.TextureDepth, AccessFlags.WriteAll);
					else builder.SetRenderAttachment(cameraTextures.TextureDepth, 0, AccessFlags.WriteAll);

					builder.SetGlobalTextureAfterPass(cameraTextures.TextureDepth, TrpConstants.ShaderIds.CameraDepthTexture);
					break;
				//TargetDepthへのコピー。現状はシーンのGizmoやgridの適切な描画のために必要な処理。
				case CopyDepthMode.ToTarget:
					passData.Src = cameraTextures.AttachmentDepth;
					builder.SetRenderAttachmentDepth(cameraTextures.TargetDepth, AccessFlags.WriteAll);
					//何かセットしないと警告が出る。
					builder.SetRenderAttachment(cameraTextures.TargetColor, 0, AccessFlags.Write);
					break;
			}
			passData.Material = _copyDepthMaterial;
			passData.Camera = passParams.Camera;

			builder.UseTexture(passData.Src, AccessFlags.Read);
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				RTHandle src = passData.Src;
				Material material = passData.Material;
				bool dstIsTarget = passData.Mode == CopyDepthMode.ToTarget;

				Vector2 viewportScale = src.useScaling ? src.rtHandleProperties.rtHandleScale : Vector2.one;
				Vector4 scaleBias = new(viewportScale.x, viewportScale.y, 0, 0);

				//TODO: MSAA対応。CopyDepthシェーダー側は対応済み。

				context.cmd.SetKeyword(KeywordOutputDepth, dstIsTarget);
				material.SetTexture(TrpConstants.ShaderIds.DepthAttachment, src);
				material.SetFloat(TrpConstants.ShaderIds.ZWrite, dstIsTarget.ToInt());
				Blitter.BlitTexture(context.cmd, passData.Src, scaleBias, material, 0);
			});
		}
	}
}