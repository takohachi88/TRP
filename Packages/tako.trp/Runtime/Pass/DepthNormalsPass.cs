using Unity.Profiling.LowLevel;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

namespace Trp
{
	/// <summary>
	/// DepthTextureとNormalsTextureを生成する。
	/// </summary>
	internal class DepthNormalsPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Create(nameof(DepthNormalsPass), MarkerFlags.Default);
		private static readonly ShaderTagId ShaderTagId = new("DepthNormalsOnly");

		private class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams)
		{
			if (!passParams.UseDepthNormalsTexture) return;
			RecordRenderGraph(ref passParams, uint.MaxValue, RenderQueueRange.all, true, true);
		}

		/// <summary>
		/// GPU遮蔽カリング用にDepthNormalsOnlyを描画する。
		/// 新規のDepthOnlyパスは作らず、既存のDepthNormalsOnlyを深度ピラミッド入力として再利用する。
		/// </summary>
		internal void RecordGpuOcclusionPrepass(ref PassParams passParams, uint batchLayerMask, bool createTargets, bool setGlobals)
		{
			RecordRenderGraph(ref passParams, batchLayerMask, RenderQueueRange.opaque, createTargets, setGlobals);
		}

		private void RecordRenderGraph(
			ref PassParams passParams,
			uint batchLayerMask,
			RenderQueueRange renderQueueRange,
			bool createTargets,
			bool setGlobals)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(nameof(DepthNormalsPass), out PassData passData, Sampler);

			DrawingSettings drawingSettings = new(ShaderTagId, new SortingSettings(passParams.Camera)
			{
				criteria = SortingCriteria.CommonOpaque,
			})
			{
				perObjectData = PerObjectData.None,
			};
			FilteringSettings filteringSettings = new(renderQueueRange, passParams.CommonSettings.OpaqueLayerMask, (uint)passParams.RenderingLayerMask)
			{
				batchLayerMask = batchLayerMask,
			};
			passData.RendererListHandle = renderGraph.CreateRendererList(new RendererListParams(passParams.CullingResults, drawingSettings, filteringSettings));

			TextureHandle normals;
			TextureHandle depth;
			if (createTargets)
			{
				normals = renderGraph.CreateTexture(new TextureDesc(passParams.AttachmentSize.x, passParams.AttachmentSize.y)
				{
					colorFormat = GraphicsFormat.R8G8B8A8_SNorm,
					clearBuffer = true,
					name = "CameraNormalsTexture",
				});
				depth = renderGraph.CreateTexture(new TextureDesc(passParams.AttachmentSize.x, passParams.AttachmentSize.y)
				{
					colorFormat = GraphicsFormat.D32_SFloat,
					clearBuffer = true,
					name = "CameraDepthTexture",
				});
				passParams.CameraTextures.TextureNormals = normals;
				passParams.CameraTextures.TextureDepth = depth;
			}
			else
			{
				normals = passParams.CameraTextures.TextureNormals;
				depth = passParams.CameraTextures.TextureDepth;
			}

			AccessFlags attachmentAccess = createTargets ? AccessFlags.Write : AccessFlags.ReadWrite;
			builder.SetRenderAttachment(normals, 0, attachmentAccess);
			builder.SetRenderAttachmentDepth(depth, attachmentAccess);
			if (setGlobals)
			{
				builder.SetGlobalTextureAfterPass(normals, TrpConstants.ShaderIds.CameraNormalsTexture);
				builder.SetGlobalTextureAfterPass(depth, TrpConstants.ShaderIds.CameraDepthTexture);
			}

			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (data, context) =>
			{
				context.cmd.DrawRendererList(data.RendererListHandle);
			});
		}
	}
}
