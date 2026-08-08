using Trp;
using UnityEditor;
using UnityEngine;

namespace TrpEditor.ShaderGui
{
	/// <summary>
	/// ライティング対応シェーダー共通の GUI。
	/// </summary>
	public abstract class LitGui : TrpShaderGui
	{
		protected override void BasicGui(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			Material material = materialEditor.target as Material;

			TrpColorBlendGui(materialEditor, material);

			EditorGUILayout.Space();
			BasicPropertiesGui(materialEditor, properties);

			// 半透明オブジェクトを深度・影マップへ書き込まない。
			bool enableOpaquePasses = UsesOpaquePasses(material);
			material.SetShaderPassEnabled(TrpConstants.PassNames.DEPTH_NORMALS_ONLY, enableOpaquePasses);
			material.SetShaderPassEnabled("ShadowCaster", enableOpaquePasses);
		}
	}

	public class ToonGui : LitGui
	{
	}

	public class PbrFilamentGui : LitGui
	{
	}
}
