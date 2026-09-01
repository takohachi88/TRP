#if HAS_VFX_GRAPH

using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
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
}

#endif
