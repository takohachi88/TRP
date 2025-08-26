using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.VFX;

namespace Trp
{
	/// <summary>
	/// TRPのセットアップを行う。
	/// 各種カメラ系テクスチャの確保や、シェーダー変数の設定など。
	/// </summary>
	internal class SetupPass
	{
		/// <summary>
		/// 最終的な描画先。（backbufferかCamera.targetTexture）
		/// </summary>
		private RTHandle _targetColorRt, _targetDepthRt;
		/// <summary>
		/// 中間テクスチャ。CameraStack的な状況では1枚を使い回す。
		/// </summary>
		private RTHandle _attachmentColorRt, _attachmentDepthRt;

		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Setup);
		private static readonly int IdAttachmentSize = Shader.PropertyToID("_AttachmentSize");
		private static readonly int IdAspectFit = Shader.PropertyToID("_AspectFit");
		private static readonly int IdScaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
		private static readonly int IdTime = Shader.PropertyToID("_Time");

		private class PassData
		{
			public bool UseIntermediateAttachments;
			public Vector2Int AttachmentSize;
			public Vector2 AspectFit;
			public Camera Camera;
			public bool IsFirstCamera;
			public CullingResults CullingResults;
		}

		/// <summary>
		/// URPのScriptableRenderer.csから流用。
		/// </summary>
		/// <param name="renderGraph"></param>
		/// <param name="useIntermediateColorTarget"></param>
		/// <returns></returns>
		private protected int AdjustAndGetScreenMSAASamples(RenderGraph renderGraph, bool useIntermediateColorTarget)
		{
			// In the editor (ConfigureTargetTexture in PlayModeView.cs) and many platforms, the system render target is always allocated without MSAA    
			if (!SystemInfo.supportsMultisampledBackBuffer) return 1;

			// In the players, when URP main rendering is done to an intermediate target and NRP enabled
			// we disable multisampling for the system backbuffer as a bandwidth optimization
			// doing so, we avoid storing costly msaa samples back to system memory for nothing
			bool canOptimizeScreenMSAASamples = useIntermediateColorTarget
											 && renderGraph.nativeRenderPassesEnabled
											 && Screen.msaaSamples > 1;

			if (canOptimizeScreenMSAASamples) Screen.SetMSAASamples(1);

			return Mathf.Max(Screen.msaaSamples, 1);
		}

		private void AllocTargets(ref RTHandle targetColor, ref RTHandle targetDepth, RenderTexture cameraTargetTexture)
		{
			RenderTargetIdentifier idTargetColor = cameraTargetTexture ? new RenderTargetIdentifier(cameraTargetTexture) : BuiltinRenderTextureType.CameraTarget;
			RenderTargetIdentifier idTargetDepth = cameraTargetTexture ? new RenderTargetIdentifier(cameraTargetTexture) : BuiltinRenderTextureType.Depth;

			if (targetColor == null) targetColor = RTHandles.Alloc(idTargetColor, "TargetColor");
			else if (targetColor.nameID != idTargetColor) RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref targetColor, idTargetColor);

