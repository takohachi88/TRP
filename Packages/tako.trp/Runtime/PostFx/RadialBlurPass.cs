using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// RadialBlurのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "RadialBlur", fileName = nameof(RadialBlurPass))]
	public class RadialBlurPass : PostFxSinglePassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
		private static readonly int IdBlurIntensity = Shader.PropertyToID("_BlurIntensity");
		private static readonly int IdCenter = Shader.PropertyToID("_Center");
		private static readonly int IdSampleCount = Shader.PropertyToID("_SampleCount");
		private static readonly int IdNoiseGradientTexture = Shader.PropertyToID("_NoiseGradientTexture");
		private static readonly int IdNoiseTiling = Shader.PropertyToID("_NoiseTiling");
		private static readonly int IdNoiseIntensity = Shader.PropertyToID("_NoiseIntensity");


		private LocalKeyword _keywordDither;
		private LocalKeyword _keywordNoiseGradientTexture;

		protected override void OnInitialize()
		{
			_keywordDither = new(PassShader, "_DITHER");
			_keywordNoiseGradientTexture = new(PassShader, "_NOISE_GRADIENT_TEXTURE");
			base.OnInitialize();
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public RadialBlur RadialBlur;
			public LocalKeyword KeywordDither;
			public LocalKeyword KeywordNoiseGradientTexture;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			RadialBlur radialBlur = volumeStack.GetComponent<RadialBlur>();
			if (!radialBlur || !radialBlur.IsActive()) return LastTarget.None;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Material = PassMaterial;
			passData.Camera = passParams.Camera;
			passData.RadialBlur = radialBlur;
			passData.KeywordDither = _keywordDither;
			passData.KeywordNoiseGradientTexture = _keywordNoiseGradientTexture;

			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				RadialBlur radialBlur = passData.RadialBlur;
				Material material = passData.Material;
				TextureHandle src = passData.Src;

				material.SetFloat(IdIntensity, radialBlur.intensity.value);
				material.SetFloat(IdBlurIntensity, radialBlur.blurIntensity.value);
				material.SetVector(IdCenter, radialBlur.center.value);
				int sampleCount = radialBlur.sampleCount.value;
				material.SetVector(IdSampleCount, new(sampleCount, 1f / sampleCount, 1f / (sampleCount - 1f)));
				material.SetKeyword(passData.KeywordDither, radialBlur.dither.value);
				material.SetKeyword(passData.KeywordNoiseGradientTexture, radialBlur.noiseGradientTexture.value);

				if (radialBlur.noiseGradientTexture.value)
				{
					material.SetTexture(IdNoiseGradientTexture, radialBlur.noiseGradientTexture.value);
					material.SetFloat(IdNoiseTiling, radialBlur.noiseTiling.value);
					material.SetFloat(IdNoiseIntensity, radialBlur.noiseIntensity.value);
				}

				Blit(context.cmd, src, material, 0);
			});

			return LastTarget.Dst;
		}
	}
}