using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

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
		[SerializeField] private LightCookieSettings _lightCookieSettings;
		[SerializeField] private ShadowSettings _shadowSettings;
		[SerializeField] private bool _useOpaqueTextureOnReflection, _useDepthTextureOnReflection;
		[SerializeField, Min(4)] private int _postFxLutSize = 32;
		[SerializeField] private FilterMode _postFxLutFilterMode = FilterMode.Bilinear;
		[SerializeField, Min(1)] private int _defaultMaxBackbufferCameraCount = 16;
		[SerializeField, Min(1)] private int _defaultMaxRenderTextureCameraCount = 16;
		[SerializeField] private LayerMask _opaqueLayerMask = ~0;
		[SerializeField] private LayerMask _transparentLayerMask = ~0;
		public List<CustomPass> CustomPasses;
		public bool UseHdr => _useHdr;
		public MSAASamples Msaa => _msaaSamples;
		public LightingForwardPlusSettings LightingSettings => _lightingSettings;
		public LightCookieSettings LightCookieSettings  => _lightCookieSettings;
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
		public LayerMask OpaqueLayerMask => _opaqueLayerMask;
		public LayerMask TransparentLayerMask => _transparentLayerMask;
	}

	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU + "TrpAsset", fileName = "TrpAsset")]
	public class TrpAsset : RenderPipelineAsset<Trp>, IGPUResidentRenderPipeline
	{
		[SerializeField] private TrpCommonSettings _commonSettings;

		// GPU Resident Drawerは、互換シェーダーを使用するMeshRendererの描画提出を
		// BatchRendererGroupへ移し、メインスレッドの描画提出コストを削減する。
		// TRPではまずインスタンシング描画のみを提供する。GPU Occlusion Cullingは
		// 専用の深度ピラミッドを必要とするため、現段階では明示的に無効としている。
		[Header("GPU Resident Drawer")]
		[SerializeField] private GPUResidentDrawerMode _gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
		[Tooltip("画面に占める面積がこの値（%）未満の GPU Resident Drawer 対象Meshを描画しません。0 は無効です。LODGroup のRendererには適用されません。")]
		[SerializeField, Range(0f, 20f)] private float _gpuResidentDrawerSmallMeshScreenPercentage;
		[Tooltip("既存の DepthNormalsOnly パスから深度ピラミッドを作成し、GPU Resident Drawer の遮蔽カリングを有効にします。対応する DepthNormalsOnly パスを持たないシェーダーは遮蔽物として扱われません。現状DepthNormalsOnlyパスの描画のほうがトータルで重たいケースのほうが多いため、使用は慎重に。")]
		[SerializeField] private bool _gpuResidentDrawerEnableOcclusionCulling;

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
		public override Material defaultLineMaterial => _resources?.UnlitMaterial;
		public override Material defaultTerrainMaterial => _resources?.UnlitMaterial;

		/// <summary>
		/// GPU Resident Drawerの動作モード。
		/// 実行中に切り替えた場合も、次フレームの再初期化で反映される。
		/// </summary>
		public GPUResidentDrawerMode gpuResidentDrawerMode
		{
			get => _gpuResidentDrawerMode;
			set
			{
				if (_gpuResidentDrawerMode == value) return;
				_gpuResidentDrawerMode = value;
				GPUResidentDrawer.ReinitializeIfNeeded();
			}
		}

		GPUResidentDrawerSettings IGPUResidentRenderPipeline.gpuResidentDrawerSettings => new()
		{
			mode = _gpuResidentDrawerMode,
			// 深度ピラミッドはTrpRenderer内でDepthNormalsOnlyの出力から更新する。
			enableOcclusionCulling = _gpuResidentDrawerEnableOcclusionCulling,
			allowInEditMode = true,
			supportDitheringCrossFade = false,
			smallMeshScreenPercentage = _gpuResidentDrawerSmallMeshScreenPercentage,
			shadowSmallMeshScreenPercentages = default,
		};

		/// <summary>
		/// TRPの通常のRendererList描画はBatchRendererGroupの間接描画を受け入れられる。
		/// ただし、対象シェーダーにはDOTS Instancingバリアントが必要である。
		/// </summary>
		public bool IsGPUResidentDrawerSupportedBySRP(out string message, out LogType severity)
		{
			message = string.Empty;
			severity = LogType.Log;
			return true;
		}

		// Inspectorから設定を変えた際にもGRDを再初期化し、enum表示のまま即時反映する。
		protected override void OnValidate()
		{
			base.OnValidate();
			GPUResidentDrawer.ReinitializeIfNeeded();
		}

		protected override RenderPipeline CreatePipeline()
		{
			_resources = GraphicsSettings.GetRenderPipelineSettings<TrpResources>();
			
			return new Trp(_commonSettings, _resources);
		}

		protected override void EnsureGlobalSettings()
		{
			base.EnsureGlobalSettings();

#if UNITY_EDITOR
			TrpGlobalSettings.Ensure();
#endif
		}
	}
}
