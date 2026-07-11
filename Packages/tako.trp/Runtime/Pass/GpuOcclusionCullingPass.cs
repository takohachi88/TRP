using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// GPU Resident Drawerの遮蔽カリングをTRPのRenderGraphへ記録する。
	/// 深度の生成にはTRP既存のDepthNormalsOnlyパスを利用し、DepthOnlyパスは追加しない。
	/// </summary>
	internal sealed class GpuOcclusionCullingPass
	{
		/// <summary>
		/// 前フレームと当フレームの深度ピラミッドを組み合わせて遮蔽判定を更新する。
		/// </summary>
		/// <returns>DepthNormalsOnlyを遮蔽カリング用に描画した場合はtrue。</returns>
		internal bool RecordRenderGraph(ref PassParams passParams, DepthNormalsPass depthNormalsPass)
		{
			if (!GPUResidentDrawer.IsInstanceOcclusionCullingEnabled()) return false;

			// 1回目は前フレームの最終深度ピラミッドで全instanceを判定する。
			RecordInstanceOcclusionTest(passParams.RenderGraph, passParams.Camera, OcclusionTest.TestAll);
			depthNormalsPass.RecordGpuOcclusionPrepass(ref passParams, OcclusionTest.TestAll.GetBatchLayerMask(), true, false);
			UpdateOccluders(passParams.RenderGraph, passParams.Camera, passParams.AttachmentSize, passParams.CameraTextures.TextureDepth);

			// 前フレームでは隠れていたinstanceだけを当フレーム深度で再判定する。
			// 同じ深度・法線RTへ追記するため、最終ピラミッドは両方の描画結果を含む。
			RecordInstanceOcclusionTest(passParams.RenderGraph, passParams.Camera, OcclusionTest.TestCulled);
			depthNormalsPass.RecordGpuOcclusionPrepass(ref passParams, OcclusionTest.TestCulled.GetBatchLayerMask(), false, true);
			UpdateOccluders(passParams.RenderGraph, passParams.Camera, passParams.AttachmentSize, passParams.CameraTextures.TextureDepth);

			// GeometryPassが参照する最終の間接描画結果を確定する。
			RecordInstanceOcclusionTest(passParams.RenderGraph, passParams.Camera, OcclusionTest.TestAll);
			return true;
		}

		private static void RecordInstanceOcclusionTest(RenderGraph renderGraph, Camera camera, OcclusionTest occlusionTest)
		{
			Span<SubviewOcclusionTest> subviewOcclusionTests = stackalloc SubviewOcclusionTest[1]
			{
				new SubviewOcclusionTest
				{
					cullingSplitIndex = 0,
					occluderSubviewIndex = 0,
				},
			};
			GPUResidentDrawer.InstanceOcclusionTest(
				renderGraph,
				new OcclusionCullingSettings(camera.GetEntityId(), occlusionTest),
				subviewOcclusionTests);
		}

		private static void UpdateOccluders(RenderGraph renderGraph, Camera camera, Vector2Int depthSize, TextureHandle depthTexture)
		{
			Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
			Span<OccluderSubviewUpdate> subviewUpdates = stackalloc OccluderSubviewUpdate[1]
			{
				new OccluderSubviewUpdate(0)
				{
					depthSliceIndex = 0,
					depthOffset = Vector2Int.zero,
					viewMatrix = viewMatrix,
					invViewMatrix = viewMatrix.inverse,
					gpuProjMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true),
					viewOffsetWorldSpace = Vector3.zero,
				},
			};
			GPUResidentDrawer.UpdateInstanceOccluders(
				renderGraph,
				new OccluderParameters(camera.GetEntityId())
				{
					subviewCount = 1,
					depthTexture = depthTexture,
					depthSize = depthSize,
					depthIsArray = false,
				},
				subviewUpdates);
		}
	}
}
