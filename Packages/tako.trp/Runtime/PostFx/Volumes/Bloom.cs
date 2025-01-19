using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Bloom")]
	public class Bloom : VolumeComponent, IPostProcessComponent
	{
		public ClampedFloatParameter intensity = new(0, 0, 1);
		public bool IsActive() => 0 < intensity.value;
	}
}