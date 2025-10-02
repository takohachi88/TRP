using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// OIT（order independent transparency）の描画。
	/// </summary>
	public class OitPass
	{
		private static readonly ProfilingSampler SamplerDraw = ProfilingSampler.Get(TrpProfileId.OitDraw);
		private static readonly ProfilingSampler SamplerComposite = ProfilingSampler.Get(TrpProfileId.OitComposite);
		private static readonly ShaderTagId IdOit = new ShaderTagId("Oit");
		private static readonly int IdAccumulateTexture = Shader.PropertyToID("_AccumulateTexture");
		private static readonly int IdRevealageTexture = Shader.PropertyToID("_RevealageTexture");
		private readonly Material _material;

		public OitPass(Shader shader)
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
			public TextureHandle Src;
			public TextureHandle Dst;
			public TextureHandle Depth;
			public TextureHandle Acculumation;
			public TextureHandle Revealage;
			public Material Material;
		}

		public void RecordRenderGraph(ref PassParams passParams, float oitScale)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			RendererListHandle oitList = renderGraph.CreateRendererList(
				new RendererListDesc(IdOit, passParams.CullingResults, passParams.Camera)
				{
					sortingCriteria = SortingCriteria.None,//OITはソート不要。
					renderQueueRange = RenderQueueRange.transparent,
					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			//TODO: OITなオブジェクトがないなら以下の処理をスキップする。

			TextureDesc desc = new((int)(passParams.AttachmentSize.x * oitScale), (int)(passParams.AttachmentSize.y * oitScale));

			desc.name = "OitAccumulation";
			desc.format = GraphicsFormat.R16G16B16A16_SFloat;
			desc.clearColor = Color.clear;
			TextureHandle accumulation = renderGraph.CreateTexture(desc);

			desc.name = "OitRevealage";
			desc.format = GraphicsFormat.R16G16B16A16_SFloat;
			desc.clearColor = Color.white;
			TextureHandle revealage = renderGraph.CreateTexture(desc);

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerDraw.name, out PassData passData, SamplerDraw))
			{
				passData.Acculumation = accumulation;
				passData.Revealage = revealage;
				passData.Material = _material;
				passData.RendererListHandle = oitList;
				builder.UseRendererList(oitList);
				builder.SetRenderAttachment(accumulation, 0, AccessFlags.Write);
				builder.SetRenderAttachment(revealage, 1, AccessFlags.Write);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					context.cmd.DrawRendererList(passData.RendererListHandle);
					passData.Material.SetTexture(IdAccumulateTexture, passData.Acculumation);
					passData.Material.SetTexture(IdRevealageTexture, passData.Revealage);
				});
			}


			TextureHandle src = passParams.CameraTextures.AttachmentColor;
			desc = new(passParams.AttachmentSize.x, passParams.AttachmentSize.y);
			desc = src.GetDescriptor(renderGraph);
			desc.name = "OitComposite";
			TextureHandle dst = renderGraph.CreateTexture(desc);

			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(SamplerComposite.name, out PassData passData, SamplerComposite))
			{
				passData.Src = src;
				passData.Dst = dst;
				passData.Material = _material;

				builder.UseRendererList(oitList);
				builder.UseTexture(accumulation, AccessFlags.Read);
				builder.UseTexture(revealage, AccessFlags.Read);
				builder.UseTexture(src, AccessFlags.ReadWrite);
				builder.UseTexture(dst, AccessFlags.ReadWrite);
				builder.AllowPassCulling(true);
				builder.SetRenderFunc<PassData>(static (passData, context) =>
				{
					CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
					Blitter.BlitCameraTexture(cmd, passData.Src, passData.Dst, passData.Material, 0);
					Blitter.BlitCameraTexture(cmd, passData.Dst, passData.Src);
				});
			}
		}
	}
}
