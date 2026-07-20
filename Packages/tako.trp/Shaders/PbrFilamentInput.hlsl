#ifndef TRP_PBR_FILAMENT_INPUT_INCLUDED
#define TRP_PBR_FILAMENT_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

// R: Metallic、G: Ambient Occlusion、A: Smoothness。
// 既定のwhiteテクスチャでも各スカラー値だけで正しく動作する。
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _EmissionColor;
half _Cutoff;
half _Metallic;
half _Smoothness;
half _BumpScale;
half _OcclusionStrength;
half _SpecularIblStrength;
half _UseBurleyDiffuse;
half _VertexSway;
float _HexTilingRotationStrength;
half _HexTilingGain;
half _ShadowNormalDistortion;
half _SwayAmplitude;
half _SwayPeriodScale;
half3 _Padding1;
CBUFFER_END

// 頂点カラーRを根元から先端までの揺れウェイトとして使用する。
float3 ApplyPbrVertexSway(float3 positionOS, half vertexWeight)
{
    if (_VertexSway < 0.5h || vertexWeight <= 0.0h)
    {
        return positionOS;
    }

    float3 positionWS = TransformObjectToWorld(positionOS);
    float period = max((float)_SwayPeriodScale, 0.01);
    float spatialPhase = dot(positionWS.xz, float2(0.173, 0.219));
    float sway = sin(_Time.y * TWO_PI * rcp(period) + spatialPhase) * _SwayAmplitude * saturate(vertexWeight);

    // ワールド空間の一定方向へ動かし、オブジェクトの向きが異なっても風向きを揃える。
    positionWS.xz += float2(0.8, 0.6) * sway;
    return TransformWorldToObject(positionWS);
}

#endif
