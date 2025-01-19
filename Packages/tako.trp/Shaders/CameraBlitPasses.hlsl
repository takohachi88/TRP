#ifndef TRP_CAMERA_BLIT_PASSES
#define TRP_CAMERA_BLIT_PASSES

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"


half4 CopyPassFragment(Varyings input) : SV_TARGET
{
	return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, input.uv, 0);
}

half CopyDepthPassFragment(Varyings input) : SV_DEPTH
{
	return SAMPLE_DEPTH_TEXTURE_LOD(_BlitTexture, sampler_PointClamp, input.uv, 0);
}


#endif