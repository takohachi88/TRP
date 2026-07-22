using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor
{
	/// <summary>
	/// MaterialPropertyInterpolatorの補間対象を一覧表示するInspector。
	/// </summary>
	[CustomEditor(typeof(MaterialPropertyInterpolator)), CanEditMultipleObjects]
	public sealed class MaterialPropertyInterpolatorEditor : Editor
	{
		private SerializedProperty _materialIndex;
		private SerializedProperty _weight;
		private SerializedProperty _properties;

		private void OnEnable()
		{
			_materialIndex = serializedObject.FindProperty("_materialIndex");
			_weight = serializedObject.FindProperty("_weight");
			_properties = serializedObject.FindProperty("_properties");

			foreach (Object currentTarget in targets)
			{
				((MaterialPropertyInterpolator)currentTarget).RefreshProperties();
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUI.BeginChangeCheck();
			DrawMaterialPopup();
			bool materialChanged = EditorGUI.EndChangeCheck();

			EditorGUILayout.PropertyField(_weight, new GUIContent("Weight", "0で元の値、1で設定値になります。"));
			DrawProperties(_properties);

			serializedObject.ApplyModifiedProperties();

			if (materialChanged)
			{
				foreach (Object currentTarget in targets)
				{
					MaterialPropertyInterpolator interpolator = (MaterialPropertyInterpolator)currentTarget;
					interpolator.RecreateMaterialInstance();
					EditorUtility.SetDirty(interpolator);
				}
				serializedObject.Update();
			}
		}

		private void DrawMaterialPopup()
		{
			if (targets.Length != 1)
			{
				EditorGUILayout.PropertyField(_materialIndex, new GUIContent("Material Index"));
				return;
			}

			MaterialPropertyInterpolator interpolator = (MaterialPropertyInterpolator)target;
			Renderer targetRenderer = interpolator.GetComponent<Renderer>();
			Material[] materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
			if (materials == null || materials.Length == 0)
			{
				EditorGUILayout.HelpBox("Rendererにマテリアルが設定されていません。", MessageType.Warning);
				return;
			}

			string[] options = new string[materials.Length];
			for (int i = 0; i < materials.Length; i++)
			{
				string materialName = materials[i] != null ? materials[i].name : "None";
				options[i] = $"{i}: {materialName}";
			}

			_materialIndex.intValue = EditorGUILayout.Popup("Material", Mathf.Clamp(_materialIndex.intValue, 0, materials.Length - 1), options);
		}

		internal static void DrawProperties(SerializedProperty properties)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Material Properties", EditorStyles.boldLabel);

			if (properties.arraySize == 0)
			{
				EditorGUILayout.HelpBox("対象マテリアルに表示可能なプロパティがありません。", MessageType.Info);
				return;
			}

			for (int i = 0; i < properties.arraySize; i++)
			{
				DrawProperty(properties.GetArrayElementAtIndex(i));
			}
		}

		private static void DrawProperty(SerializedProperty property)
		{
			SerializedProperty enabled = property.FindPropertyRelative("_enabled");
			SerializedProperty displayName = property.FindPropertyRelative("_displayName");
			SerializedProperty name = property.FindPropertyRelative("_name");
			SerializedProperty type = property.FindPropertyRelative("_type");
			MaterialPropertyInterpolator.PropertyType propertyType =
				(MaterialPropertyInterpolator.PropertyType)type.enumValueIndex;

			bool canInterpolate = propertyType != MaterialPropertyInterpolator.PropertyType.Unsupported;
			string tooltip = canInterpolate ? name.stringValue : "この型は線形補間に対応していません。";
			GUIContent label = new($"{displayName.stringValue} ({name.stringValue})", tooltip);

			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(!canInterpolate))
				{
					enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
				}
				EditorGUILayout.LabelField(label);
			}

			if (!canInterpolate)
			{
				EditorGUILayout.LabelField("補間非対応", EditorStyles.miniLabel);
				return;
			}

			using (new EditorGUI.DisabledScope(!enabled.boolValue))
			{
				EditorGUI.indentLevel++;
				SerializedProperty value = propertyType switch
				{
					MaterialPropertyInterpolator.PropertyType.Float => property.FindPropertyRelative("_floatValue"),
					MaterialPropertyInterpolator.PropertyType.Range => property.FindPropertyRelative("_floatValue"),
					MaterialPropertyInterpolator.PropertyType.Integer => property.FindPropertyRelative("_intValue"),
					MaterialPropertyInterpolator.PropertyType.Toggle => property.FindPropertyRelative("_toggleValue"),
					MaterialPropertyInterpolator.PropertyType.Color => property.FindPropertyRelative("_colorValue"),
					MaterialPropertyInterpolator.PropertyType.Vector => property.FindPropertyRelative("_vectorValue"),
					MaterialPropertyInterpolator.PropertyType.Texture => property.FindPropertyRelative("_textureValue"),
					_ => null
				};
				if (value != null) EditorGUILayout.PropertyField(value, GUIContent.none);
				if (propertyType == MaterialPropertyInterpolator.PropertyType.Toggle ||
					propertyType == MaterialPropertyInterpolator.PropertyType.Texture)
				{
					SerializedProperty threshold = property.FindPropertyRelative("_threshold");
					EditorGUILayout.Slider(threshold, 0f, 1f, new GUIContent("Threshold", "Weightがこの値以上になると切り替わります。"));
				}
				if (propertyType == MaterialPropertyInterpolator.PropertyType.Texture &&
					property.FindPropertyRelative("_hasScaleOffset").boolValue)
				{
					EditorGUILayout.PropertyField(property.FindPropertyRelative("_textureScale"), new GUIContent("Tiling"));
					EditorGUILayout.PropertyField(property.FindPropertyRelative("_textureOffset"), new GUIContent("Offset"));
				}
				EditorGUI.indentLevel--;
			}
		}
	}
}
