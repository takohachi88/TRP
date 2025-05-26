#if HAS_VFX_GRAPH

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// PlanarPrimitiveでシェーダーの拡張を行えるようにしたもの。
	/// </summary>
	[VFXInfo(variantProvider = typeof(VfxPlanarPrimitiveOutputProvider))]
	internal class VfxCustomShaderPlanarPrimitiveOutput : VfxCustomShaderOutput
	{
		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField, Tooltip("Specifies what primitive type to use for this output. Triangle outputs have fewer vertices, octagons can be used to conform the geometry closer to the texture to avoid overdraw, and quads are a good middle ground.")]
		protected VFXPrimitiveType primitiveType = VFXPrimitiveType.Quad;

		public override string codeGeneratorTemplate => RenderPipeTemplate("CustomShaderPlanarPrimitive/VfxCustomShaderPlanarPrimitive");

		public override string name => "Output Particle".AppendLabel("TRP Shader") + $"\n{ObjectNames.NicifyVariableName(primitiveType.ToString())}";

		public override VFXTaskType taskType
		{
			get
			{
				return VFXPlanarPrimitiveHelper.GetTaskType(primitiveType);
			}
		}
		public override bool supportsUV { get { return GetOrRefreshShaderGraphObject() == null; } }
		public override bool implementsMotionVector { get { return true; } }

		public override IEnumerable<string> additionalDefines
		{
			get
			{
				foreach (var def in base.additionalDefines)
					yield return def;

				yield return VFXPlanarPrimitiveHelper.GetShaderDefine(primitiveType);
			}
		}

		protected IEnumerable<VFXPropertyWithValue> optionalInputProperties
		{
			get
			{
				yield return new VFXPropertyWithValue(new VFXProperty(GetFlipbookType(), "mainTexture", new TooltipAttribute("Specifies the base color (RGB) and opacity (A) of the particle.")), (usesFlipbook ? null : VFXResources.defaultResources.particleTexture));
			}
		}
		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				IEnumerable<VFXPropertyWithValue> properties = base.inputProperties;
				if (GetOrRefreshShaderGraphObject() == null)
					properties = properties.Concat(optionalInputProperties);

				if (primitiveType == VFXPrimitiveType.Octagon)
					properties = properties.Concat(PropertiesFromType(typeof(VFXPlanarPrimitiveHelper.OctagonInputProperties)));
				return properties;
			}
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var exp in base.CollectGPUExpressions(slotExpressions))
				yield return exp;
			if (GetOrRefreshShaderGraphObject() == null)
			{
				yield return slotExpressions.First(o => o.name == "mainTexture");
			}
			if (primitiveType == VFXPrimitiveType.Octagon)
				yield return slotExpressions.First(o => o.name == "cropFactor");
		}

		protected override IEnumerable<string> untransferableSettings
		{
			get
			{
				foreach (var setting in base.untransferableSettings)
				{
					yield return setting;
				}
				yield return "primitiveType";
			}
		}
	}

	internal class VfxPlanarPrimitiveOutputSubVariantProvider : VariantProvider
	{
		private VFXPrimitiveType _mainVariantType;

		public VfxPlanarPrimitiveOutputSubVariantProvider(VFXPrimitiveType type) => _mainVariantType = type;

		public override IEnumerable<Variant> GetVariants()
		{
			foreach (var primitive in Enum.GetValues(typeof(VFXPrimitiveType)).Cast<VFXPrimitiveType>())
			{
				if (primitive == _mainVariantType)
					continue;

				yield return new Variant(
					"Output Particle".AppendLabel("TRP Shader", false).AppendLabel(primitive.ToString(), false),
					null,
					typeof(VfxCustomShaderPlanarPrimitiveOutput),
					new[] { new KeyValuePair<string, object>("primitiveType", primitive) });
			}
		}
	}

	internal class VfxPlanarPrimitiveOutputProvider : VariantProvider
	{
		public override IEnumerable<Variant> GetVariants()
		{
			yield return new Variant(
				"Output Particle".AppendLabel("TRP Shader", false).AppendLabel(VFXPrimitiveType.Quad.ToString(), false),
				VFXLibraryStringHelper.Separator("Output Basic", 2),
				typeof(VfxCustomShaderPlanarPrimitiveOutput),
				new[] { new KeyValuePair<string, object>("primitiveType", VFXPrimitiveType.Quad) },
				() => new VFXPlanarPrimitiveOutputSubVariantProvider(VFXPrimitiveType.Quad));
		}
	}
}

#endif