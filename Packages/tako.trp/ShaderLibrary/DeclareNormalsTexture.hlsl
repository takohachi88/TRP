#ifndef TRP_DECLARE_NORMALS_TEXTURE_INCLUDED
#define TRP_DECLARE_NORMALS_TEXTURE_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

TEXTURE2D(_CameraNormalsTexture);

half3 SampleSceneNormal(float2 uv, SAMPLER(samplerParam))
{
	return SAMPLE_TEXTURE2D(_CameraNormalsTexture, samplerParam, uv).rgb;
}

half3 SampleSceneNormal(float2 uv)
{
	return SampleSceneNormal(uv, sampler_PointClamp);
}

half3 LoadSceneNormal(uint2 coords)
{
	return LOAD_TEXTURE2D(_CameraNormalsTexture, coords).rgb;
}

#endif