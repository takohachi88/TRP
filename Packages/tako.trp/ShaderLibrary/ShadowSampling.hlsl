#ifndef TRP_SHADOW_SAMPLING_INCLUDED
#define TRP_SHADOW_SAMPLING_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/LightingInput.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"

struct TrpShadowContext
{
    float3 positionWS;
    float3 positionWSdx;
    float3 positionWSdy;
    half3 normalWS;
    float2 positionPx;
    half dither;
    half directionalFade;
    half cascadeIndex;
};

// フラグメントの分岐やライトループの前で一度呼び、座標の微分を全ライトで共有する。
TrpShadowContext TrpPrepareShadows(float3 positionWS, half3 normalWS, float2 positionPx)
{
    TrpShadowContext context = (TrpShadowContext)0;
    context.positionWS = positionWS;
    context.positionWSdx = ddx(positionWS);
    context.positionWSdy = ddy(positionWS);
    context.normalWS = normalWS;
    context.positionPx = positionPx;
    context.dither = InterleavedGradientNoise(positionPx, 0);
    context.directionalFade = 1;
    if (_DirectionalLightCount > 0)
    {
        context.directionalFade = GetDirectionalShadowFade(positionWS);
        UNITY_BRANCH
        if (context.directionalFade < 1)
            context.cascadeIndex = ComputeCascadeIndex(positionWS, context.dither);
    }
    return context;
}

int TrpGetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// インデックス外、影なし、距離フェード完了時は1を返す。
half TrpSampleDirectionalShadow(TrpShadowContext context, int lightIndex)
{
    half visibility = 1;
    UNITY_BRANCH
    if (lightIndex >= 0 && lightIndex < _DirectionalLightCount && context.directionalFade < 1)
    {
        DirectionalLight light = GetDirectionalLight(lightIndex);
        UNITY_BRANCH
        if (light.attenuation > 0)
        {
            visibility = SampleDirectionalShadow(context.cascadeIndex, context.positionWS, context.normalWS,
                light.normalBias, light.shadowMapTileStartIndex, light.shadowFilter, light.shadowPcss,
                context.positionWSdx, context.positionWSdy, context.dither);
            visibility = lerp(visibility, 1.0h, context.directionalFade);
        }
    }
    return visibility;
}

// 手軽に最初のDirectional Lightの影だけを使う場合の入口。
half TrpSampleDirectionalShadow(float3 positionWS, half3 normalWS, float2 positionPx)
{
    return TrpSampleDirectionalShadow(TrpPrepareShadows(positionWS, normalWS, positionPx), 0);
}

struct TrpPunctualShadowList
{
    int first;
    int count;
};

// Forward+の候補を得る。ライトがないカメラではタイルバッファを参照しない。
TrpPunctualShadowList TrpGetPunctualShadowList(TrpShadowContext context)
{
    TrpPunctualShadowList lights = (TrpPunctualShadowList)0;
    UNITY_BRANCH
    if (_PunctualLightCount > 0)
    {
        ForwardPlusTile tile = GetForwardPlusTile(context.positionPx * _AttachmentSize.xy);
        lights.first = tile.GetFirstLightIndexInTile();
        lights.count = tile.GetLightCount();
    }
    return lights;
}

// slotは画素の候補リスト内の番号。lightIndexは照明にも使えるGPUライト番号。
int TrpGetPunctualLightIndex(TrpPunctualShadowList lights, int slot)
{
    int lightIndex = -1;
    UNITY_BRANCH
    if (slot >= 0 && slot < lights.count)
        lightIndex = _TileBuffer[lights.first + slot];
    return lightIndex;
}

half TrpSamplePunctualShadow(TrpShadowContext context, int lightIndex)
{
    half visibility = 1;
    UNITY_BRANCH
    if (lightIndex >= 0 && lightIndex < _PunctualLightCount)
    {
        PunctualLight light = GetPunctualLight(lightIndex);
        UNITY_BRANCH
        if (light.attenuation > 0)
        {
            float3 toLight = light.position - context.positionWS;
            float distanceSquared = dot(toLight, toLight);
            half3 direction = toLight * rsqrt(max(distanceSquared, 0.00001));
            // タイルの候補にはライトの範囲やスポットのコーン外の画素も含まれる。
            half spotFactor = dot(light.direction, direction) * light.spotAngles.x + light.spotAngles.y;
            UNITY_BRANCH
            if (distanceSquared * light.rangeInverseSquare < 1 && spotFactor > 0)
            {
                visibility = GetPunctualShadow(context.positionWS, context.positionWSdx, context.positionWSdy,
                    context.normalWS, light.position, light.direction, direction, light.type,
                    light.shadowMapTileStartIndex, light.shadowFilter, light.shadowPcss, context.dither);
            }
        }
    }
    return visibility;
}

#endif
