using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
    [Serializable, VolumeComponentMenu("TRP/ChannelMixer")]
    public class ChannelMixer : VolumeComponent, IPostProcessComponent
    {
		public Vector3Parameter red = new(Vector3.right);
		public Vector3Parameter green = new(Vector3.up);
		public Vector3Parameter blue = new(Vector3.forward);
		public bool IsActive() => 
			red.value != Vector3.right||
			green.value != Vector3.up ||
			blue.value != Vector3.forward;
	}
}