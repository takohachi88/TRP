using System.Diagnostics;
using TakoLib.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// Wireモードの描画。
	/// </summary>
	internal class WireOverlayPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.WireOverlay);

		public class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordRenderGraph(ref PassParams passParams)
		{
#if UNITY_EDITOR
			if (passParams.Camera.cameraType != CameraType.SceneView) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler))
			{
				builder.SetRenderAttachment(passParams.CameraTextures.TargetColor, 0, AccessFlags.Write);

				passData.RendererListHandle = renderGraph.CreateWireOverlayRendererList(passParams.Camera);
				builder.UseRendererList(passData.RendererListHandle);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.DrawRendererList(data.RendererListHandle);
				});
			}
#endif
		}
	}
}