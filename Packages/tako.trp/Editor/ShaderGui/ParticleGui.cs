using TakoLib.Common;
using TakoLib.Common.Extensions;
using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class ParticleGui : TrpShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{

			EditorGUILayout.LabelField("Custom Vertex Stream情報");
			using (new EditorGUILayout.VerticalScope(GUI.skin.box))
			{
				EditorGUILayout.LabelField("time: TEXCOORD0.z");
				EditorGUILayout.LabelField("distortStrength: TEXCOORD0.w");
				EditorGUILayout.LabelField("dissolveProgress: TEXCOORD1.x");
				EditorGUILayout.LabelField("edgeWidth: TEXCOORD1.y");
				EditorGUILayout.LabelField("edgeColor: TEXCOORD1.zw, TEXCOORD2.xy");
			}

			EditorGUILayout.Space();

			Material material = materialEditor.target as Material;
			AlphaBlendMode alphaBlendMode = TrpColorBlendGui(materialEditor, material);

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_BASEUVMODE", properties), "BaseUvMode");
			materialEditor.TextureProperty(FindProperty("_BaseMap", properties), "BaseMap");
			materialEditor.FloatProperty(FindProperty("_Rgb", properties), "Rgb");
			materialEditor.FloatProperty(FindProperty("_A", properties), "A");

			EditorGUILayout.Space();

			if (alphaBlendMode == AlphaBlendMode.AlphaTest)
			{
				materialEditor.ShaderProperty(FindProperty("_Cutoff", properties), "Alpha Cutoff");
				EditorGUILayout.Space();
			}

			if (alphaBlendMode is not (AlphaBlendMode.Opaque or AlphaBlendMode.AlphaTest))
			{
				materialEditor.ShaderProperty(FindProperty("_SOFT_PARTICLE", properties), "SoftParticle");
				EditorGUI.indentLevel++;
				materialEditor.FloatProperty(FindProperty("_Near", properties), "Near");
				materialEditor.FloatProperty(FindProperty("_Far", properties), "Far");
				EditorGUI.indentLevel--;

				EditorGUILayout.Space();
			}

			bool useDistort = false;
			using (new EditorGUILayout.VerticalScope(GUI.skin.box))
			{
				MaterialProperty distortModeProperty = FindProperty("_DISTORTMODE", properties);
				materialEditor.ShaderProperty(distortModeProperty, "DistortMode");
				useDistort = ((int)distortModeProperty.floatValue).ToBool();
				if (useDistort)
				{
					EditorGUI.indentLevel++;
					materialEditor.TextureProperty(FindProperty("_DistortMap", properties), "DistortMap");
					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.Space();

			using (new EditorGUILayout.VerticalScope(GUI.skin.box))
			{
				MaterialProperty dissolveModeProperty = FindProperty("_DISSOLVEMODE", properties);
				materialEditor.ShaderProperty(dissolveModeProperty, "DissolveMode");
				if (((int)dissolveModeProperty.floatValue).ToBool())
				{
					EditorGUI.indentLevel++;
					materialEditor.TextureProperty(FindProperty("_DissolveMap", properties), "DissolveMap");
					materialEditor.ShaderProperty(FindProperty("_DissolveSmooth", properties), "DissolveSmooth");
					if (useDistort) ToggleProperty(FindProperty("_DistortDissolve", properties));
					materialEditor.ShaderProperty(FindProperty("_EdgeColorBlendMode", properties), "EdgeColorBlendMode");
					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.Space();

			using (new EditorGUILayout.VerticalScope(GUI.skin.box))
			{
				MaterialProperty litProperty = FindProperty("_LIT", properties);
				materialEditor.ShaderProperty(litProperty, "Lit");
				if (((int)litProperty.floatValue).ToBool())
				{
					EditorGUI.indentLevel++;

					materialEditor.ShaderProperty(FindProperty("_MaskMap", properties), "MaskMap (R: Metallic, G: AO, A: Smoothness)");
					materialEditor.ShaderProperty(FindProperty("_Metallic", properties), "Metallic");
					materialEditor.ShaderProperty(FindProperty("_Smoothness", properties), "Smoothness");
					materialEditor.ShaderProperty(FindProperty("_OcclusionStrength", properties), "OcclusionStrength");
					materialEditor.ShaderProperty(FindProperty("_BumpMap", properties), "BumpMap");
					materialEditor.ShaderProperty(FindProperty("_BumpScale", properties), "BumpScale");

					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Cull");

			// 深度・影パスは、不透明またはアルファテスト時のみ描画する。
			bool enableDepthPasses = UsesOpaquePasses(material);
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, enableDepthPasses);
			material.SetShaderPassEnabled("ShadowCaster", enableDepthPasses);

			if (alphaBlendMode == AlphaBlendMode.AlphaTest) material.EnableKeyword("_ALPHATEST");
			else material.DisableKeyword("_ALPHATEST");

			// Opaque の場合だけディゾルブ境界を硬く切る。
			if (enableDepthPasses && alphaBlendMode != AlphaBlendMode.AlphaTest) material.EnableKeyword("_ALPHAOPAQUE");
			else material.DisableKeyword("_ALPHAOPAQUE");
		}

		private void ToggleProperty(MaterialProperty property)
		{
			property.floatValue = EditorGUILayout.Toggle(property.displayName, ((int)property.floatValue).ToBool()).ToInt();
		}
	}
}
