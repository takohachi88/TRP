using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TakoLib.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	public class DebugForwardPlus
	{
		private const string PANEL_NAME_FORWARD_PLUS = "Forward+";

		private static readonly Dictionary<Camera, CameraDebugValue> _cameraDebugValue = new();

		public class CameraDebugValue
		{
			public bool ShowTiles = false;
			public float Opacity = 0.5f;
		}

		public CameraDebugValue GetCameraDebugValue(Camera camera) => _cameraDebugValue?[camera];

		[Conditional(Defines.UNITY_EDITOR)]
		public void Register(IReadOnlyList<Camera> cameras)
		{
			// RenderTextureに描画するカメラの場合はForward+のデバッグは表示しない。
			if (cameras.Any(camera => camera.cameraType == CameraType.Game && camera.targetTexture)) return;

			DebugUI.Container[] perCameraContainers = new DebugUI.Container[cameras.Count];
			for (int i = 0; i < cameras.Count; i++)
			{
				CameraDebugValue cameraDebugValue = null;
				if (_cameraDebugValue.ContainsKey(cameras[i]))
				{
					cameraDebugValue = _cameraDebugValue[cameras[i]];
				}
				else
				{
					cameraDebugValue = new();
					_cameraDebugValue.Add(cameras[i], cameraDebugValue);
				}

				perCameraContainers[i] = new DebugUI.Container
				{
					displayName = cameras[i].name,
					children =
						{
							new DebugUI.BoolField
							{
								displayName = "Show Tiles",
								getter = () => cameraDebugValue.ShowTiles,
								setter = value => cameraDebugValue.ShowTiles = value,
							},
							new DebugUI.FloatField
							{
								displayName = "Tiles Opacity",
								min = static () => 0f,
								max = static () => 1f,
								getter = () => cameraDebugValue.Opacity,
								setter = value => cameraDebugValue.Opacity = value,
							}
						}
				};
			}
			DebugUI.Panel panel = DebugManager.instance.GetPanel(PANEL_NAME_FORWARD_PLUS, true);
			panel.children.Clear();
			panel.children.Add(perCameraContainers);
		}

		public void Dispose()
		{
			_cameraDebugValue.Clear();
			DebugManager.instance.RemovePanel(PANEL_NAME_FORWARD_PLUS);
		}
	}
}