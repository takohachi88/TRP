using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor.Rendering;
#endif

namespace Trp
{
	[CreateAssetMenu(menuName = "Rendering/Trp/TrpGlobalSettings", fileName = "TrpGlobalSettings")]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class TrpGlobalSettings : RenderPipelineGlobalSettings<TrpGlobalSettings, Trp>
	{
		[SerializeField] private RenderPipelineGraphicsSettingsContainer _settings = new();

		protected override List<IRenderPipelineGraphicsSettings> settingsList => _settings.settingsList;

		public override void Reset()
		{
#if UNITY_EDITOR
			//この処理がないとBlitter.Initializeでエラーになる？
			EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(this);
			Initialize();
#endif
		}
	}
}
