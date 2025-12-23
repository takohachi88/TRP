using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Trp
{
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	[DisplayInfo(name = "TRP Global Settings Asset")]
	[DisplayName("TRP")]
	public class TrpGlobalSettings : RenderPipelineGlobalSettings<TrpGlobalSettings, Trp>
	{
		//他の命名規則と異なるが、m_SettingListとsettingListの二つは必ずこの命名でなければGraphicsSettingsに表示されない。
		//GraphicsSettingsInspectorUtility.TryGetSettingsListFromRenderPipelineGlobalSettingsメソッドからプロパティ名を文字列で指定されているため。
		[SerializeField] RenderPipelineGraphicsSettingsContainer m_Settings = new();
		protected override List<IRenderPipelineGraphicsSettings> settingsList => m_Settings.settingsList;

		public static void Ensure()
		{
#if UNITY_EDITOR
			//この処理がないとBlitter.Initializeでエラーになる。
			TrpGlobalSettings globalSettings = GraphicsSettings.GetSettingsForRenderPipeline<Trp>() as TrpGlobalSettings;
			if (RenderPipelineGlobalSettingsUtils.TryEnsure<TrpGlobalSettings, Trp>(ref globalSettings, "Assets/TrpGlobalSettings.asset", true))
			{
				AssetDatabase.SaveAssetIfDirty(globalSettings);
			}
#endif
		}
	}
}
