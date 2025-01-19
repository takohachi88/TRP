using System.Diagnostics;
using TakoLib.Common;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// SceneView描画時にエンジン側の描画処理（例えばgridの描画）が適切に行われるようにするためのパス。
	/// </summary>
	public class SetEditorTargetPass
	{
		private class DummyPassData { }

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordAndExecute(RenderGraph renderGraph)
		{
			using (var builder = renderGraph.AddUnsafePass("SetEditorTarget", out DummyPassData _))
			{
				builder.AllowPassCulling(false);

				builder.SetRenderFunc((DummyPassData data, UnsafeGraphContext context) =>
				{
					context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
						RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, // color
						RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare); // depth
				});
			}
		}
	}
}