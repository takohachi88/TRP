using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// Blurのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Blur", fileName = nameof(BlurPass))]
	public class BlurPass : PostFxSinglePassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

		protected override void OnInitialize()
		{
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public Blur Blur;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Blur blur = volumeStack.GetComponent<Blur>();
			if (!blur || !blur.IsActive()) return LastTarget.None;

			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Material = PassMaterial;
			passData.Camera = passParams.Camera;

			builder.UseTexture(src, AccessFlags.ReadWrite);
			builder.UseTexture(dst, AccessFlags.ReadWrite);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				Blur blur = passData.Blur;
				Material material = passData.Material;
				TextureHandle src = passData.Src;
				TextureHandle dst = passData.Dst;
				//TODO: 実装
			});

			return LastTarget.Dst;
		}
	}
}