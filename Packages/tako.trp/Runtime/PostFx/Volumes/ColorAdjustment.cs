using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
    [Serializable, VolumeComponentMenu("TRP/ColorGrading")]
    public class ColorAdjustment : VolumeComponent, IPostProcessComponent
    {
		public ClampedFloatParameter contrast = new(0, -1, 1);
		public ColorParameter colorFilter = new(Color.white);
		public ClampedFloatParameter hue = new(0, -1, 1);
		public ClampedFloatParameter saturation = new(1, 0, 2);

		private static readonly Vector3 defaultSaturationFactor = new(0.2126729f, 0.7151522f, 0.0721750f);

		[Tooltip("ACES使用時には適用されません。")]
		public NoInterpVector3Parameter SaturationFactor = new(defaultSaturationFactor);

		public bool IsActive() =>
			0 < contrast.value ||
			colorFilter.value != Color.white ||
			0 < hue.value ||
			1 != saturation.value ||
			defaultSaturationFactor != SaturationFactor.value;
    }
}