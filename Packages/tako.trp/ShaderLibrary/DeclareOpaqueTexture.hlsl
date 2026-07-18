#ifndef TRP_DECLARE_OPAQUE_TEXTURE_INCLUDED
#define TRP_DECLARE_OPAQUE_TEXTURE_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

TEXTURE2D(_CameraOpaqueTexture);

half3 SampleSceneOpaque(float2 uv, SAMPLER(samplerParam))
{
	return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, samplerParam, uv).rgb;
}

half3 SampleSceneOpaque(float2 uv)
{
	return SampleSceneOpaque(uv, sampler_LinearClamp);
}

half3 LoadSceneOpaque(uint2 coords)
{
	return LOAD_TEXTURE2D(_CameraOpaqueTexture, coords).rgb;
}

#endif