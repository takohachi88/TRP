using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp
{
	public enum SoftShadowQuality
	{
		Low,
		Medium,
		High,
	}

	/// <summary>Lightに付随するTRP固有データ。設定はLightのInspectorから編集する。</summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Light))]
	public sealed class TrpLightData : MonoBehaviour, IAdditionalData
	{
		[SerializeField, Min(0)] private float _blurRadius = 2;
		[SerializeField] private SoftShadowQuality _quality = SoftShadowQuality.Medium;
		public float BlurRadius => Mathf.Max(0, _blurRadius);
		public SoftShadowQuality Quality => _quality;

		// Runtimeで生成され、まだ追加データを持たないLightには既定値を使用する。
		// 毎フレームの取得で配列や一時オブジェクトを生成しない。
		internal static float4 GetFilter(Light light)
		{
			if (light.shadows != LightShadows.Soft) return new float4(0, 0, 0, 0);
			return light.TryGetComponent<TrpLightData>(out var data) && data.enabled
				? new float4(data.BlurRadius, (int)data.Quality, 0, 0)
				: new float4(2, (int)SoftShadowQuality.Medium, 0, 0);
		}
	}
}
