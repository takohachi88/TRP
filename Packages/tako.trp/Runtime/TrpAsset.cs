using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TakoLib.Common.Extensions;


#if UNITY_EDITOR
using UnityEditor.Rendering;
#endif

namespace Trp
{
	/// <summary>
	/// TRPの共通設定。
	/// </summary>
	[Serializable]
	public class TrpCommonSettings
	{
		[SerializeField, Range(0.1f, 1f)] private float _renderScale = 1;
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private MSAASamples _msaaSamples;
		[SerializeField] private DepthBits _depthBits = DepthBits.Depth32;
		[SerializeField] private ShadowSettings _shadowSettings;
		[SerializeField] private bool _useOpaqueTextureOnReflection, _useDepthTextureOnReflection;
		[SerializeField, Min(4)] private int _postFxLutSize = 32;
		[SerializeField] private FilterMode _postFxLutFilterMode = FilterMode.Bilinear;
		[SerializeField, Min(1)] private int _defaultMaxBackbufferCameraCount = 16;
		[SerializeField, Min(1)] private int _defaultMaxRenderTextureCameraCount = 16;

		public float RenderScale => _renderScale;
		public bool UseScaledRendering => _renderScale < 0.9f;
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

		public int PostFxLutSize => _postFxLutSize;
		public FilterMode PostFxLutFilterMode => _postFxLutFilterMode;
		public int DefaultMaxbackbufferCameraCount => _defaultMaxBackbufferCameraCount;
		public int DefaultMaxRenderTextureCameraCount => _defaultMaxRenderTextureCameraCount;
	}

	[CreateAssetMenu(menuName = "Rendering/Trp/TrpAsset", fileName = "TrpAsset")]
	public class TrpAsset : RenderPipelineAsset<Trp>
	{
		[SerializeField] private TrpCommonSettings _commonSettings;

		/// <summary>
		/// シェーダーのRenderPipelineタグで指定するタグ名。
		/// </summary>
		public override string renderPipelineShaderTag => "Trp";

		private TrpResources _resources;

		public override Shader defaultShader => _resources?.UnlitShader;
		public override Material defaultMaterial => _resources?.UnlitMaterial; //TODO:Litにする。
		public override Material default2DMaterial => _resources?.SpriteUnlitMaterial;
		public override Material defaultUIMaterial => _resources?.UIMaterial;

		protected override RenderPipeline CreatePipeline()
		{
#if UNITY_EDITOR
			//この処理がないとBlitter.Initializeでエラーになる。
			TrpGlobalSettings globalSettings = GraphicsSettings.GetSettingsForRenderPipeline<Trp>() as TrpGlobalSettings;
			if (RenderPipelineGlobalSettingsUtils.TryEnsure<TrpGlobalSettings, Trp>(ref globalSettings, "Assets/TrpGlobalSettings.asset", true))
			{
				AssetDatabase.SaveAssetIfDirty(globalSettings);
			}
#endif
			_resources = GraphicsSettings.GetRenderPipelineSettings<TrpResources>();

			return new Trp(_commonSettings, _resources);
		}
	}
}