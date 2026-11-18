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
		[SerializeField] private bool _shadowDithering;
		[SerializeField] private bool _pcss;
		[SerializeField, Range(0, 20)] private float _angularDiameter = 0.5f;
		[SerializeField, Min(0)] private float _sourceRadius = 0.05f;
		[SerializeField, Min(0)] private float _maxPenumbraSize = 1;
		[SerializeField, Min(0.001f)] private float _maxSamplingDistance = 10;
		[SerializeField] private bool _enableShadowTint;
		[SerializeField, ColorUsage(false)] private Color _shadowTint = Color.black;
		[SerializeField] private bool _penumbraTint;
		public float BlurRadius => Mathf.Max(0, _blurRadius);
		public SoftShadowQuality Quality => _quality;

		// PCSSの幅はワールド単位で制御する。Directionalだけは光源の角直径を使用する。
		internal static float4 GetPcss(Light light)
		{
			if (light.shadows != LightShadows.Soft ||
				!light.TryGetComponent<TrpLightData>(out var data) || !data.enabled || !data._pcss)
				return 0;
			float size = light.type == LightType.Directional
				? Mathf.Tan(Mathf.Clamp(data._angularDiameter, 0, 20) * Mathf.Deg2Rad * 0.5f)
				: Mathf.Max(0, data._sourceRadius);
			return new float4(size, Mathf.Max(0, data._maxPenumbraSize), Mathf.Max(0.001f, data._maxSamplingDistance), 1);
		}

		internal static float4 GetShadowTint(Light light)
		{
			if (light.shadows == LightShadows.None ||
				!light.TryGetComponent<TrpLightData>(out var data) || !data.enabled || !data._enableShadowTint)
				return 0;
			Color tint = data._shadowTint;
			// HDRPと同じく、有彩色のPenumbra Tintのみガンマ2.2で補正する。
			bool penumbra = data._penumbraTint && (tint.r != tint.g || tint.g != tint.b);
			float3 rgb = math.saturate(new float3(tint.r, tint.g, tint.b));
			if (penumbra) rgb = math.pow(rgb, new float3(2.2f));
			// w=0で無効、1で影全体、2で半影のみ。シェーダー側で色計算を分岐する。
			return new float4(rgb, penumbra ? 2 : 1);
		}

		// Runtimeで生成され、まだ追加データを持たないLightには既定値を使用する。
		// 毎フレームの取得で配列や一時オブジェクトを生成しない。
		internal static float4 GetFilter(Light light)
		{
			if (light.shadows != LightShadows.Soft) return new float4(0, 0, 0, 0);
			return light.TryGetComponent<TrpLightData>(out var data) && data.enabled
				? new float4(data.BlurRadius, (int)data.Quality, data._shadowDithering ? 1 : 0, 0)
				: new float4(2, (int)SoftShadowQuality.Medium, 0, 0);
		}
	}
}
