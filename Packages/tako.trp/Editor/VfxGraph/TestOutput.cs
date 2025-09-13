using UnityEditor.VFX;
using UnityEngine;

[VFXInfo(name = "Output Particle/Test/Mesh", category = "#2Output Basic")]
internal class VfxTestOutput : VFXAbstractParticleOutput
{
	public override string name => "Output Particle".AppendLabel("Test") + "\nMesh";

	[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
	private float _inspectorOnly;

	[VFXSetting(VFXSettingAttribute.VisibleFlags.InGraph)]
	private float _graphOnly;

	[VFXSetting(VFXSettingAttribute.VisibleFlags.Default), SerializeField]
	private float _graphAndInspector;


	[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Range(1, 4), Tooltip("Specifies the number of different meshes (up to 4). Mesh per particle can be specified with the meshIndex attribute."), SerializeField]
	private uint MeshCount = 1;
	[VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Tooltip("When enabled, screen space LOD is used to determine with meshIndex to use per particle."), SerializeField]
	private bool lod = false;
}
/*internal class VfxTestMeshOutput : VFXMeshOutput
{
	public override string name => "Output Particle".AppendLabel("Test") + "\nMesh";
}*/