using TakoLib.Common;
using TakoLibEditor.Common;
using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class ParticleUnlitGui : TakoLibShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;
			ColorBlendGui(material);

			EditorGUILayout.Space();

			materialEditor.FloatProperty(FindProperty("_Rgb", properties), "Rgb");
			materialEditor.FloatProperty(FindProperty("_A", properties), "A");

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_SOFT_PARTICLE", properties), "SoftParticle");
			EditorGUI.indentLevel++;
			materialEditor.FloatProperty(FindProperty("_Near", properties), "Near");
			materialEditor.FloatProperty(FindProperty("_Far", properties), "Far");
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Cull");

			// DepthNormalsパスの有効/無効を切り替え。
			AlphaBlendMode alphaBlendMode = (AlphaBlendMode)material.GetFloat(ShaderUtility.IdAlphaBlend);
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, alphaBlendMode is AlphaBlendMode.Opaque or AlphaBlendMode.AlphaTest);
		}
	}
}
