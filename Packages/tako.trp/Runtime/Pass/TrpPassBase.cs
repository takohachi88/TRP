using UnityEngine;

namespace Trp
{
	/// <summary>
	/// レンダーパスの基底クラス。
	/// URPでいうところのScriptableRenderPass。
	/// </summary>
	public abstract class TrpPassBase : ScriptableObject
	{
		/// <summary>
		/// 実行タイミング。
		/// </summary>
		public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

		public abstract void RecordRenderGraph(ref PassParams passParams);
	}
}