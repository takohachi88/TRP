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

		private readonly List<Camera> _otherCameras = new(16);
		private readonly List<Camera> _gameViewCameras = new(16);

		private static readonly ProfilingSampler Sampler = new("TRP");

		internal Trp(TrpCommonSettings commonSettings, TrpResources resources)
		{
			_resources = resources;

			//MSAAの設定。
			int qualitySettingsMsaaSampleCount = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
			if (qualitySettingsMsaaSampleCount != (int)commonSettings.Msaa)
			{
				QualitySettings.antiAliasing = (int)commonSettings.Msaa;
			}

			_renderer = new(commonSettings, _resources);
			RenderingUtils.Initialize(_resources.CameraBlitShader);
			Blitter.Initialize(_resources.CoreBlitShader, _resources.CoreBlitColorAndDepthShader);
			RTHandles.Initialize(Screen.width, Screen.height);
			CameraCaptureBridge.enabled = true;
			_postFxPassGroup = _resources.PostFxGroup;
			_postFxPassGroup.Initialize();

			VolumeManager.instance.Initialize();

			_renderGraph.nativeRenderPassesEnabled = true;

			GraphicsSettings.useScriptableRenderPipelineBatching = true;
		}

		public class CameraComparer : IComparer<Camera>
		{
			public int Compare(Camera a, Camera b) => a.depth.CompareTo(b.depth);
		}

		protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
		{
			//UIオーバーレイをTRP側で描画するため、エンジン側の描画はオフにする。ただしCameraが無いならオンにしておく。
			SupportedRenderingFeatures.active.rendersUIOverlay = 0 < cameras.Count;

			cameras.Sort(_comparer);
			_otherCameras.Clear();
			_gameViewCameras.Clear();

			foreach (Camera camera in cameras)
			{
				if (camera.cameraType == CameraType.Game && !camera.targetTexture) _gameViewCameras.Add(camera);
				else if(camera.cameraType != CameraType.VR) _otherCameras.Add(camera); //VR非対応のため、CameraType.VRは無視。
			}

			//Game画面（backbuffer）を描画先としないカメラ。
			for (int i = 0; i < _otherCameras.Count; i++)
			{
				RendererParams renderParams = new()
				{
					RenderGraph = _renderGraph,
					Context = context,
					Camera = _otherCameras[i],
					IsFirstToBackbuffer = false,
					IsLastToBackbuffer = false,
					Resources = _resources,
					PostFxPassGroup = _postFxPassGroup,
				};
				_renderer.Render(renderParams);
			}

			//Game画面（backbuffer）を描画先とするカメラ。
			for (int i = 0; i < _gameViewCameras.Count; i++)
			{
				RendererParams renderParams = new()
				{
					RenderGraph = _renderGraph,
					Context = context,
					Camera = _gameViewCameras[i],
					IsFirstToBackbuffer = i == 0,
					IsLastToBackbuffer = i == _gameViewCameras.Count - 1,
					Resources = _resources,
					PostFxPassGroup = _postFxPassGroup,
				};
				using (new ProfilingScope(Sampler))
				{
					_renderer.Render(renderParams);
				}
			}
			_renderGraph.EndFrame();

			string s=null;
			foreach (var v in _otherCameras) s+= v.ToString();
			foreach (var v in _gameViewCameras) s += v.ToString();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			VolumeManager.instance.Deinitialize();
			_renderer.Dispose();
			CameraCaptureBridge.enabled = false;
			_renderGraph.Cleanup();
			RenderingUtils.Dispose();
			Blitter.Cleanup();
		}
	}
}