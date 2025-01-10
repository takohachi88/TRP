using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TakoLib.Rp.PostFx;

#if UNITY_EDITOR
using UnityEditor.Rendering;
#endif

namespace TakoLib.Rp
{
	/// <summary>
	/// TRPの共通設定。
	/// </summary>
	[Serializable]
	public class TrpCommonSettings
	{
		[SerializeField, Range(TrpConstants.RENDER_SCALE_MIN, TrpConstants.RENDER_SCALE_MAX)] private float _renderScale = 1f;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private MSAASamples _msaaSamples;
		[SerializeField] private DepthBits _depthBits = DepthBits.Depth32;
		[SerializeField] private ShadowSettings _shadowSettings;
		[SerializeField] private bool _useOpaqueTextureOnReflection, _useDepthTextureOnReflection;
		[SerializeField] private bool _useLightPerObject;

		public float RenderScale => _renderScale;
		public bool UseHdr => _useHdr;
		public MSAASamples Msaa => _msaaSamples;
		public DepthBits DepthBits => _depthBits;
		public ShadowSettings ShadowSettings => _shadowSettings;

		/// <summary>
		/// ReflectionProbeの描画でOpaqueTextureを生成するかどうか。
		/// </summary>
		public bool UseOaqueTextureOnReflection => _useOpaqueTextureOnReflection;
		/// <summary>
		/// ReflectionProbeの描画でDepthTextureを生成するかどうか。
		/// </summary>
		public bool UseDepthTextureOnReflection => _useDepthTextureOnReflection;

		public bool UseLightPerObject => _useLightPerObject;
	}


	[CreateAssetMenu(menuName = "Rendering/Trp/TrpAsset", fileName = "TrpAsset")]
	public class TrpAsset : RenderPipelineAsset<Trp>
	{
		[SerializeField] private TrpCommonSettings _commonSettings;
		[SerializeField] private PostFxPassGroup _overridePostFxGroup;
		[SerializeField, HideInInspector] private InternalResources _internalResources;

		[SerializeField] private TrpGlobalSettings _globalSettings;

		protected override RenderPipeline CreatePipeline()
		{
#if UNITY_EDITOR
			_internalResources = AssetDatabase.LoadAssetAtPath<InternalResources>("Packages/takolib.rp/Runtime/Data/InternalResources.asset");

			//この処理がないとBlitter.Initializeでエラーになる。
			if (!_globalSettings)
			{
				_globalSettings = RenderPipelineGlobalSettingsUtils.Create<TrpGlobalSettings>("Assets/TrpGlobalSettings.asset");
				EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<Trp>(_globalSettings);
			}
#endif

			return new Trp(_commonSettings, _internalResources, _overridePostFxGroup);
		}
	}
}