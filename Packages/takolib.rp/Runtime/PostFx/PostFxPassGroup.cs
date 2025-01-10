using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp.PostFx
{
	/// <summary>
	/// ポストエフェクトの処理。
	/// </summary>
	[CreateAssetMenu(menuName = "Rendering/Trp/PostFx/PostFxPassGroup")]
	public class PostFxPassGroup : ScriptableObject
	{
		[SerializeField] private PostFxPassBase[] _passes;

		private static readonly ProfilingSampler Sampler = ProfilingSampler.Get(TrpProfileId.PostProcess);

		
		public void Initialize()
		{
			foreach (PostFxPassBase pass in _passes)
			{
				pass.Initialize();
			}
		}

		internal class PassData
		{
			public TextureHandle LastTargetHandle;
			public TextureHandle Src;
			public LastTarget LastTarget;
		}

		internal TextureHandle RecordRenderGraph(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			TrpCameraData cameraData = passParams.CameraData;
			TextureHandle src = passParams.CameraTextures.AttachmentColor;

			VolumeStack volumeStack = VolumeManager.instance.stack;
			VolumeManager.instance.Update(volumeStack, passParams.Camera.transform, cameraData ? cameraData.VolumeMask : 1);

			LastTarget target = LastTarget.None;
			TextureHandle lastTargetHandle = src;

			TextureDesc desc = passParams.ColorDescriptor;
			desc.name = "Post Process Dst";
			TextureHandle dst = renderGraph.CreateTexture(desc);

			foreach (PostFxPassBase pass in _passes)
			{
				target = pass.RecordRenderGraph(ref passParams, src, dst, volumeStack);
				if (target == LastTarget.Dst) (src, dst) = (dst, src);
			}

			return src;

/*			//src != lastTargetHandleなら少なくとも一度はBlitがあったということになる。
			if (passData.LastTarget != LastTarget.None && !passData.Src.Equals(passData.LastTargetHandle))
			{
				//RenderingUtils.BlitToCamera(context.cmd, passData.Src, passData.LastTargetHandle);
				return ;
			}
			return lastTargetHandle;*/
		}

		public void Dispose()
		{
			foreach (var pass in _passes) pass.Dispose();
		}
	}
}
