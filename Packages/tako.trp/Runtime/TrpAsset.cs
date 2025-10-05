using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Trp
{
	/// <summary>
	/// TRPの共通設定。
	/// </summary>
	[Serializable]
	public class TrpCommonSettings
	{
		[SerializeField] private bool _useHdr = true;
		[SerializeField] private MSAASamples _msaaSamples;
		[SerializeField] private LightingForwardPlusSettings _lightingSettings;
		[SerializeField] private ShadowSettings _shadowSettings;
		[SerializeField] private bool _useOpaqueTextureOnReflection, _useDepthTextureOnReflection;
		[SerializeField, Min(4)] private int _postFxLutSize = 32;
		[SerializeField] private FilterMode _postFxLutFilterMode = FilterMode.Bilinear;
		[SerializeField, Min(1)] private int _defaultMaxBackbufferCameraCount = 16;
		[SerializeField, Min(1)] private int _defaultMaxRenderTextureCameraCount = 16;
		public List<CustomPass> CustomPasses;
		public bool UseHdr => _useHdr;
		public MSAASamples Msaa => _msaaSamples;
		public LightingForwardPlusSettings LightingSettings => _lightingSettings;
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
		public int DefaultMaxBackbufferCameraCount => _defaultMaxBackbufferCameraCount;
		public int DefaultMaxRenderTextureCameraCount => _defaultMaxRenderTextureCameraCount;
	}

	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU + "TrpAsset", fileName = "TrpAsset")]
	public class TrpAsset : RenderPipelineAsset<Trp>
	{
		[SerializeField] private TrpCommonSettings _commonSettings;

#if UNITY_EDITOR
		[SerializeField] private bool _useSmoothNormalImporter = true;
		[SerializeField] private string _smoothNormalImportRegex = "_outline";
		[SerializeField, Min(2)] private int _smoothNormalTexcoordIndex = 2;
		public bool UseSmoothNormalImporter => _useSmoothNormalImporter;
		public string SmoothNormalImportRegex => _smoothNormalImportRegex;
		public int SmoothNormalTexcoordIndex => _smoothNormalTexcoordIndex;
#endif

		/// <summary>
		/// シェーダーのRenderPipelineタグで指定するタグ名。
		/// </summary>
		public override string renderPipelineShaderTag => "Trp";

		private TrpResources _resources;

		public override Shader defaultShader => _resources?.UnlitShader;
		public override Material defaultMaterial => _resources?.UnlitMaterial; //TODO:Litにする。
		public override Material default2DMaterial => _resources?.SpriteUnlitMaterial;
		public override Material defaultUIMaterial => _resources?.UiMaterial;
		public override Material defaultParticleMaterial => _resources?.ParticleUnlitMaterial;

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