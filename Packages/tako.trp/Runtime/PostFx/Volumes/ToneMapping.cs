using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/ToneMapping")]
	public class ToneMapping : VolumeComponent, IPostProcessComponent
	{
		public enum Mode
		{
			None,
			Neutral,
			Reinhard,
			Aces,
		}

		public EnumParameter<Mode> mode = new(Mode.None);
		public bool IsActive() => mode != Mode.None;
	}
}