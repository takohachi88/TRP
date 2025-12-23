using System.Collections.Generic;
using Trp.PostFx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// Rendererに渡すデータ。
	/// </summary>
	internal readonly ref struct RendererParams
	{
		public readonly RenderGraph RenderGraph { get; init; }
		public readonly ScriptableRenderContext Context { get; init; }
		public Camera Camera { get; init; }
		public bool IsFirstToBackbuffer { get; init; }
		public bool IsLastToBackbuffer { get; init; }
		public bool IsFirstRuntimeCamera { get; init; }
		internal readonly TrpResources Resources { get; init; }
		public PostFxPassGroup PostFxPassGroup { get; init; }
		public bool TargetIsGameRenderTexture { get; init; }
		public readonly IReadOnlyList<Camera> BackbufferCameras { get; init; }
		public readonly IReadOnlyList<Camera> RenderTextureCameras { get; init; }
		public readonly IReadOnlyList<Camera> EditorCameras { get; init; }
		public readonly DebugForwardPlus.CameraDebugValue ForwardPlusCameraDebugValue { get; init; }
	}


	public class CameraTextures
	{
		public TextureHandle AttachmentColor { get; set; }
		public TextureHandle AttachmentDepth { get; set; }
		public TextureHandle TextureOpaque { get; internal set; }
		public TextureHandle TextureTransparent { get; internal set; }
		public TextureHandle TextureDepth { get; internal set; }
		public TextureHandle TextureNormals { get; internal set; }
		internal TextureHandle TargetColor { get; set; }
		internal TextureHandle TargetDepth { get; set; }
		public TextureHandle PostProcessLut { get; set; }
	}

	public class LightingResources
	{
		internal BufferHandle DirectionalLightDataBuffer { get; set; }
		internal BufferHandle PunctualLightDataBuffer { get; set; }
		internal BufferHandle ForwardPlusTileBuffer { get; set; }
		public TextureHandle DirectionalShadowMap { get; set; }
		public TextureHandle PunctualShadowMap { get; set; }
		public BufferHandle DirectionalShadowCascadesBuffer { get; set; }
		public BufferHandle DirectionalShadowMatricesBuffer { get; set; }
		public BufferHandle PunctualShadowDataBuffer { get; set; }
	}

	public struct PassParams
	{
		public TrpCommonSettings CommonSettings { get; init; }
		public readonly ScriptableRenderContext RenderContext { get; init; }
		public RenderGraph RenderGraph { get; init; }
		public readonly Camera Camera { get; init; }
		public readonly TrpCameraData CameraData { get; init; }
		public readonly CameraTextures CameraTextures { get; init; }
		public readonly LightingResources LightingResources { get; init; }
		public readonly Vector2Int AttachmentSize { get; init; }
		public Vector2 AspectFit { get; internal set; }
		public Vector2 AspectFitRcp { get; internal set; }
		public readonly CameraClearFlags ClearFlags { get; init; }
		public readonly bool UseScaledRendering { get; init; }
		public readonly bool UseOpaqueTexture { get; init; }
		public readonly bool UseDepthNormalsTexture { get; init; }
		public readonly bool UseNormalsTexture { get; init; }
		public readonly bool UseTransparentTexture { get; init; }
		public readonly float RenderScale { get; init; }
		public readonly bool DrawShadow { get; init; }
		public readonly bool UseHdr { get; init; }
		public bool UseAlpha { get; internal set; }
		public readonly bool UsePostFx { get; init; }
		public readonly bool IsSceneViewOrPreview { get; init; }
		public readonly CullingResults CullingResults { get; init; }
		public readonly RenderingLayerMask RenderingLayerMask { get; init; }
		internal readonly int LutSize { get; init; }
		public readonly bool TargetIsGameRenderTexture { get; init; }
		/// <summary>
		/// backbufferに描画する最初のGameカメラであるかどうか。
		/// </summary>
		public readonly bool IsFirstToBackbuffer { get; init; }
		/// <summary>
		/// backbufferに描画する最後のGameカメラであるかどうか。
		/// </summary>
		public readonly bool IsLastToBackbuffer { get; init; }
		/// <summary>
		/// TRPにおいてもっとも最初に処理されるランタイムのカメラかどうか。
		/// </summary>
		public readonly bool IsFirstRuntimeCamera { get; init; }

		public readonly IReadOnlyList<Camera> BackbufferCameras { get; init; }
		public readonly IReadOnlyList<Camera> RenderTextureCameras { get; init; }
		public readonly IReadOnlyList<Camera> EditorCameras { get; init; }
		internal readonly MSAASamples Msaa { get; init; }
		internal readonly DebugForwardPlus.CameraDebugValue ForwardPlusCameraDebugValue { get; init; }
	}


	/// <summary>
	/// カメラの描画を行う。
	/// </summary>
	public class TrpRenderer
	{
		private TrpCommonSettings _commonSettings;

		private PostFxPassGroup _postFxPassGroup;

		private readonly SetupPass _setupPass = new();
		private readonly CreatePostFxLutPass _lutPass;
		private readonly LightingForwardPlusPass _lightingPass;
		private readonly DepthNormalsPass _depthNormalsPass = new();
		private readonly GeometryPass _geometryPass = new();
		private readonly OitPass _oitPass;
		private readonly OutlinePass _outlinePass = new();
		private readonly SkyboxPass _skyboxPass = new();
		private readonly CopyColorPass _copyColorPass;
		private readonly CopyDepthPass _copyDepthPass;

		private readonly FinalBlitPass _finalBlitPass = new();
		private readonly WireOverlayPass _wireOverlayPass = new();
		private readonly GizmoPass _gizmoPass = new();
		private readonly UiPass _uiPass = new();
		private readonly DebugForwardPlusPass _debugForwardPlusPass;
		private readonly CameraCapturePass _cameraCapturePass = new();
		private readonly SetEditorTargetPass _setEditorTargetPass = new();

		private readonly Material _coreBlitMaterial;

		private readonly CameraTextures _cameraTextures = new();
		private readonly LightingResources _lightingResources = new();

		internal TrpRenderer(TrpCommonSettings commonSettings, TrpResources resources)
		{
			_commonSettings = commonSettings;

			_coreBlitMaterial = CoreUtils.CreateEngineMaterial(resources.CoreBlitShader);

			_lightingPass = new(commonSettings.LightCookieSettings);
			_copyColorPass = new(_coreBlitMaterial);
			_debugForwardPlusPass = new (resources.DebugForwardPlusTileShader);
			_copyDepthPass = new(resources.CopyDepthShader);
			_lutPass = new(resources.PostFxLutShader);
			_oitPass = new(resources.OitCompositeShader);
		}

		/// <summary>
		/// カメラ一つ分の描画。
		/// </summary>
		/// <param name="rendererParams"></param>
		internal void Render(ref RendererParams rendererParams)
		{
			//ローカル変数群。再取得・再定義せず必ずこれらを使うこと。
			RenderGraph renderGraph = rendererParams.RenderGraph;
			ScriptableRenderContext context = rendererParams.Context;
			bool isFirstToBackbuffer = rendererParams.IsFirstToBackbuffer;
			bool isLastToBackbuffer = rendererParams.IsLastToBackbuffer;
			bool usePostFx = false;
			bool useHdr = _commonSettings.UseHdr;
			bool useAlpha = false;
			float renderScale = 1;
			bool bilinear = true;
			BlendMode blendSrc = BlendMode.One;
			BlendMode blendDst = BlendMode.Zero;
			bool useOpaqueTexture = true;
			bool useTransparentTexture = true;
			bool useDepthNormalsTexture = true;
			bool drawShadow = true;
			bool useOutline = true;
			bool useOit = true;
			float oitScale = 1f;
			int renderingLayerMask = -1;
			MSAASamples msaa = MSAASamples.None;

			//引数でcmdに名前を付けるとSamplerのnameよりもcmdのnameの方が優先されてしまうので、このcmdには名前を付けてはならない。
			CommandBuffer cmd = CommandBufferPool.Get();
			ProfilingSampler sampler;

			_postFxPassGroup = rendererParams.PostFxPassGroup;

			Camera camera = rendererParams.Camera;
			TrpCameraData cameraData = camera.GetComponent<TrpCameraData>();
			if (camera.cameraType == CameraType.Game && !cameraData)
			{
				//エラーとしたいところだが、ビルド時に複数カメラがあると一瞬TrpCameraDataが取得できなくなるフレームがあり、その度にエラーログが出ても困るため通常のログ表示にする。
				Debug.Log($"{camera.name} has no {nameof(TrpCameraData)}.");
				return;
			}

			Vector2Int attachmentSize = new(camera.pixelWidth, camera.pixelHeight);
			bool useScaledRendering = false;

			//カメラごとの設定値を適用する。
			//cameraDataがある=CameraType.Gameである。
			if (cameraData)
			{
				useScaledRendering = cameraData.UseScaledRendering;
				if(useScaledRendering) renderScale = cameraData.RenderScale;
				useHdr &= cameraData.UseHdr;
				usePostFx = cameraData.UsePostx;
				bilinear = cameraData.Bilinear;

				useOpaqueTexture = cameraData.UseOpaqueTexture;
				useTransparentTexture = cameraData.UseTransparentTexture;
				useDepthNormalsTexture = cameraData.UseDepthNormalsTexture;
				drawShadow = cameraData.DrawShadow;
				useOutline = cameraData.UseOutline;
				useOit = cameraData.UseOit;
				oitScale = cameraData.OitScale;
				renderingLayerMask = cameraData.RenderingLayerMask;
				msaa = _commonSettings.Msaa;
				sampler = cameraData.Sampler;
			}
			else
			{
				sampler = ProfilingSampler.Get(camera.cameraType);
			}

			CullingResults cullingResults;
			bool isSceneViewOrPreview = camera.cameraType is CameraType.SceneView or CameraType.Preview;

			//OpaqueTextureやDepthTextureを生成するかどうか。
			//ReflectionProbe描画の場合は独自のフラグとなる。
			if (camera.cameraType == CameraType.Reflection)
			{
				useOpaqueTexture = _commonSettings.UseOaqueTextureOnReflection;
				useTransparentTexture = false;
				useDepthNormalsTexture = _commonSettings.UseDepthTextureOnReflection;
			}

#if UNITY_EDITOR
			//Sceneウィンドウ描画時。
			if (camera.cameraType == CameraType.SceneView)
			{
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
				
				useOpaqueTexture = true;
				useTransparentTexture = true;
				useDepthNormalsTexture = true;
				usePostFx = CoreUtils.ArePostProcessesEnabled(camera);
			}

			//Preview描画時。
			if (camera.cameraType == CameraType.Preview)
			{
				useOpaqueTexture = false;
				useTransparentTexture = false;
				useDepthNormalsTexture = true;
				usePostFx = false;
			}
#endif

			if (camera.cameraType is CameraType.Reflection or CameraType.Preview) ScriptableRenderContext.EmitGeometryForCamera(camera);

			//カリング。
			if (camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
			{
				cullingParameters.shadowDistance = Mathf.Min(_commonSettings.ShadowSettings.MaxShadowDistance, camera.farClipPlane);
				cullingResults = context.Cull(ref cullingParameters);
			}
			else return;//カリング失敗ならこのカメラの描画は中断。

			//ScaledRenderingする場合。
			if (useScaledRendering)
			{
				attachmentSize.x = (int)(attachmentSize.x * renderScale);
				attachmentSize.y = (int)(attachmentSize.y * renderScale);
			}

			//RTHandleのreferenceSizeの指定。
			RTHandles.SetReferenceSize(attachmentSize.x, attachmentSize.y);

			//Volumeの更新。
			VolumeManager.instance.Update(VolumeManager.instance.stack, camera.transform, cameraData ? cameraData.VolumeMask : 1);

			//RenderGraphの登録と実行。
			RenderGraphParameters renderGraphParameters = new()
			{
				executionId = camera.GetEntityId(),
				generateDebugData = camera.cameraType != CameraType.Preview && !camera.isProcessingRenderRequest,
				commandBuffer = cmd,
				scriptableRenderContext = context,
				currentFrameIndex = Time.frameCount,
			};

			using (new ProfilingScope(cmd, sampler))
			{
				renderGraph.BeginRecording(renderGraphParameters);

				//Passに渡すデータの構築。
				PassParams passParams = new()
				{
					CommonSettings = _commonSettings,
					RenderContext = context,
					RenderGraph = renderGraph,
					Camera = camera,
					CameraData = cameraData,
					CameraTextures = _cameraTextures,
					LightingResources = _lightingResources,
					AttachmentSize = attachmentSize,
					UseScaledRendering = useScaledRendering,
					UseOpaqueTexture = useOpaqueTexture,
					UseDepthNormalsTexture = useDepthNormalsTexture,
					UseTransparentTexture = useTransparentTexture,
					RenderScale = renderScale,
					DrawShadow = drawShadow,
					UseHdr = useHdr,
					UseAlpha = useAlpha,
					UsePostFx = usePostFx,
					IsSceneViewOrPreview = isSceneViewOrPreview,
					CullingResults = cullingResults,
					RenderingLayerMask = renderingLayerMask,
					LutSize = _commonSettings.PostFxLutSize,
					TargetIsGameRenderTexture = rendererParams.TargetIsGameRenderTexture,
					IsFirstToBackbuffer = isFirstToBackbuffer,
					IsLastToBackbuffer = isLastToBackbuffer,
					IsFirstRuntimeCamera = rendererParams.IsFirstRuntimeCamera,
					EditorCameras = rendererParams.BackbufferCameras,
					RenderTextureCameras = rendererParams.RenderTextureCameras,
					BackbufferCameras = rendererParams.BackbufferCameras,
					Msaa = msaa,
					ForwardPlusCameraDebugValue = rendererParams.ForwardPlusCameraDebugValue,
				};

				//ライティングの情報。
				_lightingPass.RecordRenderGraph(ref passParams, _commonSettings.LightingSettings);

				//レンダリングの前段階的な処理。各種テクスチャの確保など。
				_setupPass.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterSetup);

				//PostProcessで使うLUTの生成。
				_lutPass.RecordRenderGraph(ref passParams, _commonSettings.PostFxLutFilterMode);

				//Opaqueジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, true);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingOpaques);

				//Outlineの描画。
				if(useOutline) _outlinePass.RecordRenderGraph(ref passParams);

				//Skyboxの描画。
				if (camera.clearFlags == CameraClearFlags.Skybox) _skyboxPass.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingSkybox);

				//OpaqueTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Opaque);

				//CameraDepthTextureの作成。
				_depthNormalsPass.RecordRenderGraph(ref passParams);

				//OITの描画。
				if(useOit) _oitPass.RecordRenderGraph(ref passParams, oitScale);

				//Transparentジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, false);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingTransparents);

				//TransparentTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Transparent);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.AttachmentColor, _cameraTextures.AttachmentDepth, GizmoSubset.PreImageEffects);

				//ポストエフェクトの描画。
				if (usePostFx) _postFxPassGroup.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingPostProcessing);

				//Forward+デバッグ表示。
				if (camera.cameraType is not (CameraType.Preview)) _debugForwardPlusPass.RecordRenderGraph(ref passParams);

				//中間バッファから画面への描画。
				if (isLastToBackbuffer || camera.targetTexture) _finalBlitPass.RecordRenderGraph(ref passParams, _cameraTextures.AttachmentColor, blendSrc, blendDst);

				//TargetDepthへのコピー。
				if(isSceneViewOrPreview) _copyDepthPass.RecordRenderGraph(ref passParams);

				//WireOverlayモードの描画。
				_wireOverlayPass.RecordRenderGraph(ref passParams);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.TargetColor, _cameraTextures.TargetDepth, GizmoSubset.PostImageEffects);

				//OverlayなUIの描画。
				if (isLastToBackbuffer) _uiPass.RecordRenderGraph(ref passParams);

				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRendering);

				//CameraCaptureBridge対応。
				if(isLastToBackbuffer) _cameraCapturePass.RecordRenderGraph(ref passParams);

				//SceneView描画時にgridなどのエンジン側の描画処理が適切に行われるようにする。
				if (camera.cameraType == CameraType.SceneView) _setEditorTargetPass.RecordAndExecute(ref passParams);

				//RenderGraph終了。
				renderGraph.EndRecordingAndExecute();
			}

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			context.Submit();
		}

		private void ExecuteCustomPasses(TrpCameraData cameraData, ref PassParams passParams, ExecutionPhase phase)
		{
			foreach (CustomPass pass in _commonSettings.CustomPasses)
			{
				if (pass.PassObject && pass.Enabled && pass.PassObject.Phase == phase) pass.PassObject.Execute(ref passParams);
			}
			if (cameraData) cameraData.ExecuteCustomPasses(ref passParams, phase);
		}

		public void Dispose()
		{
			_setupPass.Dispose();
			_copyDepthPass.Dispose();
			if(_postFxPassGroup) _postFxPassGroup.Dispose();
			CoreUtils.Destroy(_coreBlitMaterial);
			_lutPass.Dispose();
			_lightingPass.Dispose();
			_oitPass.Dispose();
		}
	}
}