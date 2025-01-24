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
		private class PassData
		{
		}

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordAndExecute(ref PassParams passParams)
		{
			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass("SetEditorTarget", out PassData _);
			builder.SetRenderAttachment(passParams.CameraTextures.TargetColor, 0, AccessFlags.None);
			builder.SetRenderAttachmentDepth(passParams.CameraTextures.TargetDepth, AccessFlags.None);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>((_, _) => { });
		}
	}
}