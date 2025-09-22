using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// Depthをコピーする。
	/// </summary>
	internal class CopyDepthPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.CopyDepth);

		private readonly Material _copyDepthToTargetMaterial;

		private static readonly GlobalKeyword KeywordOutputDepth = GlobalKeyword.Create("_OUTPUT_DEPTH");

		internal CopyDepthPass(Shader copyDepthShader)
		{
			_copyDepthToTargetMaterial = CoreUtils.CreateEngineMaterial(copyDepthShader);
		}

		internal void Dispose()
		{
			CoreUtils.Destroy(_copyDepthToTargetMaterial);
		}

		public class PassData
		{
			public TextureHandle Src;
			public Material Material;
			public Camera Camera;
			public MSAASamples Msaa;
		}

		internal void RecordRenderGraph(ref PassParams passParams)
		{
			if (!passParams.UseDepthTexture) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			CameraTextures cameraTextures = passParams.CameraTextures;

			//TargetDepthへのコピー。現状はシーンのGizmoやgridの適切な描画のために必要な処理。
			passData.Src = cameraTextures.AttachmentDepth;
			passData.Material = _copyDepthToTargetMaterial;
			passData.Camera = passParams.Camera;
			passData.Msaa = passParams.Msaa;

			builder.SetRenderAttachmentDepth(cameraTextures.TargetDepth, AccessFlags.WriteAll);
			//何かセットしないと警告が出る。
			builder.SetRenderAttachment(cameraTextures.TargetColor, 0, AccessFlags.Write);
			builder.UseTexture(passData.Src, AccessFlags.Read);
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				RTHandle src = passData.Src;
				Material material = passData.Material;

				RenderingUtils.SetDepthMsaa(context.cmd, passData.Msaa);
				context.cmd.SetKeyword(KeywordOutputDepth, true);
				material.SetTexture(TrpConstants.ShaderIds.DepthAttachment, src);
				material.SetFloat(TrpConstants.ShaderIds.ZWrite, 1);
				Blitter.BlitTexture(context.cmd, passData.Src, Vector2.one, material, 0);
			});
		}
	}
}