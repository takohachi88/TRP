namespace Trp
{
	public enum ExecutionPhase
	{
		BeforeSetup,
		AfterSetup,
		AfterRenderingOpaques,
		AfterRenderingSkybox,
		BeforeRenderingTransparents,
		AfterRenderingTransparents,
		AfterRenderingPostProcessing,
		AfterRendering,
	}
}
