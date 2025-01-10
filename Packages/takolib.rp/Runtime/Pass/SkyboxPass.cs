using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp
{
	/// <summary>
	/// Skyboxの描画用パス。
	/// </summary>
	internal class SkyboxPass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.Skybox);

		public class PassData
		{
			public RendererListHandle RendererListHandle;
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			using IRasterRenderGraphBuilder builder = passParams.RenderGraph.AddRasterRenderPass(Sampler.name, out PassData passData, Sampler);
			
			RendererListHandle rendererListHandle = passParams.RenderGraph.CreateSkyboxRendererList(passParams.Camera);
			passData.RendererListHandle = rendererListHandle;

			CameraTextures cameraTextures = passParams.CameraTextures;
			
			builder.UseRendererList(rendererListHandle);
			builder.SetRenderAttachment(cameraTextures.AttachmentColor, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(cameraTextures.AttachmentDepth, AccessFlags.Write);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<PassData>(static (passData, context) => context.cmd.DrawRendererList(passData.RendererListHandle));
		}
	}
}