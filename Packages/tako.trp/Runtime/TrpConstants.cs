using UnityEngine;

namespace Trp
{
	public static class TrpConstants
	{
		public const string PATH_CREATE_MENU = "Rendering/TRP/";
		public const string PATH_CREATE_MENU_POST_FX = PATH_CREATE_MENU + "PostFx/";

		public static class PassNames
		{
			public static readonly string SRP_DEFAULT_UNLIT = "SRPDefaultUnlit";
			public static readonly string LIT = "Lit";
			public static readonly string OUTLINE = "Outline";
		}

		public static class ShaderIds
		{
			public static readonly int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
			public static readonly int CameraNormalsTexture = Shader.PropertyToID("_CameraNormalsTexture");
			public static readonly int CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
			public static readonly int CameraTransparentTexture = Shader.PropertyToID("_CameraTransparentTexture");
		
			internal static readonly int DepthAttachment = Shader.PropertyToID("_DepthAttachment");
			public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		}
	}
}