#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TakoLibEditor.Rp
{
	public static class TrpEditorGuiLayout
	{
		public static LayerMask LayerMaskField(
			string label,
			LayerMask layerMask)
		{
			List<string> layers = new List<string>();
			List<int> layerNumbers = new List<int>();

			for (var i = 0; i < 32; ++i)
			{
				string layerName = LayerMask.LayerToName(i);
				if (!string.IsNullOrEmpty(layerName))
				{
					layers.Add(layerName);
					layerNumbers.Add(i);
				}
			}

			int maskWithoutEmpty = 0;
			for (var i = 0; i < layerNumbers.Count; ++i)
			{
				if (0 < ((1 << layerNumbers[i]) & layerMask.value))
					maskWithoutEmpty |= 1 << i;
			}

			maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
			int mask = 0;
			for (var i = 0; i < layerNumbers.Count; ++i)
			{
				if (0 < (maskWithoutEmpty & (1 << i)))
					mask |= 1 << layerNumbers[i];
			}
			layerMask.value = mask;

			return layerMask;
		}
	}
}
#endif