#ifndef TRP_DEPTH_ONLY_PASS_INCLUDED
#define TRP_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
};

struct Varyings
{
    #if defined(ALPHA_CLIP)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
};

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    #if defined(ALPHA_CLIP)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    #if defined(ALPHA_CLIP)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif
    return input.positionCS.z;
}
#endif