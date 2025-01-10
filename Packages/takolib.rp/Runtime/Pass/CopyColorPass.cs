using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp
{
	/// <summary>
	/// ColorをglobalなTextureにコピーする。
	/// </summary>
	internal class CopyColorPass
	{
		private static readonly ProfilingSampler SamplerOpaque = ProfilingSampler.Get(TrpProfileId.CopyColorToOpaque);
		private static readonly ProfilingSampler SamplerTransparent = ProfilingSampler.Get(TrpProfileId.CopyColorToTransparent);

		private static readonly int IdCameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
		private static readonly int IdCameraTransparentTexture = Shader.PropertyToID("_CameraTransparentTexture");

		public enum CopyColorMode
		{
			Opaque,
			Transparent,
		}

		private readonly Material _coreBlitMaterial;

		internal CopyColorPass(Material coreBlitMaterial)
		{
			_coreBlitMaterial = coreBlitMaterial;
		}

		internal void RecordRenderGraph(ref PassParams passParams, CopyColorMode mode)
		{
			RenderGraph renderGraph = passParams.RenderGraph;

			switch (mode)
			{
				case CopyColorMode.Opaque:
					if (!passParams.UseOpaqueTexture) return;

					TextureDesc opaqueDesc = passParams.ColorDescriptor;
					opaqueDesc.name = "CameraOpaqueTexture";

					CopyTexture(
						passParams.RenderGraph,
						passParams.CameraTextures.AttachmentColor,
						passParams.CameraTextures.TextureOpaque = renderGraph.CreateTexture(opaqueDesc),
						IdCameraOpaqueTexture,
						SamplerOpaque,
						passParams.Camera);
					break;
				case CopyColorMode.Transparent:
					if (!passParams.UseTransparentTexture) return;

					TextureDesc transparentDesc = passParams.ColorDescriptor;
					transparentDesc.name = "CameraTransparentTexture";

					CopyTexture(
						passParams.RenderGraph,
						passParams.CameraTextures.AttachmentColor,
						passParams.CameraTextures.TextureTransparent = renderGraph.CreateTexture(transparentDesc),
						IdCameraTransparentTexture,
						SamplerTransparent,
						passParams.Camera);
					break;
			}
		}

		private class PassData
		{
			public TextureHandle Attachment;
			public TextureHandle Texture;
			public Material BlitMaterial;
			public Camera Camera;
		}

		private void CopyTexture(RenderGraph renderGraph, TextureHandle attachment, TextureHandle texture, int dstId, ProfilingSampler sampler, Camera camera)
		{
			//renderGraph.AddCopyPass(attachment, texture);
			//return;
			
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(sampler.name, out PassData passData, sampler);

			passData.Attachment = attachment;
			passData.Texture = texture;
			passData.BlitMaterial = _coreBlitMaterial;
			passData.Camera = camera;

			builder.AllowGlobalStateModification(true);
			builder.UseTexture(attachment, AccessFlags.Read);
			builder.SetRenderAttachment(texture, 0, AccessFlags.Write);
			builder.SetGlobalTextureAfterPass(passData.Texture, dstId);
			builder.AllowPassCulling(true);
			builder.SetRenderFunc<PassData>(static (passData, context) =>
			{
				RenderingUtils.Blit(context.cmd, passData.Attachment, passData.Texture, passData.Camera);
			});
		}
	}
}
