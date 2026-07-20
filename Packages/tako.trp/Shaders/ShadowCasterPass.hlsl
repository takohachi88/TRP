#ifndef TRP_SHADOW_CASTER_PASS_INCLUDED
#define TRP_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

#ifndef TRP_SAMPLE_BASE_MAP
    #define TRP_SAMPLE_BASE_MAP(uv) SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv)
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
#if defined(TRP_APPLY_VERTEX_DEFORMATION)
    half4 color : COLOR;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
#if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

#if defined(TRP_APPLY_VERTEX_DEFORMATION)
    input.positionOS.xyz = TRP_APPLY_VERTEX_DEFORMATION(input.positionOS.xyz, input.color);
#endif

#if defined(_ALPHATEST_ON)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    
    //shadow pancakingの回避。
#if UNITY_REVERSED_Z
	output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
        AlphaClip(TRP_SAMPLE_BASE_MAP(input.uv).a * _BaseColor.a, _Cutoff);
#endif

#if defined(LOD_FADE_CROSSFADE)
        //LODFadeCrossFade(input.positionCS);
#endif

    return 0;
}

#endif
