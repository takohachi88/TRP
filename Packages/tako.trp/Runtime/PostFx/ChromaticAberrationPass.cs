using TakoLib.Common.Extensions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// RadialBlurのポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "ChromaticAberration", fileName = nameof(ChromaticAberration))]
	public class ChromaticAberrationPass : PostFxSinglePassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
		private static readonly int IdCenter = Shader.PropertyToID("_Center");
		private static readonly int IdDirection = Shader.PropertyToID("_Direction");
		private static readonly int IdLimit = Shader.PropertyToID("_Limit");
		private static readonly int IdSampleCount = Shader.PropertyToID("_SampleCount");
		private static readonly int IdChromaLut = Shader.PropertyToID("_ChromaLut");
		private static readonly int IdIntensityMap = Shader.PropertyToID("_IntensityMap");
		private static readonly int IdUseIntensityMap = Shader.PropertyToID("_UseIntensityMap");

		[SerializeField] private Texture2D _defaultChromaLut;

		private LocalKeyword _keywordDither;
		private LocalKeyword _keywordRadial;
		private LocalKeyword _keywordDirection;

		protected override void OnInitialize()
		{
			_keywordDither = new(PassShader, "_DITHER");
			_keywordRadial = new(PassShader, "_RADIAL");
			_keywordDirection = new(PassShader, "_DIRECTION");
			base.OnInitialize();
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Camera Camera;
			public Material Material;
			public LocalKeyword KeywordDither;
			public LocalKeyword KeywordRadial;
			public LocalKeyword KeywordDirection;
			public ChromaticAberration ChromaticAberration;
			public bool UseChromaticAberration;
			public Texture2D ChromaLut;
			public Vector2 AspectFitRcp;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			ChromaticAberration chromaticAberration = volumeStack.GetComponent<ChromaticAberration>();
			if (!chromaticAberration || !chromaticAberration.IsActive()) return LastTarget.None;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Material = PassMaterial;
			passData.Camera = passParams.Camera;
			passData.KeywordDither = _keywordDither;
			passData.KeywordRadial = _keywordRadial;
			passData.KeywordDirection = _keywordDirection;
			passData.ChromaticAberration = chromaticAberration;
			passData.UseChromaticAberration = 0 < chromaticAberration.intensity.value;
			passData.ChromaLut = chromaticAberration.chromaLut.value ? (Texture2D)chromaticAberration.chromaLut.value : _defaultChromaLut;
			passData.AspectFitRcp = passParams.AspectFitRcp;

			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				ChromaticAberration chromaticAberration = passData.ChromaticAberration;
				Material material = passData.Material;
				TextureHandle src = passData.Src;
				TextureHandle dst = passData.Dst;

				material.SetFloat(IdIntensity, chromaticAberration.intensity.value);
				material.SetVector(IdCenter, chromaticAberration.center.value);
				float radian = chromaticAberration.direction.value * Mathf.Deg2Rad;
				material.SetVector(IdDirection, Vector2.Scale(new(Mathf.Cos(radian), Mathf.Sin(radian)), passData.AspectFitRcp));
				material.SetFloat(IdLimit, chromaticAberration.limit.value);
				int sampleCount = chromaticAberration.sampleCount.value;
				material.SetVector(IdSampleCount, new(sampleCount, 1f / sampleCount, 1f / (sampleCount - 1f)));
				material.SetTexture(IdChromaLut, passData.ChromaLut);
				material.SetTexture(IdIntensityMap, chromaticAberration.intensityMap.value);
				material.SetFloat(IdUseIntensityMap, chromaticAberration.intensityMap.overrideState.ToInt());
				material.SetKeyword(passData.KeywordDither, chromaticAberration.dither.value);
				material.SetKeyword(passData.KeywordRadial, chromaticAberration.mode.value == ChromaticAberration.Mode.Radial);
				material.SetKeyword(passData.KeywordDirection, chromaticAberration.mode.value == ChromaticAberration.Mode.Direction);

				Blit(context.cmd, src, material, 0);
			});

			return LastTarget.Dst;
		}
	}
}