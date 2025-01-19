#ifndef TRP_DECLARE_DEPTH_TEXTURE_INCLUDED
#define TRP_DECLARE_DEPTH_TEXTURE_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

TEXTURE2D_FLOAT(_CameraDepthTexture);

half SampleSceneDepth(float2 uv, SAMPLER(samplerParam))
{
	return SAMPLE_TEXTURE2D(_CameraDepthTexture, samplerParam, uv).r;
}

half SampleSceneDepth(float2 uv)
{
	return SampleSceneDepth(uv, sampler_LinearClamp);
}

half LoadSceneDepth(uint2 coords)
{
	return LOAD_TEXTURE2D(_CameraDepthTexture, coords).r;
}

#endif