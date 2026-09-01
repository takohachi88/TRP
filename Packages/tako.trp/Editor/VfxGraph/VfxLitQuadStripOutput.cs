#if HAS_VFX_GRAPH

using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// TRPのPBRライティングでTrail／Ribbonを描画するQuad Strip Output。
	/// Mask MapはR: Metallic、G: AO、A: Smoothnessとして扱う。
	/// </summary>
	[VFXHelpURL("Context-OutputPrimitive")]
	[VFXInfo(name = "Output ParticleStrip|TRP Lit|Quad", category = "#3Output Strip",
		synonyms = new[] { "Trail", "Ribbon" })]
	internal class VfxLitQuadStripOutput : VfxQuadStripOutput
	{
		public override string name => "Output ParticleStrip".AppendLabel("TRP Lit").AppendLabel("Quad");
		public override string codeGeneratorTemplate => RenderPipeTemplate("VFXParticleLitPlanarPrimitive");
		public override bool isLitShader => true;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField,
		 Tooltip("When enabled, samples a mask map. The red, green and alpha channels contain metallic, ambient occlusion and smoothness respectively.")]
		private bool useMaskMap;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField,
		 Tooltip("When enabled, samples a tangent-space normal map.")]
		private bool useNormalMap;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField,
		 Tooltip("When enabled, samples an emission map and multiplies it by Emission Color.")]
		private bool useEmissionMap;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField,
		 Tooltip("When enabled, bends normals across the strip width to create a rounder trail.")]
		private bool normalBending;

		public class LitInputProperties
		{
			[Range(0, 1), Tooltip("Controls the metallic value. When a Mask Map is used, this multiplies its red channel.")]
			public float metallic;

			[Range(0, 1), Tooltip("Controls the smoothness value. When a Mask Map is used, this multiplies its alpha channel.")]
			public float smoothness = 0.5f;

			[Range(0, 1), Tooltip("Controls the effect of the Mask Map ambient occlusion channel.")]
			public float occlusionStrength = 1.0f;
		}

		public class NormalInputProperties
		{
			[Range(-3, 3), Tooltip("Controls the strength of the tangent-space normal map.")]
			public float normalScale = 1.0f;
		}

		public class EmissionInputProperties
		{
			[ColorUsage(true, true), Tooltip("Multiplies the emission map color.")]
			public Color emissionColor = Color.black;
		}

		public class NormalBendingInputProperties
		{
			[Range(0, 1), Tooltip("Controls the curvature of the normals across the strip width.")]
			public float normalBendingFactor = 0.1f;
		}

		protected override bool hasAnyMap =>
			base.hasAnyMap || useMaskMap || useNormalMap || useEmissionMap;

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties)
					yield return property;

				foreach (var property in PropertiesFromType(typeof(LitInputProperties)))
					yield return property;

				if (useBaseColorMap != BaseColorMapMode.None)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "mainTexture",
							new TooltipAttribute("Specifies the base color (RGB) and opacity (A) of the trail.")),
						usesFlipbook ? null : VFXResources.defaultResources.particleTexture);
				}

				if (useMaskMap)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "maskMap",
							new TooltipAttribute("Mask Map: Metallic (R), Ambient Occlusion (G), Smoothness (A).")),
						usesFlipbook ? null : VFXResources.defaultResources.maskTexture);
				}

				if (useNormalMap)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "normalMap",
							new TooltipAttribute("Specifies a tangent-space normal map.")),
						usesFlipbook ? null : VFXResources.defaultResources.normalTexture);
					foreach (var property in PropertiesFromType(typeof(NormalInputProperties)))
						yield return property;
				}

				if (useEmissionMap)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "emissionMap",
							new TooltipAttribute("Specifies the emissive color map.")));
					foreach (var property in PropertiesFromType(typeof(EmissionInputProperties)))
						yield return property;
				}

				if (normalBending)
				{
					foreach (var property in PropertiesFromType(typeof(NormalBendingInputProperties)))
						yield return property;
				}
			}
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(
			IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var expression in base.CollectGPUExpressions(slotExpressions))
				yield return expression;

			yield return slotExpressions.First(o => o.name == nameof(LitInputProperties.metallic));
			yield return slotExpressions.First(o => o.name == nameof(LitInputProperties.smoothness));
			yield return slotExpressions.First(o => o.name == nameof(LitInputProperties.occlusionStrength));

			if (useBaseColorMap != BaseColorMapMode.None)
				yield return slotExpressions.First(o => o.name == "mainTexture");
			if (useMaskMap)
				yield return slotExpressions.First(o => o.name == "maskMap");
			if (useNormalMap)
			{
				yield return slotExpressions.First(o => o.name == "normalMap");
				yield return slotExpressions.First(o => o.name == nameof(NormalInputProperties.normalScale));
			}
			if (useEmissionMap)
			{
				yield return slotExpressions.First(o => o.name == "emissionMap");
				yield return slotExpressions.First(o => o.name == nameof(EmissionInputProperties.emissionColor));
			}
			if (normalBending)
			{
				yield return slotExpressions.First(o =>
					o.name == nameof(NormalBendingInputProperties.normalBendingFactor));
			}
		}

		public override IEnumerable<string> additionalDefines
		{
			get
			{
				foreach (var define in base.additionalDefines)
					yield return define;

				yield return "TRP_VFX_LIT";
				yield return "FORCE_NORMAL_VARYING";
				// Quad StripはCull Offで描画するため、カメラ側へ法線を向けて両面を同じ明るさにする。
				yield return "TRP_VFX_TWO_SIDED_LIGHTING";
				if (useMaskMap)
					yield return "TRP_VFX_USE_MASK_MAP";
				if (useNormalMap)
					yield return "USE_NORMAL_MAP";
				if (useEmissionMap)
					yield return "TRP_VFX_USE_EMISSION_MAP";
				if (normalBending)
					yield return "USE_NORMAL_BENDING";
			}
		}
	}
}

#endif
