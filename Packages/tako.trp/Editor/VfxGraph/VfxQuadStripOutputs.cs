#if HAS_VFX_GRAPH

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// Shader Graphを使わずにTRPでQuad Stripを描画するOutputコンテクストの共通実装。
	/// Stripの各Particleを連結し、Trail／Ribbonとして描画するための属性とUV設定を提供する。
	/// </summary>
	internal abstract class VfxQuadStripOutput : VFXShaderGraphParticleOutput
	{
		private const string WriteToPositionMessage =
			"Writing to Position attribute in a strip output can produce unexpected behavior";

		[VFXSetting, SerializeField,
		 Tooltip("Specifies the way the UVs are interpolated along the strip. They can either be stretched or repeated per segment.")]
		protected StripTilingMode tilingMode = StripTilingMode.Stretch;

		[VFXSetting, SerializeField, Tooltip("When enabled, uvs for the strips are swapped.")]
		protected bool swapUV;

		protected VfxQuadStripOutput() : base(true)
		{
		}

		public override VFXTaskType taskType => VFXTaskType.ParticleQuadOutput;
		public override bool supportsUV => true;
		public override bool implementsMotionVector => false;
		public override CullMode defaultCullMode => CullMode.Off;

		public class CustomUVInputProperties
		{
			[Tooltip("Specifies the texture coordinate value (u or v depending on swap UV being enabled) used along the strip.")]
			public float texCoord;
		}

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties)
					yield return property;

				if (tilingMode == StripTilingMode.Custom)
				{
					foreach (var property in PropertiesFromType(typeof(CustomUVInputProperties)))
						yield return property;
				}
			}
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(
			IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var expression in base.CollectGPUExpressions(slotExpressions))
				yield return expression;

			if (tilingMode == StripTilingMode.Custom)
				yield return slotExpressions.First(o => o.name == nameof(CustomUVInputProperties.texCoord));
		}

		public override IEnumerable<VFXAttributeInfo> attributes
		{
			get
			{
				yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Read);
				// このVFX GraphバージョンのStrip OutputはcolorModeを持たないため、
				// 標準のUnlit Quad Stripと同様にColor属性を常時読み取る。
				yield return new VFXAttributeInfo(VFXAttribute.Color, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.Alpha, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AxisX, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AxisY, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AxisZ, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AngleX, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AngleY, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.AngleZ, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.PivotX, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.PivotY, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.PivotZ, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.Size, VFXAttributeMode.Read);
				yield return new VFXAttributeInfo(VFXAttribute.ScaleY, VFXAttributeMode.Read);

				foreach (var attribute in flipbookAttributes)
					yield return attribute;
			}
		}

		public override IEnumerable<string> additionalDefines
		{
			get
			{
				foreach (var define in base.additionalDefines)
					yield return define;

				if (tilingMode == StripTilingMode.Stretch)
					yield return "VFX_STRIPS_UV_STRECHED";
				else if (tilingMode == StripTilingMode.RepeatPerSegment)
					yield return "VFX_STRIPS_UV_PER_SEGMENT";

				if (swapUV)
					yield return "VFX_STRIPS_SWAP_UV";

				yield return VFXPlanarPrimitiveHelper.GetShaderDefine(VFXPrimitiveType.Quad);
			}
		}

		protected override IEnumerable<string> filteredOutSettings
		{
			get
			{
				foreach (var setting in base.filteredOutSettings)
					yield return setting;

				// TRP組み込みシェーダー専用のため、Shader GraphとRay Tracingの設定は公開しない。
				yield return nameof(colorMapping);
				yield return nameof(enableRayTracing);
			}
		}

		protected override IEnumerable<string> untransferableSettings
		{
			get
			{
				foreach (var setting in base.untransferableSettings)
					yield return setting;
				yield return nameof(enableRayTracing);
			}
		}

		public override sealed bool CanBeCompiled()
		{
			return VFXLibrary.currentSRPBinder is VfxTrpBinder && base.CanBeCompiled();
		}

		public override void OnEnable()
		{
			// このOutputはTRP組み込みシェーダーのみを使い、Shader Graphを参照しない。
			shaderGraph = null;
			colorMapping = ColorMappingMode.Default;
			base.OnEnable();
			RestoreMissingBaseColorMapDefault();
		}

		private void RestoreMissingBaseColorMapDefault()
		{
			// Base Color Mapが有効なままTextureだけnullだと、Trailの色が照明前に暗くなる。
			// Lit／Unlitの両方で、標準VFX Outputと同じ既定Textureを復元する。
			if (usesFlipbook || useBaseColorMap == BaseColorMapMode.None)
				return;

			VFXSlot mainTextureSlot = inputSlots.FirstOrDefault(slot => slot.name == "mainTexture");
			if (mainTextureSlot == null || mainTextureSlot.value != null)
				return;

			Texture2D defaultTexture = VFXResources.defaultResources.particleTexture;
			if (defaultTexture == null)
			{
				// VFXManager.editorResourcesの初期化順に依存せず、既存Graphを補正できるようにする。
				defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
					"Packages/com.unity.visualeffectgraph/Textures/DefaultDot.tga");
			}

			if (defaultTexture != null)
				mainTextureSlot.value = defaultTexture;
		}

		internal override void GenerateErrors(VFXErrorReporter report)
		{
			base.GenerateErrors(report);
			if (GetAttributesInfos().Any(info =>
				    info.mode.HasFlag(VFXAttributeMode.Write) && info.attrib.Equals(VFXAttribute.Position)))
			{
				report.RegisterError("WritePositionInStrip", VFXErrorType.Warning,
					WriteToPositionMessage, this);
			}
		}
	}

	/// <summary>
	/// TRPのUnlitシェーダーでTrail／Ribbonを描画するQuad Strip Output。
	/// </summary>
	[VFXHelpURL("Context-OutputPrimitive")]
	[VFXInfo(name = "Output ParticleStrip|TRP Unlit|Quad", category = "#3Output Strip",
		synonyms = new[] { "Trail", "Ribbon" })]
	internal class VfxUnlitQuadStripOutput : VfxQuadStripOutput
	{
		public override string name => "Output ParticleStrip".AppendLabel("TRP Unlit").AppendLabel("Quad");
		public override string codeGeneratorTemplate => RenderPipeTemplate("VFXParticlePlanarPrimitive");

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties)
					yield return property;

				if (useBaseColorMap != BaseColorMapMode.None)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "mainTexture",
							new TooltipAttribute("Specifies the base color (RGB) and opacity (A) of the trail.")),
						usesFlipbook ? null : VFXResources.defaultResources.particleTexture);
				}
			}
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(
			IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var expression in base.CollectGPUExpressions(slotExpressions))
				yield return expression;

			if (useBaseColorMap != BaseColorMapMode.None)
				yield return slotExpressions.First(o => o.name == "mainTexture");
		}
	}

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
