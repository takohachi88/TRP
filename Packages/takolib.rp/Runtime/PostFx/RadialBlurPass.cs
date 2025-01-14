using Mono.Cecil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp.PostFx
{
	/// <summary>
	/// RadialBlurのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "RadialBlur")]
	public class RadialBlurPass : PostFxPassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
		private static readonly int IdBlurIntensity = Shader.PropertyToID("_BlurIntensity");
		private static readonly int IdCenter = Shader.PropertyToID("_Center");
		private static readonly int IdSampleCount = Shader.PropertyToID("_SampleCount");
		private static readonly int IdChromaticAberrationIntensity = Shader.PropertyToID("_ChromaticAberrationIntensity");
		private static readonly int IdNoiseGradientTexture = Shader.PropertyToID("_NoiseGradientTexture");
		private static readonly int IdNoiseTiling = Shader.PropertyToID("_NoiseTiling");
		private static readonly int IdNoiseIntensity = Shader.PropertyToID("_NoiseIntensity");
		private static readonly int IdChromaticAberrationLimit = Shader.PropertyToID("_ChromaticAberrationLimit");


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
			public RadialBlur RadialBlur;
			public LocalKeyword KeywordDither;
			public LocalKeyword KeywordNoiseGradientTexture;
			public bool UseChromaticAberration;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			RadialBlur radialBlur = volumeStack.GetComponent<RadialBlur>();
			if (!radialBlur || !radialBlur.IsActive()) return LastTarget.None;

			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Material = PassMaterial;
			passData.Camera = passParams.Camera;
			passData.RadialBlur = radialBlur;
			passData.KeywordDither = _keywordDither;
			passData.KeywordNoiseGradientTexture = _keywordNoiseGradientTexture;
			passData.UseChromaticAberration = 0 < radialBlur.chromaticAberrationIntensity.value;

			builder.UseTexture(src, passData.UseChromaticAberration ? AccessFlags.ReadWrite : AccessFlags.Read);
			builder.UseTexture(dst, passData.UseChromaticAberration ? AccessFlags.ReadWrite : AccessFlags.Write);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				RadialBlur radialBlur = passData.RadialBlur;
				Material material = passData.Material;
				TextureHandle src = passData.Src;
				TextureHandle dst = passData.Dst;
				RenderBufferLoadAction load = RenderBufferLoadAction.DontCare;
				RenderBufferStoreAction store = RenderBufferStoreAction.Store;

				material.SetFloat(IdIntensity, radialBlur.intensity.value);
				material.SetFloat(IdBlurIntensity, radialBlur.blurIntensity.value);
				material.SetVector(IdCenter, radialBlur.center.value);
				material.SetFloat(IdSampleCount, radialBlur.sampleCount.value);
				material.SetFloat(IdChromaticAberrationIntensity, radialBlur.chromaticAberrationIntensity.value);
				material.SetKeyword(passData.KeywordDither, radialBlur.dither.value);
				material.SetKeyword(passData.KeywordNoiseGradientTexture, radialBlur.noiseGradientTexture.value);
				
				if (radialBlur.noiseGradientTexture.value)
				{
					material.SetTexture(IdNoiseGradientTexture, radialBlur.noiseGradientTexture.value);
					material.SetFloat(IdNoiseTiling, radialBlur.noiseTiling.value);
					material.SetFloat(IdNoiseIntensity, radialBlur.noiseIntensity.value);
				}

				//色収差がある場合。
				//色収差のポストエフェクトは別であるが、RadialBlurのCenterと位置を合わせたいことが多いため、RadialBlur側にも実装してしまう。
				if (passData.UseChromaticAberration)
				{
					material.SetFloat(IdChromaticAberrationLimit, radialBlur.chromaticAberrationLimit.value);
					Blitter.BlitTexture(cmd, src, dst, load, store, material, 1);
					Blitter.BlitTexture(cmd, dst, src, load, store, material, 0);
				}
				else //色収差がない場合。
				{
					Blitter.BlitCameraTexture(cmd, src, dst, load, store, material, 0);
				}
			});

			return passData.UseChromaticAberration ? LastTarget.Src : LastTarget.Dst;
		}
	}
}