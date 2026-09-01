#if HAS_VFX_GRAPH

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// TRPのPBRライティングでMesh Particleを描画するOutputコンテキスト。
	/// VFX Graph標準のMesh Outputと同じ描画・Multi Mesh機能を保ち、
	/// TRPのMask Map規約（R: Metallic、G: AO、A: Smoothness）を入力として公開する。
	/// </summary>
	[VFXHelpURL("Context-OutputParticleMesh")]
	[VFXInfo(name = "Output Particle|TRP Lit|Mesh", category = "#2Output Basic")]
	internal class VfxLitMeshOutput : VFXShaderGraphParticleOutput, IVFXMultiMeshOutput
	{
		public override string name => "Output Particle".AppendLabel("TRP Lit").AppendLabel("Mesh");
		public override string codeGeneratorTemplate => RenderPipeTemplate("VFXParticleLitMesh");
		public override VFXTaskType taskType => VFXTaskType.ParticleMeshOutput;
		public override bool supportsUV => true;
		public override bool implementsMotionVector => true;
		public override bool isLitShader => true;
		public override CullMode defaultCullMode => CullMode.Back;

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
		 Tooltip("When enabled, back faces invert their normals so that two-sided meshes receive correct lighting.")]
		private bool doubleSided;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Range(1, 4), SerializeField,
		 Tooltip("Specifies the number of different meshes (up to 4). Mesh per particle can be specified with the meshIndex attribute.")]
		private uint MeshCount = 1;

		[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField,
		 Tooltip("When enabled, screen space LOD is used to determine which meshIndex to use per particle.")]
		private bool lod;

		public uint meshCount => HasStrips(true) ? 1 : MeshCount;

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

		protected override bool hasAnyMap =>
			base.hasAnyMap || useMaskMap || useNormalMap || useEmissionMap;

		public override VFXOutputUpdate.Features outputUpdateFeatures
		{
			get
			{
				VFXOutputUpdate.Features features = base.outputUpdateFeatures;
				if (!HasStrips(true))
				{
					if (MeshCount > 1)
						features |= VFXOutputUpdate.Features.MultiMesh;
					if (lod)
						features |= VFXOutputUpdate.Features.LOD;
				}

				if (HasSorting() &&
				    (VFXOutputUpdate.HasFeature(features, VFXOutputUpdate.Features.IndirectDraw) || needsOwnSort))
				{
					features |= VFXSortingUtility.IsPerCamera(sortMode)
						? VFXOutputUpdate.Features.CameraSort
						: VFXOutputUpdate.Features.Sort;
				}

				return features;
			}
		}

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties)
					yield return property;

				foreach (var property in PropertiesFromType(nameof(LitInputProperties)))
					yield return property;

				foreach (var property in VFXMultiMeshHelper.GetInputProperties(MeshCount, outputUpdateFeatures))
					yield return property;

				if (useBaseColorMap != BaseColorMapMode.None)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "mainTexture",
							new TooltipAttribute("Specifies the base color (RGB) and opacity (A) of the particle.")),
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
					foreach (var property in PropertiesFromType(nameof(NormalInputProperties)))
						yield return property;
				}

				if (useEmissionMap)
				{
					yield return new VFXPropertyWithValue(
						new VFXProperty(GetFlipbookType(), "emissionMap",
							new TooltipAttribute("Specifies the emissive color map.")));
					foreach (var property in PropertiesFromType(nameof(EmissionInputProperties)))
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
		}

		public override IEnumerable<string> additionalDefines
		{
			get
			{
				foreach (var define in base.additionalDefines)
					yield return define;

				yield return "TRP_VFX_LIT";
				if (useMaskMap)
					yield return "TRP_VFX_USE_MASK_MAP";
				if (useNormalMap)
					yield return "USE_NORMAL_MAP";
				if (useEmissionMap)
					yield return "TRP_VFX_USE_EMISSION_MAP";
				if (doubleSided)
					yield return "USE_DOUBLE_SIDED";
			}
		}

		protected override IEnumerable<string> filteredOutSettings
		{
			get
			{
				foreach (var setting in base.filteredOutSettings)
					yield return setting;

				// Litのベースカラー合成は固定し、Shader GraphとRay Tracingはこのコンテキストでは扱わない。
				yield return nameof(colorMapping);
				yield return nameof(enableRayTracing);

				if (!VFXViewPreference.displayExperimentalOperator)
				{
					yield return nameof(MeshCount);
					yield return nameof(lod);
				}
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

		public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
		{
			VFXExpressionMapper mapper = base.GetExpressionMapper(target);
			if (target == VFXDeviceTarget.CPU)
			{
				foreach (var propertyName in VFXMultiMeshHelper.GetCPUExpressionNames(MeshCount))
				{
					mapper.AddExpression(inputSlots.First(s => s.name == propertyName).GetExpression(),
						propertyName, -1);
				}
			}
			return mapper;
		}

		public override sealed bool CanBeCompiled()
		{
			return VFXLibrary.currentSRPBinder is VfxTrpBinder && base.CanBeCompiled();
		}

		public override void OnEnable()
		{
			// このOutputはTRP組み込みPBR専用であり、Shader Graphを経由しない。
			shaderGraph = null;
			colorMapping = ColorMappingMode.Default;
			base.OnEnable();
			RestoreMissingBaseColorMapDefault();
		}

		private void RestoreMissingBaseColorMapDefault()
		{
			// Particle.shaderのBase Mapは白が既定であり、未設定のMapでAlbedoを暗くしない。
			// 初期化順によってVFXResourcesがまだ取得できず、nullのまま保存された既存Contextもここで補正する。
			if (usesFlipbook || useBaseColorMap == BaseColorMapMode.None)
				return;

			VFXSlot mainTextureSlot = inputSlots.FirstOrDefault(slot => slot.name == "mainTexture");
			if (mainTextureSlot == null || mainTextureSlot.value != null)
				return;

			Texture2D defaultTexture = VFXResources.defaultResources.particleTexture;
			if (defaultTexture == null)
			{
				// VFXManager.editorResourcesの準備前でも、Package内の既定Textureを直接解決できるようにする。
				defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
					"Packages/com.unity.visualeffectgraph/Textures/DefaultDot.tga");
			}

			if (defaultTexture != null)
				mainTextureSlot.value = defaultTexture;
		}

		internal override void GenerateErrors(VFXErrorReporter report)
		{
			base.GenerateErrors(report);
			if (GetData() is VFXDataParticle dataParticle && dataParticle.boundsMode != BoundsSettingMode.Manual)
			{
				report.RegisterError("WarningBoundsComputation", VFXErrorType.Warning,
					"Bounds computation cannot infer the scale of an output mesh. " +
					"Use bounds padding to avoid bounds that are too small or too large.", this);
			}
		}

		public override IEnumerable<VFXExpression> instancingSplitCPUExpressions
		{
			get
			{
				foreach (var expression in base.instancingSplitCPUExpressions)
					yield return expression;

				// Multi Meshは後段で分割されるため、ここでは単一Meshだけを対象にする。
				if (meshCount == 1)
				{
					foreach (var propertyName in VFXMultiMeshHelper.GetCPUExpressionNames(1))
					{
						VFXExpression expression = inputSlots.First(s => s.name == propertyName).GetExpression();
						if (expression != null && !expression.IsAny(VFXExpression.Flags.Constant))
							yield return expression;
					}
				}
			}
		}
	}
}

#endif
