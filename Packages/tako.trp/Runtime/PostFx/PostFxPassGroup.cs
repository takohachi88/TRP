using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// ポストエフェクトの処理。
	/// </summary>
	[CreateAssetMenu(menuName = TrpConstants.PATH_CREATE_MENU_POST_FX + "PostFxPassGroup", fileName = nameof(PostFxPassGroup))]
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

			//登録されたパスを全て実行する。
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