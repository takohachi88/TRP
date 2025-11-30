#ifndef TRP_SHADOW_INCLUDED
#define TRP_SHADOW_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

//ditherでcasacade同士をフェードする。
float CascadeFade(float distanceSqrs, float radiusSqr, float cascadeRange, float fadeRange, half dither)
{
    return step(saturate((distanceSqrs - radiusSqr) * cascadeRange * fadeRange), dither);
}

//cascadeの番号を取得する処理の最適化。（分岐を回避。）
//URPから移植。
half ComputeCascadeIndex(float3 positionWS, half dither)
{
    float3 fromCenter0 = positionWS - _CullingSphere0.xyz;
    float3 fromCenter1 = positionWS - _CullingSphere1.xyz;
    float3 fromCenter2 = positionWS - _CullingSphere2.xyz;
    float3 fromCenter3 = positionWS - _CullingSphere3.xyz;
    float4 distancesSqrs = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distancesSqrs < _CullingSphereRadiusSqrs); //内側から順に、(1, 1, 1, 1)→(0, 1, 1, 1)→(0, 0, 1, 1)→(0, 0, 0, 1)→(0, 0, 0, 0)となる。
    weights.yzw = saturate(weights.yzw - weights.xyz); //内側から順に、(1, 0, 0, 0)→(0, 1, 0, 0)→(0, 0, 1, 0)→(0, 0, 0, 1)→(0, 0, 0, 0)となる。

    half index = min(half(4.0) - dot(weights, half4(4, 3, 2, 1)), MAX_CASCADE_COUNT - 1);
    index += CascadeFade(
        distancesSqrs[index],
        _CullingSphereRadiusSqrs[index],
        _CullingSphereRanges[index],
        _ShadowParams1.x,
        dither);
    return min(index, MAX_CASCADE_COUNT - 1);
}

//directional shadowの範囲外のフェードアウトの設定。
half GetDirectionalShadowFade(float3 positionWS, half shadow)
{
    float3 cameraToPixel = positionWS - _WorldSpaceCameraPos;
    half distanceFade = smoothstep(_ShadowParams1.z - _ShadowParams1.y, _ShadowParams1.z, dot(cameraToPixel, cameraToPixel));
    shadow = lerp(shadow, 1, distanceFade);
    return shadow;
}

half GetDirectionalShadow(half cascadeIndex, float3 positionWS, half3 normalWS, half normalBias, int mapTileStartIndex)
{
    const half cascadeCount = _ShadowParams1.w;
    float3 positionSTS = mul(_WorldToDirectionalShadows[mapTileStartIndex + cascadeIndex], float4(positionWS + normalWS * normalBias, 1.0)).xyz;
    
    #define SAMPLE_SHADOW(offset) SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(positionSTS.xy + offset, positionSTS.z))
    half shadow = 0;
    float2 texelSize = _DirectionalShadowMap_TexelSize.xy;
    shadow += SAMPLE_SHADOW(0);
    shadow += SAMPLE_SHADOW(float2(1, 1) * texelSize);
    shadow += SAMPLE_SHADOW(float2(1, -1) * texelSize);
    shadow += SAMPLE_SHADOW(float2(-1, 1) * texelSize);
    shadow += SAMPLE_SHADOW(float2(-1, -1) * texelSize);
    shadow *= 0.2;
    shadow = GetDirectionalShadowFade(positionWS, shadow);
    return shadow;
}

#endif
