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
		// TrailもOpaqueかつ明示的に有効な場合だけShadowCasterとして登録する。
		public override bool hasShadowCasting => isBlendModeOpaque && castShadows;
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
				// ShadowCasterはOpaqueだけを対象にし、Transparentでは設定自体を表示しない。
				if (!isBlendModeOpaque)
					yield return nameof(castShadows);
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
}

#endif
