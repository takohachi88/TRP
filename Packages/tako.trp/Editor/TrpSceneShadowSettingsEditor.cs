using System.Collections.Generic;
using Trp;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrpEditor
{
	[CustomEditor(typeof(TrpSceneShadowSettings))]
	internal sealed class TrpSceneShadowSettingsEditor : Editor
	{
		private readonly ShadowSettingsGUI _shadowSettingsGUI = new();
		private readonly List<GameObject> _roots = new();
		private readonly List<TrpSceneShadowSettings> _components = new();
		private SerializedProperty _settings;

		private void OnEnable() => _settings = serializedObject.FindProperty("_settings");

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			var component = (TrpSceneShadowSettings)target;
			if (HasDuplicates(component.gameObject.scene))
			{
				EditorGUILayout.HelpBox(
					"このシーンに TRP Scene Shadow Settings が複数あります。1つだけ配置してください。",
					MessageType.Warning);
			}

			_shadowSettingsGUI.Draw(_settings);
			serializedObject.ApplyModifiedProperties();
		}

		private bool HasDuplicates(Scene scene)
		{
			if (!scene.IsValid() || !scene.isLoaded) return false;

			// 無効なコンポーネントや非アクティブな GameObject も、シーン内での重複に含める。
			scene.GetRootGameObjects(_roots);
			int count = 0;
			foreach (GameObject root in _roots)
			{
				root.GetComponentsInChildren(true, _components);
				count += _components.Count;
				if (count > 1) break;
			}
			return count > 1;
		}
	}
}
