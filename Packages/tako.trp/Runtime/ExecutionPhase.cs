using UnityEngine;

namespace Trp
{
	public enum ExecutionPhase
	{
		AfterSetup,
		AfterRenderingOpaques,
		AfterRenderingSkybox,
		AfterRenderingTransparents,
		AfterRenderingPostProcessing,
		AfterRendering,
	}
}
