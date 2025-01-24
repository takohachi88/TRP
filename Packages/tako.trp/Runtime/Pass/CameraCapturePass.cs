using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

namespace Trp
{
	/// <summary>
	/// CameraCaptureBridgeに対応するためのパス。
	/// </summary>
	public class CameraCapturePass
	{
		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.CameraCapture);

		public class PassData
		{
			public TextureHandle Src;
			public IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> CaptureActions;
		}

		public void RecordRenderGraph(ref PassParams passParams)
		{
			IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> captureActions = CameraCaptureBridge.GetCaptureActions(passParams.Camera);
			if (captureActions == null) return;

			using IUnsafeRenderGraphBuilder builder = passParams.RenderGraph.AddUnsafePass(Sampler.name, out PassData passData, Sampler);

			passData.Src = passParams.CameraTextures.AttachmentColor;
			passData.CaptureActions = captureActions;

			builder.AllowPassCulling(false);
			builder.UseTexture(passData.Src);
			builder.SetRenderFunc<PassData>((passData, context) =>
			{
				CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
				IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> captureActions = passData.CaptureActions;
				for (captureActions.Reset(); captureActions.MoveNext();) captureActions.Current(passData.Src, cmd);
			});
		}
	}
}