using UnityEngine;
using UnityEngine.Rendering;
using TakoLib.Common.Extensions;
using Trp.PostFx;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.VFX;

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
		/// <summary>
		/// backbufferに描画する最初のGameカメラであるかどうか。
		/// </summary>
		public bool IsFirstToBackbuffer { get; init; }
		/// <summary>
		/// backbufferに描画する最後のGameカメラであるかどうか。
		/// </summary>
		public bool IsLastToBackbuffer { get; init; }
		internal InternalResources InternalResources { get; init; }
		public PostFxPassGroup PostFxPassGroup { get; init; }
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
		private RTHandle _targetColor, _targetDepth;

		private TrpCommonSettings _commonSettings;

		private InternalResources _internalResources;

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
		private readonly Material _copyDepthMaterial;

		private readonly CameraTextures _cameraTextures = new();

		internal TrpRenderer(TrpCommonSettings commonSettings, InternalResources internalResources)
		{
			_commonSettings = commonSettings;

			_coreBlitMaterial = CoreUtils.CreateEngineMaterial(internalResources.CoreBlitShader);
			_copyDepthMaterial = CoreUtils.CreateEngineMaterial(internalResources.CopyDepthShader);

			_copyColorPass = new(_coreBlitMaterial);
			_copyDepthPass = new(_copyDepthMaterial);

			_lutPass = new(internalResources.PostProcessLutShader);
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
			float renderScale = _commonSettings.RenderScale;
			bool bilinear = true;
			BlendMode blendSrc = BlendMode.One;
			BlendMode blendDst = BlendMode.Zero;
			bool useOpaqueTexture = true;
			bool useTransparentTexture = true;
			bool useDepthTexture = true;
			int renderingLayerMask = -1;

			//引数でcmdに名前を付けるとSamplerのnameよりもcmdのnameの方が優先されてしまうので、cmdには名前を付けてはならない。
			CommandBuffer cmd = CommandBufferPool.Get();
			ProfilingSampler sampler;

			_postFxPassGroup = rendererParams.PostFxPassGroup;

			Camera camera = rendererParams.Camera;
			TrpCameraData cameraData = camera.GetComponent<TrpCameraData>();

			//カメラごとの設定値を適用する。
			//cameraDataがある=CameraType.Gameである。
			if (cameraData)
			{
				useHdr &= cameraData.UseHdr;
				usePostFx = cameraData.UsePostx;
				renderScale *= cameraData.RenderScale;
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

			bool useIntermediateAttachments;
			bool useScaledRendering = !renderScale.IsInRange(0.9f, 1.1f);
			Vector2Int attachmentSize = new (camera.pixelWidth, camera.pixelHeight);
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
				
				useScaledRendering = false;//Sceneウィンドウ描画時はScaledRenderingしない。
				useOpaqueTexture = true;
				useTransparentTexture = true;
				useDepthTexture = true;
				usePostFx = CoreUtils.ArePostProcessesEnabled(camera);
			}

			//Preview描画時。
			if (camera.cameraType == CameraType.Preview)
			{
				useScaledRendering = false;
				useOpaqueTexture = false;
				useTransparentTexture = false;
				useDepthTexture = true;
				usePostFx = false;
			}
#endif

			RTHandles.SetReferenceSize(attachmentSize.x, attachmentSize.y);

			//VFX Graphのセットアップ。
			VFXManager.PrepareCamera(camera);

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

			//中間バッファを使うかどうか。
			useIntermediateAttachments = useScaledRendering || useOpaqueTexture || useTransparentTexture || useDepthTexture || usePostFx;

			//RenderGraphの登録と実行。
			RenderGraphParameters renderGraphParameters = new()
			{
				executionName = cameraData ? cameraData.CameraName : "Scene View",
				commandBuffer = cmd,
				scriptableRenderContext = context,
				currentFrameIndex = Time.frameCount,
			};

			//Volumeの更新。
			VolumeManager.instance.Update(VolumeManager.instance.stack, camera.transform, cameraData ? cameraData.VolumeMask : 1);

			using (new ProfilingScope(cmd, sampler))
			{
				renderGraph.BeginRecording(renderGraphParameters);

				TextureDesc colorDescriptor = new(attachmentSize.x, attachmentSize.y)
				{
					//TODO: GetCompatibleFormatにする？
					colorFormat = RenderingUtils.ColorFormat(useHdr),
					clearBuffer = true,
					clearColor = Color.clear,
				};

				//各種RTHandleの確保。
				AllocTargets(ref _targetColor, ref _targetDepth, camera.targetTexture);

				//Passに渡すデータの構築。
				PassParams passParams = new()
				{
					RenderGraph = renderGraph,
					Camera = camera,
					CameraData = cameraData,
					ColorDescriptor = colorDescriptor,
					CameraTextures = _cameraTextures,
					UseIntermediateAttachments = useIntermediateAttachments,
					AttachmentSize = attachmentSize,
					UseScaledRendering = useScaledRendering,
					UseOpaqueTexture = useOpaqueTexture,
					UseDepthTexture = useDepthTexture,
					UseTransparentTexture = useTransparentTexture,
					RenderScale = renderScale,
					UseHdr = useHdr,
					IsSceneViewOrPreview = isSceneViewOrPreview,
					CullingResults = cullingResults,
					RenderingLayerMask = renderingLayerMask,
					LutSize = _commonSettings.PostFxLutSize,
				};

				//レンダリングの前段階的な処理。各種テクスチャの確保など。
				_setupPass.RecordRenderGraph(
					ref passParams,
					_targetColor,
					_targetDepth,
					isFirstToBackbuffer
					);

				//PostProcessで使うLUTの生成。
				_lutPass.RecordRenderGraph(ref passParams, _commonSettings.PostFxLutFilterMode);

				//Opaqueジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, true);

				//Skyboxの描画。
				if (camera.clearFlags == CameraClearFlags.Skybox) _skyboxPass.RecordRenderGraph(ref passParams);

				//OpaqueTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Opaque);

				//CameraDepthTextureの作成。
				_copyDepthPass.RecordRenderGraph(ref passParams, CopyDepthPass.CopyDepthMode.ToDepthTexture);

				//Transparentジオメトリの描画。
				_geometryPass.RecordRenderGraph(ref passParams, false);

				//TransparentTextureへのコピー。
				_copyColorPass.RecordRenderGraph(ref passParams, CopyColorPass.CopyColorMode.Transparent);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.AttachmentDepth, GizmoSubset.PreImageEffects);

				//ポストエフェクトの描画。
				if (usePostFx)
				{
					TextureHandle src = _postFxPassGroup.RecordRenderGraph(ref passParams);
					_finalBlitPass.RecordRenderGraph(ref passParams, src, blendSrc, blendDst);
				}
				else if (useIntermediateAttachments)
				{
					//中間バッファから画面への描画。
					_finalBlitPass.RecordRenderGraph(ref passParams, passParams.CameraTextures.AttachmentColor, blendSrc, blendDst);
				}

				//TargetDepthへのコピー。
				if(isSceneViewOrPreview) _copyDepthPass.RecordRenderGraph(ref passParams, CopyDepthPass.CopyDepthMode.ToTarget);

				//WireOverlayモードの描画。
				_wireOverlayPass.RecordRenderGraph(ref passParams);

				//Gizmoの描画。
				_gizmoPass.RecordRenderGraph(ref passParams, _cameraTextures.TargetDepth, GizmoSubset.PostImageEffects);

				//UIの描画。
				if(isLastToBackbuffer) _uiPass.RecordRenderGraph(ref passParams);

				//CameraCaptureBridge対応。
				_cameraCapturePass.RecordRenderGraph(ref passParams);

				//SceneView描画時にgridなどのエンジン側の描画処理が適切に行われるようにする。
				if (camera.cameraType == CameraType.SceneView) _setEditorTargetPass.RecordAndExecute(ref passParams);

				//RenderGraph終了。
				renderGraph.EndRecordingAndExecute();
			}

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			context.Submit();
		}

		private void AllocTargets(ref RTHandle color, ref RTHandle depth, RenderTexture cameraTargetTexture)
		{
			RenderTargetIdentifier idColor = cameraTargetTexture ? new RenderTargetIdentifier(cameraTargetTexture) : BuiltinRenderTextureType.CameraTarget;
			RenderTargetIdentifier idDepth = cameraTargetTexture ? new RenderTargetIdentifier(cameraTargetTexture) : BuiltinRenderTextureType.Depth;

			if (color == null) color = RTHandles.Alloc(idColor, "BackbufferColor");
			else if (color.nameID != idColor) RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref color, idColor);
			
			if (depth == null) depth = RTHandles.Alloc(idDepth, "BackbufferDepth");
			else if (depth.nameID != idDepth) RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref depth, idDepth);
		}

		public void Dispose()
		{
			_postFxPassGroup.Dispose();
			_targetColor?.Release();
			_targetDepth?.Release();
			CoreUtils.Destroy(_coreBlitMaterial);
			CoreUtils.Destroy(_copyDepthMaterial);

			_lutPass.Dispose();
		}
	}
}