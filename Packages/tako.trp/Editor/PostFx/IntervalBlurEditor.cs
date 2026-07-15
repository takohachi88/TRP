using Trp.PostFx;
using UnityEditor;
using UnityEditor.Rendering;

namespace TrpEditor.PostFx
{
	/// <summary>
	/// 選択中の間隔モードに必要な項目だけを表示する。
	/// </summary>
	[CustomEditor(typeof(IntervalBlur))]
	public sealed class IntervalBlurEditor : VolumeComponentEditor
	{
		private SerializedDataParameter _intensity;
		private SerializedDataParameter _intervalMode;
		private SerializedDataParameter _intervalTime;
		private SerializedDataParameter _intervalFrameCount;
		private SerializedDataParameter _fadeOut;

		public override void OnEnable()
		{
			PropertyFetcher<IntervalBlur> fetchers = new(serializedObject);
			_intensity = Unpack(fetchers.Find(x => x.intensity));
			_intervalMode = Unpack(fetchers.Find(x => x.intervalMode));
			_intervalTime = Unpack(fetchers.Find(x => x.intervalTime));
			_intervalFrameCount = Unpack(fetchers.Find(x => x.intervalFrameCount));
			_fadeOut = Unpack(fetchers.Find(x => x.fadeOut));
		}

		public override void OnInspectorGUI()
		{
			PropertyField(_intensity);
			PropertyField(_intervalMode);
			PropertyField(_fadeOut);

			switch ((IntervalBlurIntervalMode)_intervalMode.value.enumValueIndex)
			{
				case IntervalBlurIntervalMode.Time:
					PropertyField(_intervalTime);
					break;
				case IntervalBlurIntervalMode.FrameCount:
					PropertyField(_intervalFrameCount);
					break;
			}
		}
	}
}
