using TakoLib.Common.Extensions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Trp
{
	/// <summary>
	/// BlitterのShaderのPassの番号と紐づく。
	/// </summary>
	public enum CopyPassMode
	{
		Color,
		Depth,
	};

	public static class RenderingUtils
	{
		private static int IdSrcBlend = Shader.PropertyToID("_CameraSrcBlend");
		private static int IdDstBlend = Shader.PropertyToID("_CameraDstBlend");

		private static readonly GlobalKeyword KeywordOutputDepth = GlobalKeyword.Create("_OUTPUT_DEPTH");
		private static readonly GlobalKeyword KeywordDepthMsaa2 = GlobalKeyword.Create("_DEPTH_MSAA_2");
		private static readonly GlobalKeyword KeywordDepthMsaa4 = GlobalKeyword.Create("_DEPTH_MSAA_4");
		private static readonly GlobalKeyword KeywordDepthMsaa8 = GlobalKeyword.Create("_DEPTH_MSAA_8");
		private static Material _cameraBlitMaterial;
		private static Material _copyDepthMaterial;

		public static Rect FullViewRect => new(0, 0, 1, 1);

		internal static void Initialize()
		{
			TrpResources resources = GraphicsSettings.GetRenderPipelineSettings<TrpResources>();
			_cameraBlitMaterial = CoreUtils.CreateEngineMaterial(resources.CameraBlitShader);
			_copyDepthMaterial = CoreUtils.CreateEngineMaterial(resources.CopyDepthShader);
		}

		internal static void Dispose()
		{
			CoreUtils.Destroy(_cameraBlitMaterial);
			CoreUtils.Destroy(_copyDepthMaterial);
		}

		public static void SetKeywords(this Material material, LocalKeyword[] keywords, int index)
		{
			for (int i = 0; i < keywords.Length; i++)
			{
				if (i == index) material.EnableKeyword(keywords[i]);
				else material.DisableKeyword(keywords[i]);
			}
		}


		internal static Vector4 GetFinalBlitScaleBias(RTHandle source, RTHandle destination, Camera camera)
		{
			Vector2 viewportScale = source.useScaling ? source.rtHandleProperties.rtHandleScale : Vector2.one;
			bool yflip = IsRenderTargetProjectionMatrixFlipped(destination, camera);
			return !yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : viewportScale;
		}

		public static bool IsRenderTargetProjectionMatrixFlipped(RTHandle color, Camera camera)
		{
			if (!SystemInfo.graphicsUVStartsAtTop) return true;
			return camera.targetTexture != null || IsHandleYFlipped(color, camera);
		}

		public static bool IsHandleYFlipped(RTHandle handle, Camera camera)
		{
			if (!SystemInfo.graphicsUVStartsAtTop) return true;

			if (camera.cameraType is (CameraType.SceneView or CameraType.Preview)) return true;

			RenderTargetIdentifier handleId = new(handle.nameID, 0, CubemapFace.Unknown, 0);
			bool isBackbuffer = handleId == BuiltinRenderTextureType.CameraTarget || handleId == BuiltinRenderTextureType.Depth;

			return !isBackbuffer;
		}

		public static GraphicsFormat DepthStencilFormat => GraphicsFormat.D32_SFloat_S8_UInt;

		public static GraphicsFormat ColorFormat(bool useHdr, bool useAlpha)
			=> (useHdr, useAlpha) switch
			{
				(true, true) => GraphicsFormat.R16G16B16A16_UNorm,
				(true, false) => GraphicsFormat.B10G11R11_UFloatPack32,
				(false, true) => GraphicsFormat.R8G8B8A8_UNorm,
				(false, false) => GraphicsFormat.R8G8B8A8_UNorm,
			};

		public static void BlitToCamera(
			RasterCommandBuffer cmd,
			RTHandle src,
			RTHandle dst,
			Camera camera,
			BlendMode blendSrc = BlendMode.One,
			BlendMode blendDst = BlendMode.Zero,
			Rect? viewPort = null,
			RenderBufferLoadAction loadAction = RenderBufferLoadAction.DontCare,
			RenderBufferStoreAction storeAction = RenderBufferStoreAction.Store)
		{
			cmd.SetGlobalFloat(IdSrcBlend, (int)blendSrc);
			cmd.SetGlobalFloat(IdDstBlend, (int)blendDst);

			if (viewPort.HasValue) cmd.SetViewport(viewPort.Value);

			Blitter.BlitTexture(cmd, src, GetFinalBlitScaleBias(src, dst, camera), _cameraBlitMaterial, (int)CopyPassMode.Color);
			//元に戻す。
			cmd.SetGlobalFloat(IdSrcBlend, 1);
			cmd.SetGlobalFloat(IdSrcBlend, 0);
		}
		public static void Blit(
			RasterCommandBuffer cmd,
			RTHandle src,
			RTHandle dst,
			Camera camera,
			Rect? viewPort = null,
			RenderBufferLoadAction loadAction = RenderBufferLoadAction.DontCare,
			RenderBufferStoreAction storeAction = RenderBufferStoreAction.Store)
		{
			if (viewPort.HasValue) cmd.SetViewport(viewPort.Value);

			Blitter.BlitTexture(cmd, src, GetFinalBlitScaleBias(src, dst, camera), _cameraBlitMaterial, (int)CopyPassMode.Color);
		}

		public static void SetResolusion(int width, int height, FullScreenMode fullScreenMode)
		{
			Screen.SetResolution(width, height, fullScreenMode);
			RtHandlePool.Instance.Dispose();
		}

		private class RasterPassData
		{
			public TextureHandle Src;
			public TextureHandle Dst;
			public Material Material;
			public int PassIndex;
			public MSAASamples Msaa;
		}

		/// <summary>
		/// RenderGraphに単純なBlitを行うパスを追加する。
		/// RenderGraph.AddBlitPassメソッドはUnsafeパスとなりpass mergingできないという問題があるため、Rasterパス版を用意。
		/// </summary>
		/// <param name="renderGraph"></param>
		/// <param name="src"></param>
		/// <param name="dst"></param>
		/// <param name="material"></param>
		/// <param name="passIndex"></param>
		/// <param name="passName"></param>
		/// <returns></returns>
		public static IRasterRenderGraphBuilder AddBlitPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst, Material material, int passIndex, string passName = "BlitPass", AccessFlags dstAccess = AccessFlags.WriteAll)
		{
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out RasterPassData passData);
			passData.Src = src;
			passData.Material = material;
			passData.PassIndex = passIndex;
			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, dstAccess);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<RasterPassData>(static (passData, context) => Blitter.BlitTexture(context.cmd, passData.Src, Vector2.one, passData.Material, passData.PassIndex));
			return builder;
		}

		/// <summary>
		/// RenderGraphに単純なBlitを行うパスを追加する。
		/// RenderGraph.AddBlitPassメソッドはUnsafeパスとなりpass mergingできないという問題があるため、Rasterパス版を用意。
		/// </summary>
		/// <param name="renderGraph"></param>
		/// <param name="src"></param>
		/// <param name="dst"></param>
		/// <param name="passName"></param>
		/// <returns></returns>
		public static IRasterRenderGraphBuilder AddBlitPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst, bool linear, string passName = "BlitPass", AccessFlags dstAccess = AccessFlags.WriteAll)
		{
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out RasterPassData passData);
			passData.Src = src;
			passData.Material = Blitter.GetBlitMaterial(TextureDimension.Tex2D);
			passData.PassIndex = linear.ToInt();
			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachment(dst, 0, dstAccess);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<RasterPassData>(static (passData, context) => Blitter.BlitTexture(context.cmd, passData.Src, Vector2.one, passData.Material, passData.PassIndex));
			return builder;
		}

		/// <summary>
		/// RenderGraphに単純なDepthのBlitを行うパスを追加する。
		/// RenderGraph.AddBlitPassメソッドはUnsafeパスとなりpass mergingできないという問題があるため、Rasterパス版を用意。
		/// </summary>
		/// <param name="renderGraph"></param>
		/// <param name="src"></param>
		/// <param name="dst"></param>
		/// <param name="passName"></param>
		/// <returns></returns>
		public static IRasterRenderGraphBuilder AddBlitDepthPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst, string passName = "BlitPass", AccessFlags dstAccess = AccessFlags.WriteAll)
		{
			using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out RasterPassData passData);
			passData.Src = src;
			passData.Dst = dst;
			passData.Material = Blitter.GetBlitMaterial(TextureDimension.Tex2D);
			passData.Msaa = src.GetDescriptor(renderGraph).msaaSamples;
			builder.UseTexture(src, AccessFlags.Read);
			builder.SetRenderAttachmentDepth(dst, dstAccess);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);
			builder.SetRenderFunc<RasterPassData>(static (passData, context) =>
			{
				context.cmd.SetKeyword(KeywordOutputDepth, true);
				SetDepthMsaa(context.cmd, passData.Msaa);
				_copyDepthMaterial.SetTexture(TrpConstants.ShaderIds.DepthAttachment, passData.Src);
				_copyDepthMaterial.SetFloat(TrpConstants.ShaderIds.ZWrite, 1);
				Blitter.BlitTexture(context.cmd, passData.Src, Vector2.one, _copyDepthMaterial, 0);
			});
			return builder;
		}

		public static void SetDepthMsaa(IBaseCommandBuffer cmd, MSAASamples msaa)
		{
			cmd.SetKeyword(KeywordDepthMsaa2, msaa == MSAASamples.MSAA2x);
			cmd.SetKeyword(KeywordDepthMsaa4, msaa == MSAASamples.MSAA4x);
			cmd.SetKeyword(KeywordDepthMsaa8, msaa == MSAASamples.MSAA8x);
		}
	}
}
