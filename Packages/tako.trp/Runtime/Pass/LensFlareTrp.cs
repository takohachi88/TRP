namespace UnityEngine.Rendering
{
	/// <summary>
	/// Core RP LibraryのLensFlareCommonSRP.csを改造。ScreenSpaceLensFlareの部分のみ抜き出し。
	/// </summary>
	public sealed class LensFlareTrp
	{
		internal static readonly int _LensFlareScreenSpaceBloomMipTexture = Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture");
		internal static readonly int _LensFlareScreenSpaceResultTexture = Shader.PropertyToID("_LensFlareScreenSpaceResultTexture");
		internal static readonly int _LensFlareScreenSpaceSpectralLut = Shader.PropertyToID("_LensFlareScreenSpaceSpectralLut");
		internal static readonly int _LensFlareScreenSpaceStreakTex = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex");
		internal static readonly int _LensFlareScreenSpaceMipLevel = Shader.PropertyToID("_LensFlareScreenSpaceMipLevel");
		internal static readonly int _LensFlareScreenSpaceTintColor = Shader.PropertyToID("_LensFlareScreenSpaceTintColor");
		internal static readonly int _LensFlareScreenSpaceParams1 = Shader.PropertyToID("_LensFlareScreenSpaceParams1");
		internal static readonly int _LensFlareScreenSpaceParams2 = Shader.PropertyToID("_LensFlareScreenSpaceParams2");
		internal static readonly int _LensFlareScreenSpaceParams3 = Shader.PropertyToID("_LensFlareScreenSpaceParams3");
		internal static readonly int _LensFlareScreenSpaceParams4 = Shader.PropertyToID("_LensFlareScreenSpaceParams4");
		internal static readonly int _LensFlareScreenSpaceParams5 = Shader.PropertyToID("_LensFlareScreenSpaceParams5");

		internal static readonly int IdStreaksOrientation = Shader.PropertyToID("_StreaksOrientation");
		internal static readonly int IdStreaksAccumulationTex = Shader.PropertyToID("_LensFlareScreenSpaceStreaksAccumulationTex");


		/// <summary>
		/// Effective Job of drawing Lens Flare Screen Space.
		/// </summary>
		/// <param name="lensFlareShader">Lens Flare material (HDRP or URP shader)</param>
		/// <param name="cam">Camera</param>
		/// <param name="actualWidth">Width actually used for rendering after dynamic resolution and XR is applied.</param>
		/// <param name="actualHeight">Height actually used for rendering after dynamic resolution and XR is applied.</param>
		/// <param name="tintColor">tintColor to multiply all the flare by</param>
		/// <param name="originalBloomTexture">original Bloom texture used to write on at the end of compositing</param>
		/// <param name="bloomMipTexture">Bloom mip texture used as data for the effect</param>
		/// <param name="spectralLut">spectralLut used for chromatic aberration effect</param>
		/// <param name="streakTextureTmp">Texture used for the multiple pass streaks effect</param>
		/// <param name="streakTextureTmp2">Texture used for the multiple pass streaks effect</param>
		/// <param name="streaksAccumulation"></param>
		/// <param name="parameters1">globalIntensity, regularIntensity, reverseIntensity, warpedIntensity</param>
		/// <param name="parameters2">vignetteEffect, startingPosition, scale, freeSlot</param>
		/// <param name="parameters3">samples, sampleDimmer, chromaticAbberationIntensity, chromaticAbberationSamples</param>
		/// <param name="parameters4">streaksIntensity, streaksLength, streaksOrientation, streaksThreshold</param>
		/// <param name="parameters5">downsampleStreak, warpedFlareScaleX, warpedFlareScaleY, freeSlot</param>
		/// <param name="streaksCount"></param>
		/// <param name="streaksSampleCountMultiplier"></param>
		/// <param name="cmd">Command Buffer</param>
		/// <param name="result">Result RT for the Lens Flare Screen Space</param>
		/// <param name="debugView">Information if we are in debug mode or not</param>
		static public void DoLensFlareScreenSpaceCommon(
			Material lensFlareShader,
			Camera cam,
			float actualWidth,
			float actualHeight,
			Color tintColor,
			Texture originalBloomTexture,
			Texture bloomMipTexture,
			Texture spectralLut,
			Texture streakTextureTmp,
			Texture streakTextureTmp2,
			Texture streaksAccumulation,
			Vector4 parameters1,
			Vector4 parameters2,
			Vector4 parameters3,
			Vector4 parameters4,
			Vector4 parameters5,
			int streaksCount,
			float streaksSampleCountMultiplier,
			Rendering.CommandBuffer cmd,
			RTHandle result,
			bool debugView)
		{

			//Multiplying parameters value here for easier maintenance since they are the same numbers between SRPs
			parameters2.x = Mathf.Pow(parameters2.x, 0.25f);        // Vignette effect
			parameters3.z = parameters3.z / 20f;                    // chromaticAbberationIntensity
			parameters4.y = parameters4.y * 10f;                    // Streak Length
			parameters4.z = parameters4.z / 90f;                    // Streak Orientation
			parameters5.y = 1.0f / parameters5.y;                   // WarpedFlareScale X
			parameters5.z = 1.0f / parameters5.z;                   // WarpedFlareScale Y

			cmd.SetViewport(new Rect() { width = actualWidth, height = actualHeight });
			if (debugView)
			{
				// Background pitch black to see only the flares
				cmd.ClearRenderTarget(false, true, Color.black);
			}

#if UNITY_EDITOR
			if (cam.cameraType == CameraType.SceneView)
			{
				// Determine whether the "Flare" checkbox is checked for the current view.
				for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++) // Using a foreach on an ArrayList generates garbage ...
				{
					var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
					if (sv.camera == cam && !sv.sceneViewState.flaresEnabled)
					{
						return;
					}
				}
			}
#endif

			// Multiple scaleX by aspect ratio so that default 1:1 scale for warped flare stays circular (as in data driven lens flare)
			float warpedScaleX = parameters5.y;
			warpedScaleX *= actualWidth / actualHeight;
			parameters5.y = warpedScaleX;

			// This is to make sure the streak length is the same in all resolutions
			float streaksLength = parameters4.y;
			streaksLength *= actualWidth * 0.0005f;
			parameters4.y = streaksLength;

			// List of the passes in LensFlareScreenSpace.shader
			int prefilterPass = lensFlareShader.FindPass("LensFlareScreenSpac Prefilter");
			int downSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Downsample");
			int upSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Upsample");
			int accumulatePass = lensFlareShader.FindPass("LensFlareScreenSpace Streaks Accumulate");
			int compositionPass = lensFlareShader.FindPass("LensFlareScreenSpace Composition");
			int writeToBloomPass = lensFlareShader.FindPass("LensFlareScreenSpace Write to BloomTexture");

			// Setting the input textures
			cmd.SetGlobalTexture(_LensFlareScreenSpaceBloomMipTexture, bloomMipTexture);
			cmd.SetGlobalTexture(_LensFlareScreenSpaceSpectralLut, spectralLut);

			// Setting parameters of the effects
			cmd.SetGlobalVector(_LensFlareScreenSpaceParams1, parameters1);
			cmd.SetGlobalVector(_LensFlareScreenSpaceParams2, parameters2);
			cmd.SetGlobalVector(_LensFlareScreenSpaceParams3, parameters3);
			cmd.SetGlobalVector(_LensFlareScreenSpaceParams4, parameters4);
			cmd.SetGlobalVector(_LensFlareScreenSpaceParams5, parameters5);
			cmd.SetGlobalColor(_LensFlareScreenSpaceTintColor, tintColor);

			// We only do the first 3 pass if StreakIntensity (parameters4.x) is set to something above 0 to save costs
			if (parameters4.x > 0)
			{
				int maxLevel = Mathf.FloorToInt(Mathf.Log(Mathf.Max(actualHeight, actualWidth), 2.0f) * streaksSampleCountMultiplier);
				int maxLevelDownsample = Mathf.Max(1, maxLevel);
				int maxLevelUpsample = 2;

				float orientation = parameters4.z;
				for (int j = 0; j < streaksCount; j++)
				{
					cmd.SetGlobalFloat(IdStreaksOrientation, orientation + (j / (float)streaksCount) * 2f);

					// Prefilter
					Rendering.CoreUtils.SetRenderTarget(cmd, streakTextureTmp);
					DrawQuad(cmd, lensFlareShader, prefilterPass);

					int startIndex = 0;
					bool even = false;

					// Downsample
					for (int i = 0; i < maxLevelDownsample; i++)
					{
						even = (i % 2 == 0);
						cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, i);
						cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp : streakTextureTmp2);
						Rendering.CoreUtils.SetRenderTarget(cmd, even ? streakTextureTmp2 : streakTextureTmp);

						DrawQuad(cmd, lensFlareShader, downSamplePass);
					}

					//Since we do a ping pong between streakTextureTmp & streakTextureTmp2, we need to know which texture is the last;
					if (even)
						startIndex = 1;

					//Upsample
					for (int i = startIndex; i < (startIndex + maxLevelUpsample); i++)
					{
						even = (i % 2 == 0);
						cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, (i - startIndex));
						cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp : streakTextureTmp2);
						Rendering.CoreUtils.SetRenderTarget(cmd, even ? streakTextureTmp2 : streakTextureTmp);
						DrawQuad(cmd, lensFlareShader, upSamplePass);
					}

					cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp2 : streakTextureTmp);
					
					CoreUtils.SetRenderTarget(cmd, streaksAccumulation);
					DrawQuad(cmd, lensFlareShader, accumulatePass);
				}
				cmd.SetGlobalTexture(IdStreaksAccumulationTex, streaksAccumulation);
			}

			// Composition (Flares + Streaks)
			Rendering.CoreUtils.SetRenderTarget(cmd, result);
			DrawQuad(cmd, lensFlareShader, compositionPass);

			// Final pass, we add the result of the previous pass to the Original Bloom Texture.
			cmd.SetGlobalTexture(_LensFlareScreenSpaceResultTexture, result);
			Rendering.CoreUtils.SetRenderTarget(cmd, originalBloomTexture);
			DrawQuad(cmd, lensFlareShader, writeToBloomPass);
		}

		private static void DrawQuad(CommandBuffer cmd, Material material, int pass)
		{
			cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Quads, 4, 1);
		}
	}
}
