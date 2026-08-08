using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class UnlitGui : TrpShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;

			TrpColorBlendGui(materialEditor, material);

			EditorGUILayout.Space();
			BasicPropertiesGui(materialEditor, properties);

			// 実際の RenderQueue と ZWrite から DepthNormals パスの有効/無効を切り替える。
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, UsesOpaquePasses(material));
		}
	}
}
