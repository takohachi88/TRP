#if HAS_VFX_GRAPH

using UnityEngine;
using UnityEditor.VFX;
using System;
using Trp;

namespace TrpEditor.VfxGraph
{
	class VfxTrpBinder : VFXSRPBinder
	{
		public override string templatePath { get { return "Packages/tako.trp/Editor/VfxGraph/Shaders"; } }
		public override string runtimePath { get { return "Packages/tako.trp/Runtime/VfxGraph/Shaders"; } }

		public override string SRPAssetTypeStr { get { return typeof(TrpAsset).Name; } }

		public override Type SRPOutputDataType => null;

		public override bool IsShaderVFXCompatible(Shader shader)
		{
			return true;
		}
	}
}

#endif