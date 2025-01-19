using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.VFX;

namespace Trp
{
	/// <summary>
	/// 各種カメラ系テクスチャの確保や、シェーダー変数の設定など。
	/// </summary>
	internal class SetupPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Setup);
		private static readonly int IdAttachmentSize = Shader.PropertyToID("_AttachmentSize");
		private static readonly int IdAspectFit = Shader.PropertyToID("_AspectFit");
		private static readonly int IdTime = Shader.PropertyToID("_Time");

		private class PassData
		{
			public bool UseIntermediateAttachments;
			public Vector2Int AttachmentSize;
			public Camera Camera;
			public bool IsFirstToBackbuffer;
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
			// In the editor, the system render target is always allocated with no msaa
			// See: ConfigureTargetTexture in PlayModeView.cs
			if (Application.isEditor) return 1;

			// In the players, when URP main rendering is done to an intermediate target and NRP enabled
			// we disable multisampling for the system backbuffer as a bandwidth optimization
			// doing so, we avoid storing costly msaa samples back to system memory for nothing
			bool canOptimizeScreenMSAASamples = useIntermediateColorTarget
											 && renderGraph.nativeRenderPassesEnabled
											 && Screen.msaaSamples > 1;

			if (canOptimizeScreenMSAASamples) Screen.SetMSAASamples(1);

			return Mathf.Max(Screen.msaaSamples, 1);
		}


		internal void RecordRenderGraph(
			ref PassParams passParams,
			RTHandle targetColorRt,
			RTHandle targetDepthRt,
			bool isFirstToBackbuffer)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			bool useIntermediateAttachments = passParams.UseIntermediateAttachments;
			Camera camera = passParams.Camera;
			Vector2Int attachmentSize = passParams.AttachmentSize;
			CullingResults cullingResults = passParams.CullingResults;
			CameraTextures cameraTextures = passParams.CameraTextures;

			//RasterPassはSetRenderAttachmentが必須だが、このパスではその必要がないためUnsafePassにする。RGViewerの表記の観点からもその方が良い。
			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			TextureHandle colorAttachment = default, depthAttachment = default;

			passData.UseIntermediateAttachments = useIntermediateAttachments;
			passData.AttachmentSize = attachmentSize;
			passData.Camera = camera;
			passData.IsFirstToBackbuffer = isFirstToBackbuffer;
			passData.CullingResults = cullingResults;

			//MSAAの設定。
			int msaa = AdjustAndGetScreenMSAASamples(renderGraph, useIntermediateAttachments);

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

			ImportResourceParams importBackbufferParams = new()
			{
				clearOnFirstUse = clearColor,
				clearColor = clearColor ? Color.clear : camera.backgroundColor.linear,
				discardOnLastUse = false,
			};

			ImportResourceParams importBackbufferParamsDepth = new()
			{
				clearOnFirstUse = clearDepth,
				discardOnLastUse = false,
			};

			RenderTargetInfo importInfo = camera.targetTexture ?
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
					format = GraphicsFormat.B10G11R11_UFloatPack32,
				};

			//TODO: C#10になったらwith式で書き換える。
			RenderTargetInfo importInfoDepth = importInfo;
			importInfoDepth.format = camera.targetTexture ? camera.targetTexture.depthStencilFormat : RenderingUtils.DepthStencilFormat;

			//backbufferのインポート。
			//NRP有効時、ImportTextureでは必ずImportInfoを伴うオーバーロードを用いる必要がある。
			TextureHandle targetColor = renderGraph.ImportTexture(targetColorRt, importInfo, importBackbufferParams);
			TextureHandle targetDepth = renderGraph.ImportTexture(targetDepthRt, importInfoDepth, importBackbufferParamsDepth);

			TextureDesc depthDesc = new(attachmentSize.x, attachmentSize.y)
			{
				name = "CameraDepthTexture",
				format = camera.cameraType == CameraType.Game ? RenderingUtils.DepthFormat : RenderingUtils.DepthStencilFormat,
				clearBuffer = true,
			};
			cameraTextures.TextureDepth = renderGraph.CreateTexture(depthDesc);

			if (useIntermediateAttachments)
			{
				TextureDesc desc = new(attachmentSize.x, attachmentSize.y)
				{
					name = "ColorAttachment",
					format = GraphicsFormat.R16G16B16A16_UNorm,
					clearBuffer = clearColor,
					clearColor = clearColor ? camera.backgroundColor.linear : Color.clear,
				};
				colorAttachment = renderGraph.CreateTexture(desc);

				desc = new(attachmentSize.x, attachmentSize.y)
				{
					name = "DepthAttachment",
					format = RenderingUtils.DepthStencilFormat,
					clearBuffer = clearDepth,
				};
				depthAttachment = renderGraph.CreateTexture(desc);
			}
			else
			{
				colorAttachment = targetColor;
				depthAttachment = targetDepth;
			}

			cameraTextures.AttachmentColor = colorAttachment;
			cameraTextures.AttachmentDepth = depthAttachment;
			cameraTextures.TargetColor = targetColor;
			cameraTextures.TargetDepth = targetDepth;

			builder.AllowPassCulling(false);//副作用があるパスなのでCullしない。
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				UnsafeCommandBuffer cmd = context.cmd;
				Vector2Int attachmentSize = passData.AttachmentSize;

				//アスペクト比の補正に用いるパラメータ。
				float attachmentWidthRatio = attachmentSize.x / (float)attachmentSize.y;
				float attachmentHeightRatio = attachmentSize.y / (float)attachmentSize.x;
				bool isWide = 1f <= attachmentWidthRatio;
				cmd.SetGlobalVector(IdAspectFit, isWide ? new(attachmentWidthRatio, 1f) : new(1f, attachmentHeightRatio));

				//TODO:ライティング関係の設定。


				if (passData.Camera.targetTexture || passData.IsFirstToBackbuffer)
				{
					//時間変数の設定。
					cmd.SetGlobalFloat(IdTime, Time.time);
				}

				//カメラのVP行列の値などをGPUに送信。
				cmd.SetupCameraProperties(passData.Camera);

				//画面サイズをGPUに送信。
				cmd.SetGlobalVector(IdAttachmentSize, new(1f / attachmentSize.x, 1f / attachmentSize.y, attachmentSize.x, attachmentSize.y));

				//VFX Graph。
				CommandBufferHelpers.VFXManager_ProcessCameraCommand(passData.Camera, cmd, default, passData.CullingResults);
			});
		}
	}
}