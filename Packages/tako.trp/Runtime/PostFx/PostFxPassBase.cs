using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp.PostFx
{
	/// <summary>
	/// 最後の描画先。
	/// </summary>
	public enum LastTarget
	{
		/// <summary>
		/// Blitが発生しなかった。
		/// </summary>
		None,

		/// <summary>
		/// 最後のBlit先が当初のSrcとなった。
		/// </summary>
		Src,

		/// <summary>
		/// 最後のBlit先が当初のDstとなった。
		/// </summary>
		Dst,
	}

	/// <summary>
	/// ポストエフェクトはこれを継承する。
	/// </summary>
	public abstract class PostFxPassBase : ScriptableObject
	{
		private static readonly int IdSrcSize = Shader.PropertyToID("_SrcSize");

		public void Initialize()
		{
			OnInitialize();
		}

		/// <summary>
		/// srcのサイズをシェーダーに転送する。
		/// </summary>
		/// <param name="material"></param>
		/// <param name="src"></param>
		protected static void SetSrcSize(Material material, RTHandle src)
		{
			float width = src.rt.width;
			float height = src.rt.height;
			material.SetVector(IdSrcSize, new(width, height, 1f / width, 1f / height));
		}

		protected static void FinalBlit(RasterCommandBuffer cmd, TextureHandle src, TextureHandle dst, Material material, int passIndex, Camera camera)
		{
			Vector4 scaleBias = RenderingUtils.GetFinalBlitScaleBias(src, dst, camera);
			Blitter.BlitTexture(cmd, src, scaleBias, material, passIndex);
		}

		protected static void Blit(RasterCommandBuffer cmd, TextureHandle src, Material material, int passIndex)
		{
			Blitter.BlitTexture(cmd, src, Vector2.one, material, passIndex);
		}

		protected virtual void OnInitialize() { }

		public abstract LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack);

		public void Dispose()
		{
			OnDispose();
		}

		protected virtual void OnDispose() { }
	}
}
