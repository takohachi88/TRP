using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
    [Serializable, VolumeComponentMenu("TRP/Mosaic")]
    public class Mosaic : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new(0, 0, 1);

        public NoInterpMinIntParameter cellDensity = new(20, 1);

        public bool IsActive() => 0 < intensity.value;
    }
}