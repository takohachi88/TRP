using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
    [Serializable, VolumeComponentMenu("TRP/Nega")]
    public class Nega : VolumeComponent, IPostProcessComponent
    {
        public MinFloatParameter intensity = new(0, 0);

        public bool IsActive() => 0 < intensity.value;
    }
}