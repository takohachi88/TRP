using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TakoLib.Rp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/LiftGammaGain")]
	public class LiftGammaGain : VolumeComponent, IPostProcessComponent
	{
		public ColorParameter lift = new(new(1, 1, 1, 0));
		public ColorParameter gamma = new(new(1, 1, 1, 0));
		public ColorParameter gain = new(new(1, 1, 1, 0));

		public bool IsActive() =>
			lift.value != Color.white ||
			gamma.value != Color.white ||
			gain.value != Color.white;
	}
}