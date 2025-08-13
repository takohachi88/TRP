using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using System;

namespace Trp.PostFx
{
	/// <summary>
	/// PostFxで使うLUTの生成。
	/// </summary>
	internal class CreatePostFxLutPass : IDisposable
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.CreateLut);
		private Material _material;
		private static readonly int IdLutParam = Shader.PropertyToID("_LutParams");
		private static readonly int IdNegaIntensity = Shader.PropertyToID("_NegaIntensity");
	
		private static readonly int IdColorAdjustmentParams = Shader.PropertyToID("_ColorAdjustmentParams");
		private static readonly int IdColorFilter = Shader.PropertyToID("_ColorFilter");
		private static readonly int IdSaturationFactor = Shader.PropertyToID("_SaturationFactor");

		private static readonly int IdLggLift = Shader.PropertyToID("_LggLift");
		private static readonly int IdLggGamma = Shader.PropertyToID("_LggGamma");
		private static readonly int IdLggGain = Shader.PropertyToID("_LggGain");

		private static readonly int IdSmhShadows = Shader.PropertyToID("_SmhShadows");
		private static readonly int IdSmhMidtones = Shader.PropertyToID("_SmhMidtones");
		private static readonly int IdSmhHighlights = Shader.PropertyToID("_SmhHighlights");
		private static readonly int IdSmhRange = Shader.PropertyToID("_SmhRange");

		private static readonly int IdChannelMixerRed = Shader.PropertyToID("_ChannelMixerRed");
		private static readonly int IdChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
		private static readonly int IdChannelMixerBlue = Shader.PropertyToID("_ChannelMixerBlue");

		private static readonly int IdTonemappingMode = Shader.PropertyToID("_TonemappingMode");

		public CreatePostFxLutPass(Shader shader)
		{
			_material = CoreUtils.CreateEngineMaterial(shader);
		}

		public void Dispose()
		{
			CoreUtils.Destroy(_material);
		}

		public class PassData
		{
			public Material Material;
			public TextureHandle Lut;
			public int LutSize;

			public bool UseNega;
			public bool UseColorAdjustment;
			public bool UseLiftGammaGain;
			public bool UseShadowsMidtonesHighlights;
			public bool UseChannelMixer;
			public bool UseToneMapping;

			public Nega Nega;
			public ColorAdjustment ColorAdjustment;
			public LiftGammaGain LiftGammaGain;
			public ShadowsMidtonesHighlights ShadowsMidtonesHighlights;
			public ChannelMixer ChannelMixer;
			public ToneMapping ToneMapping;
		}

		public void RecordRenderGraph(ref PassParams passParams, FilterMode lutFilterMode)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			int lutSize = passParams.LutSize;

			VolumeStack volumeStack = VolumeManager.instance.stack;
			Nega nega = volumeStack?.GetComponent<Nega>();
			ColorAdjustment colorAdjustment = volumeStack?.GetComponent<ColorAdjustment>();
			LiftGammaGain liftGammaGain = volumeStack?.GetComponent<LiftGammaGain>();
			ShadowsMidtonesHighlights shadowsMidtonesHighlights = volumeStack?.GetComponent<ShadowsMidtonesHighlights>();
			ChannelMixer channelMixer = volumeStack?.GetComponent<ChannelMixer>();
			ToneMapping toneMapping = volumeStack?.GetComponent<ToneMapping>();

			bool useNega = nega && nega.IsActive();
			bool useColorAdjustment = colorAdjustment && colorAdjustment.IsActive();
			bool useLiftGammaGain = liftGammaGain && liftGammaGain.IsActive();
			bool useShadowsMidtonesHighlights = shadowsMidtonesHighlights && shadowsMidtonesHighlights.IsActive();
			bool useChannelMixer = channelMixer && channelMixer.IsActive();
			bool useToneMapping = toneMapping && toneMapping.IsActive();

			if (!useNega &&
				!useColorAdjustment &&
				!useLiftGammaGain &&
				!useShadowsMidtonesHighlights &&
				!useChannelMixer &&
				!useToneMapping
				)
				return;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			TextureDesc desc = new(lutSize * lutSize, lutSize)
			{
				name = "PostProcessLut",
				format = RenderingUtils.ColorFormat(true),
				filterMode = lutFilterMode,
				wrapMode = TextureWrapMode.Clamp,
			};
			passData.Lut = passParams.CameraTextures.PostProcessLut = renderGraph.CreateTexture(desc);
			passData.Material = _material;
			passData.LutSize = lutSize;

			passData.UseNega = useNega;
			passData.UseColorAdjustment = useColorAdjustment;
			passData.UseLiftGammaGain = useLiftGammaGain;
			passData.UseShadowsMidtonesHighlights = useShadowsMidtonesHighlights;
			passData.UseChannelMixer = useChannelMixer;
			passData.UseToneMapping = useToneMapping;

			passData.Nega = nega;
			passData.ColorAdjustment = colorAdjustment;
			passData.LiftGammaGain = liftGammaGain;
			passData.ShadowsMidtonesHighlights = shadowsMidtonesHighlights;
			passData.ChannelMixer = channelMixer;
			passData.ToneMapping = toneMapping;

			builder.SetRenderAttachment(passParams.CameraTextures.PostProcessLut, 0, AccessFlags.WriteAll);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				Material material = passData.Material;

				int lutSize = passData.LutSize;
				//LUTのベースを生成するために必要な値の設定。
				material.SetVector(IdLutParam, new(lutSize, 0.5f / (lutSize * lutSize), 0.5f / lutSize, lutSize / (lutSize - 1f)));

				//Nega
				material.SetFloat(IdNegaIntensity, passData.Nega.intensity.value);

				//ColorAdjustment
				ColorAdjustment colorAdjustment = passData.ColorAdjustment;
				material.SetVector(IdColorAdjustmentParams, new(colorAdjustment.contrast.value + 1f, colorAdjustment.hue.value * 0.5f, colorAdjustment.saturation.value));
				material.SetColor(IdColorFilter, colorAdjustment.colorFilter.value);
				material.SetVector(IdSaturationFactor, colorAdjustment.SaturationFactor.value);

				//LiftGammaGain
				var (lift, gamma, gain) = ColorUtils.PrepareLiftGammaGain(
					passData.LiftGammaGain.lift.value,
					passData.LiftGammaGain.gamma.value,
					passData.LiftGammaGain.gain.value
				);
				material.SetVector(IdLggLift, lift);
				material.SetVector(IdLggGamma, gamma);
				material.SetVector(IdLggGain, gain);

				//ShadowsMidtonesHighlights
				ShadowsMidtonesHighlights smh = passData.ShadowsMidtonesHighlights;
				material.SetColor(IdSmhShadows, smh.shadows.value);
				material.SetColor(IdSmhMidtones, smh.midtones.value);
				material.SetColor(IdSmhHighlights, smh.highlights.value);
				material.SetVector(IdSmhRange, new(smh.shadowsRange.value.x, smh.shadowsRange.value.y, smh.highlightRange.value.x, smh.highlightRange.value.y));

				//ChannelMixer
				ChannelMixer channelMixer = passData.ChannelMixer;
				material.SetVector(IdChannelMixerRed, channelMixer.red.value);
				material.SetVector(IdChannelMixerGreen, channelMixer.green.value);
				material.SetVector(IdChannelMixerBlue, channelMixer.blue.value);

				//ToneMapping
				material.SetFloat(IdTonemappingMode, (int)passData.ToneMapping.mode.value);

				Blitter.BlitTexture(context.cmd, passData.Lut, Vector2.one, material, 0);
			});
		}
	}
}