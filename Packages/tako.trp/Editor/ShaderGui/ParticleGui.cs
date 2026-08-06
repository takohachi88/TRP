using TakoLib.Common;
using TakoLib.Common.Extensions;
using TakoLibEditor.Common;
using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class ParticleGui : TakoLibShaderGui
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
			ColorBlendGui(material);

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_BASEUVMODE", properties), "BaseUvMode");
			materialEditor.TextureProperty(FindProperty("_BaseMap", properties), "BaseMap");
			materialEditor.FloatProperty(FindProperty("_Rgb", properties), "Rgb");
			materialEditor.FloatProperty(FindProperty("_A", properties), "A");

			EditorGUILayout.Space();

			AlphaBlendMode alphaBlendMode = (AlphaBlendMode)material.GetFloat(ShaderUtility.IdAlphaBlend);
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
					if(useDistort) ToggleProperty(FindProperty("_DistortDissolve", properties));
					materialEditor.ShaderProperty(FindProperty("_EdgeColorBlendMode", properties), "EdgeColorBlendMode");
					EditorGUI.indentLevel--;
				}
			}

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Cull");

			// DepthNormalsパスの有効/無効を切り替え。
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, alphaBlendMode is AlphaBlendMode.Opaque or AlphaBlendMode.AlphaTest);

			if (alphaBlendMode == AlphaBlendMode.AlphaTest) material.EnableKeyword("_ALPHATEST");
			else material.DisableKeyword("_ALPHATEST");
		}

		private void ToggleProperty(MaterialProperty property)
		{
			property.floatValue = EditorGUILayout.Toggle(property.displayName, ((int)property.floatValue).ToBool()).ToInt();
		}
	}
}
