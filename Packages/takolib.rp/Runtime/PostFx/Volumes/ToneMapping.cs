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
}