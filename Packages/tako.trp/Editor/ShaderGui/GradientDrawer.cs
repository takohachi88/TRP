using System;
using System.IO;
using System.Linq;
using TakoLibEditor.Common;
using Trp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrpEditor.ShaderGui
{
	/// <summary>
	/// Gradient Textureをマテリアルインスペクターから編集するDrawer。
	/// ShaderLabのTextureプロパティへ[Gradient]を付けて使用する。
	/// </summary>
	public sealed class GradientDrawer : MaterialPropertyDrawer
	{
		private const string ColorSpecifyModePropertyName = "_colorSpecifyMode";
		private const string ImporterGradientPropertyName = "_gradient";
		private const string SizePropertyName = "_size";
		private const string VerticalPropertyName = "_vertical";
		private const string WrapModePropertyName = "_wrapMode";
		private const string ImporterFilterModePropertyName = "_filterMode";
		private const string SpritePropertyName = "_sprite";
		private const string GradientModeName = "Gradient";

		private const string DataPropertyNamePropertyName = "_propertyName";
		private const string DataGradientPropertyName = "_gradient";
		private const string DataWidthPropertyName = "_width";
		private const string DataFilterModePropertyName = "_filterMode";
		private const string DataTexturePropertyName = "_texture";

		private const int DefaultWidth = 32;
		private const int MinWidth = 2;
		private const float ActionButtonWidth = 48f;
		private const string ClipboardPrefix = "TRP_GRADIENT:";

		private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
		private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;

		[Serializable]
		private sealed class GradientClipboardData
		{
			public int Version = 1;
			public int Mode;
			public Color[] Colors;
			public float[] ColorTimes;
			public float[] Alphas;
			public float[] AlphaTimes;
		}

		public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
		{
			int lineCount = 1;
			if (prop.hasMixedValue || prop.textureValue == null) return GetHeight(lineCount);

			if (TryGetGradientImporter(prop.textureValue, out _))
			{
				// Gradient、横幅、Filter Mode。
				lineCount += 3;
			}
			else if (TryGetEditableMaterial(editor, out Material material) &&
			         TryGetGradientData(material, prop.name, prop.textureValue, out _))
			{
				// Gradient、横幅、Filter Mode。
				lineCount += 3;
			}

			return GetHeight(lineCount);
		}

		public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
		{
			if (prop.propertyType != ShaderPropertyType.Texture)
			{
				EditorGUI.LabelField(position, label, "[Gradient] は Texture プロパティ専用です。");
				return;
			}

			Material material = null;
			MaterialGradientData data = null;
			bool canEditMaterial =
				!prop.hasMixedValue && TryGetEditableMaterial(editor, out material);
			bool hasMaterialGradient =
				canEditMaterial &&
				TryGetGradientData(material, prop.name, prop.textureValue, out data);
			string actionLabel = hasMaterialGradient
				? "Delete"
				: !prop.hasMixedValue && prop.textureValue == null ? "Create" : null;

			if (DrawTextureProperty(GetLineRect(ref position), prop, label, editor, actionLabel))
			{
				string propertyName = prop.name;
				if (hasMaterialGradient)
				{
					// サブアセットの削除はInspectorの描画完了後に行い、PropertiesGUIの再入を防ぐ。
					EditorApplication.delayCall += () => DeleteMaterialGradient(material, propertyName, data);
				}
				else
				{
					Material[] materials = editor.targets.OfType<Material>().ToArray();
					ShowCreateMenu(propertyName, materials, canEditMaterial ? material : null);
				}

				GUIUtility.ExitGUI();
			}

			if (prop.hasMixedValue || prop.textureValue == null) return;

			if (TryGetGradientImporter(prop.textureValue, out ProcedualTextureImporter importer))
			{
				DrawGradientImporterProperties(ref position, importer);
			}
			else if (hasMaterialGradient)
			{
				DrawMaterialGradientProperties(ref position, material, data);
			}
		}

		private static float GetHeight(int lineCount)
		{
			return lineCount * LineHeight + (lineCount - 1) * Spacing;
		}

		private static Rect GetLineRect(ref Rect position)
		{
			Rect result = new(position.x, position.y, position.width, LineHeight);
			position.y += LineHeight + Spacing;
			return result;
		}

		private static bool DrawTextureProperty(
			Rect position,
			MaterialProperty prop,
			string label,
			MaterialEditor editor,
			string actionLabel)
		{
			Rect textureRect = position;
			Rect actionButtonRect = position;
			if (!string.IsNullOrEmpty(actionLabel))
			{
				actionButtonRect.xMin = actionButtonRect.xMax - ActionButtonWidth;
				textureRect.xMax = actionButtonRect.xMin - Spacing;
			}

			editor.BeginAnimatedCheck(textureRect, prop);
			MaterialEditor.BeginProperty(textureRect, prop);
			EditorGUI.showMixedValue = prop.hasMixedValue;

			EditorGUI.BeginChangeCheck();
			Texture texture = EditorGUI.ObjectField(textureRect, label, prop.textureValue, typeof(Texture2D), false) as Texture;
			if (EditorGUI.EndChangeCheck())
			{
				editor.RegisterPropertyChangeUndo(label);
				prop.textureValue = texture;
			}

			EditorGUI.showMixedValue = false;
			MaterialEditor.EndProperty();
			editor.EndAnimatedCheck();

			return !string.IsNullOrEmpty(actionLabel) && GUI.Button(actionButtonRect, actionLabel);
		}

		private static void ShowCreateMenu(
			string propertyName,
			Material[] materials,
			Material editableMaterial)
		{
			GenericMenu menu = new();
			menu.AddItem(
				new GUIContent("Standalone Asset"),
				false,
				() => CreateAndAssignStandaloneGradient(propertyName, materials));

			if (editableMaterial != null)
			{
				menu.AddItem(
					new GUIContent("Material Sub-Asset"),
					false,
					() => CreateAndAssignMaterialGradient(editableMaterial, propertyName));
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Material Sub-Asset"));
			}

			menu.ShowAsContext();
		}

		private static bool TryGetGradientImporter(Texture texture, out ProcedualTextureImporter importer)
		{
			importer = null;
			if (texture == null) return false;

			string assetPath = AssetDatabase.GetAssetPath(texture);
			if (string.IsNullOrEmpty(assetPath)) return false;

			importer = AssetImporter.GetAtPath(assetPath) as ProcedualTextureImporter;
			if (importer == null) return false;

			SerializedObject importerObject = new(importer);
			SerializedProperty modeProperty = importerObject.FindProperty(ColorSpecifyModePropertyName);
			return modeProperty != null && GetSelectedEnumName(modeProperty) == GradientModeName;
		}

		private static string GetSelectedEnumName(SerializedProperty enumProperty)
		{
			int index = enumProperty.enumValueIndex;
			string[] names = enumProperty.enumNames;
			return 0 <= index && index < names.Length ? names[index] : string.Empty;
		}

		private static void DrawGradientImporterProperties(
			ref Rect position,
			ProcedualTextureImporter importer)
		{
			SerializedObject importerObject = new(importer);
			importerObject.Update();

			SerializedProperty gradientProperty = importerObject.FindProperty(ImporterGradientPropertyName);
			SerializedProperty sizeProperty = importerObject.FindProperty(SizePropertyName);
			SerializedProperty filterModeProperty = importerObject.FindProperty(ImporterFilterModePropertyName);
			if (gradientProperty == null || sizeProperty == null || filterModeProperty == null) return;

			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel++;
			try
			{
				Rect gradientRect = GetLineRect(ref position);
				DrawGradientProperty(
					gradientRect,
					gradientProperty,
					gradient => ApplyImporterGradient(importer, gradient));

				Vector2Int size = sizeProperty.vector2IntValue;
				size.x = Mathf.Max(MinWidth, EditorGUI.IntField(GetLineRect(ref position), "Width", size.x));
				size.y = 1;
				sizeProperty.vector2IntValue = size;

				EditorGUI.PropertyField(GetLineRect(ref position), filterModeProperty, new GUIContent("Filter Mode"));
			}
			finally
			{
				EditorGUI.indentLevel--;
			}

			if (!EditorGUI.EndChangeCheck()) return;

			// Importerの設定を保存して再生成し、参照中のTexture2Dを即座に更新する。
			importerObject.ApplyModifiedProperties();
			importer.SaveAndReimport();
		}

		private static void CreateAndAssignStandaloneGradient(string propertyName, Material[] materials)
		{
			string assetPath = EditorUtility.SaveFilePanelInProject(
				"Create Gradient Ramp",
				"New Gradient Ramp",
				"procedualtexture",
				"Gradient Ramp の保存先を選択してください。");
			if (string.IsNullOrEmpty(assetPath)) return;

			// ScriptedImporterが認識する空ファイルを作成し、初回Import後にRamp用設定へ揃える。
			File.WriteAllBytes(assetPath, new byte[1]);
			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

			ProcedualTextureImporter importer = AssetImporter.GetAtPath(assetPath) as ProcedualTextureImporter;
			if (importer == null)
			{
				Debug.LogError($"[{nameof(GradientDrawer)}] Procedual Texture の作成に失敗しました: {assetPath}");
				return;
			}

			ConfigureNewStandaloneGradient(importer);

			Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
			if (texture == null)
			{
				Debug.LogError($"[{nameof(GradientDrawer)}] 作成した Texture2D を読み込めませんでした: {assetPath}");
				return;
			}

			Undo.RecordObjects(materials, "Create Gradient Ramp");
			foreach (Material material in materials)
			{
				if (material == null || !material.HasProperty(propertyName)) continue;

				material.SetTexture(propertyName, texture);
				EditorUtility.SetDirty(material);
			}

			EditorGUIUtility.PingObject(texture);
		}

		private static void ConfigureNewStandaloneGradient(ProcedualTextureImporter importer)
		{
			SerializedObject importerObject = new(importer);
			importerObject.Update();

			SerializedProperty modeProperty = importerObject.FindProperty(ColorSpecifyModePropertyName);
			SerializedProperty gradientProperty = importerObject.FindProperty(ImporterGradientPropertyName);
			SerializedProperty sizeProperty = importerObject.FindProperty(SizePropertyName);
			SerializedProperty verticalProperty = importerObject.FindProperty(VerticalPropertyName);
			SerializedProperty wrapModeProperty = importerObject.FindProperty(WrapModePropertyName);
			SerializedProperty filterModeProperty = importerObject.FindProperty(ImporterFilterModePropertyName);
			SerializedProperty spriteProperty = importerObject.FindProperty(SpritePropertyName);

			SetEnumByName(modeProperty, GradientModeName);
			gradientProperty.gradientValue = CreateDefaultGradient();
			sizeProperty.vector2IntValue = new Vector2Int(DefaultWidth, 1);
			verticalProperty.boolValue = false;
			wrapModeProperty.enumValueIndex = (int)TextureWrapMode.Clamp;
			filterModeProperty.enumValueIndex = (int)FilterMode.Bilinear;
			spriteProperty.boolValue = false;

			importerObject.ApplyModifiedProperties();
			importer.SaveAndReimport();
		}

		private static void SetEnumByName(SerializedProperty enumProperty, string valueName)
		{
			if (enumProperty == null) return;

			int index = Array.IndexOf(enumProperty.enumNames, valueName);
			if (0 <= index) enumProperty.enumValueIndex = index;
		}

		private static bool TryGetEditableMaterial(MaterialEditor editor, out Material material)
		{
			material = null;
			if (editor.targets.Length != 1 || editor.target is not Material targetMaterial) return false;

			string assetPath = AssetDatabase.GetAssetPath(targetMaterial);
			if (string.IsNullOrEmpty(assetPath)) return false;
			if (!string.Equals(Path.GetExtension(assetPath), ".mat", StringComparison.OrdinalIgnoreCase) ||
			    AssetDatabase.LoadMainAssetAtPath(assetPath) != targetMaterial)
			{
				return false;
			}

			material = targetMaterial;
			return true;
		}

		private static bool TryGetGradientData(
			Material material,
			string propertyName,
			Texture texture,
			out MaterialGradientData data)
		{
			data = null;
			if (material == null || texture == null) return false;

			string materialPath = AssetDatabase.GetAssetPath(material);
			data = AssetDatabase.LoadAllAssetsAtPath(materialPath)
				.OfType<MaterialGradientData>()
				.FirstOrDefault(candidate =>
					candidate.PropertyName == propertyName && candidate.Texture == texture);
			return data != null;
		}

		private static void DrawMaterialGradientProperties(
			ref Rect position,
			Material material,
			MaterialGradientData data)
		{
			SerializedObject dataObject = new(data);
			dataObject.Update();

			SerializedProperty gradientProperty = dataObject.FindProperty(DataGradientPropertyName);
			SerializedProperty widthProperty = dataObject.FindProperty(DataWidthPropertyName);
			SerializedProperty filterModeProperty = dataObject.FindProperty(DataFilterModePropertyName);
			if (gradientProperty == null || widthProperty == null || filterModeProperty == null) return;

			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel++;
			try
			{
				Rect gradientRect = GetLineRect(ref position);
				DrawGradientProperty(
					gradientRect,
					gradientProperty,
					gradient => ApplyMaterialGradient(material, data, gradient));
				widthProperty.intValue = Mathf.Max(
					MinWidth,
					EditorGUI.IntField(GetLineRect(ref position), "Width", widthProperty.intValue));
				EditorGUI.PropertyField(GetLineRect(ref position), filterModeProperty, new GUIContent("Filter Mode"));
			}
			finally
			{
				EditorGUI.indentLevel--;
			}

			if (!EditorGUI.EndChangeCheck()) return;

			dataObject.ApplyModifiedProperties();
			Undo.RecordObject(data.Texture, "Edit Material Gradient");
			RegenerateTexture(data);
			EditorUtility.SetDirty(data);
			EditorUtility.SetDirty(data.Texture);
			EditorUtility.SetDirty(material);
		}

		private static void CreateAndAssignMaterialGradient(Material material, string propertyName)
		{
			if (material == null || !material.HasProperty(propertyName)) return;

			string materialPath = AssetDatabase.GetAssetPath(material);
			if (string.IsNullOrEmpty(materialPath)) return;

			// プロパティを一度空にしても既存のサブアセットがあれば再利用し、重複生成を避ける。
			MaterialGradientData existingData = AssetDatabase.LoadAllAssetsAtPath(materialPath)
				.OfType<MaterialGradientData>()
				.FirstOrDefault(candidate => candidate.PropertyName == propertyName && candidate.Texture != null);
			if (existingData != null)
			{
				Undo.RecordObject(material, "Assign Material Gradient");
				material.SetTexture(propertyName, existingData.Texture);
				EditorUtility.SetDirty(material);
				AssetDatabase.SaveAssetIfDirty(material);
				EditorGUIUtility.PingObject(material);
				return;
			}

			MaterialGradientData data = ScriptableObject.CreateInstance<MaterialGradientData>();
			data.name = $"{propertyName}_GradientData";
			data.hideFlags = HideFlags.HideInHierarchy;

			Texture2D texture = new(DefaultWidth, 1, TextureFormat.RGBA32, false, false)
			{
				name = $"{propertyName}_Gradient",
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
			};

			ConfigureData(data, propertyName, texture);
			RegenerateTexture(data);

			Undo.RecordObject(material, "Create Material Gradient");
			AssetDatabase.AddObjectToAsset(data, material);
			AssetDatabase.AddObjectToAsset(texture, material);
			Undo.RegisterCreatedObjectUndo(data, "Create Material Gradient");
			Undo.RegisterCreatedObjectUndo(texture, "Create Material Gradient");

			EditorUtility.SetDirty(data);
			EditorUtility.SetDirty(texture);
			// 先にサブアセットを保存し、Texture2Dに安定したローカルfileIDを割り当てる。
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);

			Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			MaterialGradientData savedData = AssetDatabase.LoadAllAssetsAtPath(materialPath)
				.OfType<MaterialGradientData>()
				.FirstOrDefault(candidate => candidate.PropertyName == propertyName && candidate.Texture != null);
			if (savedMaterial == null || savedData == null)
			{
				Debug.LogError($"[{nameof(GradientDrawer)}] Material Gradient の再読み込みに失敗しました: {materialPath}");
				return;
			}

			// 永続化済みのTexture2Dを設定し、マテリアル側にもサブアセットのfileIDを保存する。
			Undo.RecordObject(savedMaterial, "Assign Material Gradient");
			savedMaterial.SetTexture(propertyName, savedData.Texture);
			EditorUtility.SetDirty(savedMaterial);
			AssetDatabase.SaveAssetIfDirty(savedMaterial);
			EditorGUIUtility.PingObject(savedMaterial);
		}

		private static void DrawGradientProperty(
			Rect position,
			SerializedProperty gradientProperty,
			Action<Gradient> pasteAction)
		{
			HandleGradientContextMenu(position, gradientProperty.gradientValue, pasteAction);
			EditorGUI.PropertyField(position, gradientProperty, new GUIContent("Gradient"));
		}

		private static void HandleGradientContextMenu(
			Rect position,
			Gradient gradient,
			Action<Gradient> pasteAction)
		{
			Event current = Event.current;
			if (current.type != EventType.ContextClick || !position.Contains(current.mousePosition)) return;

			GenericMenu menu = new();
			menu.AddItem(new GUIContent("Copy"), false, () => CopyGradient(gradient));
			if (TryReadGradientFromClipboard(out _))
			{
				menu.AddItem(new GUIContent("Paste"), false, () =>
				{
					if (TryReadGradientFromClipboard(out Gradient pastedGradient))
						pasteAction(pastedGradient);
				});
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("Paste"));
			}

			menu.ShowAsContext();
			current.Use();
		}

		private static void CopyGradient(Gradient gradient)
		{
			if (gradient == null) return;

			GradientColorKey[] colorKeys = gradient.colorKeys;
			GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
			GradientClipboardData data = new()
			{
				Mode = (int)gradient.mode,
				Colors = new Color[colorKeys.Length],
				ColorTimes = new float[colorKeys.Length],
				Alphas = new float[alphaKeys.Length],
				AlphaTimes = new float[alphaKeys.Length],
			};

			for (int i = 0; i < colorKeys.Length; i++)
			{
				data.Colors[i] = colorKeys[i].color;
				data.ColorTimes[i] = colorKeys[i].time;
			}
			for (int i = 0; i < alphaKeys.Length; i++)
			{
				data.Alphas[i] = alphaKeys[i].alpha;
				data.AlphaTimes[i] = alphaKeys[i].time;
			}

			EditorGUIUtility.systemCopyBuffer = ClipboardPrefix + JsonUtility.ToJson(data);
		}

		private static bool TryReadGradientFromClipboard(out Gradient gradient)
		{
			gradient = null;
			string clipboard = EditorGUIUtility.systemCopyBuffer;
			if (string.IsNullOrEmpty(clipboard) || !clipboard.StartsWith(ClipboardPrefix, StringComparison.Ordinal))
				return false;

			GradientClipboardData data;
			try
			{
				data = JsonUtility.FromJson<GradientClipboardData>(clipboard[ClipboardPrefix.Length..]);
			}
			catch (ArgumentException)
			{
				return false;
			}

			if (data == null || data.Version != 1 ||
				data.Colors == null || data.ColorTimes == null || data.Colors.Length != data.ColorTimes.Length || data.Colors.Length == 0 ||
				data.Alphas == null || data.AlphaTimes == null || data.Alphas.Length != data.AlphaTimes.Length || data.Alphas.Length == 0)
			{
				return false;
			}

			GradientColorKey[] colorKeys = new GradientColorKey[data.Colors.Length];
			GradientAlphaKey[] alphaKeys = new GradientAlphaKey[data.Alphas.Length];
			for (int i = 0; i < colorKeys.Length; i++)
				colorKeys[i] = new GradientColorKey(data.Colors[i], data.ColorTimes[i]);
			for (int i = 0; i < alphaKeys.Length; i++)
				alphaKeys[i] = new GradientAlphaKey(data.Alphas[i], data.AlphaTimes[i]);

			gradient = new Gradient
			{
				mode = (GradientMode)data.Mode,
			};
			gradient.SetKeys(colorKeys, alphaKeys);
			return true;
		}

		private static void ApplyImporterGradient(ProcedualTextureImporter importer, Gradient gradient)
		{
			if (importer == null || gradient == null) return;

			Undo.RecordObject(importer, "Paste Gradient");
			SerializedObject importerObject = new(importer);
			importerObject.Update();
			importerObject.FindProperty(ImporterGradientPropertyName).gradientValue = gradient;
			importerObject.ApplyModifiedProperties();
			importer.SaveAndReimport();
		}

		private static void ApplyMaterialGradient(Material material, MaterialGradientData data, Gradient gradient)
		{
			if (material == null || data == null || data.Texture == null || gradient == null) return;

			Undo.RecordObjects(new UnityEngine.Object[] { material, data, data.Texture }, "Paste Gradient");
			SerializedObject dataObject = new(data);
			dataObject.Update();
			dataObject.FindProperty(DataGradientPropertyName).gradientValue = gradient;
			dataObject.ApplyModifiedProperties();
			RegenerateTexture(data);
			EditorUtility.SetDirty(data);
			EditorUtility.SetDirty(data.Texture);
			EditorUtility.SetDirty(material);
		}

		private static void DeleteMaterialGradient(
			Material material,
			string propertyName,
			MaterialGradientData data)
		{
			if (material == null || data == null || data.Texture == null) return;

			string materialPath = AssetDatabase.GetAssetPath(material);
			if (string.IsNullOrEmpty(materialPath) ||
			    AssetDatabase.GetAssetPath(data) != materialPath ||
			    AssetDatabase.GetAssetPath(data.Texture) != materialPath)
			{
				return;
			}

			Texture2D texture = data.Texture;
			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Delete Material Gradient");
			Undo.RecordObject(material, "Delete Material Gradient");
			material.SetTexture(propertyName, null);
			EditorUtility.SetDirty(material);

			// マテリアル参照を解除してから、対応するTextureと設定データだけを削除する。
			Undo.DestroyObjectImmediate(texture);
			Undo.DestroyObjectImmediate(data);
			Undo.CollapseUndoOperations(undoGroup);

			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
		}

		private static void ConfigureData(
			MaterialGradientData data,
			string propertyName,
			Texture2D texture)
		{
			SerializedObject dataObject = new(data);
			dataObject.FindProperty(DataPropertyNamePropertyName).stringValue = propertyName;
			dataObject.FindProperty(DataGradientPropertyName).gradientValue = CreateDefaultGradient();
			dataObject.FindProperty(DataWidthPropertyName).intValue = DefaultWidth;
			dataObject.FindProperty(DataFilterModePropertyName).enumValueIndex = (int)FilterMode.Bilinear;
			dataObject.FindProperty(DataTexturePropertyName).objectReferenceValue = texture;
			dataObject.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void RegenerateTexture(MaterialGradientData data)
		{
			if (data == null || data.Texture == null) return;

			Gradient gradient = data.Gradient;
			int width = Mathf.Max(MinWidth, data.Width);

			Texture2D texture = data.Texture;
			texture.Reinitialize(width, 1, TextureFormat.RGBA32, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = data.FilterMode;

			Color[] pixels = new Color[width];
			float widthMinusOneRcp = 1f / (width - 1);
			for (int x = 0; x < width; x++)
			{
				pixels[x] = gradient.Evaluate(x * widthMinusOneRcp);
			}

			texture.SetPixels(pixels);
			texture.Apply(false, false);
		}

		private static Gradient CreateDefaultGradient()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[]
				{
					new GradientColorKey(Color.black, 0f),
					new GradientColorKey(Color.white, 1f),
				},
				new[]
				{
					new GradientAlphaKey(1f, 0f),
					new GradientAlphaKey(1f, 1f),
				});
			return gradient;
		}
	}
}
