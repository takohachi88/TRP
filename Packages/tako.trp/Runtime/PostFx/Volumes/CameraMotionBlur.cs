using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	/// <summary>
	/// カメラの移動と回転から、画面全体に方向性ブラーを適用する。
	/// </summary>
	[Serializable, VolumeComponentMenu("TRP/Camera Motion Blur")]
	public class CameraMotionBlur : VolumeComponent, IPostProcessComponent
	{
		[Tooltip("カメラモーションブラー全体の強度。")]
		public ClampedFloatParameter intensity = new(0f, 0f, 1f);

		[Tooltip("カメラの横・縦移動1ユニットを、何ピクセルのブラーへ変換するか。")]
		public MinFloatParameter translationSensitivity = new(12f, 0f);

		[Tooltip("カメラのYaw・Pitch回転1度を、何ピクセルのブラーへ変換するか。")]
		public MinFloatParameter rotationSensitivity = new(2f, 0f);

		[Tooltip("最終的なブラー幅の上限（ピクセル）。")]
		public NoInterpMinFloatParameter maxBlurRadius = new(64f, 0f);

		[Tooltip("ブラー方向に沿って読み取るサンプル数。")]
		public NoInterpClampedIntParameter sampleCount = new(3, 2, 8);

		[Tooltip("サンプル位置をピクセルごとにずらし、バンディングを軽減する。")]
		public BoolParameter dither = new(true);

		public bool IsActive() => 0f < intensity.value && 0f < maxBlurRadius.value;
	}
}
