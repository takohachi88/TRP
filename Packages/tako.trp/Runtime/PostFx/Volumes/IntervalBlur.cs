using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace Trp.PostFx
{
	/// <summary>
	/// 一定間隔で保存した過去フレームを現在フレームへ合成する。
	/// </summary>
	[MovedFrom(true, sourceNamespace: "Trp.PostFx", sourceAssembly: "Trp", sourceClassName: "TemporalBlur")]
	[Serializable, VolumeComponentMenu("TRP/Interval Blur"), SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class IntervalBlur : VolumeComponent, IPostProcessComponent
	{
		[Tooltip("保存した過去フレームを合成する最大強度。")]
		public ClampedFloatParameter intensity = new(0f, 0f, 1f);

		[Tooltip("履歴を更新する間隔を、時間または描画フレーム数で指定する。")]
		public IntervalBlurIntervalModeParameter intervalMode = new(IntervalBlurIntervalMode.Time);

		[Tooltip("時間モードで履歴を更新する間隔（秒）。")]
		public MinFloatParameter intervalTime = new(0.05f, 0.01f);

		[Tooltip("フレームモードで履歴を更新する間隔（カメラの描画回数）。")]
		public MinIntParameter intervalFrameCount = new(5, 2);

		[Tooltip("残像の実時間ベースの減衰速度。正の値ならIntervalに関係なく滑らかに見えなくなる。")]
		public ClampedFloatParameter fadeOut = new(0.1f, 0f, 1f);

		public bool IsActive() => 0f < intensity.value;
	}

	[MovedFrom(true, sourceNamespace: "Trp.PostFx", sourceAssembly: "Trp", sourceClassName: "TemporalBlurIntervalMode")]
	public enum IntervalBlurIntervalMode
	{
		FrameCount,
		Time,
	}

	[Serializable]
	[MovedFrom(true, sourceNamespace: "Trp.PostFx", sourceAssembly: "Trp", sourceClassName: "TemporalBlurIntervalModeParameter")]
	public sealed class IntervalBlurIntervalModeParameter : VolumeParameter<IntervalBlurIntervalMode>
	{
		public IntervalBlurIntervalModeParameter(IntervalBlurIntervalMode value, bool overrideState = false)
			: base(value, overrideState) { }
	}
}
