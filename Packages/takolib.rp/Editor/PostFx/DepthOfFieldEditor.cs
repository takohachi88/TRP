using UnityEditor;
using UnityEditor.Rendering;

namespace TakoLib.Rp.PostFx
{
	[CustomEditor(typeof(DepthOfField))]
	public class DepthOfFieldEditor : VolumeComponentEditor
	{
		private SerializedDataParameter _mode;

		private SerializedDataParameter _focusDistance;
		private SerializedDataParameter _focalLength;
		private SerializedDataParameter _aperture;
		private SerializedDataParameter _bladeCount;
		private SerializedDataParameter _bladeCurvature;
		private SerializedDataParameter _bladeRotation;

		public override void OnEnable()
		{
			PropertyFetcher<DepthOfField> fetchers = new(serializedObject);

			_mode = Unpack(fetchers.Find(x => x.mode));

			_focusDistance = Unpack(fetchers.Find(x => x.focusDistance));
			_focalLength = Unpack(fetchers.Find(x => x.focalLength));
			_aperture = Unpack(fetchers.Find(x => x.aperture));
			_bladeCount = Unpack(fetchers.Find(x => x.bladeCount));
			_bladeCurvature = Unpack(fetchers.Find(x => x.bladeCurvature));
			_bladeRotation = Unpack(fetchers.Find(x => x.bladeRotation));
		}

		public override void OnInspectorGUI()
		{
			PropertyField(_mode);

			switch (_mode.value.intValue)
			{
				case (int)DepthOfFieldMode.BokehUrp:
					PropertyField(_focusDistance);
					PropertyField(_focalLength);
					PropertyField(_aperture);
					PropertyField(_bladeCount);
					PropertyField(_bladeCurvature);
					PropertyField(_bladeRotation);
					break;
				default: break;
			}
		}
	}
}