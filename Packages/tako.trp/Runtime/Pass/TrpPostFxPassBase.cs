using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// PostFxパスの基底クラス。
	/// </summary>
	public abstract class TrpPostFxPassBase : ScriptableObject
	{
		public abstract void RecordRenderGraph(ref PassParams passParams);
	}
}