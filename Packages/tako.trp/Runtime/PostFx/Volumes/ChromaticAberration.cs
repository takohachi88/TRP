using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/ChromaticAberration"), SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class ChromaticAberration : VolumeComponent, IPostProcessComponent
	{
		public enum Mode
		{
			Radial,
			Direction,
		}

		public ClampedFloatParameter intensity = new(0, 0, 1);
		public EnumParameter<Mode> mode = new(Mode.Radial);
		public CommonCenterParameter center = new(new(0.5f, 0.5f));
		public NoInterpClampedFloatParameter direction = new(0, 0, 360);
		public NoInterpClampedFloatParameter limit = new(0.1f, 0, 1);
		public NoInterpClampedIntParameter sampleCount = new(3, 3, 9);
		public BoolParameter dither = new(true);
		public Texture2DParameter chromaLut = new(null);
		public Texture2DParameter intensityMap = new(null);

		public bool IsActive() => 0 < intensity.value;
	}
}