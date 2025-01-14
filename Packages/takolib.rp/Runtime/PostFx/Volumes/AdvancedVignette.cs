using System;
using UnityEngine.Rendering;
using UnityEngine;

namespace TakoLib.Rp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/AdvancedVignette")]
	public class AdvancedVignette : VolumeComponent, IPostProcessComponent
	{
		public ClampedFloatParameter intensity = new(0, 0, 1);
		public NoInterpClampedFloatParameter smoothness = new(0, 0, 1);
		public BoolParameter FitAspect = new(false);
		public NoInterpColorParameter color = new(new Color(0, 0.4f, 0.4f));
		public NoInterpVector2Parameter center = new(new Vector2(0.5f, 0.5f));
		public BlendModeParameter blendMode = new(AdvancedVignetteBlendMode.Multiply);

		public bool IsActive() => 0 < intensity.value;
	}
}