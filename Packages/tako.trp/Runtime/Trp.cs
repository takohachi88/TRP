using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Trp.PostFx;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// TakoLibのRender PipelineなのでTRP。
	/// </summary>
	public class Trp : RenderPipeline
	{
		private TrpRenderer _renderer;
		private TrpResources _resources;
		private PostFxPassGroup _postFxPassGroup;

		private readonly CameraComparer _comparer = new();

		private readonly RenderGraph _renderGraph = new RenderGraph("TRP Render Graph");

		private readonly List<Camera> _renderTextureCameras;//描画先がRenderTexture。
		private readonly List<Camera> _backbufferCameras;//描画先がbackbuffer。
		private readonly List<Camera> _editorCameras = new(3); //描画先がSceneやPreview。

		private static readonly ProfilingSampler Sampler = new("TRP");
		private static readonly ProfilingSampler SamplerRenderTexture = new("TRP RenderTexture");

		private static readonly DebugForwardPlus DebugForwardPlus = new();

		internal Trp(TrpCommonSettings commonSettings, TrpResources resources)
		{
			_resources = resources;

			//MSAAの設定。
			int qualitySettingsMsaaSampleCount = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
			if (qualitySettingsMsaaSampleCount != (int)commonSettings.Msaa)
			{
				QualitySettings.antiAliasing = (int)commonSettings.Msaa;
				Debug.Log($"QualitySettings.antialiasing was set to {QualitySettings.antiAliasing}.");
			}

			_renderer = new(commonSettings, _resources);
			RenderingUtils.Initialize();
			Blitter.Initialize(_resources.CoreBlitShader, _resources.CoreBlitColorAndDepthShader);
			RTHandles.Initialize(Screen.width, Screen.height);
			CameraCaptureBridge.enabled = true;
			_postFxPassGroup = _resources.PostFxGroup;
			_postFxPassGroup.Initialize();

			LensFlareCommonSRP.mergeNeeded = 0;
			LensFlareCommonSRP.maxLensFlareWithOcclusionTemporalSample = 1;
			LensFlareCommonSRP.Initialize();
			VolumeManager.instance.Initialize();
			RtHandlePool.Instance.Initialize();

			GraphicsSettings.useScriptableRenderPipelineBatching = true;

			_renderTextureCameras = new(commonSettings.DefaultMaxRenderTextureCameraCount);
			_backbufferCameras = new(commonSettings.DefaultMaxBackbufferCameraCount);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			LensFlareCommonSRP.Dispose();
			VolumeManager.instance?.Deinitialize();
			_renderer.Dispose();
			RtHandlePool.Instance?.Dispose();
			CameraCaptureBridge.enabled = false;
			_renderGraph.Cleanup();
			RenderingUtils.Dispose();
			Blitter.Cleanup();
			DebugForwardPlus.Dispose();
		}

		public class CameraComparer : IComparer<Camera>
		{
			public int Compare(Camera a, Camera b) => a.depth.CompareTo(b.depth);
		}

		/// <summary>
		/// SRP標準のカメラ描画通知を発火してからTRPのカメラ描画を実行する。
		/// GPU Resident Drawerはこの通知でカリング用のカメラ状態を準備する。
		/// </summary>
		private void RenderCamera(ref RendererParams renderParams)
		{
			BeginCameraRendering(renderParams.Context, renderParams.Camera);
			try
			{
				_renderer.Render(ref renderParams);
			}
			finally
			{
				EndCameraRendering(renderParams.Context, renderParams.Camera);
			}
		}

		/// <summary>
		/// プロファイリング対象のカメラ描画でも、カメラ通知の対称性を維持する。
		/// </summary>
		private void RenderCamera(ref RendererParams renderParams, ProfilingSampler sampler)
		{
			BeginCameraRendering(renderParams.Context, renderParams.Camera);
			try
			{
				using (new ProfilingScope(sampler))
				{
					_renderer.Render(ref renderParams);
				}
			}
			finally
			{
				EndCameraRendering(renderParams.Context, renderParams.Camera);
			}
		}

		protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
		{
			// 設定アセットでの有効・無効切り替えや、エディタ上での互換性変化を反映する。
			// URPと同様にフレーム先頭で確認し、GPU Resident Drawerの寿命をTRPに同期させる。
			GPUResidentDrawer.ReinitializeIfNeeded();
			BeginContextRendering(context, cameras);
			try
			{

			//UIオーバーレイをTRP側で描画するため、エンジン側の描画はオフにする。ただしCameraが無いならオンにしておく。
			SupportedRenderingFeatures.active.rendersUIOverlay = 0 < cameras.Count;

			cameras.Sort(_comparer);
			_editorCameras.Clear();
			_renderTextureCameras.Clear();
			_backbufferCameras.Clear();

			DebugForwardPlus.Register(cameras);

			//TODO: Reflectionの場合の処理。
			foreach (Camera camera in cameras)
			{
				if ((camera.cameraType == CameraType.Game && camera.targetTexture) || camera.cameraType == CameraType.Reflection) _renderTextureCameras.Add(camera);
				else if (camera.cameraType == CameraType.Game && !camera.targetTexture) _backbufferCameras.Add(camera);
				else if (camera.cameraType is CameraType.SceneView or CameraType.Preview) _editorCameras.Add(camera); //VR非対応のため、CameraType.VRは含めない。
			}

			//RenderTextureを描画先とするカメラの描画。
			for (int i = 0; i < _renderTextureCameras.Count; i++)
			{
				RendererParams renderParams = new()
				{
					RenderGraph = _renderGraph,
					Context = context,
					Camera = _renderTextureCameras[i],
					IsFirstToBackbuffer = false,
					IsLastToBackbuffer = false,
					IsFirstRuntimeCamera = i == 0,
					TargetIsGameRenderTexture = true,
					Resources = _resources,
					PostFxPassGroup = _postFxPassGroup,
					EditorCameras = _editorCameras,
					RenderTextureCameras = _renderTextureCameras,
					BackbufferCameras = _backbufferCameras,
					ForwardPlusCameraDebugValue = null,
				};
				RenderCamera(ref renderParams, SamplerRenderTexture);
			}

			//Game画面（backbuffer）を描画先とするカメラの描画。
			//実際にはURPのCameraStackのように、一枚の中間テクスチャに描画していき、最後のCameraの処理においてbackbufferに描画する。
			for (int i = 0; i < _backbufferCameras.Count; i++)
			{
				RendererParams renderParams = new()
				{
					RenderGraph = _renderGraph,
					Context = context,
					Camera = _backbufferCameras[i],
					IsFirstToBackbuffer = i == 0,
					IsLastToBackbuffer = i == _backbufferCameras.Count - 1,
					IsFirstRuntimeCamera = i == 0 && _renderTextureCameras.Count == 0,
					TargetIsGameRenderTexture = false,
					Resources = _resources,
					PostFxPassGroup = _postFxPassGroup,
					EditorCameras = _editorCameras,
					RenderTextureCameras = _renderTextureCameras,
					BackbufferCameras = _backbufferCameras,
					ForwardPlusCameraDebugValue = DebugForwardPlus.GetCameraDebugValue(_backbufferCameras[i]),
				};
				RenderCamera(ref renderParams, Sampler);
			}

			//SceneViewやPreviewなどエディタ用の描画のカメラの描画。
			for (int i = 0; i < _editorCameras.Count; i++)
			{
				RendererParams renderParams = new()
				{
					RenderGraph = _renderGraph,
					Context = context,
					Camera = _editorCameras[i],
					IsFirstToBackbuffer = false,
					IsLastToBackbuffer = false,
					IsFirstRuntimeCamera = false,
					TargetIsGameRenderTexture = false,
					Resources = _resources,
					PostFxPassGroup = _postFxPassGroup,
					EditorCameras = _editorCameras,
					RenderTextureCameras = _renderTextureCameras,
					BackbufferCameras = _backbufferCameras,
					ForwardPlusCameraDebugValue = DebugForwardPlus.GetCameraDebugValue(_editorCameras[i]),
				};
				RenderCamera(ref renderParams);
			}

			_renderGraph.EndFrame();
			}
			finally
			{
				EndContextRendering(context, cameras);
			}
		}
	}
}
