using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// BloomとScreenSpaceLensFlareのポストエフェクト。
	/// URPから移植。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "Bloom", fileName = nameof(BloomLfssPass))]
	public class BloomLfssPass : PostFxPassBase
	{
		private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
		private static readonly int IdParams = Shader.PropertyToID("_Params");
		private static readonly int IdCompositeParams = Shader.PropertyToID("_CompositeParams");
		private static readonly int IdBloomTexture = Shader.PropertyToID("_BloomTexture");
		private static readonly int IdLensDirtTexture = Shader.PropertyToID("_LensDirtTexture");
		private static readonly int IdLensDirtParams = Shader.PropertyToID("_LensDirtParams");
		private static readonly int IdLensDirtIntensity = Shader.PropertyToID("_LensDirtIntensity");
		private static readonly int IdSrcTextureLowMip = Shader.PropertyToID("_SrcTextureLowMip");

		private static readonly string BloomHq = "_BLOOM_HQ";
		private static readonly string BloomLq = "_BLOOM_LQ";
		private static readonly string BloomDirt = "_BLOOM_DIRT";

		[SerializeField] private Shader _bloomShader;
		private Material _bloomMaterial;
		private Material[] _bloomUpsampleMaterials = new Material[MAX_PYRAMID_SIZE];
		private const int MAX_PYRAMID_SIZE = 16;
		private TextureHandle[] _bloomMipUps = new TextureHandle[MAX_PYRAMID_SIZE];
		private TextureHandle[] _bloomMipDowns = new TextureHandle[MAX_PYRAMID_SIZE];
		private string[] _bloomMipUpNames = new string[MAX_PYRAMID_SIZE];
		private string[] _bloomMipDownNames = new string[MAX_PYRAMID_SIZE];
		private BloomMaterialParams _bloomParamsPrev;

		[SerializeField] private Shader _lfssShader;
		private Material _lfssMaterial;

		private static readonly GraphicsFormat _colorFormat = GraphicsFormat.R16G16B16A16_SFloat;

		private static readonly ProfilingSampler SamplerBlitBloomMipmaps = ProfilingSampler.Get(TrpProfileId.BlitBloomMipmaps);
		private static readonly ProfilingSampler SamplerBloomComposite = ProfilingSampler.Get(TrpProfileId.BloomComposite);
		private static readonly ProfilingSampler SamplerLfss = ProfilingSampler.Get(TrpProfileId.LensFlareScreenSpace);

		protected override void OnInitialize()
		{
			_bloomMaterial = CoreUtils.CreateEngineMaterial(_bloomShader);
			for (int i = 0; i < MAX_PYRAMID_SIZE; i++)
			{
				_bloomUpsampleMaterials[i] = CoreUtils.CreateEngineMaterial(_bloomShader);
				_bloomMipUpNames[i] = $"_BloomMipUp{i}";
				_bloomMipDownNames[i] = $"_BloomMipDown{i}";
			}

			_lfssMaterial = CoreUtils.CreateEngineMaterial(_lfssShader);
		}

		protected override void OnDispose()
		{
			CoreUtils.Destroy(_bloomMaterial);
			for (int i = 0; i < MAX_PYRAMID_SIZE; i++) CoreUtils.Destroy(_bloomUpsampleMaterials[i]);

			CoreUtils.Destroy(_lfssMaterial);
		}

		public override LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack)
		{
			Bloom bloom = volumeStack.GetComponent<Bloom>();
			LensFlareScreenSpace lfss = volumeStack.GetComponent<LensFlareScreenSpace>();

			RenderGraph renderGraph = passParams.RenderGraph;

			bool bloomActive = bloom && bloom.IsActive();
			bool lfssActive = lfss && lfss.IsActive();

			if (bloomActive || lfssActive)
			{
				BloomTexture(renderGraph, src, out TextureHandle bloomTexture, bloom, passParams.AttachmentSize);
				if (lfssActive)
				{
					int maxBloomMip = Mathf.Clamp(lfss.bloomMip.value, 0, bloom.maxIterations.value / 2);
					bloomTexture = RenderLensFlareScreenSpace(
						renderGraph,
						passParams.Camera,
						in src,
						_bloomMipUps[0],
						_bloomMipUps[maxBloomMip],
						lfss,
						passParams.AttachmentSize);
				}
				BloomCompositePass(renderGraph, in bloomTexture, bloom, passParams.AttachmentSize, src, dst, passParams.Camera);

				return LastTarget.Dst;
			}
			return LastTarget.None;
		}



		private class BloomPassData
		{
			internal int MipCount;

			internal Material Material;
			internal Material[] UpsampleMaterials;

			internal TextureHandle Src;

			internal TextureHandle[] BloomMipUps;
			internal TextureHandle[] BloomMipDowns;
		}

		internal struct BloomMaterialParams
		{
			internal Vector4 parameters;
			internal bool highQualityFiltering;
			internal bool enableAlphaOutput;

			internal bool Equals(ref BloomMaterialParams other)
			{
				return parameters == other.parameters &&
					   highQualityFiltering == other.highQualityFiltering &&
					   enableAlphaOutput == other.enableAlphaOutput;
			}
		}

		private void BloomTexture(RenderGraph renderGraph, in TextureHandle src, out TextureHandle dst, Bloom bloom, Vector2Int attachmentSize)
		{
			// Start at half-res
			int downres = 1;
			switch (bloom.downscale.value)
			{
				case BloomDownscaleMode.Half:
					downres = 1;
					break;
				case BloomDownscaleMode.Quarter:
					downres = 2;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			//We should set the limit the downres result to ensure we dont turn 1x1 textures, which should technically be valid
			//into 0x0 textures which will be invalid
			int tw = Mathf.Max(1, attachmentSize.x >> downres);
			int th = Mathf.Max(1, attachmentSize.y >> downres);

			// Determine the iteration count
			int maxSize = Mathf.Max(tw, th);
			int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
			int mipCount = Mathf.Clamp(iterations, 1, bloom.maxIterations.value);

			// Pre-filtering parameters
			float clamp = bloom.clamp.value;
			float threshold = Mathf.GammaToLinearSpace(bloom.threshold.value);
			float thresholdKnee = threshold * 0.5f; // Hardcoded soft knee

			// Material setup
			float scatter = Mathf.Lerp(0.05f, 0.95f, bloom.scatter.value);

			BloomMaterialParams bloomParams = new BloomMaterialParams();
			bloomParams.parameters = new Vector4(scatter, clamp, threshold, thresholdKnee);
			bloomParams.highQualityFiltering = bloom.highQualityFiltering.value;

			// Setting keywords can be somewhat expensive on low-end platforms.
			// Previous params are cached to avoid setting the same keywords every frame.
			Material material = _bloomMaterial;
			bool bloomParamsDirty = !_bloomParamsPrev.Equals(ref bloomParams);
			bool isParamsPropertySet = material.HasProperty(IdParams);
			if (bloomParamsDirty || !isParamsPropertySet)
			{
				material.SetVector(IdParams, bloomParams.parameters);
				CoreUtils.SetKeyword(material, BloomHq, bloomParams.highQualityFiltering);

				// These materials are duplicate just to allow different bloom blits to use different textures.
				for (uint i = 0; i < MAX_PYRAMID_SIZE; ++i)
				{
					var materialPyramid = _bloomUpsampleMaterials[i];
					materialPyramid.SetVector(IdParams, bloomParams.parameters);
					CoreUtils.SetKeyword(materialPyramid, BloomHq, bloomParams.highQualityFiltering);
				}

				_bloomParamsPrev = bloomParams;
			}

			// Create bloom mip pyramid textures
			{
				TextureDesc desc = new(tw, th)
				{
					format = _colorFormat,
					name = _bloomMipDownNames[0],
					filterMode = FilterMode.Bilinear,
				};
				_bloomMipDowns[0] = renderGraph.CreateTexture(desc);
				desc.name = _bloomMipUpNames[0];
				_bloomMipUps[0] = renderGraph.CreateTexture(desc);

				for (int i = 1; i < mipCount; i++)
				{
					tw = Mathf.Max(1, tw >> 1);
					th = Mathf.Max(1, th >> 1);
					ref TextureHandle mipDown = ref _bloomMipDowns[i];
					ref TextureHandle mipUp = ref _bloomMipUps[i];

					desc.width = tw;
					desc.height = th;

					// NOTE: Reuse RTHandle names for TextureHandles
					desc.name = _bloomMipDownNames[i];
					mipDown = renderGraph.CreateTexture(desc);
					desc.name = _bloomMipUpNames[i];
					mipUp = renderGraph.CreateTexture(desc);
				}
			}

			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<BloomPassData>(SamplerBlitBloomMipmaps.name, out var passData, SamplerBlitBloomMipmaps))
			{
				passData.MipCount = mipCount;
				passData.Material = _bloomMaterial;
				passData.UpsampleMaterials = _bloomUpsampleMaterials;
				passData.Src = src;
				passData.BloomMipDowns = _bloomMipDowns;
				passData.BloomMipUps = _bloomMipUps;

				// TODO RENDERGRAPH: properly setup dependencies between passes
				builder.AllowPassCulling(false);

				builder.UseTexture(src, AccessFlags.Read);
				for (int i = 0; i < mipCount; i++)
				{
					builder.UseTexture(_bloomMipDowns[i], AccessFlags.ReadWrite);
					builder.UseTexture(_bloomMipUps[i], AccessFlags.ReadWrite);
				}

				builder.SetRenderFunc(static (BloomPassData data, UnsafeGraphContext context) =>
				{
					// TODO: can't call BlitTexture with unsafe command buffer
					var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
					var material = data.Material;
					int mipCount = data.MipCount;

					var loadAction = RenderBufferLoadAction.DontCare;   // Blit - always write all pixels
					var storeAction = RenderBufferStoreAction.Store;    // Blit - always read by then next Blit

					// Prefilter
					Blitter.BlitCameraTexture(cmd, data.Src, data.BloomMipDowns[0], loadAction, storeAction, material, 0);

					// Downsample - gaussian pyramid
					// Classic two pass gaussian blur - use mipUp as a temporary target
					//   First pass does 2x downsampling + 9-tap gaussian
					//   Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
					TextureHandle lastDown = data.BloomMipDowns[0];
					for (int i = 1; i < mipCount; i++)
					{
						TextureHandle mipDown = data.BloomMipDowns[i];
						TextureHandle mipUp = data.BloomMipUps[i];

						Blitter.BlitCameraTexture(cmd, lastDown, mipUp, loadAction, storeAction, material, 1);
						Blitter.BlitCameraTexture(cmd, mipUp, mipDown, loadAction, storeAction, material, 2);

						lastDown = mipDown;
					}

					// Upsample (bilinear by default, HQ filtering does bicubic instead
					for (int i = mipCount - 2; i >= 0; i--)
					{
						TextureHandle lowMip = (i == mipCount - 2) ? data.BloomMipDowns[i + 1] : data.BloomMipUps[i + 1];
						TextureHandle highMip = data.BloomMipDowns[i];
						TextureHandle dst = data.BloomMipUps[i];

						// We need a separate material for each upsample pass because setting the low texture mip source
						// gets overriden by the time the render func is executed.
						// Material is a reference, so all the blits would share the same material state in the cmdbuf.
						// NOTE: another option would be to use cmd.SetGlobalTexture().
						var upMaterial = data.UpsampleMaterials[i];
						upMaterial.SetTexture(IdSrcTextureLowMip, lowMip);

						Blitter.BlitCameraTexture(cmd, highMip, dst, loadAction, storeAction, upMaterial, 3);
					}
				});

				dst = passData.BloomMipUps[0];
			}
		}



		private class UberSetupBloomPassData
		{
			internal Vector4 BloomParams;
			internal Vector4 DirtScaleOffset;
			internal float DirtIntensity;
			internal Texture DirtTexture;
			internal bool HighQualityFilteringValue;
			internal TextureHandle BloomTexture;
			internal Material Material;
			internal TextureHandle Src;
			internal TextureHandle Dst;
			internal Camera Camera;
		}


		private void BloomCompositePass(RenderGraph rendergraph, in TextureHandle bloomTexture, Bloom bloom, Vector2Int attachmentSize, TextureHandle src, TextureHandle dst, Camera camera)
		{
			using (IRasterRenderGraphBuilder builder = rendergraph.AddRasterRenderPass(SamplerBloomComposite.name, out UberSetupBloomPassData passData, SamplerBloomComposite))
			{
				// Setup bloom on uber
				var tint = bloom.tint.value.linear;
				var luma = ColorUtils.Luminance(tint);
				tint = luma > 0f ? tint * (1f / luma) : Color.white;
				var bloomParams = new Vector4(bloom.intensity.value, tint.r, tint.g, tint.b);

				// Setup lens dirtiness on uber
				// Keep the aspect ratio correct & center the dirt texture, we don't want it to be
				// stretched or squashed
				var dirtTexture = bloom.dirtTexture.value == null ? Texture2D.blackTexture : bloom.dirtTexture.value;
				float dirtRatio = dirtTexture.width / (float)dirtTexture.height;
				float screenRatio = attachmentSize.x / (float)attachmentSize.y;
				var dirtScaleOffset = new Vector4(1f, 1f, 0f, 0f);
				float dirtIntensity = bloom.dirtIntensity.value;

				if (dirtRatio > screenRatio)
				{
					dirtScaleOffset.x = screenRatio / dirtRatio;
					dirtScaleOffset.z = (1f - dirtScaleOffset.x) * 0.5f;
				}
				else if (screenRatio > dirtRatio)
				{
					dirtScaleOffset.y = dirtRatio / screenRatio;
					dirtScaleOffset.w = (1f - dirtScaleOffset.y) * 0.5f;
				}

				passData.BloomParams = bloomParams;
				passData.DirtScaleOffset = dirtScaleOffset;
				passData.DirtIntensity = dirtIntensity;
				passData.DirtTexture = dirtTexture;
				passData.HighQualityFilteringValue = bloom.highQualityFiltering.value;

				passData.BloomTexture = bloomTexture;
				passData.Material = _bloomMaterial;
				passData.Src = src;
				passData.Dst = dst;
				passData.Camera = camera;

				builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
				builder.UseTexture(bloomTexture, AccessFlags.Read);
				builder.UseTexture(src, AccessFlags.Read);

				// TODO RENDERGRAPH: properly setup dependencies between passes
				builder.AllowPassCulling(false);

				builder.SetRenderFunc(static (UberSetupBloomPassData data, RasterGraphContext context) =>
				{
					Material material = data.Material;
					material.SetVector(IdCompositeParams, data.BloomParams);
					material.SetVector(IdLensDirtParams, data.DirtScaleOffset);
					material.SetFloat(IdLensDirtIntensity, data.DirtIntensity);

					// Keyword setup - a bit convoluted as we're trying to save some variants in Uber...
					if (data.HighQualityFilteringValue) material.EnableKeyword(BloomHq);
					else material.EnableKeyword(BloomLq);
					if (0 < data.DirtIntensity && data.DirtTexture)
					{
						material.EnableKeyword(BloomDirt);
						material.SetTexture(IdLensDirtTexture, data.DirtTexture);
					}
					else material.DisableKeyword(BloomDirt);

					material.SetTexture(IdBloomTexture, data.BloomTexture);
					Blit(context.cmd, data.Src, material, 4);
				});
			}
		}


		private class LensFlareScreenSpacePassData
		{
			internal TextureHandle Dst;
			internal TextureHandle StreakTmpTexture;
			internal TextureHandle StreakTmpTexture2;
			internal TextureHandle StreaksAccumulation;
			internal TextureHandle originalBloomTexture;
			internal TextureHandle ScreenSpaceLensFlareBloomMipTexture;
			internal TextureHandle Result;
			internal Vector2Int AttachmentSize;
			internal Camera camera;
			internal Material material;
			internal LensFlareScreenSpace ScreenSpaceLensFlare;
			internal int Downsample;
		}

		private TextureHandle RenderLensFlareScreenSpace(
			RenderGraph renderGraph,
			Camera camera,
			in TextureHandle dst,
			TextureHandle bloomTexture,
			TextureHandle sslfBloomMipTexture,
			LensFlareScreenSpace lfss,
			Vector2Int attachmentSize)
		{
			var downsample = (int)lfss.resolution.value;

			int width = attachmentSize.x / downsample;
			int height = attachmentSize.y / downsample;

			TextureDesc streakTextureDesc = new(width, height)
			{
				format = _colorFormat,
				clearBuffer = true,
				filterMode = FilterMode.Bilinear,
			};
			streakTextureDesc.name = "_StreakTmpTexture";
			TextureHandle streakTmpTexture = renderGraph.CreateTexture(streakTextureDesc);
			streakTextureDesc.name = "_StreakTmpTexture2";
			TextureHandle streakTmpTexture2 = renderGraph.CreateTexture(streakTextureDesc);
			streakTextureDesc.name = "_StreaksAccumulation";
			TextureHandle streaksAccumulation = renderGraph.CreateTexture(streakTextureDesc);
			streakTextureDesc.name = "Lens Flare Screen Space Result";
			TextureHandle resultTexture = renderGraph.CreateTexture(streakTextureDesc);

			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(SamplerLfss.name, out LensFlareScreenSpacePassData passData, SamplerLfss))
			{
				// Use WriteTexture here because DoLensFlareScreenSpaceCommon will call SetRenderTarget internally.
				// TODO RENDERGRAPH: convert SRP core lensflare to be rendergraph friendly
				passData.Dst = dst;
				builder.UseTexture(dst, AccessFlags.Write);
				passData.StreakTmpTexture = streakTmpTexture;
				builder.UseTexture(streakTmpTexture, AccessFlags.ReadWrite);
				passData.StreakTmpTexture2 = streakTmpTexture2;
				builder.UseTexture(streakTmpTexture2, AccessFlags.ReadWrite);
				passData.StreaksAccumulation = streaksAccumulation;
				builder.UseTexture(streaksAccumulation, AccessFlags.ReadWrite);
				passData.ScreenSpaceLensFlareBloomMipTexture = sslfBloomMipTexture;
				builder.UseTexture(sslfBloomMipTexture, AccessFlags.ReadWrite);
				passData.originalBloomTexture = bloomTexture;
				builder.UseTexture(bloomTexture, AccessFlags.ReadWrite);
				passData.AttachmentSize = attachmentSize;
				passData.camera = camera;
				passData.material = _lfssMaterial;
				passData.ScreenSpaceLensFlare = lfss; // NOTE: reference, assumed constant until executed.
				passData.Downsample = downsample;
				passData.Result = resultTexture;
				builder.UseTexture(resultTexture, AccessFlags.Write);

				builder.SetRenderFunc<LensFlareScreenSpacePassData>(static (data, context) =>
				{
					UnsafeCommandBuffer cmd = context.cmd;
					Camera camera = data.camera;
					LensFlareScreenSpace lfss = data.ScreenSpaceLensFlare;

					LensFlareTrp.DoLensFlareScreenSpaceCommon(
						data.material,
						camera,
						data.AttachmentSize.x,
						data.AttachmentSize.y,
						data.ScreenSpaceLensFlare.tintColor.value,
						data.originalBloomTexture,
						data.ScreenSpaceLensFlareBloomMipTexture,
						null, // We don't have any spectral LUT in URP
						data.StreakTmpTexture,
						data.StreakTmpTexture2,
						data.StreaksAccumulation,
						new Vector4(
							lfss.intensity.value,
							lfss.firstFlareIntensity.value,
							lfss.secondaryFlareIntensity.value,
							lfss.warpedFlareIntensity.value),
						new Vector4(
							lfss.vignetteEffect.value,
							lfss.startingPosition.value,
							lfss.scale.value,
							0), // Free slot, not used
						new Vector4(
							lfss.samples.value,
							lfss.sampleDimmer.value,
							lfss.chromaticAbberationIntensity.value,
							0), // No need to pass a chromatic aberration sample count, hardcoded at 3 in shader
						new Vector4(
							lfss.streaksIntensity.value,
							lfss.streaksLength.value,
							lfss.streaksOrientation.value,
							lfss.streaksThreshold.value),
						new Vector4(
							data.Downsample,
							lfss.warpedFlareScale.value.x,
							lfss.warpedFlareScale.value.y,
							0), // Free slot, not used
						lfss.streaksCount.value,
						lfss.streaksSampleCountMultiplier.value,
						CommandBufferHelpers.GetNativeCommandBuffer(cmd),
						data.Result,
						false);
				});
				return passData.originalBloomTexture;
			}
		}
	}
}