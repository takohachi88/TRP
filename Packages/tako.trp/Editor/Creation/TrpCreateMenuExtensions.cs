using TakoLibEditor.Common;
using UnityEditor;

namespace TrpEditor
{
	/// <summary>
	/// TRP用のファイル生成メニュー。
	/// </summary>
	public class TrpCreateMenuExtensions : CreateMenuExtensions<TrpCreateMenuExtensions>
	{
		private const string TrpMenuItemShaderRoot = MenuItemShaderRoot + "TRP/";
		private const string TrpMenuItemCsRoot = MenuItemCsRoot + "TRP/";

		/// <summary>
		/// TRPのCustomPassObjectのC#ファイルを作成する。
		/// </summary>
		[MenuItem(TrpMenuItemCsRoot + "Custom Pass Object", priority = 0)]
		private static void CreateTrpCustomPassObject() => CreateFile(FileType.Cs, "TrpCustomPassObject", "CustomPassObject");

		/// <summary>
		/// TRPのUI標準シェーダーを作成する。
		/// </summary>
		[MenuItem(TrpMenuItemShaderRoot + "UI Default Shader", priority = -1000)]
		private static void CreateTrpUiDefaultShader() => CreateFile(FileType.Shader, "TrpUiDefault", "UiDefault");

		/// <summary>
		/// TRPのSpriteUnlitシェーダーを作成する。
		/// </summary>
		[MenuItem(TrpMenuItemShaderRoot + "Sprite Unlit Shader", priority = -999)]
		private static void CreateTrpSpriteUnlitShader() => CreateFile(FileType.Shader, "TrpSpriteUnlit", "SpriteUnlit");
	}
}