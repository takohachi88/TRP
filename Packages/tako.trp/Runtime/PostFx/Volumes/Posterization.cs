using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
    [Serializable, VolumeComponentMenu("TRP/Posterization")]
    public class Posterization : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new(0, 0, 1);
        public MinIntParameter toneCount = new(3, 1);

        public bool IsActive() => 0 < intensity.value;
    }
}