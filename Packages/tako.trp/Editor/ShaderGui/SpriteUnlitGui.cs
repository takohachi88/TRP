using TakoLibEditor.Common;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	public class SpriteUnlitGui : TakoLibShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;
			ColorBlendGui(material);
		}
	}
}
