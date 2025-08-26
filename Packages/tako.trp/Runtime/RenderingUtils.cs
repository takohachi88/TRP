using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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

	public class RenderingUtils
	{
		private static int IdSrcBlend = Shader.PropertyToID("_CameraSrcBlend");
		private static int IdDstBlend = Shader.PropertyToID("_CameraDstBlend");

		private static Material _cameraBlitMaterial;
		public static Rect FullViewRect => new(0, 0, 1, 1);

		internal static void Initialize(Shader cameraBlitShader)
		{
			_cameraBlitMaterial = CoreUtils.CreateEngineMaterial(cameraBlitShader);
		}

		internal static void Dispose()
		{
			CoreUtils.Destroy(_cameraBlitMaterial);
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
		public static GraphicsFormat DepthFormat => GraphicsFormat.R32_SFloat;

		public static GraphicsFormat ColorFormat(bool useHdr, bool useAlpha)
			=> (useHdr, useAlpha) switch
			{
				(true, true) => GraphicsFormat.R16G16B16A16_UNorm,
				(true, false) => GraphicsFormat.B10G11R11_UFloatPack32,
				(false, true) => GraphicsFormat.R8G8B8A8_UNorm,
				(false, false) => SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8_UNorm, GraphicsFormatUsage.Render),
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
	}
}