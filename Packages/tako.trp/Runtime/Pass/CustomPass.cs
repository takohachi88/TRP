using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// TRP外から処理を追加したいときに用いる。
	/// Cameraに登録する。
	/// </summary>
	[Serializable]
	public class CustomPass
	{
		[SerializeField] private bool _enabled = true;
		[SerializeField] private ExecutionPhase _phase;
		public virtual bool Enabled => _enabled;
		public ExecutionPhase Phase => _phase;
		public virtual void Execute(ref PassParams passParams)
		{

		}
	}

	public ref struct PassParams
	{
		public RenderGraph RenderGraph { get; init; }
		public readonly Camera Camera { get; init; }
		public readonly TrpCameraData CameraData { get; init; }
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
		public bool UseAlpha { get; internal set; }
		public readonly bool UsePostFx { get; init; }
		public readonly bool IsSceneViewOrPreview { get; init; }
		public readonly CullingResults CullingResults { get; init; }
		public readonly RenderingLayerMask RenderingLayerMask { get; init; }
		internal readonly int LutSize { get; init; }
		public readonly bool TargetIsGameRenderTexture { get; init; }
		/// <summary>
		/// backbufferに描画する最初のGameカメラであるかどうか。
		/// </summary>
		public readonly bool IsFirstToBackbuffer { get; init; }
		/// <summary>
		/// backbufferに描画する最後のGameカメラであるかどうか。
		/// </summary>
		public readonly bool IsLastToBackbuffer { get; init; }
		/// <summary>
		/// TRPにおいてもっとも最初に処理されるカメラかどうか。
		/// </summary>
		public readonly bool IsFirstCamera { get; init; }
	}
}