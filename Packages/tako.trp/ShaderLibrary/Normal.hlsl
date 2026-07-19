#ifndef TRP_NORMAL
#define TRP_NORMAL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
{
    half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
    return UnpackNormalScale(n, scale);
}

#endif
