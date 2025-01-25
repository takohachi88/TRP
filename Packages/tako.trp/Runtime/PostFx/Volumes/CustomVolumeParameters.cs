using System;
using UnityEngine.Rendering;
using UnityEngine;

namespace Trp.PostFx
{
	/// <summary>
	/// 中心座標を表す共通パラメータ。
	/// CommonProperties.centerにより制御される。
	/// </summary>
	[Serializable]
	public class CommonCenterParameter : VolumeParameter<Vector2>
	{
		public CommonCenterParameter(Vector2 value, bool overrideState = false) : base(value, overrideState) { }

		public override Vector2 value
		{
			get
			{
				CommonControl commonControl = VolumeManager.instance.stack.GetComponent<CommonControl>();
				return !overrideState && commonControl && commonControl.center.overrideState ? commonControl.center.value : m_Value;
			}
			set => m_Value = value;
		}
	}
}