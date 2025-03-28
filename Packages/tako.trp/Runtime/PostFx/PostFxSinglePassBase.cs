using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	/// <summary>
	/// 一つのシェーダーとマテリアルを使う場合のポストエフェクトはこれを継承する。
	/// </summary>
	public abstract class PostFxSinglePassBase : PostFxPassBase
	{
		[SerializeField] protected Shader PassShader;

		protected ProfilingSampler Sampler;

		protected Material PassMaterial;

		protected override void OnInitialize()
		{
			PassMaterial = CoreUtils.CreateEngineMaterial(PassShader);
			Sampler = new(GetType().Name);
		}

		protected override void OnDispose()
		{
			CoreUtils.Destroy(PassMaterial);
			base.OnDispose();
		}
	}
}
