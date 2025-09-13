using UnityEngine;
using UnityEditor;
using Trp;
using TakoLibEditor.Common;
using UnityEngine.Rendering;

namespace TrpEditor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Light))]
	[SupportedOnRenderPipeline(typeof(TrpAsset))]
	public class TrpLightEditor : LightEditor
	{
		public override void OnInspectorGUI()
		{
			EditorGUILayout.LabelField("TRP Light", EditorStyles.boldLabel);
			TakoLibEditorUtility.DrawSeparator(5);

			if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
			{
				settings.DrawInnerAndOuterSpotAngle();
				settings.ApplyModifiedProperties();
			}

			base.OnInspectorGUI();
		}
	}
}
