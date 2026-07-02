using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// WbOit（Weighted Blended Order Independent Transparency）の描画。
	/// </summary>
	public class WbOitPass
	{
		private static readonly ProfilingSampler SamplerDraw = ProfilingSampler.Create(nameof(WbOitPass) + ".Draw", MarkerFlags.Default);
		private static readonly ProfilingSampler SamplerComposite = ProfilingSampler.Create(nameof(WbOitPass) + ".Composite", MarkerFlags.Default);
		private static readonly ShaderTagId IdWbOit = new ShaderTagId("WbOit");
		private static readonly int IdRevealageTexture = Shader.PropertyToID("_RevealageTexture");
		private readonly Material _material;

		public WbOitPass(Shader shader)
		{
			_material = CoreUtils.CreateEngineMaterial(shader);
		}

		public void Dispose()
		{
			CoreUtils.Destroy(_material);
		}

		private class PassData
		{
			public RendererListHandle RendererListHandle;
			public TextureHandle Acculumation;
			public TextureHandle Revealage;
			public Material Material;
		}

		public void RecordRenderGraph(ref PassParams passParams, float WbOitScale)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			RendererListHandle WbOitList = renderGraph.CreateRendererList(
				new RendererListDesc(IdWbOit, passParams.CullingResults, passParams.Camera)
				{
					layerMask = passParams.CommonSettings.TransparentLayerMask,
					sortingCriteria = SortingCriteria.None,//WbOitはソート不要。
					renderQueueRange = RenderQueueRange.transparent,
					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			//TODO: WbOitなオブジェクトがないなら以下の処理をスキップする。

			TextureDesc desc = new((int)(passParams.AttachmentSize.x * WbOitScale), (int)(passParams.AttachmentSize.y * WbOitScale));

			desc.name = "WbOitAccumulation";
			desc.format = GraphicsFormat.R16G16B16A16_SFloat;
			desc.clearBuffer = true;
			desc.clearColor = Color.clear;
			TextureHandle accumulation = renderGraph.CreateTexture(desc);

			desc.name = "WbOitRevealage";
			desc.format = GraphicsFormat.R8_UNorm;
			desc.clearColor = Color.white;
			TextureHandle revealage = renderGraph.CreateTexture(desc);

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerDraw.name, out PassData passData, SamplerDraw))
			{
				passData.Acculumation = accumulation;
				passData.Revealage = revealage;
				passData.RendererListHandle = WbOitList;
				builder.UseRendererList(WbOitList);
				builder.SetRenderAttachment(accumulation, 0, AccessFlags.Write);
				builder.SetRenderAttachment(revealage, 1, AccessFlags.Write);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					context.cmd.DrawRendererList(passData.RendererListHandle);
				});
			}

			TextureHandle cameraColor = passParams.CameraTextures.AttachmentColor;
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerComposite.name, out PassData passData, SamplerComposite))
			{
				passData.Acculumation = accumulation;
				passData.Revealage = revealage;
				passData.Material = _material;

				builder.UseTexture(accumulation, AccessFlags.Read);
				builder.UseTexture(revealage, AccessFlags.Read);
				builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					passData.Material.SetTexture(IdRevealageTexture, passData.Revealage);
					Blitter.BlitTexture(context.cmd, passData.Acculumation, Vector2.one, passData.Material, 0);
				});
			}
		}
	}
}
