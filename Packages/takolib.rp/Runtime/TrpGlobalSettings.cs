using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor.Rendering;
#endif

namespace TakoLib.Rp
{
	[CreateAssetMenu(menuName = "Rendering/Trp/TrpGlobalSettings", fileName = "TrpGlobalSettings")]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class TrpGlobalSettings : RenderPipelineGlobalSettings
	{
		[SerializeField] private RenderPipelineGraphicsSettingsContainer _settings = new();

		protected override List<IRenderPipelineGraphicsSettings> settingsList => _settings.settingsList;

		private void Reset()
		{
#if UNITY_EDITOR
			//この処理がないとBlitter.Initializeでエラーになる？
			EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(this);
			Initialize();
#endif
		}
	}
}
