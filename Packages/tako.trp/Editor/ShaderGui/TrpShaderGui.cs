using TakoLib.Common;
using TakoLibEditor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrpEditor.ShaderGui
{
	/// <summary>
	/// TRP シェーダー共通の GUI 基底クラス。
	/// </summary>
	public abstract class TrpShaderGui : TakoLibShaderGui
	{
		/// <summary>
		/// AlphaBlend が変更しないプロパティを Basic タブへ描画する。
		/// </summary>
		protected void BasicPropertiesGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			foreach (MaterialProperty property in properties)
			{
				if (IsAlphaBlendControlledProperty(property.name) ||
				    property.name == "_VertexColorBlend" ||
				    (property.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
				{
					continue;
				}

				materialEditor.ShaderProperty(property, property.displayName);
			}
		}

		/// <summary>
		/// AlphaBlend が変更する実パラメーターを Advanced タブへ描画する。
		/// RenderQueue・Pass・Keyword は TakoLibShaderGui の共通 Advanced GUI が描画する。
		/// </summary>
		protected override void AdvancedGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			foreach (MaterialProperty property in properties)
			{
				if (IsAlphaBlendControlledProperty(property.name))
				{
					materialEditor.ShaderProperty(property, property.displayName);
				}
			}
		}

		/// <summary>
		/// 実際の描画設定からアルファブレンド方式を判定して表示する。
		/// エディタ表示専用の MaterialProperty は保持しない。
		/// </summary>
		protected AlphaBlendMode TrpColorBlendGui(MaterialEditor materialEditor, Material material)
		{
			AlphaBlendMode alphaBlendMode = AlphaBlendMode.Custom;
			if (HasAlphaBlendProperties(material))
			{
				alphaBlendMode = GetAlphaBlendMode(material);

				EditorGUI.BeginChangeCheck();
				alphaBlendMode = (AlphaBlendMode)EditorGUILayout.EnumPopup("AlphaBlend", alphaBlendMode);
				if (EditorGUI.EndChangeCheck())
				{
					materialEditor.RegisterPropertyChangeUndo("Alpha Blend");
				}

				SetAlphaBlendMode(material, alphaBlendMode);
			}

			// 頂点カラーブレンドはシェーダー実行時にも参照されるため、実プロパティとして扱う。
			if (material.HasProperty(ShaderUtility.IdVertexColorBlend))
			{
				VertexColorBlendMode vertexColorBlendMode =
					(VertexColorBlendMode)Mathf.RoundToInt(material.GetFloat(ShaderUtility.IdVertexColorBlend));

				EditorGUI.BeginChangeCheck();
				vertexColorBlendMode =
					(VertexColorBlendMode)EditorGUILayout.EnumPopup("VertexColorBlend", vertexColorBlendMode);
				if (EditorGUI.EndChangeCheck())
				{
					materialEditor.RegisterPropertyChangeUndo("Vertex Color Blend");
					ShaderUtility.SetVertexColorBlendMode(material, vertexColorBlendMode);
				}
			}

			return alphaBlendMode;
		}

		/// <summary>
		/// 不透明キューかつ深度書き込みが有効なら、深度・影用パスを使用する。
		/// </summary>
		protected static bool UsesOpaquePasses(Material material)
		{
			bool isOpaqueQueue = material.renderQueue <= (int)RenderQueue.GeometryLast;
			bool writesDepth = !material.HasProperty(ShaderUtility.IdZWrite) ||
			                   material.GetFloat(ShaderUtility.IdZWrite) > 0.5f;
			return isOpaqueQueue && writesDepth;
		}

		private static bool HasAlphaBlendProperties(Material material)
		{
			return material.HasProperty(ShaderUtility.IdBlendSrc) &&
			       material.HasProperty(ShaderUtility.IdBlendDst) &&
			       material.HasProperty(ShaderUtility.IdBlendOp) &&
			       material.HasProperty(ShaderUtility.IdMultiplyRgbA);
		}

		private static bool IsAlphaBlendControlledProperty(string propertyName)
		{
			return propertyName is
				"_BlendSrc" or
				"_BlendDst" or
				"_BlendOp" or
				"_MultiplyRgbA" or
				"_ZWrite";
		}

		private static AlphaBlendMode GetAlphaBlendMode(Material material)
		{
			BlendMode blendSrc = (BlendMode)Mathf.RoundToInt(material.GetFloat(ShaderUtility.IdBlendSrc));
			BlendMode blendDst = (BlendMode)Mathf.RoundToInt(material.GetFloat(ShaderUtility.IdBlendDst));
			BlendOp blendOp = (BlendOp)Mathf.RoundToInt(material.GetFloat(ShaderUtility.IdBlendOp));
			bool multiplyRgbA = material.GetFloat(ShaderUtility.IdMultiplyRgbA) > 0.5f;
			bool isTransparentQueue = material.renderQueue > (int)RenderQueue.GeometryLast;

			// Opaque と AlphaTest は同じ Blend 設定なので、RenderQueue で区別する。
			if (!isTransparentQueue && blendSrc == BlendMode.One && blendDst == BlendMode.Zero && blendOp == BlendOp.Add)
			{
				return material.renderQueue >= (int)RenderQueue.AlphaTest
					? AlphaBlendMode.AlphaTest
					: AlphaBlendMode.Opaque;
			}

			if (!isTransparentQueue) return AlphaBlendMode.Custom;

			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendOp.Add, true))
			{
				return AlphaBlendMode.Transparent;
			}
			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.SrcAlpha, BlendMode.One, BlendOp.Add, false))
			{
				return AlphaBlendMode.Additive;
			}
			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.DstColor, BlendMode.OneMinusSrcAlpha, BlendOp.Add, true))
			{
				return AlphaBlendMode.Mutiply;
			}
			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.SrcAlpha, BlendMode.OneMinusSrcColor, BlendOp.Add, true))
			{
				return AlphaBlendMode.Screen;
			}
			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.OneMinusDstColor, BlendMode.OneMinusSrcAlpha, BlendOp.Add, true))
			{
				return AlphaBlendMode.Nega;
			}
			if (Matches(blendSrc, blendDst, blendOp, multiplyRgbA,
				    BlendMode.SrcAlpha, BlendMode.One, BlendOp.ReverseSubtract, true))
			{
				return AlphaBlendMode.Subtractive;
			}

			return AlphaBlendMode.Custom;
		}

		private static bool Matches(
			BlendMode blendSrc,
			BlendMode blendDst,
			BlendOp blendOp,
			bool multiplyRgbA,
			BlendMode expectedSrc,
			BlendMode expectedDst,
			BlendOp expectedOp,
			bool expectedMultiplyRgbA)
		{
			return blendSrc == expectedSrc &&
			       blendDst == expectedDst &&
			       blendOp == expectedOp &&
			       multiplyRgbA == expectedMultiplyRgbA;
		}

		private static void SetAlphaBlendMode(Material material, AlphaBlendMode mode)
		{
			switch (mode)
			{
				case AlphaBlendMode.Custom:
					return;
				case AlphaBlendMode.Opaque:
				case AlphaBlendMode.AlphaTest:
					SetBlendSettings(material, BlendMode.One, BlendMode.Zero, false, BlendOp.Add);
					break;
				case AlphaBlendMode.Transparent:
					SetBlendSettings(material, BlendMode.One, BlendMode.OneMinusSrcAlpha, true, BlendOp.Add);
					break;
				case AlphaBlendMode.Additive:
					SetBlendSettings(material, BlendMode.SrcAlpha, BlendMode.One, false, BlendOp.Add);
					break;
				case AlphaBlendMode.Mutiply:
					SetBlendSettings(material, BlendMode.DstColor, BlendMode.OneMinusSrcAlpha, true, BlendOp.Add);
					break;
				case AlphaBlendMode.Screen:
					SetBlendSettings(material, BlendMode.SrcAlpha, BlendMode.OneMinusSrcColor, true, BlendOp.Add);
					break;
				case AlphaBlendMode.Nega:
					SetBlendSettings(material, BlendMode.OneMinusDstColor, BlendMode.OneMinusSrcAlpha, true, BlendOp.Add);
					break;
				case AlphaBlendMode.Subtractive:
					SetBlendSettings(material, BlendMode.SrcAlpha, BlendMode.One, true, BlendOp.ReverseSubtract);
					break;
			}

			if (mode == AlphaBlendMode.Opaque)
			{
				material.renderQueue = (int)RenderQueue.Geometry;
				SetZWriteIfPresent(material, true);
			}
			else if (mode == AlphaBlendMode.AlphaTest)
			{
				material.renderQueue = (int)RenderQueue.AlphaTest;
				SetZWriteIfPresent(material, true);
			}
			else
			{
				material.renderQueue = (int)RenderQueue.Transparent;
				SetZWriteIfPresent(material, false);
			}
		}

		private static void SetBlendSettings(
			Material material,
			BlendMode blendSrc,
			BlendMode blendDst,
			bool multiplyRgbA,
			BlendOp blendOp)
		{
			material.SetFloat(ShaderUtility.IdBlendSrc, (float)blendSrc);
			material.SetFloat(ShaderUtility.IdBlendDst, (float)blendDst);
			material.SetFloat(ShaderUtility.IdMultiplyRgbA, multiplyRgbA ? 1f : 0f);
			material.SetFloat(ShaderUtility.IdBlendOp, (float)blendOp);
		}

		private static void SetZWriteIfPresent(Material material, bool enabled)
		{
			if (material.HasProperty(ShaderUtility.IdZWrite))
			{
				material.SetFloat(ShaderUtility.IdZWrite, enabled ? 1f : 0f);
			}
		}
	}
}
