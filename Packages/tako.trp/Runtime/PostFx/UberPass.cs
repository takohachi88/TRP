using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// 一部のポストエフェクトは負荷軽減のためまとめて処理する。
	/// 順番を安易に変えられないというデメリットあり。
	/// 一旦以下の順で実行する。
	/// Mosaic
	/// Posterization
	/// LUT
	/// AdvancedVignette
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Uber", fileName = nameof(UberPass))]
	internal class UberPass : PostFxPassBase
	{
		private static readonly int IdMosaicIntensity = Shader.PropertyToID("_MosaicIntensity");
		private static readonly int IdMosaicCellDensity = Shader.PropertyToID("_MosaicCellDensity");

		private static readonly int IdPosterizationIntensity = Shader.PropertyToID("_PosterizationIntensity");
		private static readonly int IdPosterizationToneCount = Shader.PropertyToID("_ToneCount");

		private LocalKeyword _keywordLut;
		private LocalKeyword _keywordVignette;
		private static readonly int IdVignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
		private static readonly int IdVignetteSmoothness = Shader.PropertyToID("_VignetteSmoothness");
		private static readonly int IdVignetteFitAspect = Shader.PropertyToID("_VignetteFitAspect");
		private static readonly int IdVignetteColor = Shader.PropertyToID("_VignetteColor");
		private static readonly int IdVignetteCenter = Shader.PropertyToID("_VignetteCenter");
		private static readonly int IdVignetteBlendMode = Shader.PropertyToID("_VignetteBlendMode");

		private static readonly int IdLutParams = Shader.PropertyToID("_LutParams");
		private static readonly int IdLut = Shader.PropertyToID("_Lut");


		protected override void OnInitialize()
		{
			_keywordLut = new(PassShader, "_LUT");
			_keywordVignette = new(PassShader, "_VIGNETTE");
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public LocalKeyword KeywordLut;
			public LocalKeyword KeywordVignette;
			public bool UseLut;
			public bool UsePosterization;
			public bool UseVignette;
			public bool UseMosaic;
			public Posterization Posterization;
			public AdvancedVignette Vignette;
			public Mosaic Mosaic;
			public int LutSize;
			public TextureHandle Lut;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Mosaic mosaic = volumeStack?.GetComponent<Mosaic>();
			Posterization posterization = volumeStack?.GetComponent<Posterization>();
			AdvancedVignette advancedVignette = volumeStack?.GetComponent<AdvancedVignette>();

			bool useMosaic = mosaic && mosaic.IsActive();
			bool usePosterization = posterization && posterization.IsActive();
			bool useVignette = advancedVignette && advancedVignette.IsActive();
			bool useLut = passParams.CameraTextures.PostProcessLut.IsValid();

			if (!useMosaic &&
				!usePosterization &&
				!useVignette &&
				!useLut)
				return LastTarget.None;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Camera = passParams.Camera;
			passData.Material = PassMaterial;
			passData.KeywordVignette = _keywordVignette;

			passData.UsePosterization = usePosterization;
			passData.UseVignette = useVignette;
			passData.UseMosaic = useMosaic;

			passData.Posterization = posterization;
			passData.Vignette = advancedVignette;
			passData.Mosaic = mosaic;

			passData.KeywordLut = _keywordLut;
			passData.LutSize = passParams.LutSize;
			passData.Lut = passParams.CameraTextures.PostProcessLut;

			passData.UseLut = useLut;
			if (passData.UseLut) builder.UseTexture(passData.Lut, AccessFlags.Read);

			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				Material material = passData.Material;
				material.SetKeyword(passData.KeywordVignette, passData.UseVignette);
				material.SetFloat(IdPosterizationIntensity, passData.Posterization.intensity.value);
				material.SetFloat(IdVignetteIntensity, passData.Vignette.intensity.value * 3f);

				if (passData.UseMosaic)
				{
					float t = passData.Mosaic.intensity.value;
					t = 1 - t;
					t = 1 - t * t * t * t * t;
					material.SetFloat(IdMosaicIntensity, t);
					material.SetFloat(IdMosaicCellDensity, passData.Mosaic.cellDensity.value);
				}
				else material.SetFloat(IdMosaicIntensity, 0);

				if (passData.UsePosterization)
				{
					material.SetFloat(IdPosterizationToneCount, passData.Posterization.toneCount.value);
				}

				material.SetKeyword(passData.KeywordLut, passData.UseLut);
				if (passData.UseLut)
				{
					int lutSize = passData.LutSize;
					material.SetVector(IdLutParams, new(1f / (lutSize * lutSize), 1f / lutSize, lutSize - 1f));
					material.SetTexture(IdLut, passData.Lut);
				}

				if (passData.UseVignette)
				{
					material.SetFloat(IdVignetteSmoothness, passData.Vignette.smoothness.value);
					material.SetFloat(IdVignetteFitAspect, passData.Vignette.FitAspect.value ? 1 : 0);
					material.SetVector(IdVignetteColor, passData.Vignette.color.value);
					material.SetVector(IdVignetteCenter, passData.Vignette.center.value);
					material.SetFloat(IdVignetteBlendMode, (int)passData.Vignette.blendMode.value);
				}

				Vector4 scaleBias = RenderingUtils.GetFinalBlitScaleBias(passData.Src, passData.Dst, passData.Camera);
				Blitter.BlitTexture(context.cmd, passData.Src, scaleBias, material, 0);
			});

			return LastTarget.Dst;
		}
	}
}