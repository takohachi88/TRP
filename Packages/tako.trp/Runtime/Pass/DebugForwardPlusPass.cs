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

		private const string PANEL_NAME_FORWARD_PLUS = "Forward+";

		private static bool _showTiles;
		private static float _tileOpacity = 0.5f;

		private readonly Material _tileMaterial;

		private static readonly int IdDebugForwardPlusTileOpacity = Shader.PropertyToID("_DebugForwardPlusTileOpacity");


		public DebugForwardPlusPass(Shader debugForwardPlusTileShader)
		{
#if UNITY_EDITOR
			_tileMaterial = CoreUtils.CreateEngineMaterial(debugForwardPlusTileShader);

			DebugManager.instance.GetPanel(PANEL_NAME_FORWARD_PLUS, true).children.Add(
				new DebugUI.BoolField
				{
					displayName = "Show Tiles",
					getter = static () => _showTiles,
					setter = static value => _showTiles = value,
				},
				new DebugUI.FloatField
				{
					displayName = "Tiles Opacity",
					min = static () => 0f,
					max = static () => 1f,
					getter = static () => _tileOpacity,
					setter = static value => _tileOpacity = value,
				});
#endif
		}

		public class PassData
		{
			public TextureHandle Src;
			public Material Material;
		}

		[Conditional(Defines.UNITY_EDITOR)]
		public void RecordRenderGraph(ref PassParams passParams)
		{
#if UNITY_EDITOR
			if (
				passParams.Camera.cameraType is not (CameraType.Game or CameraType.SceneView) ||
				!_showTiles ||
				!passParams.ForwardPlusTileBuffer.IsValid()) return;

			RenderGraph renderGraph = passParams.RenderGraph;

			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);
			passData.Src = passParams.CameraTextures.TargetColor;
			passData.Material = _tileMaterial;
			builder.UseTexture(passData.Src, AccessFlags.Write);
			builder.UseBuffer(passParams.ForwardPlusTileBuffer);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				context.cmd.SetGlobalFloat(IdDebugForwardPlusTileOpacity, _tileOpacity);
				context.cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 0, MeshTopology.Triangles, 3, 1);
			});
#endif
		}

		public void Dispose()
		{
			DebugManager.instance.RemovePanel(PANEL_NAME_FORWARD_PLUS);
		}
	}
}