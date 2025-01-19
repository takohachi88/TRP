using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using Trp;

namespace TrpEditor
{
	[CustomEditor(typeof(Camera))]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	[CanEditMultipleObjects]
	public class TrpCameraEditor : Editor
	{
		/// <summary>
		/// エディタ拡張で扱うCameraのプロパティ。
		/// optionalなものはnullable。
		/// </summary>
		public class Properties
		{
			public CameraClearFlags ClearFlags;
			public Color? BackgroundColor;
			public bool IsOrthographic;
			public float? OrthographicSize;
			public float? FieldOfView;
			public float Near;
			public float Far;
			public Rect Rect;
			public int Depth;
			public LayerMask CullingMask;

			public void SetProperties(Camera camera)
			{
				camera.clearFlags = ClearFlags;
				if (BackgroundColor.HasValue) camera.backgroundColor = BackgroundColor.Value;
				camera.orthographic = IsOrthographic;
				if (OrthographicSize.HasValue) camera.orthographicSize = OrthographicSize.Value;
				if (FieldOfView.HasValue) camera.fieldOfView = FieldOfView.Value;
				camera.nearClipPlane = Near;
				camera.farClipPlane = Far;
				camera.rect = Rect;
				camera.depth = Depth;
				camera.cullingMask = CullingMask;
			}
		}

		private Properties _properties = new();
		private Camera _camera;
		private TrpCameraData _cameraData;

		private enum ToolbarMode
		{
			Trp,
			Origin,
		}
		private string[] _toolbarModeNames = Enum.GetNames(typeof(ToolbarMode));
		private ToolbarMode _toolbarMode;

		private string[] _projectionLabels = new[] { "Perspective", "Orthographic" };



		public override void OnInspectorGUI()
		{
			_toolbarMode = (ToolbarMode)GUILayout.Toolbar((int)_toolbarMode, _toolbarModeNames);

			EditorGUILayout.Space();

			switch (_toolbarMode)
			{
				case ToolbarMode.Trp:
					DrawCustomGui();
					break;
					case ToolbarMode.Origin:
					base.OnInspectorGUI();
					break;
			}
		}

		private void DrawCustomGui()
		{
			using (EditorGUI.ChangeCheckScope check = new())
			{
				//CameraとTrpCameraDataコンポーネントの取得。
				if (!_camera)
				{
					_camera = (Camera)target;
				}
				if (!_cameraData)
				{
					_cameraData = _camera.GetComponent<TrpCameraData>();
					if (!_cameraData) _cameraData = _camera.gameObject.AddComponent<TrpCameraData>();//TrpCameraDataが付いてない場合は付ける。
				}

				serializedObject.Update();

				//各種プロパティの描画。
				_properties.ClearFlags = (CameraClearFlags)EditorGUILayout.EnumPopup("Clear Flags", _camera.clearFlags);
				_properties.BackgroundColor = _camera.backgroundColor;
				if (_camera.clearFlags is CameraClearFlags.Color or CameraClearFlags.SolidColor)
				{
					_properties.BackgroundColor = EditorGUILayout.ColorField("Background Color", _camera.backgroundColor);
				}
				_properties.IsOrthographic = EditorGUILayout.Toggle("Orthographic", _camera.orthographic);
				if (_properties.IsOrthographic) _properties.OrthographicSize = Mathf.Max(0, EditorGUILayout.FloatField("Orthographic Size", _camera.orthographicSize));
				else _properties.FieldOfView = EditorGUILayout.Slider("FoV", _camera.fieldOfView, 1, 179);
				_properties.Near = EditorGUILayout.FloatField("Near", _camera.nearClipPlane);
				_properties.Far = EditorGUILayout.FloatField("Far", _camera.farClipPlane);
				_properties.Rect = EditorGUILayout.RectField("View Port Rect", _camera.rect);
				_properties.Depth = EditorGUILayout.IntField("Order", (int)_camera.depth);
				_properties.CullingMask = TrpEditorGuiLayout.LayerMaskField("Culling Mask", _camera.cullingMask);

				serializedObject.ApplyModifiedProperties();

				//Redoや更新の対応。
				if (check.changed)
				{
					Undo.RecordObject(_camera, name);

					_properties.SetProperties(_camera);

					EditorUtility.SetDirty(_camera);
					EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
				}
			}
		}
	}
}