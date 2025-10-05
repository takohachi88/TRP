using TakoLib.Common;
using TakoLibEditor.Common;
using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class UnlitGui : TakoLibShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;
			ColorBlendGui(material);

			EditorGUILayout.Space();

			materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Cull");

			// DepthNormalsパスの有効/無効を切り替え。
			AlphaBlendMode alphaBlendMode = (AlphaBlendMode)material.GetFloat(ShaderUtility.IdAlphaBlend);
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, alphaBlendMode is AlphaBlendMode.Opaque or AlphaBlendMode.AlphaTest);
		}
	}
}
