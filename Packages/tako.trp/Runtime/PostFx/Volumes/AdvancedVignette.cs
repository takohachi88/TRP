using System;
using UnityEngine.Rendering;
using UnityEngine;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/AdvancedVignette")]
	public class AdvancedVignette : VolumeComponent, IPostProcessComponent
	{
		public enum Mode
		{
			Alpha,
			Additive,
			Multiply,
			Nega,
		}

		public ClampedFloatParameter intensity = new(0, 0, 1);
		public NoInterpClampedFloatParameter smoothness = new(0, 0, 1);
		public BoolParameter FitAspect = new(false);
		public NoInterpColorParameter color = new(new Color(0, 0.4f, 0.4f));
		public CommonCenterParameter center = new(new(0.5f, 0.5f));
		public EnumParameter<Mode> blendMode = new(Mode.Multiply);

		public bool IsActive() => 0 < intensity.value;
	}
}