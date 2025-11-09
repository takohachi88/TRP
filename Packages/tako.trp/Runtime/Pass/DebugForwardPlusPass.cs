using System.Diagnostics;
using TakoLib.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// Forward+のデバッグ。
	/// </summary>
	internal class DebugForwardPlusPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.DebugForwardPlus);
		private readonly Material _tileMaterial;

		private static readonly int IdDebugForwardPlusTileOpacity = Shader.PropertyToID("_DebugForwardPlusTileOpacity");


		public DebugForwardPlusPass(Shader debugForwardPlusTileShader)
		{
#if UNITY_EDITOR
			_tileMaterial = CoreUtils.CreateEngineMaterial(debugForwardPlusTileShader);
#endif
		}

		private class PassData
		{
			public Material Material;
			public DebugForwardPlus.CameraDebugValue CameraDebugValue;
		}


		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordRenderGraph(ref PassParams passParams)
		{
#if UNITY_EDITOR
			if (!passParams.LightingResources.ForwardPlusTileBuffer.IsValid()) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			if (passParams.ForwardPlusCameraDebugValue == null || !passParams.ForwardPlusCameraDebugValue.ShowTiles) return;

			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);
			passData.Material = _tileMaterial;
			passData.CameraDebugValue = passParams.ForwardPlusCameraDebugValue;
			builder.SetRenderAttachment(passParams.CameraTextures.AttachmentColor, 0, AccessFlags.Write);
			builder.UseBuffer(passParams.LightingResources.ForwardPlusTileBuffer);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.SetGlobalFloat(IdDebugForwardPlusTileOpacity, passData.CameraDebugValue.Opacity);
				context.cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 0, MeshTopology.Triangles, 3, 1);
			});
#endif
		}
	}
}