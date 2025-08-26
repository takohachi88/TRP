using UnityEngine;
using UnityEngine.Rendering;
using Trp.PostFx;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace Trp
{
	/// <summary>
	/// Rendererに渡すデータ。
	/// </summary>
	internal readonly ref struct RendererParams
	{
		public RenderGraph RenderGraph { get; init; }
		public ScriptableRenderContext Context { get; init; }
		public Camera Camera { get; init; }
		public bool IsFirstToBackbuffer { get; init; }
		public bool IsLastToBackbuffer { get; init; }
		public bool IsFirstCamera { get; init; }
		internal TrpResources Resources { get; init; }
		public PostFxPassGroup PostFxPassGroup { get; init; }
		public bool TargetIsGameRenderTexture { get; init; }
	}


	public class CameraTextures
	{
		internal TextureHandle AttachmentColor { get; set; }
		internal TextureHandle AttachmentDepth { get; set; }
		public TextureHandle TextureOpaque { get; internal set; }
		public TextureHandle TextureTransparent { get; internal set; }
		public TextureHandle TextureDepth { get; internal set; }
		internal TextureHandle TargetColor { get; set; }
		internal TextureHandle TargetDepth { get; set; }
		internal TextureHandle PostProcessLut { get; set; }
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
		private readonly DepthOnlyPass _depthOnlyPass = new();
		private readonly GeometryPass _geometryPass = new();
		private readonly SkyboxPass _skyboxPass = new();
		private readonly CopyColorPass _copyColorPass;
		private readonly CopyDepthPass _copyDepthPass;

		private readonly FinalBlitPass _finalBlitPass = new();
		private readonly WireOverlayPass _wireOverlayPass = new();
		private readonly GizmoPass _gizmoPass = new();
		private readonly UiPass _uiPass = new();
		private readonly CameraCapturePass _cameraCapturePass = new();
		private readonly SetEditorTargetPass _setEditorTargetPass = new();

		private readonly Material _coreBlitMaterial;

		private readonly CameraTextures _cameraTextures = new();

		internal TrpRenderer(TrpCommonSettings commonSettings, TrpResources resources)
		{
			_commonSettings = commonSettings;

			_coreBlitMaterial = CoreUtils.CreateEngineMaterial(resources.CoreBlitShader);

			_copyColorPass = new(_coreBlitMaterial);
			_copyDepthPass = new(resources.CopyDepthShader);
			_lutPass = new(resources.PostFxLutShader);
		}

		/// <summary>
		/// カメラ一つ分の描画。
		/// </summary>
		/// <param name="rendererParams"></param>
		internal void Render(RendererParams rendererParams)
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
			bool useDepthTexture = true;
			int renderingLayerMask = -1;

			//引数でcmdに名前を付けるとSamplerのnameよりもcmdのnameの方が優先されてしまうので、このcmdには名前を付けてはならない。
			CommandBuffer cmd = CommandBufferPool.Get();
			ProfilingSampler sampler;

			_postFxPassGroup = rendererParams.PostFxPassGroup;

			Camera camera = rendererParams.Camera;
			TrpCameraData cameraData = camera.GetComponent<TrpCameraData>();

			Vector2Int attachmentSize = new(camera.pixelWidth, camera.pixelHeight);
			bool useScaledRendering = false;

			//カメラごとの設定値を適用する。
			//cameraDataがある=CameraType.Gameである。
			if (cameraData)
			{
				useScaledRendering = cameraData.UseScaledRenering;
				if(useScaledRendering) renderScale = cameraData.RenderScale / 100f;
				useHdr &= cameraData.UseHdr;
				usePostFx = cameraData.UsePostx;
				bilinear = cameraData.Bilinear;
				blendSrc = cameraData.BlendSrc;
				blendDst = cameraData.BlendDst;

				useOpaqueTexture = cameraData.UseOpaqueTexture;
				useTransparentTexture = cameraData.UseTransparentTexture;
				useDepthTexture = cameraData.UseDepthTexture;
				
				renderingLayerMask = cameraData.RenderingLayerMask;

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
				useDepthTexture = _commonSettings.UseDepthTextureOnReflection;
			}

#if UNITY_EDITOR
			//Sceneウィンドウ描画時。
			if (camera.cameraType == CameraType.SceneView)
			{
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
				
				useOpaqueTexture = true;
				useTransparentTexture = true;
				useDepthTexture = true;
				usePostFx = CoreUtils.ArePostProcessesEnabled(camera);
			}

			//Preview描画時。
			if (camera.cameraType == CameraType.Preview)
			{
				useOpaqueTexture = false;
				useTransparentTexture = false;
				useDepthTexture = true;
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
				executionName = cameraData ? cameraData.CameraName : camera.cameraType.ToString(),
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
					RenderGraph = renderGraph,
					Camera = camera,
					CameraData = cameraData,
					CameraTextures = _cameraTextures,
					AttachmentSize = attachmentSize,
					UseScaledRendering = useScaledRendering,
					UseOpaqueTexture = useOpaqueTexture,
					UseDepthTexture = useDepthTexture,
					UseTransparentTexture = useTransparentTexture,
					RenderScale = renderScale,
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
					IsFirstCamera = rendererParams.IsFirstCamera,
				};

				//レンダリングの前段階的な処理。各種テクスチャの確保など。
				_setupPass.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterSetup);

				//PostProcessで使うLUTの生成。
				_lutPass.RecordRenderGraph(ref passParams, _commonSettings.PostFxLutFilterMode);

				//Opaqueジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, true);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingOpaques);

				//Skyboxの描画。
				if (camera.clearFlags == CameraClearFlags.Skybox) _skyboxPass.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingSkybox);

				//OpaqueTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Opaque);

				//CameraDepthTextureの作成。
				_copyDepthPass.RecordRenderGraph(ref passParams, CopyDepthPass.CopyDepthMode.ToDepthTexture);

				//Transparentジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, false);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingTransparents);

				//TransparentTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Transparent);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.AttachmentDepth, GizmoSubset.PreImageEffects);

				//ポストエフェクトの描画。
				TextureHandle postFxDst = passParams.CameraTextures.AttachmentColor;
				if (usePostFx) postFxDst = _postFxPassGroup.RecordRenderGraph(ref passParams);
				ExecuteCustomPasses(cameraData, ref passParams, ExecutionPhase.AfterRenderingPostProcessing);

				//中間バッファから画面への描画。
				if(isLastToBackbuffer || isSceneViewOrPreview) _finalBlitPass.RecordRenderGraph(ref passParams, postFxDst, blendSrc, blendDst);

				//TargetDepthへのコピー。
				if(isSceneViewOrPreview) _copyDepthPass.RecordRenderGraph(ref passParams, CopyDepthPass.CopyDepthMode.ToTarget);

				//WireOverlayモードの描画。
				_wireOverlayPass.RecordRenderGraph(ref passParams);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.TargetDepth, GizmoSubset.PostImageEffects);

				//OverlayなUIの描画。
				if(isLastToBackbuffer) _uiPass.RecordRenderGraph(ref passParams);

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
			if (cameraData) cameraData.ExecuteCustomPasses(ref passParams, phase);
		}

		public void Dispose()
		{
			_setupPass.Dispose();
			_copyDepthPass.Dispose();
			_postFxPassGroup.Dispose();
			CoreUtils.Destroy(_coreBlitMaterial);
			_lutPass.Dispose();
		}
	}
}