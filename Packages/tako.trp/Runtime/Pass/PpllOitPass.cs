using System;
using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// Per-Pixel Linked List Order Independent Transparency.
	/// Gatherで透明フラグメントをGPU上の連結リストへ格納し、Resolveで深度順に並べて合成する。
	/// </summary>
	public sealed class PpllOitPass
	{
		// Nodeは「RGBA8、24bit深度+8bitサンプル番号、次Nodeのindex」の3 uint。
		private const int NodeStride = sizeof(uint) * 3;
		private const int ClearThreadCount = 256;

		private static readonly ProfilingSampler SamplerClear = ProfilingSampler.Create(nameof(PpllOitPass) + ".Clear", MarkerFlags.Default);
		private static readonly ProfilingSampler SamplerGather = ProfilingSampler.Create(nameof(PpllOitPass) + ".Gather", MarkerFlags.Default);
		private static readonly ProfilingSampler SamplerResolve = ProfilingSampler.Create(nameof(PpllOitPass) + ".Resolve", MarkerFlags.Default);
		private static readonly ShaderTagId IdPpllOit = new("PpllOit");
		private static readonly int IdHeads = Shader.PropertyToID("_PpllOitHeads");
		private static readonly int IdNodes = Shader.PropertyToID("_PpllOitNodes");
		private static readonly int IdMaxNodeCount = Shader.PropertyToID("_PpllOitMaxNodeCount");
		private static readonly int IdTextureSize = Shader.PropertyToID("_PpllOitTextureSize");
		private static readonly int IdPixelCount = Shader.PropertyToID("_PpllOitPixelCount");

		private readonly Material _resolveMaterial;
		private readonly ComputeShader _clearCompute;
		private readonly int _clearKernel;

		public static bool IsSupported =>
			SystemInfo.supportsComputeShaders &&
			SystemInfo.supportedRandomWriteTargetCount >= 3;

		public PpllOitPass(Shader resolveShader, ComputeShader clearCompute)
		{
			_resolveMaterial = CoreUtils.CreateEngineMaterial(resolveShader);
			_clearCompute = clearCompute;
			_clearKernel = clearCompute.FindKernel("ClearPpllOitHeads");
		}

		public void Dispose()
		{
			CoreUtils.Destroy(_resolveMaterial);
		}

		private sealed class PassData
		{
			public RendererListHandle RendererList;
			public BufferHandle Heads;
			public BufferHandle Nodes;
			public Material Material;
			public ComputeShader Compute;
			public int Kernel;
			public int PixelCount;
			public int DispatchCount;
			public int MaxNodeCount;
			public Vector4 TextureSize;
		}

		public void RecordRenderGraph(ref PassParams passParams, int averageFragmentsPerPixel)
		{
			RenderGraph renderGraph = passParams.RenderGraph;
			int width = Mathf.Max(1, passParams.AttachmentSize.x);
			int height = Mathf.Max(1, passParams.AttachmentSize.y);
			int pixelCount = width * height;

			// AverageFragmentsPerPixelは総容量の見積もり値。局所的にはこの数を超えて格納できる。
			long requestedNodeCount = (long)pixelCount * Mathf.Max(1, averageFragmentsPerPixel);
			long deviceNodeLimit = Math.Max(1L, SystemInfo.maxGraphicsBufferSize / NodeStride - 1L);
			int maxNodeCount = (int)Math.Min(requestedNodeCount, Math.Min(deviceNodeLimit, int.MaxValue - 1L));
			Vector4 textureSize = new(width, height, 1f / width, 1f / height);

			RendererListHandle rendererList = renderGraph.CreateRendererList(
				new RendererListDesc(IdPpllOit, passParams.CullingResults, passParams.Camera)
				{
					layerMask = passParams.CommonSettings.TransparentLayerMask,
					sortingCriteria = SortingCriteria.None,
					renderQueueRange = RenderQueueRange.transparent,
					renderingLayerMask = (uint)passParams.RenderingLayerMask,
				});

			// Headsはピクセルごとの先頭Node index。0をリスト終端として使う。
			BufferHandle heads = renderGraph.CreateBuffer(new BufferDesc(pixelCount, sizeof(uint), GraphicsBuffer.Target.Raw)
			{
				name = "PPLL OIT Heads",
			});
			// index 0は終端値として予約するため、実容量より1要素多く確保する。
			BufferHandle nodes = renderGraph.CreateBuffer(new BufferDesc(
				maxNodeCount + 1,
				NodeStride,
				GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter)
			{
				name = "PPLL OIT Fragment and Link Buffer",
			});

			// 前フレームの連結リストを参照しないようHeadsを0で初期化し、Node counterを1へ戻す。
			using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(SamplerClear.name, out PassData passData, SamplerClear))
			{
				passData.Heads = heads;
				passData.Nodes = nodes;
				passData.Compute = _clearCompute;
				passData.Kernel = _clearKernel;
				passData.PixelCount = pixelCount;
				passData.DispatchCount = Mathf.CeilToInt(pixelCount / (float)ClearThreadCount);

				builder.UseBuffer(heads, AccessFlags.Write);
				builder.UseBuffer(nodes, AccessFlags.Write);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc<PassData>(static (data, context) =>
				{
					context.cmd.SetBufferCounterValue(data.Nodes, 1u);
					context.cmd.SetComputeIntParam(data.Compute, IdPixelCount, data.PixelCount);
					context.cmd.SetComputeBufferParam(data.Compute, data.Kernel, IdHeads, data.Heads);
					context.cmd.DispatchCompute(data.Compute, data.Kernel, data.DispatchCount, 1, 1);
				});
			}

			TextureHandle cameraColor = passParams.CameraTextures.AttachmentColor;
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerGather.name, out PassData passData, SamplerGather))
			{
				passData.RendererList = rendererList;
				passData.MaxNodeCount = maxNodeCount + 1;
				passData.TextureSize = textureSize;

				builder.UseRendererList(rendererList);
				// Pixel-shader UAV slots start after the active color target on D3D11.
				builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
				builder.UseBufferRandomAccess(nodes, 1, true, AccessFlags.ReadWrite);
				builder.UseBufferRandomAccess(heads, 2, AccessFlags.ReadWrite);
				builder.SetRenderAttachmentDepth(passParams.CameraTextures.AttachmentDepth, AccessFlags.Read);
				builder.AllowPassCulling(true);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc<PassData>(static (data, context) =>
				{
					context.cmd.SetGlobalInteger(IdMaxNodeCount, data.MaxNodeCount);
					context.cmd.SetGlobalVector(IdTextureSize, data.TextureSize);
					context.cmd.DrawRendererList(data.RendererList);
				});
			}

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(SamplerResolve.name, out PassData passData, SamplerResolve))
			{
				passData.Heads = heads;
				passData.Nodes = nodes;
				passData.Material = _resolveMaterial;
				passData.TextureSize = textureSize;

				builder.UseBuffer(heads, AccessFlags.Read);
				builder.UseBuffer(nodes, AccessFlags.Read);
				builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
				builder.AllowPassCulling(true);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc<PassData>(static (data, context) =>
				{
					context.cmd.SetGlobalBuffer(IdHeads, data.Heads);
					context.cmd.SetGlobalBuffer(IdNodes, data.Nodes);
					context.cmd.SetGlobalVector(IdTextureSize, data.TextureSize);
					Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
				});
			}
		}
	}
}
