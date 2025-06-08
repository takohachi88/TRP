#if HAS_VFX_GRAPH

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace TrpEditor.VfxGraph
{
	/// <summary>
	/// MeshOutputコンテクストでシェーダーの拡張を行えるようにしたもの。
	/// VFXMeshOutputを継承すべきようにも思えるが、
	/// VFXMeshOutputのMeshCountとlodがprivateになっていることから以下の問題があり、継承ができない。
	/// ・VFXGraphのprivateプロパティは継承するとGUIに表示されない。
	/// ・継承先で同名のプロパティを継承することもできない。
	/// ・これらのプロパティはVFXMeshOutput内のロジックに関与している。
	/// このため、VFXMeshOutputをコピーするような実装になっている。
	/// </summary>
	[VFXInfo(name = "Output Particle|TRP Shader|Mesh", category = "#2Output Basic")]
	internal class VfxCustomShaderMeshOutput : VfxCustomShaderOutput, IVFXMultiMeshOutput
	{
		public override string name => "Output Particle".AppendLabel("TRP Shader", false) + "\nMesh";
		public override string codeGeneratorTemplate { get { return RenderPipeTemplate("CustomShaderMesh/VfxCustomShaderMesh"); } }
		public override VFXTaskType taskType { get { return VFXTaskType.ParticleMeshOutput; } }
		public override bool supportsUV { get { return GetOrRefreshShaderGraphObject() == null; } }
		public override bool implementsMotionVector { get { return false; } }
		public override CullMode defaultCullMode { get { return CullMode.Back; } }

		[VFXSetting, Range(1, 4), Tooltip("Specifies the number of different meshes (up to 4). Mesh per particle can be specified with the meshIndex attribute."), SerializeField]
		public uint MeshCount = 1;
		[VFXSetting, Tooltip("When enabled, screen space LOD is used to determine with meshIndex to use per particle."), SerializeField]
		private bool lod = false;

		public uint meshCount => HasStrips(true) ? 1 : MeshCount;

		public override VFXOutputUpdate.Features outputUpdateFeatures
		{
			get
			{
				VFXOutputUpdate.Features features = base.outputUpdateFeatures;
				if (!HasStrips(true)) // TODO make it compatible with strips
				{
					if (MeshCount > 1)
						features |= VFXOutputUpdate.Features.MultiMesh;
					if (lod)
						features |= VFXOutputUpdate.Features.LOD;
				}
				if (HasSorting() && VFXOutputUpdate.HasFeature(features, VFXOutputUpdate.Features.IndirectDraw) || needsOwnSort)
				{
					if (VFXSortingUtility.IsPerCamera(sortMode))
						features |= VFXOutputUpdate.Features.CameraSort;
					else
						features |= VFXOutputUpdate.Features.Sort;
				}
				return features;
			}
		}

		protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(IEnumerable<VFXNamedExpression> slotExpressions)
		{
			foreach (var exp in base.CollectGPUExpressions(slotExpressions))
				yield return exp;
			if (GetOrRefreshShaderGraphObject() == null)
				yield return slotExpressions.First(o => o.name == "mainTexture");
		}

		protected override IEnumerable<VFXPropertyWithValue> inputProperties
		{
			get
			{
				foreach (var property in base.inputProperties)
					yield return property;

				foreach (var property in VFXMultiMeshHelper.GetInputProperties(MeshCount, outputUpdateFeatures))
					yield return property;

				if (GetOrRefreshShaderGraphObject() == null)
					foreach (var property in optionalInputProperties)
						yield return property;
			}
		}

		protected override IEnumerable<string> filteredOutSettings
		{
			get
			{
				foreach (var s in base.filteredOutSettings)
					yield return s;

				yield return nameof(enableRayTracing);

				// TODO Add a experimental bool to setting attribute
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
				{
					yield return setting;
				}
				yield return nameof(enableRayTracing);
			}
		}


		protected IEnumerable<VFXPropertyWithValue> optionalInputProperties
		{
			get
			{
				yield return new VFXPropertyWithValue(new VFXProperty(GetFlipbookType(), "mainTexture", new TooltipAttribute("Specifies the base color (RGB) and opacity (A) of the particle.")), (usesFlipbook ? null : VFXResources.defaultResources.particleTexture));
			}
		}

		public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
		{
			var mapper = base.GetExpressionMapper(target);

			switch (target)
			{
				case VFXDeviceTarget.CPU:
					{
						foreach (var name in VFXMultiMeshHelper.GetCPUExpressionNames(MeshCount))
							mapper.AddExpression(inputSlots.First(s => s.name == name).GetExpression(), name, -1);
						break;
					}
				default:
					break;
			}

			return mapper;
		}

		internal override void GenerateErrors(VFXErrorReporter report)
		{
			base.GenerateErrors(report);
			var dataParticle = GetData() as VFXDataParticle;
			if (dataParticle != null && dataParticle.boundsMode != BoundsSettingMode.Manual)
				report.RegisterError("WarningBoundsComputation", VFXErrorType.Warning, $"Bounds computation have no sense of what the scale of the output mesh is," +
					$" so the resulted computed bounds can be too small or big" +
					$" Please use padding to mitigate this discrepancy.", this);
		}

		public override IEnumerable<VFXExpression> instancingSplitCPUExpressions
		{
			get
			{
				foreach (var exp in base.instancingSplitCPUExpressions)
					yield return exp;

				// Only single mesh, multi-mesh will be patched later
				if (meshCount == 1)
				{
					foreach (var name in VFXMultiMeshHelper.GetCPUExpressionNames(1))
					{
						var exp = inputSlots.First(s => s.name == name).GetExpression();
						if (exp != null && !exp.IsAny(VFXExpression.Flags.Constant))
							yield return exp;
					}
				}
			}
		}
	}
}

#endif