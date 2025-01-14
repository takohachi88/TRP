using System;
using UnityEngine.Rendering;
using UnityEngine;

namespace TakoLib.Rp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Radial Blur")]
	public class RadialBlur : VolumeComponent, IPostProcessComponent
	{
		public ClampedFloatParameter intensity = new(0, 0, 1);
		public Vector2Parameter center = new(new Vector2(0.5f, 0.5f));

		[Header("Blur")]
		public NoInterpClampedFloatParameter blurIntensity = new(0.5f, 0, 1);
		public NoInterpClampedIntParameter sampleCount = new(4, 2, 15);
		public BoolParameter dither = new BoolParameter(true);

		[Header("Noise")]
		public NoInterpClampedFloatParameter noiseIntensity = new(0, 0, 1);
		public TextureParameter noiseGradientTexture = new(null);
		public NoInterpMinFloatParameter noiseTiling = new(1, Mathf.Epsilon);

		[Header("Chromatic Aberration")]
		public ClampedFloatParameter chromaticAberrationIntensity = new(0, 0, 1);
		public NoInterpClampedFloatParameter chromaticAberrationLimit = new(0.1f, 0, 1);

		public bool IsActive() => 0 < intensity.value;
	}
}