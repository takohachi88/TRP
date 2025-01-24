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
		[SerializeField] protected Shader PassShader;

		protected ProfilingSampler Sampler;

		protected Material PassMaterial;

		private static readonly int IdSrcSize = Shader.PropertyToID("_SrcSize");

		public void Initialize()
		{
			PassMaterial = CoreUtils.CreateEngineMaterial(PassShader);
			Sampler = new(GetType().Name);
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

		protected virtual void OnInitialize() { }

		public abstract LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack);

		public virtual void Dispose()
		{
			CoreUtils.Destroy(PassMaterial);
		}
	}
}
