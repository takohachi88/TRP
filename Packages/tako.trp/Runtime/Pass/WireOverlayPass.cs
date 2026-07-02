using Unity.Profiling.LowLevel;
using System.Diagnostics;
using TakoLib.Common;
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
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Create(nameof(WireOverlayPass), MarkerFlags.Default);

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

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(nameof(WireOverlayPass), out PassData passData, Sampler))
			{
				passData.RendererListHandle = renderGraph.CreateWireOverlayRendererList(passParams.Camera);
				
				builder.SetRenderAttachment(passParams.CameraTextures.TargetColor, 0, AccessFlags.Write);
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