			if (targetDepth == null) targetDepth = RTHandles.Alloc(idTargetDepth, "TargetDepth");
			else if (targetDepth.nameID != idTargetDepth) RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref targetDepth, idTargetDepth);
		}


		internal void RecordRenderGraph(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			Camera camera = passParams.Camera;
			Vector2Int attachmentSize = passParams.AttachmentSize;
			CullingResults cullingResults = passParams.CullingResults;
			CameraTextures cameraTextures = passParams.CameraTextures;

			TextureHandle attachmentColor, attachmentDepth;

			//最終描画先をRTHandleとして確保。
			AllocTargets(ref _targetColorRt, ref _targetDepthRt, camera.targetTexture);

			//RasterPassはSetRenderAttachmentが必須だが、このパスではその必要がないためUnsafePassにする。RGViewerの表記の観点からもその方が良い。
			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.AttachmentSize = attachmentSize;
			passData.Camera = camera;
			passData.IsFirstCamera = passParams.IsFirstCamera;
			passData.CullingResults = cullingResults;

			//アスペクト比の補正に用いるパラメータ。
			float attachmentWidthRatio = attachmentSize.x / (float)attachmentSize.y;
			float attachmentHeightRatio = attachmentSize.y / (float)attachmentSize.x;
			bool isWide = 1f <= attachmentWidthRatio;
			passData.AspectFit = isWide ? new(attachmentWidthRatio, 1f) : new(1f, attachmentHeightRatio);
			passParams.AspectFit = passData.AspectFit;
			passParams.AspectFitRcp = isWide ? new(attachmentHeightRatio, 1f) : new(1f, attachmentWidthRatio);

			//MSAAの設定。
			int msaa = AdjustAndGetScreenMSAASamples(renderGraph, true);

			//出力先がdepthフォーマットのcamera.targetTextureである。
			bool isCameraTargetOffscreenDepth = camera.targetTexture && camera.targetTexture.format == RenderTextureFormat.Depth;

			if (camera.cameraType == CameraType.Preview)
			{
				camera.clearFlags = CameraClearFlags.Color;
				camera.backgroundColor = Color.gray.linear;
			}

			//CameraClearFlagsはSkybox(1)、Color(2)、Depth(3)、Nothing(4)。
			bool clearColor = camera.clearFlags <= CameraClearFlags.Color;
			bool clearDepth = camera.clearFlags <= CameraClearFlags.Depth;

			ImportResourceParams importParamsColor = new()
			{
				clearOnFirstUse = false,
				clearColor = clearColor ?  camera.backgroundColor.linear : Color.clear,
				discardOnLastUse = false,
			};

			ImportResourceParams importParamsDepth = new()
			{
				clearOnFirstUse = false,
				discardOnLastUse = false,
			};

			RenderTargetInfo importInfoColor = camera.targetTexture ?
				new()
				{
					width = camera.targetTexture.width,
					height = camera.targetTexture.height,
					volumeDepth = camera.targetTexture.volumeDepth,
					msaaSamples = camera.targetTexture.antiAliasing,
					format = camera.targetTexture.graphicsFormat,
				} :
				new()
				{
					width = Screen.width,
					height = Screen.height,
					volumeDepth = 1,
					msaaSamples = msaa,
					format = RenderingUtils.ColorFormat(passParams.UseHdr, false),
				};

			//TODO: C#10になったらwith式で書き換える。
			RenderTargetInfo importInfoDepth = importInfoColor;
			importInfoDepth.format = camera.targetTexture ? camera.targetTexture.depthStencilFormat : RenderingUtils.DepthStencilFormat;

			//backbufferのインポート。
			//NRP有効時、ImportTextureでは必ずImportInfoを伴うオーバーロードを用いる必要がある。
			//ImportBackbufferは恐らく不可で、URPでも使われていない。
			TextureHandle targetColor = renderGraph.ImportTexture(_targetColorRt, importInfoColor, importParamsColor);
			TextureHandle targetDepth = renderGraph.ImportTexture(_targetDepthRt, importInfoDepth, importParamsDepth);

			TextureDesc depthDesc = new(attachmentSize.x, attachmentSize.y)
			{
				name = "CameraDepthTexture",
				format = camera.cameraType == CameraType.Game ? RenderingUtils.DepthFormat : RenderingUtils.DepthStencilFormat,
				clearBuffer = true,
			};
			cameraTextures.TextureDepth = renderGraph.CreateTexture(depthDesc);

			//backbufferへ書き込む用のカメラである場合。
			if (camera.cameraType == CameraType.Game && !passParams.TargetIsGameRenderTexture)
			{
				if (_attachmentColorRt == null) _attachmentColorRt = RTHandles.Alloc(passParams.AttachmentSize.x, passParams.AttachmentSize.y, importInfoColor.format, name: "AttachmentColor");
				if (_attachmentDepthRt == null) _attachmentDepthRt = RTHandles.Alloc(passParams.AttachmentSize.x, passParams.AttachmentSize.y, importInfoDepth.format, name: "AttachmentDepth");

				//こちらはRenderTargetInfoを渡すとエラー。
				importParamsColor.clearOnFirstUse = clearColor;
				importParamsDepth.clearOnFirstUse = clearDepth;
				attachmentColor = renderGraph.ImportTexture(_attachmentColorRt, importParamsColor);
				attachmentDepth = renderGraph.ImportTexture(_attachmentDepthRt, importParamsDepth);
			}
			else
			{
				TextureDesc desc = new(attachmentSize.x, attachmentSize.y)
				{
					name = "AttachmentColor",
					format = importInfoColor.format,
					clearBuffer = clearColor,
					clearColor = clearColor ? camera.backgroundColor.linear : Color.clear,
				};
				attachmentColor = renderGraph.CreateTexture(desc);

				desc = new(attachmentSize.x, attachmentSize.y)
				{
					name = "AttachmentDepth",
					format = importInfoDepth.format,
					clearBuffer = clearDepth,
				};
				attachmentDepth = renderGraph.CreateTexture(desc);
			}

			cameraTextures.AttachmentColor = attachmentColor;
			cameraTextures.AttachmentDepth = attachmentDepth;
			cameraTextures.TargetColor = targetColor;
			cameraTextures.TargetDepth = targetDepth;

			builder.AllowPassCulling(false);//副作用があるパスなのでCullしない。
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				UnsafeCommandBuffer cmd = context.cmd;
				Vector2Int attachmentSize = passData.AttachmentSize;

				//アスペクト比の補正に用いるパラメータ。
				cmd.SetGlobalVector(IdAspectFit, passData.AspectFit);

				//dynamic scaling非対応だが、LensFlare（DataDriven）などで用いられるため必要。
				cmd.SetGlobalVector(IdScaledScreenParams, new (attachmentSize.x, attachmentSize.y, 1.0f + 1.0f / attachmentSize.x, 1.0f + 1.0f / attachmentSize.y));

				//TODO:ライティング関係の設定。


				if (passData.IsFirstCamera)
				{
					//時間変数の設定。
					cmd.SetGlobalFloat(IdTime, Time.time);
				}

				//カメラのVP行列の値などをGPUに送信。
				cmd.SetupCameraProperties(passData.Camera);

				//画面サイズをGPUに送信。
				cmd.SetGlobalVector(IdAttachmentSize, new(1f / attachmentSize.x, 1f / attachmentSize.y, attachmentSize.x, attachmentSize.y));

				//VFX Graphのセットアップ。
				VFXManager.PrepareCamera(passData.Camera);

				//VFX Graph。
				CommandBufferHelpers.VFXManager_ProcessCameraCommand(passData.Camera, cmd, default, passData.CullingResults);
			});
		}

		public void Dispose()
		{
			_targetColorRt?.Release();
			_targetDepthRt?.Release();
			_attachmentColorRt?.Release();
			_attachmentDepthRt?.Release();
		}
	}
}