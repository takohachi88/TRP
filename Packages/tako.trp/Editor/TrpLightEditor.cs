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
			DrawTrpShadowSettings();
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

		private void DrawTrpShadowSettings()
		{
			bool allSoft = true;
			bool hasDirectional = false;
			bool hasPunctual = false;
			Object[] dataTargets = new Object[targets.Length];
			for (int i = 0; i < targets.Length; i++)
			{
				Light light = (Light)targets[i];
				// TRPがリアルタイム影を描画するライトだけに設定を表示する。
				if (light.shadows == LightShadows.None || light.lightmapBakeType == LightmapBakeType.Baked ||
					light.type is not (LightType.Directional or LightType.Point or LightType.Spot)) return;
				allSoft &= light.shadows == LightShadows.Soft;
				hasDirectional |= light.type == LightType.Directional;
				hasPunctual |= light.type != LightType.Directional;
				light.TryGetComponent<TrpLightData>(out var data);
				dataTargets[i] = data;
			}

			// SerializedObjectで複数選択・Undo・Prefab Overrideを共通処理する。
			// この配列はEditor Inspector専用であり、毎フレームの描画処理には含まれない。
			using SerializedObject serialized = new(dataTargets);
			serialized.Update();
			if (allSoft)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("TRP Soft Shadow", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serialized.FindProperty("_shadowDithering"),
					new GUIContent("Shadow Dithering", "画素ごとにPCF／PCSSのサンプル配置を回転します。サンプル数は増やさず、フレーム番号では変化させません。オフ時は回転計算を省略します。"));
				SerializedProperty pcss = serialized.FindProperty("_pcss");
				EditorGUILayout.PropertyField(pcss, new GUIContent("PCSS", "遮蔽物との距離に応じて半影幅を変えます。オフ時は既存のPCFを使用し、遮蔽物探索を省略します。"));
				EditorGUILayout.PropertyField(serialized.FindProperty("_quality"),
					new GUIContent("Quality", "Low: 4 / Medium: 8 / High: 16 samples。PCSSでは探索とフィルターのそれぞれに使用します。"));
				if (!pcss.boolValue || pcss.hasMultipleDifferentValues)
					EditorGUILayout.PropertyField(serialized.FindProperty("_blurRadius"),
						new GUIContent("Blur Radius", "PCF用の基準テクセル半径。PCSS有効時は使用しません。"));
				if (pcss.boolValue || pcss.hasMultipleDifferentValues)
				{
					EditorGUI.indentLevel++;
					if (hasDirectional)
						EditorGUILayout.PropertyField(serialized.FindProperty("_angularDiameter"),
							new GUIContent("Angular Diameter", "Directional Lightの光源の角直径（度）。大きいほど半影が広がります。"));
					if (hasPunctual)
						EditorGUILayout.PropertyField(serialized.FindProperty("_sourceRadius"),
							new GUIContent("Source Radius", "Spot / Point Lightの仮想光源半径（ワールド単位）。"));
					EditorGUILayout.PropertyField(serialized.FindProperty("_maxPenumbraSize"),
						new GUIContent("Max Penumbra Size", "半影フィルターの最大半径（ワールド単位）。広げすぎるとサンプル不足が目立ちます。"));
					EditorGUILayout.PropertyField(serialized.FindProperty("_maxSamplingDistance"),
						new GUIContent("Max Sampling Distance", "半影幅の計算に使う遮蔽物との距離の上限（ワールド単位）。これより遠い遮蔽物も影を落とします。"));
					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("TRP Shadow Tint (PBR Only)", EditorStyles.boldLabel);
			SerializedProperty enableTint = serialized.FindProperty("_enableShadowTint");
			EditorGUILayout.PropertyField(enableTint,
				new GUIContent("Enable Shadow Tint", "PBRのみ影を着色します。オフ時はTintの色計算を省略します。Toonの影色はShadow Rampで設定してください。"));
			if (enableTint.boolValue || enableTint.hasMultipleDifferentValues)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(serialized.FindProperty("_shadowTint"), new GUIContent("Tint", "影の色。Penumbra Tint有効時は半影の色に使います。"));
				EditorGUILayout.PropertyField(serialized.FindProperty("_penumbraTint"),
					new GUIContent("Penumbra Tint", "完全な影は暗く保ち、半影だけを着色します。無彩色ではHDRPと同じく影全体のTintとして扱います。"));
				EditorGUI.indentLevel--;
			}
			serialized.ApplyModifiedProperties();
		}
	}
}
