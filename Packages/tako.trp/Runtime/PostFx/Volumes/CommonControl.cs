using System;
using UnityEngine.Rendering;

namespace Trp.PostFx
{
	/// <summary>
	/// VolumeComponent間でプロパティを共通で制御したい場合に用いる。
	/// 例えば中心座標を表すcenterというプロパティがVignetteやRadialBlurなど複数のVolumeComponentに存在するが、これらは共通で制御したいことがほとんどである。
	/// </summary>
	[Serializable, VolumeComponentMenu("TRP/CommonControl")]
	public class CommonControl : VolumeComponent, IPostProcessComponent
	{
		/// <summary>
		/// CommonCenterParameter型を制御。
		/// </summary>
		public Vector2Parameter center = new(new(0.5f, 0.5f));

		public bool IsActive() => center.overrideState;
	}
}