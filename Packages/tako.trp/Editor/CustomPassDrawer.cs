using System.Collections.Generic;
using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor
{
	/// <summary>
	/// カメラ・共通設定・Editor専用の各リストで、参照先のCustomPass設定を直接編集する。
	/// ランタイムの型にはEditor用の依存を追加しない。
	/// </summary>
	[CustomPropertyDrawer(typeof(CustomPass))]
	public sealed class CustomPassDrawer : PropertyDrawer
	{
		// パスが別のCustomPassを参照する場合も、循環参照でInspectorが再帰し続けないようにする。
		private static readonly HashSet<CustomPassObject> DrawingPasses = new();
		private static readonly GUIContent SettingsLabel = new("Pass Settings (Shared Asset)",
			"参照先のSOアセットを編集します。同じSOを使用するすべてのCustomPassに反映されます。");
		private static float LineHeight => EditorGUIUtility.singleLineHeight;
		private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded) return LineHeight;

			float height = LineHeight + (LineHeight + Spacing) * 2;
			SerializedProperty reference = property.FindPropertyRelative("_passObject");
			CustomPassObject pass = GetPass(reference);
			if (!pass) return height;

			height += LineHeight + Spacing;
			if (reference.isExpanded) height += DrawPassSettings(default, pass, false);
			return height;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			using (new EditorGUI.PropertyScope(position, label, property))
			{
				Rect row = new(position.x, position.y, position.width, LineHeight);
				property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
				if (!property.isExpanded) return;

				using (new EditorGUI.IndentLevelScope())
				{
					row.y += LineHeight + Spacing;
					SerializedProperty reference = property.FindPropertyRelative("_passObject");
					EditorGUI.PropertyField(row, reference);
					row.y += LineHeight + Spacing;
					EditorGUI.PropertyField(row, property.FindPropertyRelative(nameof(CustomPass.Enabled)));

					// 複数選択で参照先が異なる場合は、いずれか一方のSOを誤って編集させない。
					CustomPassObject pass = GetPass(reference);
					if (!pass) return;
					row.y += LineHeight + Spacing;
					reference.isExpanded = EditorGUI.Foldout(row, reference.isExpanded, SettingsLabel, true);
					if (!reference.isExpanded) return;

					row.y += LineHeight + Spacing;
					using (new EditorGUI.IndentLevelScope())
					{
						DrawPassSettings(row, pass, true);
					}
				}
			}
		}

		private static CustomPassObject GetPass(SerializedProperty reference)
		{
			return reference.hasMultipleDifferentValues ? null : reference.objectReferenceValue as CustomPassObject;
		}

		/// <summary>
		/// 高さ計算と描画で同じプロパティを走査し、配列や折りたたみ項目の高さを一致させる。
		/// SO自身のSerializedObjectへ適用することで、Undo・Dirty・OnValidateを通常のInspectorと同様に扱う。
		/// </summary>
		private static float DrawPassSettings(Rect position, CustomPassObject pass, bool draw)
		{
			if (!DrawingPasses.Add(pass)) return 0f;

			try
			{
				using SerializedObject serializedPass = new(pass);
				serializedPass.Update();
				SerializedProperty child = serializedPass.GetIterator();
				float height = 0f;
				bool enterChildren = true;
				while (child.NextVisible(enterChildren))
				{
					enterChildren = false;
					// スクリプト参照は設定ではないため、埋め込み表示では省略する。
					if (child.propertyPath == "m_Script") continue;

					float childHeight = EditorGUI.GetPropertyHeight(child, true);
					if (draw)
					{
						Rect childRect = new(position.x, position.y + height, position.width, childHeight);
						EditorGUI.PropertyField(childRect, child, true);
					}
					height += childHeight + Spacing;
				}
				if (draw) serializedPass.ApplyModifiedProperties();
				return height;
			}
			finally
			{
				DrawingPasses.Remove(pass);
			}
		}
	}
}
