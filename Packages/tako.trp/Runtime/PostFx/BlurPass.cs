using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// 横・縦の分離ブラーと解像度ピラミッドを実行するポストエフェクト。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Blur", fileName = nameof(BlurPass))]
	public class BlurPass : PostFxSinglePassBase
	{
		// Intensity をピクセル単位のブラー半径へ変換する係数。
		private const float BlurRadiusPerIntensity = 8f;

		// 高品質時の負荷を制限する、片側サンプル対数とピラミッド段数の上限。
		public const int MAX_SAMPLE_PAIRS = 16;
		private const int MAX_PYRAMID_PAIRS = 5;

		private static readonly int IdBlurDirection = Shader.PropertyToID("_BlurDirection");
		private static readonly int IdSamplePairCount = Shader.PropertyToID("_SamplePairCount");
		private static readonly int IdControlTexture = Shader.PropertyToID("_ControlTexture");
		private static readonly int IdBlurTexture = Shader.PropertyToID("_BlurTexture");
		private static readonly int IdDownSampleBlend = Shader.PropertyToID("_DownSampleBlend");

		private Material _verticalMaterial;
		private Material _compositeMaterial;
		private Material _pyramidMaterial;

		// ピラミッドのTextureHandle配列は使い回し、毎フレームの管理メモリ確保を避ける。
		private readonly TextureHandle[] _pyramidDowns = new TextureHandle[MAX_PYRAMID_PAIRS];
		private readonly TextureHandle[] _pyramidUps = new TextureHandle[MAX_PYRAMID_PAIRS - 1];

		protected override void OnInitialize()
		{
			base.OnInitialize();
			_verticalMaterial = CoreUtils.CreateEngineMaterial(PassShader);
			_compositeMaterial = CoreUtils.CreateEngineMaterial(PassShader);
			_pyramidMaterial = CoreUtils.CreateEngineMaterial(PassShader);
		}

		protected override void OnDispose()
		{
			CoreUtils.Destroy(_verticalMaterial);
			CoreUtils.Destroy(_compositeMaterial);
			CoreUtils.Destroy(_pyramidMaterial);
			base.OnDispose();
		}

		public class PassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public TextureHandle Horizontal;
			public TextureHandle Vertical;
			public TextureHandle[] PyramidDowns;
			public TextureHandle[] PyramidUps;
			public int PyramidLevels;
			public Material HorizontalMaterial;
			public Material VerticalMaterial;
			public Material CompositeMaterial;
			public Material PyramidMaterial;
			public Texture ControlTexture;
			public Vector2 BlurRadius;
			public int DownSampleFactor;
			public int SamplePairCount;
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Blur blur = volumeStack.GetComponent<Blur>();
			if (!blur || !blur.IsActive()) return LastTarget.None;

			int pyramidLevels = GetPyramidLevelCount(blur);
			int downSampleFactor = 1 << pyramidLevels;
			RenderGraph renderGraph = passParams.RenderGraph;
			TextureHandle[] pyramidDowns = _pyramidDowns;
			TextureHandle[] pyramidUps = _pyramidUps;

			// 強度に応じた段数だけ順に縮小し、最も小さい解像度でブラーを実行。
			for (int i = 0; i < pyramidLevels; i++)
			{
				int factor = 1 << (i + 1);
				TextureDesc mipDesc = new(
					Mathf.Max(1, passParams.AttachmentSize.x / factor),
					Mathf.Max(1, passParams.AttachmentSize.y / factor))
				{
					name = $"Blur Pyramid Down {i}",
					format = RenderingUtils.ColorFormat(passParams.UseHdr, passParams.UseAlpha),
					filterMode = FilterMode.Bilinear,
				};
				pyramidDowns[i] = renderGraph.CreateTexture(mipDesc);

				if (i < pyramidLevels - 1)
				{
					mipDesc.name = $"Blur Pyramid Up {i}";
					pyramidUps[i] = renderGraph.CreateTexture(mipDesc);
				}
			}

			TextureDesc blurDesc = new(
				Mathf.Max(1, passParams.AttachmentSize.x / downSampleFactor),
				Mathf.Max(1, passParams.AttachmentSize.y / downSampleFactor))
			{
				name = "Blur Horizontal",
				format = RenderingUtils.ColorFormat(passParams.UseHdr, passParams.UseAlpha),
				filterMode = FilterMode.Bilinear,
			};
			TextureHandle horizontal = renderGraph.CreateTexture(blurDesc);
			blurDesc.name = "Blur Vertical";
			TextureHandle vertical = renderGraph.CreateTexture(blurDesc);

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Src = src;
			passData.Dst = dst;
			passData.Horizontal = horizontal;
			passData.Vertical = vertical;
			passData.PyramidDowns = pyramidDowns;
			passData.PyramidUps = pyramidUps;
			passData.PyramidLevels = pyramidLevels;
			passData.HorizontalMaterial = PassMaterial;
			passData.VerticalMaterial = _verticalMaterial;
			passData.CompositeMaterial = _compositeMaterial;
			passData.PyramidMaterial = _pyramidMaterial;
			passData.ControlTexture = blur.controlTexture.value ? blur.controlTexture.value : Texture2D.whiteTexture;
			passData.BlurRadius = BlurRadiusPerIntensity * blur.intensity.value * new Vector2(Mathf.Abs(blur.scale.value.x), Mathf.Abs(blur.scale.value.y));
			passData.DownSampleFactor = downSampleFactor;
			passData.SamplePairCount = GetSamplePairCount(blur);

			builder.UseTexture(src, AccessFlags.Read);
			builder.UseTexture(dst, AccessFlags.Write);
			builder.UseTexture(horizontal, AccessFlags.ReadWrite);
			builder.UseTexture(vertical, AccessFlags.ReadWrite);
			for (int i = 0; i < pyramidLevels; i++) builder.UseTexture(pyramidDowns[i], AccessFlags.ReadWrite);
			for (int i = 0; i < pyramidLevels - 1; i++) builder.UseTexture(pyramidUps[i], AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				TextureHandle current = passData.Src;

				// 縮小時は 2x2 のテントフィルターを各段で適用し、エイリアシングを防ぐ。
				for (int i = 0; i < passData.PyramidLevels; i++)
				{
					TextureHandle down = passData.PyramidDowns[i];
					Blitter.BlitCameraTexture(cmd, current, down, passData.PyramidMaterial, 3);
					current = down;
				}

				// 各BlitのMaterialを分け、後から設定する方向値で先行する描画を上書きしないように。
				Material horizontalMaterial = passData.HorizontalMaterial;
				horizontalMaterial.SetTexture(IdControlTexture, passData.ControlTexture);
				horizontalMaterial.SetInt(IdSamplePairCount, passData.SamplePairCount);
				horizontalMaterial.SetVector(IdBlurDirection, new Vector2(passData.BlurRadius.x / passData.DownSampleFactor, 0f));
				Blitter.BlitCameraTexture(cmd, current, passData.Horizontal, horizontalMaterial, 0);

				Material verticalMaterial = passData.VerticalMaterial;
				verticalMaterial.SetTexture(IdControlTexture, passData.ControlTexture);
				verticalMaterial.SetInt(IdSamplePairCount, passData.SamplePairCount);
				verticalMaterial.SetVector(IdBlurDirection, new Vector2(0f, passData.BlurRadius.y / passData.DownSampleFactor));
				Blitter.BlitCameraTexture(cmd, passData.Horizontal, passData.Vertical, verticalMaterial, 1);

				// 最小Mipから順に拡大し、線形補間だけでは目立つ段差をテントフィルターで和らげる。
				current = passData.Vertical;
				for (int i = passData.PyramidLevels - 2; i >= 0; i--)
				{
					TextureHandle up = passData.PyramidUps[i];
					Blitter.BlitCameraTexture(cmd, current, up, passData.PyramidMaterial, 4);
					current = up;
				}

				Material compositeMaterial = passData.CompositeMaterial;
				compositeMaterial.SetTexture(IdControlTexture, passData.ControlTexture);
				compositeMaterial.SetTexture(IdBlurTexture, current);
				compositeMaterial.SetFloat(IdDownSampleBlend, passData.PyramidLevels > 0 ? 1f : 0f);
				Blitter.BlitCameraTexture(cmd, passData.Src, passData.Dst, compositeMaterial, 2);
			});

			return LastTarget.Dst;
		}

		// 現在の設定で片方向に読むテクスチャタップ数。
		public static int GetBlurTapCount(Blur blur)
		{
			return GetSamplePairCount(blur) * 2 + 1;
		}

		// 横・縦パスを合わせたブラー用テクスチャ読み取り回数。
		public static int GetTotalBlurSampleCount(Blur blur)
		{
			return GetBlurTapCount(blur) * 2;
		}

		// DownSample 設定で構築する縮小ピラミッドの段数。
		public static int GetPyramidLevelCount(Blur blur)
		{
			if (!blur.downSample.value) return 0;

			float effectiveIntensity = GetEffectiveIntensity(blur);
			if (effectiveIntensity < 1f) return 0;

			int requestedLevels = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(effectiveIntensity, 2f)) + 1, 1, MAX_PYRAMID_PAIRS);

			// Quality はブラータップ数だけを制御し、ピラミッド段数には影響させない。
			// Pyramid depth is controlled only by Intensity and DownSample.
			return requestedLevels;
		}

		// 現在のピラミッド最小Mipの解像度倍率。
		public static int GetDownSampleFactor(Blur blur)
		{
			return 1 << GetPyramidLevelCount(blur);
		}

		private static int GetSamplePairCount(Blur blur)
		{
			float radius = BlurRadiusPerIntensity * GetEffectiveIntensity(blur);
			int requiredPairs = Mathf.Max(1, Mathf.CeilToInt(radius / 2f));
			int qualityLimit = Mathf.Clamp(blur.quality.value, 1, MAX_SAMPLE_PAIRS);
			return Mathf.Min(requiredPairs, qualityLimit);
		}

		private static float GetEffectiveIntensity(Blur blur)
		{
			Vector2 scale = blur.scale.value;
			return blur.intensity.value * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
		}
	}
}
