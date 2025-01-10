using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TakoLib.Rp.PostFx;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp
{
	/// <summary>
	/// TakoLibのRender PipelineなのでTRP。
	/// </summary>
	public class Trp : RenderPipeline
	{
		private TrpRenderer _renderer;
		private InternalResources _internalResources;
		private PostFxPassGroup _postFxPassGroup;

		private readonly CameraComparer _comparer = new();

		private readonly RenderGraph _renderGraph = new RenderGraph("TRP Render Graph");

		private readonly List<Camera> _otherCameras = new(16);
		private readonly List<Camera> _gameViewCameras = new(16);


		internal Trp(TrpCommonSettings commonSettings, InternalResources internalResources, PostFxPassGroup overridePostFxGroup)
		{
			//MSAAの設定。
			int qualitySettingsMsaaSampleCount = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
			if (qualitySettingsMsaaSampleCount != (int)commonSettings.Msaa)
			{
				QualitySettings.antiAliasing = (int)commonSettings.Msaa;
			}

			_renderer = new(commonSettings, internalResources);
			RenderingUtils.Initialize(internalResources.CameraBlitShader);
			Blitter.Initialize(internalResources.CoreBlitShader, internalResources.CoreBlitColorAndDepthShader);
			RTHandles.Initialize(Screen.width, Screen.height);
			CameraCaptureBridge.enabled = true;
			_internalResources = internalResources;
			_postFxPassGroup = overridePostFxGroup ? overridePostFxGroup : internalResources.PostFxGroup;
			_postFxPassGroup.Initialize();

			VolumeManager.instance.Initialize();

			_renderGraph.nativeRenderPassesEnabled = true;
		}

		public class CameraComparer : IComparer<Camera>
		{
			public int Compare(Camera a, Camera b) => a.depth.CompareTo(b.depth);
		}

		protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }
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
					InternalResources = _internalResources,
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
					InternalResources = _internalResources,
					PostFxPassGroup = _postFxPassGroup,
				};
				using (new ProfilingScope(new ProfilingSampler("aaa")))
				{
					_renderer.Render(renderParams);
				}
			}
			_renderGraph.EndFrame();

			string s=null;
			foreach (var v in _otherCameras) s+= v.ToString();
			foreach (var v in _gameViewCameras) s += v.ToString();
			//Debug.Log(s);
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