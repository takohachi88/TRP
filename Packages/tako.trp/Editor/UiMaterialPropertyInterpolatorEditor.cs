using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor
{
	/// <summary>
	/// UiMaterialPropertyInterpolatorの補間対象を一覧表示するInspector。
	/// </summary>
	[CustomEditor(typeof(UiMaterialPropertyInterpolator)), CanEditMultipleObjects]
	public sealed class UiMaterialPropertyInterpolatorEditor : Editor
	{
		private SerializedProperty _weight;
		private SerializedProperty _properties;

		private void OnEnable()
		{
			_weight = serializedObject.FindProperty("_weight");
			_properties = serializedObject.FindProperty("_properties");

			foreach (Object currentTarget in targets)
			{
				((UiMaterialPropertyInterpolator)currentTarget).RefreshProperties();
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUILayout.PropertyField(_weight, new GUIContent("Weight", "0で元の値、1で設定値になります。"));
			MaterialPropertyInterpolatorEditor.DrawProperties(_properties);
			serializedObject.ApplyModifiedProperties();
		}
	}
}
