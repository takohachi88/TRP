using TakoLib.Common.Extensions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp.PostFx
{
	/// <summary>
	/// 一部のポストエフェクトは負荷軽減のためまとめて処理する。
	/// 順番を安易に変えられないというデメリットあり。
	/// 一旦以下の順で実行する。
	/// Mosaic
	/// Posterization
	/// Nega
	/// AdvancedVignette
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Uber")]
	internal class UberPass : PostFxPassBase
	{
		private static readonly int IdMosaicIntensity = Shader.PropertyToID("_MosaicIntensity");
		private static readonly int IdMosaicCellDensity = Shader.PropertyToID("_MosaicCellDensity");

		private static readonly int IdPosterizationIntensity = Shader.PropertyToID("_PosterizationIntensity");
		private static readonly int IdPosterizationToneCount = Shader.PropertyToID("_ToneCount");

		private static readonly int IdNega = Shader.PropertyToID("_Nega");
		private static readonly int IdNegaIntensity = Shader.PropertyToID("_NegaIntensity");

		private LocalKeyword _keywordVignette;
		private static readonly int IdVignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
		private static readonly int IdVignetteSmoothness = Shader.PropertyToID("_VignetteSmoothness");
		private static readonly int IdVignetteFitAspect = Shader.PropertyToID("_VignetteFitAspect");
		private static readonly int IdVignetteColor = Shader.PropertyToID("_VignetteColor");
		private static readonly int IdVignetteCenter = Shader.PropertyToID("_VignetteCenter");
		private static readonly int IdVignetteBlendMode = Shader.PropertyToID("_VignetteBlendMode");


		protected override void OnInitialize()
		{
			_keywordVignette = new(PassShader, "VIGNETTE");
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public LocalKeyword KeywordVignette;
			public bool UsePosterization;
			public bool UseNega;
			public bool UseVignette;
			public bool UseMosaic;
			public Posterization Posterization;
			public Nega Nega;
			public AdvancedVignette Vignette;
			public Mosaic Mosaic;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Mosaic mosaic = volumeStack?.GetComponent<Mosaic>();
			Posterization posterization = volumeStack?.GetComponent<Posterization>();
			Nega nega = volumeStack?.GetComponent<Nega>();
			AdvancedVignette advancedVignette = volumeStack?.GetComponent<AdvancedVignette>();

			bool useMosaic = mosaic && mosaic.IsActive();
			bool usePosterization = posterization && posterization.IsActive();
			bool useNega = nega && nega.IsActive();
			bool useVignette = advancedVignette && advancedVignette.IsActive();

			if (!useMosaic && !usePosterization && !useNega && !useVignette) return LastTarget.None;

			using (IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler))
			{
				passData.Src = src;
				passData.Dst = dst;
				passData.Camera = passParams.Camera;
				passData.Material = PassMaterial;
				passData.KeywordVignette = _keywordVignette;

				passData.UsePosterization = usePosterization;
				passData.UseNega = useNega;
				passData.UseVignette = useVignette;
				passData.UseMosaic = useMosaic;

				passData.Posterization = posterization;
				passData.Nega = nega;
				passData.Vignette = advancedVignette;
				passData.Mosaic = mosaic;

				builder.AllowPassCulling(false);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
				builder.UseTexture(src, AccessFlags.Read);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					IRasterCommandBuffer cmd = context.cmd;
					cmd.SetKeyword(passData.Material, passData.KeywordVignette, passData.UseVignette);
					cmd.SetGlobalFloat(IdNega, passData.UseNega.ToInt());
					cmd.SetGlobalFloat(IdPosterizationIntensity, passData.Posterization.intensity.value);
					cmd.SetGlobalFloat(IdVignetteIntensity, passData.Vignette.intensity.value * 3f);

					if (passData.UseMosaic)
					{
						float t = passData.Mosaic.intensity.value;
						t = 1 - t;
						t = 1 - t * t * t * t * t;
						cmd.SetGlobalFloat(IdMosaicIntensity, t);
						cmd.SetGlobalFloat(IdMosaicCellDensity, passData.Mosaic.cellDensity.value);
					}
					else cmd.SetGlobalFloat(IdMosaicIntensity, 0);

					if (passData.UsePosterization)
					{
						cmd.SetGlobalFloat(IdPosterizationToneCount, passData.Posterization.toneCount.value);
					}
					if (passData.UseNega)
					{
						cmd.SetGlobalFloat(IdNegaIntensity, passData.Nega.intensity.value);
					}
					if (passData.UseVignette)
					{
						cmd.SetGlobalFloat(IdVignetteSmoothness, passData.Vignette.smoothness.value);
						cmd.SetGlobalFloat(IdVignetteFitAspect, passData.Vignette.FitAspect.value ? 1 : 0);
						cmd.SetGlobalVector(IdVignetteColor, passData.Vignette.color.value);
						cmd.SetGlobalVector(IdVignetteCenter, passData.Vignette.center.value);
						cmd.SetGlobalFloat(IdVignetteBlendMode, (int)passData.Vignette.blendMode.value);
					}

					Vector4 scaleBias = RenderingUtils.GetFinalBlitScaleBias(passData.Src, passData.Dst, passData.Camera);
					Blitter.BlitTexture(context.cmd, passData.Src, scaleBias, passData.Material, 0);
				});
			}

			return LastTarget.Dst;
		}
	}
}