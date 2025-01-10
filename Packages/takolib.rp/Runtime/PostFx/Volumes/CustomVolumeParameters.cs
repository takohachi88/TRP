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
}