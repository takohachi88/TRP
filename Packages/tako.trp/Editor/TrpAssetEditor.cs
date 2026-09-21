using Trp;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace TrpEditor
{
	/// <summary>TRP Assetの設定を表示し、Cascade分割を視覚的に編集する。</summary>
	[CustomEditor(typeof(TrpAsset))]
	public sealed class TrpAssetEditor : Editor
	{
		private const float MinCascadeSize = 0.001f;

		private SerializedProperty _commonSettings;
		private readonly ShadowCascadeGUI.Cascade[][] _cascadeBuffers =
		{
			new ShadowCascadeGUI.Cascade[1],
			new ShadowCascadeGUI.Cascade[2],
			new ShadowCascadeGUI.Cascade[3],
			new ShadowCascadeGUI.Cascade[4],
		};

		private void OnEnable()
		{
			_commonSettings = serializedObject.FindProperty("_commonSettings");
			if (_commonSettings == null) return;

			_commonSettings.isExpanded = true;
			SerializedProperty shadowSettings = _commonSettings.FindPropertyRelative("_shadowSettings");
			if (shadowSettings != null) shadowSettings.isExpanded = true;
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			if (_commonSettings != null)
			{
				_commonSettings.isExpanded = EditorGUILayout.Foldout(
					_commonSettings.isExpanded,
					_commonSettings.displayName,
					true);
				if (_commonSettings.isExpanded)
				{
					EditorGUI.indentLevel++;
					DrawCommonSettings(_commonSettings);
					EditorGUI.indentLevel--;
				}
			}

			DrawPropertiesExcluding(serializedObject, "m_Script", "_commonSettings");
			serializedObject.ApplyModifiedProperties();
		}

		private void DrawCommonSettings(SerializedProperty commonSettings)
		{
			SerializedProperty property = commonSettings.Copy();
			SerializedProperty end = property.GetEndProperty();
			bool enterChildren = true;
			while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
			{
				enterChildren = false;
				if (property.depth != commonSettings.depth + 1) continue;

				if (property.name == "_shadowSettings")
				{
					DrawShadowSettings(property);
				}
				else
				{
					EditorGUILayout.PropertyField(property, true);
				}
			}
		}

		private void DrawShadowSettings(SerializedProperty shadowSettings)
		{
			shadowSettings.isExpanded = EditorGUILayout.Foldout(
				shadowSettings.isExpanded,
				shadowSettings.displayName,
				true);
			if (!shadowSettings.isExpanded) return;

			SerializedProperty maxDistance = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.MaxShadowDistance));
			SerializedProperty cascadeCount = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.CascadeCount));
			SerializedProperty cascadeRatios = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.CascadeRatios));
			SerializedProperty distanceFade = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.DistanceFade));
			SerializedProperty cascadeFade = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.CascadeFade));
			SerializedProperty directionalMapSize = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.DirectionalShadowMapSize));
			SerializedProperty punctualMapSize = shadowSettings.FindPropertyRelative(nameof(ShadowSettings.PunctualShadowMapSize));

			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(maxDistance);
			cascadeCount.intValue = EditorGUILayout.IntPopup(
				"Cascade Count",
				Mathf.Clamp(cascadeCount.intValue, 1, 4),
				new[] { "1", "2", "3", "4" },
				new[] { 1, 2, 3, 4 });

			int count = cascadeCount.intValue;
			float distance = Mathf.Max(0.001f, maxDistance.floatValue);
			Vector3 ratios = ValidateRatios(cascadeRatios.vector3Value, count);
			ShadowCascadeGUI.Cascade[] cascades = _cascadeBuffers[count - 1];
			RatiosToCascades(ratios, cascades);

			using (EditorGUI.ChangeCheckScope check = new())
			{
				ShadowCascadeGUI.DrawCascades(ref cascades, true, distance);
				if (check.changed) ratios = CascadesToRatios(cascades, ratios);
			}

			for (int i = 0; i < count - 1; i++)
			{
				float previous = i == 0 ? MinCascadeSize : ratios[i - 1] + MinCascadeSize;
				float next = i == count - 2 ? 1f - MinCascadeSize : ratios[i + 1] - MinCascadeSize;
				float splitDistance = EditorGUILayout.Slider(
					new GUIContent($"Cascade {i + 1} End"),
					ratios[i] * distance,
					previous * distance,
					next * distance);
				ratios[i] = splitDistance / distance;
			}
			cascadeRatios.vector3Value = ValidateRatios(ratios, count);

			EditorGUILayout.PropertyField(cascadeFade);
			EditorGUILayout.PropertyField(distanceFade);
			DrawMapSize(directionalMapSize, "Directional Map Size");
			DrawMapSize(punctualMapSize, "Punctual Map Size");
			EditorGUI.indentLevel--;
		}

		private static void DrawMapSize(SerializedProperty property, string label)
		{
			ShadowSettings.MapSize value = (ShadowSettings.MapSize)property.intValue;
			property.intValue = (int)(ShadowSettings.MapSize)EditorGUILayout.EnumPopup(label, value);
		}

		private static void RatiosToCascades(Vector3 ratios, ShadowCascadeGUI.Cascade[] cascades)
		{
			float previous = 0;
			for (int i = 0; i < cascades.Length; i++)
			{
				float end = i == cascades.Length - 1 ? 1 : ratios[i];
				cascades[i].size = end - previous;
				cascades[i].borderSize = 0;
				cascades[i].cascadeHandleState = i == cascades.Length - 1
					? ShadowCascadeGUI.HandleState.Hidden
					: ShadowCascadeGUI.HandleState.Enabled;
				cascades[i].borderHandleState = ShadowCascadeGUI.HandleState.Hidden;
				previous = end;
			}
		}

		private static Vector3 CascadesToRatios(ShadowCascadeGUI.Cascade[] cascades, Vector3 ratios)
		{
			float sum = 0;
			for (int i = 0; i < cascades.Length - 1; i++)
			{
				sum += cascades[i].size;
				ratios[i] = sum;
			}
			return ratios;
		}

		private static Vector3 ValidateRatios(Vector3 ratios, int count)
		{
			float previous = 0;
			for (int i = 0; i < count - 1; i++)
			{
				float remainingMinimum = (count - 1 - i) * MinCascadeSize;
				ratios[i] = Mathf.Clamp(ratios[i], previous + MinCascadeSize, 1f - remainingMinimum);
				previous = ratios[i];
			}
			return ratios;
		}
	}
}
