using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Lift, Gamma, Gain"), DisplayInfo(name = "Lift, Gamma, Gain")]
	public class LiftGammaGain : VolumeComponent, IPostProcessComponent
	{
		//LUTはrgbだが、ColorUtils.PrepareLiftGammaGainではα値も影響する。
		private static readonly Color defaultColor = new(1, 1, 1, 0);

		public ColorParameter lift = new(defaultColor);
		public ColorParameter gamma = new(defaultColor);
		public ColorParameter gain = new(defaultColor);

		public bool IsActive() =>
			lift.value != defaultColor ||
			gamma.value != defaultColor ||
			gain.value != defaultColor;
	}
}