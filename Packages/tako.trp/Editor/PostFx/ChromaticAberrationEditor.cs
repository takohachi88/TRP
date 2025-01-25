using UnityEditor;
using UnityEditor.Rendering;
using Trp.PostFx;

namespace TrpEditor.PostFx
{
	[CustomEditor(typeof(ChromaticAberration))]
	public class ChromaticAberrationEditor : VolumeComponentEditor
	{
		private SerializedDataParameter _mode;
		private SerializedDataParameter _intensity;
		private SerializedDataParameter _center;
		private SerializedDataParameter _direction;
		private SerializedDataParameter _limit;
		private SerializedDataParameter _sampleCount;
		private SerializedDataParameter _dither;
		private SerializedDataParameter _chromaMap;
		private SerializedDataParameter _intensityMap;

		public override void OnEnable()
		{
			PropertyFetcher<ChromaticAberration> fetchers = new(serializedObject);

			_mode = Unpack(fetchers.Find(x => x.mode));
			_intensity = Unpack(fetchers.Find(x => x.intensity));
			_center = Unpack(fetchers.Find(x => x.center));
			_direction = Unpack(fetchers.Find(x => x.direction));
			_limit = Unpack(fetchers.Find(x => x.limit));
			_sampleCount = Unpack(fetchers.Find(x => x.sampleCount));
			_dither = Unpack(fetchers.Find(x => x.dither));
			_chromaMap = Unpack(fetchers.Find(x => x.chromaLut));
			_intensityMap = Unpack(fetchers.Find(x => x.intensityMap));
		}

		public override void OnInspectorGUI()
		{
			PropertyField(_mode);
			PropertyField(_intensity);
			PropertyField(_sampleCount);
			PropertyField(_dither);

			switch (_mode.value.intValue)
			{
				case (int)ChromaticAberration.Mode.Radial:
					PropertyField(_center);
					PropertyField(_limit);
					PropertyField(_chromaMap);
					break;
				case (int)ChromaticAberration.Mode.Direction:
					PropertyField(_direction);
					PropertyField(_intensityMap);
					break;
				default: break;
			}
		}
	}
}