using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Lift, Gamma, Gain")]
	public class LiftGammaGain : VolumeComponent, IPostProcessComponent
	{
		public ColorParameter lift = new(new(1, 1, 1, 0));
		public ColorParameter gamma = new(new(1, 1, 1, 0));
		public ColorParameter gain = new(new(1, 1, 1, 0));

		public LiftGammaGain()
		{
			displayName = "Lift, Gamma, Gain";
		}

		public bool IsActive() =>
			lift.value != Color.white ||
			gamma.value != Color.white ||
			gain.value != Color.white;
	}
}