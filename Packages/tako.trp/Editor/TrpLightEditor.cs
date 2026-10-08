using TakoLibEditor.Common;
using Trp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrpEditor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Light))]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class TrpLightEditor : LightEditor
	{
		private const float DefaultBlurRadius = 2;

		protected override void OnEnable()
		{
			base.OnEnable();
			EnsureAdditionalData();
		}

		public override void OnInspectorGUI()
		{
			EnsureAdditionalData();

			EditorGUILayout.LabelField("TRP Light", EditorStyles.boldLabel);
			TakoLibEditorUtility.DrawSeparator(5);

			settings.Update();
			settings.DrawLightType();

			bool hasSingleType = !settings.lightType.hasMultipleDifferentValues;
			LightType lightType = hasSingleType ? (LightType)settings.lightType.intValue : default;
			bool isArea = hasSingleType && (lightType == LightType.Rectangle || lightType == LightType.Disc);

			if (!hasSingleType || lightType != LightType.Directional)
				settings.DrawRange();
			if (hasSingleType && lightType == LightType.Spot)
				settings.DrawInnerAndOuterSpotAngle();
			if (isArea)
				settings.DrawArea();

			settings.DrawColor();
			if (!isArea)
				settings.DrawLightmapping();
			settings.DrawIntensity();
			settings.DrawBounceIntensity();

			DrawShadowSettings(hasSingleType, lightType, isArea);
			settings.DrawCookie();
			if (hasSingleType && lightType == LightType.Directional)
				settings.DrawCookieSize();

			// TRPではBuilt-in Render Pipeline向けのHaloとFlareを使用しないため描画しない。
			settings.DrawRenderMode();
			settings.DrawCullingMask();
			settings.DrawRenderingLayerMask();

			settings.ApplyModifiedProperties();
			serializedObject.ApplyModifiedProperties();
			DrawSoftShadowSettings();
		}

		private void DrawShadowSettings(bool hasSingleType, LightType lightType, bool isArea)
		{
			settings.DrawShadowsType();
			if (settings.shadowsType.hasMultipleDifferentValues ||
				settings.shadowsType.intValue == (int)LightShadows.None)
				return;

			EditorGUI.indentLevel++;
			bool isBakedOrMixed = settings.lightmapping.intValue != (int)LightmapBakeType.Realtime;
			if (hasSingleType && (lightType == LightType.Point || lightType == LightType.Spot) && isBakedOrMixed)
				settings.DrawShapeRadius();
			if (hasSingleType && lightType == LightType.Directional && isBakedOrMixed)
				settings.DrawBakedShadowAngle();
			if (!isArea && settings.lightmapping.intValue != (int)LightmapBakeType.Baked)
				settings.DrawRuntimeShadow();
			EditorGUI.indentLevel--;
		}

		/// <summary>全ての選択Lightに追加データを保証する。</summary>
		private void EnsureAdditionalData()
		{
			foreach (Object item in targets)
			{
				Light light = (Light)item;
				if (!light.TryGetComponent<TrpLightData>(out var data))
					data = Undo.AddComponent<TrpLightData>(light.gameObject);

				// 以前の実装で完全非表示にした既存データも、コンポーネント見出しを再表示する。
				if ((data.hideFlags & HideFlags.HideInInspector) == 0) continue;
				Undo.RecordObject(data, "Show TRP Light Data");
				data.hideFlags &= ~HideFlags.HideInInspector;
				EditorUtility.SetDirty(data);
			}
		}

		private void DrawSoftShadowSettings()
		{
			foreach (Object item in targets)
				if (((Light)item).shadows != LightShadows.Soft) return;

			((Light)target).TryGetComponent<TrpLightData>(out var first);
			float radius = first != null ? first.BlurRadius : DefaultBlurRadius;
			SoftShadowQuality quality = first != null ? first.Quality : SoftShadowQuality.Medium;
			bool mixedRadius = false;
			bool mixedQuality = false;
			foreach (Object item in targets)
			{
				((Light)item).TryGetComponent<TrpLightData>(out var value);
				mixedRadius |= (value != null ? value.BlurRadius : DefaultBlurRadius) != radius;
				mixedQuality |= (value != null ? value.Quality : SoftShadowQuality.Medium) != quality;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("TRP Soft Shadow", EditorStyles.boldLabel);
			EditorGUI.showMixedValue = mixedRadius;
			EditorGUI.BeginChangeCheck();
			radius = Mathf.Max(0, EditorGUILayout.FloatField(
				new GUIContent("Blur Radius", "基準位置でのテクセル半径。Cascadeやライトからの距離に応じて同じワールド幅へ補正されます。"), radius));
			bool radiusChanged = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = mixedQuality;
			EditorGUI.BeginChangeCheck();
			quality = (SoftShadowQuality)EditorGUILayout.EnumPopup(new GUIContent("Quality", "Low: 4 / Medium: 8 / High: 16 samples"), quality);
			bool qualityChanged = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = false;
			if (!radiusChanged && !qualityChanged) return;

			foreach (Object item in targets)
			{
				Light light = (Light)item;
				if (!light.TryGetComponent<TrpLightData>(out var data))
					data = Undo.AddComponent<TrpLightData>(light.gameObject);
				using SerializedObject serialized = new(data);
				if (radiusChanged) serialized.FindProperty("_blurRadius").floatValue = radius;
				if (qualityChanged) serialized.FindProperty("_quality").enumValueIndex = (int)quality;
				serialized.ApplyModifiedProperties();
			}
		}
	}
}
