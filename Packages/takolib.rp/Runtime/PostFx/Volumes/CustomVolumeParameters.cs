using System;
using UnityEngine.Rendering;

namespace TakoLib.Rp.PostFx
{
    public enum AdvancedVignetteBlendMode
    {
        Alpha,
        Additive,
        Multiply,
        Nega,
    }

    [Serializable]
    public sealed class BlendModeParameter : VolumeParameter<AdvancedVignetteBlendMode>
    {
        public BlendModeParameter(AdvancedVignetteBlendMode value, bool overrideState = false) : base(value, overrideState) { }
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