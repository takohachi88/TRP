using UnityEngine;

namespace Trp
{
	public static class TrpConstants
	{
		public const float RENDER_SCALE_MIN = 0.1f;
		public const float RENDER_SCALE_MAX = 2f;

		public const string PATH_CREATE_MENU_POST_FX = "Rendering/Trp/PostFx/";

		public static class ShaderIds
		{
			public static readonly int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
			public static readonly int CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
			public static readonly int CameraTransparentTexture = Shader.PropertyToID("_CameraTransparentTexture");
		
			internal static readonly int DepthAttachment = Shader.PropertyToID("_DepthAttachment");
			public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		}
	}
}