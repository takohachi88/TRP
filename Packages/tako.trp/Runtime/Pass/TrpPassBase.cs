using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	public ref struct PassParams
	{
		public RenderGraph RenderGraph { get; init; }
		public readonly Camera Camera { get; init; }
		public readonly TrpCameraData CameraData { get; init; }
		public readonly TextureDesc ColorDescriptor { get; init; }
		public readonly CameraTextures CameraTextures { get; init; }
		public readonly Vector2Int AttachmentSize { get; init; }
		public Vector2 AspectFit { get; internal set; }
		public Vector2 AspectFitRcp { get; internal set; }
		public readonly CameraClearFlags ClearFlags { get; init; }
		public readonly bool UseScaledRendering { get; init; }
		public readonly bool UseOpaqueTexture { get; init; }
		public readonly bool UseDepthTexture { get; init; }
		public readonly bool UseTransparentTexture { get; init; }
		public readonly float RenderScale { get; init; }
		public readonly bool UseHdr { get; init; }
		public readonly bool IsSceneViewOrPreview { get; init; }
		public readonly CullingResults CullingResults { get; init; }
		public readonly RenderingLayerMask RenderingLayerMask { get; init; }
		internal readonly int LutSize { get ; init; }
	}

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