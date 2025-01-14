using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace TakoLib.Rp.PostFx
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

		public void Initialize()
		{
			PassMaterial = CoreUtils.CreateEngineMaterial(PassShader);
			Sampler = new(GetType().Name);
			OnInitialize();
		}

		protected virtual void OnInitialize() { }

		public abstract LastTarget RecordRenderGraph(ref PassParams passParams, TextureHandle src, TextureHandle dst, VolumeStack volumeStack);

		public virtual void Dispose()
		{
			CoreUtils.Destroy(PassMaterial);
		}
	}
}
