using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Trp.PostFx;

namespace TrpEditor.PostFx
{
	/// <summary>
	/// ブラー設定と、現在の設定から算出した処理負荷をインスペクタに表示。
	/// </summary>
	[CustomEditor(typeof(Blur))]
	public class BlurEditor : VolumeComponentEditor
	{
		private SerializedDataParameter _intensity;
		private SerializedDataParameter _scale;
		private SerializedDataParameter _quality;
		private SerializedDataParameter _controlTexture;
		private SerializedDataParameter _downSample;

		public override void OnEnable()
		{
			PropertyFetcher<Blur> fetchers = new(serializedObject);
			_intensity = Unpack(fetchers.Find(x => x.intensity));
			_scale = Unpack(fetchers.Find(x => x.scale));
			_quality = Unpack(fetchers.Find(x => x.quality));
			_controlTexture = Unpack(fetchers.Find(x => x.controlTexture));
			_downSample = Unpack(fetchers.Find(x => x.downSample));
		}

		public override void OnInspectorGUI()
		{
			PropertyField(_intensity);
			PropertyField(_scale);
			PropertyField(_quality, new GUIContent("Quality (Max Sample Pairs)"));
			PropertyField(_controlTexture);
			PropertyField(_downSample);

			// ControlTexture による局所的な減衰前の、最大処理負荷を表示。
			Blur blur = (Blur)target;
			int tapCount = BlurPass.GetBlurTapCount(blur);
			int totalSamples = BlurPass.GetTotalBlurSampleCount(blur);
			int pyramidLevels = BlurPass.GetPyramidLevelCount(blur);
			int downSampleFactor = BlurPass.GetDownSampleFactor(blur);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"サンプル回数（軸）: {tapCount}");
			EditorGUILayout.LabelField($"サンプル回数（合計）: {totalSamples}");
			EditorGUILayout.LabelField($"ピラミッド数: {pyramidLevels}");
			EditorGUILayout.LabelField($"最小バッファスケール: {1f / downSampleFactor} = 1/{downSampleFactor}");
		}
	}
}
