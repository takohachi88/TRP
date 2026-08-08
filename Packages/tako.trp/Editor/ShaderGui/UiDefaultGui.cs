using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class UiDefaultGui : TrpShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;
			TrpColorBlendGui(materialEditor, material);

			EditorGUILayout.Space();
			BasicPropertiesGui(materialEditor, properties);
		}
	}
}
