using System.Diagnostics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using TakoLib.Common;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Trp
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
			public TextureHandle Color;
			public TextureHandle Depth;
		}

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordRenderGraph(ref PassParams passParams, TextureHandle dstColor, TextureHandle dstDepth, GizmoSubset gizmoSubset)
		{
#if UNITY_EDITOR
			//TODO: SceneViewFilterModeが何なのか不明だがURPの実装を参考に一応記述。
			if (!Handles.ShouldRenderGizmos() || passParams.Camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered) return;

			//Rasterパスでもいけそうに見えるが、Unity6000.1.6ではUnsafeパスにしないとエラーが出た。
			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.GizmoRendererList = passParams.RenderGraph.CreateGizmoRendererList(passParams.Camera, gizmoSubset);
			passData.Color = dstColor;
			passData.Depth = dstDepth;

			builder.UseTexture(passData.Color, AccessFlags.Write);
			builder.UseTexture(dstDepth, AccessFlags.ReadWrite);
			builder.UseRendererList(passData.GizmoRendererList);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.SetRenderTarget(passData.Color, passData.Depth);
				context.cmd.DrawRendererList(passData.GizmoRendererList);
			});
#endif
		}
	}
}