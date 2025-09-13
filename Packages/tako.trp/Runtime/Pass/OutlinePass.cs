using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine;

namespace Trp
{
	/// <summary>
	/// ジオメトリの描画。
	/// </summary>
	internal class OutlinePass
	{
		private static readonly  ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Outline);
		private static readonly ShaderTagId ShaderTagId = new(TrpConstants.PassNames.OUTLINE);
		private static readonly int IdTanFov = Shader.PropertyToID("_TanFov");

		private class PassData
		{
			public float TanFov;
			public RendererListHandle RendererListHandle;
		}

		internal void RecordRenderGraph(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.TanFov = Mathf.Tan(passParams.Camera.fieldOfView / 180f);
			passData.RendererListHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagId, passParams.CullingResults, passParams.Camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,

					//rendererConfiguration =
					//PerObjectData.ReflectionProbes, //TODO: 必要か検討。

					renderQueueRange = RenderQueueRange.opaque,

					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			builder.UseRendererList(passData.RendererListHandle);
			builder.AllowGlobalStateModification(true);
			builder.AllowPassCulling(false);
			builder.SetRenderAttachment(passParams.CameraTextures.AttachmentColor, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(passParams.CameraTextures.AttachmentDepth, AccessFlags.Write);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				IRasterCommandBuffer cmd = context.cmd;
				cmd.SetGlobalFloat(IdTanFov, passData.TanFov);
				cmd.DrawRendererList(passData.RendererListHandle);
			});
		}
	}
}