using Mono.Cecil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// Bloomのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Bloom")]
	public class BloomPass : PostFxPassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");


		private LocalKeyword _keywordDither;
		private LocalKeyword _keywordNoiseGradientTexture;

		protected override void OnInitialize()
		{
			_keywordDither = new(PassShader, "_DITHER");
			_keywordNoiseGradientTexture = new(PassShader, "_NOISE_GRADIENT_TEXTURE");
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public Bloom Bloom;
			public LocalKeyword KeywordDither;
			public LocalKeyword KeywordNoiseGradientTexture;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Bloom bloom = volumeStack.GetComponent<Bloom>();
			if (!bloom || !bloom.IsActive()) return LastTarget.None;

			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Material = PassMaterial;
			passData.Camera = passParams.Camera;
			passData.Bloom = bloom;

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				Bloom radialBlur = passData.Bloom;
				Material material = passData.Material;
				TextureHandle src = passData.Src;
				TextureHandle dst = passData.Dst;
				RenderBufferLoadAction load = RenderBufferLoadAction.DontCare;
				RenderBufferStoreAction store = RenderBufferStoreAction.Store;

			});

			return LastTarget.Dst;
		}
	}
}