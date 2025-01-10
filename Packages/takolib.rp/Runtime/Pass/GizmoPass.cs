using System.Diagnostics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using TakoLib.Common;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TakoLib.Rp
{
	/// <summary>
	/// Gizmoの描画。
	/// </summary>
	internal class GizmoPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Gizmo);

		internal class PassData
		{
			public RendererListHandle GizmoRendererList;
		}

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordRenderGraph(ref PassParams passParams, TextureHandle dstDepth, GizmoSubset gizmoSubset)
		{
#if UNITY_EDITOR
			//TODO: SceneViewFilterModeが何なのか不明だがURPの実装を参考に一応記述。
			if (!Handles.ShouldRenderGizmos() || passParams.Camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered) return;

			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);

			passData.GizmoRendererList = passParams.RenderGraph.CreateGizmoRendererList(passParams.Camera, gizmoSubset);

			builder.SetRenderAttachment(passParams.CameraTextures.TargetColor, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(dstDepth, AccessFlags.ReadWrite);
			builder.UseRendererList(passData.GizmoRendererList);

			builder.AllowPassCulling(false);

			builder.SetRenderFunc<PassData>(static (passData, context) => context.cmd.DrawRendererList(passData.GizmoRendererList));
#endif
		}
	}
}