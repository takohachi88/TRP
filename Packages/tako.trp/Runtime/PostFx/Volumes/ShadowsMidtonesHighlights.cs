using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/Shadows, Midtones, Highlights"), DisplayInfo(name = "Shadows, Midtones, Highlights")]
	public class ShadowsMidtonesHighlights : VolumeComponent, IPostProcessComponent
	{
		public ColorParameter shadows = new(Color.white, false, false, true);
		public Vector2Parameter shadowsRange = new(new(0f, 0.3f));
		public ColorParameter midtones = new(Color.white, false, false, true);
		public Vector2Parameter highlightRange = new(new(0.5f, 1f));
		public ColorParameter highlights = new(Color.white, false, false, true);

		public bool IsActive() =>
			shadows.value != Color.white ||
			midtones.value != Color.white ||
			highlights.value != Color.white;
	}
}