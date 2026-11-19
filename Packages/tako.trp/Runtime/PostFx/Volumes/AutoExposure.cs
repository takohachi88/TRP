using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	/// <summary>
	/// 測光結果に応じてカメラの露出を時間方向に補正する。
	/// </summary>
	[Serializable, VolumeComponentMenu("TRP/Auto Exposure"), SupportedOnRenderPipeline(typeof(TrpAsset))]
	public sealed class AutoExposure : VolumeComponent, IPostProcessComponent
	{
		[Tooltip("リニアHDRの画面輝度を測り、露出を自動で補正します。")]
		public BoolParameter enable = new(false);

		[Tooltip("露出を下げる最大幅（ストップ）。")]
		public ClampedFloatParameter maxDarkening = new(0.75f, 0f, 4f);

		[Tooltip("露出を上げる最大幅（ストップ）。")]
		public ClampedFloatParameter maxBrightening = new(0.75f, 0f, 4f);

		[Tooltip("暗所から明所に移動したとき、目標露出との差が約63%縮まるまでの時間（秒）。")]
		public MinFloatParameter darkToLightTime = new(0.3f, 0.01f);

		[Tooltip("明所から暗所に移動したとき、目標露出との差が約63%縮まるまでの時間（秒）。")]
		public MinFloatParameter lightToDarkTime = new(1f, 0.01f);

		public bool IsActive() => enable.value && (maxDarkening.value > 0f || maxBrightening.value > 0f);
	}
}
