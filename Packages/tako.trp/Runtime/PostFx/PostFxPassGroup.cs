using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
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

		internal TextureHandle RecordRenderGraph(ref PassParams passParams)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			TextureHandle src = passParams.CameraTextures.AttachmentColor;

			VolumeStack volumeStack = VolumeManager.instance.stack;

			TextureDesc desc = passParams.ColorDescriptor;
			desc.name = "PostProcessDst";
			desc.clearBuffer = true;
			TextureHandle dst = renderGraph.CreateTexture(desc);

			foreach (PostFxPassBase pass in _passes)
			{
				LastTarget target = pass.RecordRenderGraph(ref passParams, src, dst, volumeStack);
				if (target == LastTarget.Dst) (src, dst) = (dst, src);
			}

			return src;
		}

		public void Dispose()
		{
			foreach (var pass in _passes) pass.Dispose();
		}
	}
}
