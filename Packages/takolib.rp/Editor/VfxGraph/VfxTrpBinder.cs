#if HAS_VFX_GRAPH

using UnityEngine;
using UnityEditor.VFX;
using System;

namespace TakoLibEditor.Rp
{
	class VfxTrpBinder : VFXSRPBinder
	{
		public override string templatePath { get { return "Packages/takolib.rp/Editor/VfxGraph/Shaders"; } }
		public override string runtimePath { get { return "Packages/takolib.rp/Runtime/VfxGraph/Shaders"; } }

		public override string SRPAssetTypeStr { get { return "Trp"; } }

		public override Type SRPOutputDataType => null;

		public override bool IsShaderVFXCompatible(Shader shader)
		{
			return true;
		}
	}
}

#endif