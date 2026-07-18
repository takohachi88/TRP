#ifndef TRP_DECLARE_TRANSPARENT_TEXTURE_INCLUDED
#define TRP_DECLARE_TRANSPARENT_TEXTURE_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

TEXTURE2D(_CameraTransparentTexture);

half3 SampleSceneTransparent(float2 uv, SAMPLER(samplerParam))
{
	return SAMPLE_TEXTURE2D(_CameraTransparentTexture, samplerParam, uv).rgb;
}

half3 SampleSceneTransparent(float2 uv)
{
	return SampleSceneTransparent(uv, sampler_LinearClamp);
}

half3 LoadSceneTransparent(uint2 coords)
{
	return LOAD_TEXTURE2D(_CameraTransparentTexture, coords).rgb;
}

#endif