using System;
using UnityEngine.Rendering;

namespace TakoLib.Rp.PostFx
{
	[Serializable, VolumeComponentMenu("TRP/ToneMapping")]
	public class ToneMapping : VolumeComponent, IPostProcessComponent
	{
		public ToneMappingModeParameter mode = new(ToneMappingMode.None);
		public bool IsActive() => mode != ToneMappingMode.None;
	}

	public enum ToneMappingMode
	{
		None,
		Neutral,
		Reinhard,
		Aces,
	}

	[Serializable]
	public sealed class ToneMappingModeParameter : VolumeParameter<ToneMappingMode>
	{
		public ToneMappingModeParameter(ToneMappingMode value, bool overrideState = false) : base(value, overrideState) { }
	}
}