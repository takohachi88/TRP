using UnityEngine;

namespace Trp
{
	public static class TrpConstants
	{
		public const string PATH_CREATE_MENU_POST_FX = "Rendering/Trp/PostFx/";

		public static class PassNames
		{
			public static readonly string SRP_DEFAULT_UNLIT = "SRPDefaultUnlit";
			public static readonly string LIT = "Lit";
		}

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