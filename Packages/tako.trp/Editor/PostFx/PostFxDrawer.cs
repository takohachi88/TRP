using UnityEngine;
using UnityEditor;
using Trp.PostFx;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace TrpEditor.PostFx
{
	/// <summary>
	/// 中心座標の共通制御。
	/// Vignette、RadialBlurなどで使われる。
	/// </summary>
	[VolumeParameterDrawer(typeof(CommonCenterParameter))]
	sealed class CommonCenterParameterDrawer : VolumeParameterDrawer
	{
		public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
		{
			SerializedProperty value = parameter.value;

			if (value.propertyType != SerializedPropertyType.Vector2) return false;

			CommonControl commonControl = VolumeManager.instance?.stack?.GetComponent<CommonControl>();
			CommonCenterParameter property = parameter.GetObjectRef<CommonCenterParameter>();

			//CommonControl.centerが有効かつこのcenter.overrideStateが無効なら、CommonControl.centerの値を表示する。
			if (commonControl && commonControl.center.overrideState && !property.overrideState)
			{
				EditorGUILayout.HelpBox($"{nameof(CommonControl)}.{nameof(commonControl.center)}により制御されています。", MessageType.Info);
				EditorGUILayout.Vector2Field(title, commonControl.center.value);
			}
			else
			{
				EditorGUILayout.PropertyField(value, title);
				value.vector2Value = value.vector2Value;
			}

			return true;
		}
	}
}