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

// 黄金角で配置した16点を事前計算し、実行時のsqrtとsincosを避ける。
static const float2 shadowDiskOffsets[16] =
{
    float2(0.17677670, 0.00000000),
    float2(-0.22577219, 0.20682582),
    float2(0.03455805, -0.39377118),
    float2(0.28457122, 0.37117276),
    float2(-0.52222319, -0.09237393),
    float2(0.49469539, -0.31468471),
    float2(-0.16546593, 0.61552500),
    float2(-0.31556147, -0.60759440),
    float2(0.68464216, 0.25003022),
    float2(-0.71225609, 0.29400896),
    float2(0.34335450, -0.73372862),
    float2(0.25373024, 0.80893199),
    float2(-0.76474589, -0.44318588),
    float2(0.89713398, -0.19723239),
    float2(-0.54750691, 0.77877223),
    float2(-0.12648677, -0.97608970),
};

half GetDirectionalShadow(half cascadeIndex, float3 positionWS, half3 normalWS, half normalBias, int mapTileStartIndex, float2 filter)
{
    const half cascadeCount = _ShadowParams1.w;
    float3 positionSTS = mul(_WorldToDirectionalShadows[mapTileStartIndex + cascadeIndex], float4(positionWS + normalWS * normalBias, 1.0)).xyz;
    
    // 半テクセル内側へ制限し、アトラスの隣接タイルを読まないようにする。
    uint tileIndex = (uint)(mapTileStartIndex + (int)cascadeIndex);
    uint split = (uint)_DirectionalShadowAtlasSplit;
    float scale = rcp((float)split);
    float2 origin = float2(tileIndex % split, tileIndex / split) * scale;
    float2 border = _DirectionalShadowMap_TexelSize.xy * 0.5;
    float2 uv = clamp(positionSTS.xy, origin + border, origin + scale - border);
    half shadow = 0;
    if (filter.x <= 0)
    {
        shadow = SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_PointClampCompare, float3(uv, positionSTS.z));
    }
    else
    {
        int quality = clamp((int)filter.y, 0, 2);
        float uvRadius = filter.x * _DirectionalShadowMap_TexelSize.x * _DirectionalShadowFilterScales[tileIndex].x;
        if (quality == 0)
        {
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                float2 offset = shadowDiskOffsets[i] * 2.0 * uvRadius;
                uv = clamp(positionSTS.xy + offset, origin + border, origin + scale - border);
                shadow += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(uv, positionSTS.z));
            }
            shadow *= 0.25;
        }
        else if (quality == 1)
        {
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                float2 offset = shadowDiskOffsets[i] * 1.41421356 * uvRadius;
                uv = clamp(positionSTS.xy + offset, origin + border, origin + scale - border);
                shadow += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(uv, positionSTS.z));
            }
            shadow *= 0.125;
        }
        else
        {
            [unroll]
            for (int i = 0; i < 16; i++)
            {
                float2 offset = shadowDiskOffsets[i] * uvRadius;
                uv = clamp(positionSTS.xy + offset, origin + border, origin + scale - border);
                shadow += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(uv, positionSTS.z));
            }
            shadow *= 0.0625;
        }
    }
    shadow = GetDirectionalShadowFade(positionWS, shadow);
    return shadow;
}


static const half3 pointShadowPlanes[6] =
{
    float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};

// 投影後のUV変化に対する受光面深度の変化量を求める。
// 広いPCFでも各サンプル位置に対応する深度で比較し、傾斜面の自己遮蔽を抑える。
float2 PunctualReceiverPlaneDepthGradient(float3 positionSTSdx, float3 positionSTSdy)
{
    float determinant = positionSTSdx.x * positionSTSdy.y - positionSTSdx.y * positionSTSdy.x;
    float determinantRcp = rcp(max(abs(determinant), 0.000001));
    determinantRcp *= determinant < 0.0 ? -1.0 : 1.0;
    return float2(
        positionSTSdy.y * positionSTSdx.z - positionSTSdx.y * positionSTSdy.z,
        positionSTSdx.x * positionSTSdy.z - positionSTSdy.x * positionSTSdx.z) * determinantRcp;
}

half SamplePunctualShadow(float4 positionSTS, float2 offset, float2 depthGradient, float3 bounds)
{
    positionSTS.xy += offset;
    positionSTS.z += dot(depthGradient, offset);
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);//端のほうで隣のタイルが見えてしまう問題の防止。
    return SAMPLE_TEXTURE2D_SHADOW(_PunctualShadowMap, sampler_LinearClampCompare, positionSTS);
}

half GetPunctualShadow(
    float3 positionWS,
    float3 positionWSdx,
    float3 positionWSdy,
    half3 normalWS,
    float3 lightPositionWS,
    float3 spotDirectionWS,
    half3 directionWS,
    int lightType,
    int index,
    float2 filter)
{
    float3 lightPlane = spotDirectionWS;
    if (lightType == LIGHT_TYPE_POINT)
    {
        uint faceOffset = min((uint)CubeMapFaceID(-directionWS), 5u);
        index += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    
    PunctualShadowTileBuffer shadowTile = _PunctualShadowTileBuffer[index];
    
    float3 surfaceToLight = lightPositionWS - positionWS;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = normalWS * distanceToLightPlane * shadowTile.tileData.w;
    float4 positionSTS = mul(shadowTile.worldToShadow,	float4(positionWS + normalBias, 1.0));
    float4 positionSTSdx = mul(shadowTile.worldToShadow, float4(positionWSdx, 0.0));
    float4 positionSTSdy = mul(shadowTile.worldToShadow, float4(positionWSdy, 0.0));
    float clipWRcp = rcp(max(abs(positionSTS.w), 0.00001));
    positionSTS.xyz *= clipWRcp;//透視投影なのでwで割る。
    // ddx/ddy自体は可変回数のライトループ外で取得し、ここでは商の微分を解析的に求める。
    // これにより勾配命令を含む外側ループの強制展開を避ける。
    positionSTSdx.xyz = (positionSTSdx.xyz - positionSTS.xyz * positionSTSdx.w) * clipWRcp;
    positionSTSdy.xyz = (positionSTSdy.xyz - positionSTS.xyz * positionSTSdy.w) * clipWRcp;
    float3 bounds = shadowTile.tileData.xyz;

    // Hardまたは半径0では単一の深度比較を行う。
    half shadow = 0;
    if (filter.x <= 0)
    {
        positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
        shadow = SAMPLE_TEXTURE2D_SHADOW(_PunctualShadowMap, sampler_PointClampCompare, positionSTS);
    }
    else
    {
        int quality = clamp((int)filter.y, 0, 2);
        float uvRadius = filter.x * _PunctualShadowMap_TexelSize.x * shadowTile.filterData.x * clipWRcp;
        float2 depthGradient = PunctualReceiverPlaneDepthGradient(positionSTSdx.xyz, positionSTSdy.xyz);
        if (quality == 0)
        {
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, shadowDiskOffsets[i] * 2.0 * uvRadius, depthGradient, bounds);
            }
            shadow *= 0.25;
        }
        else if (quality == 1)
        {
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, shadowDiskOffsets[i] * 1.41421356 * uvRadius, depthGradient, bounds);
            }
            shadow *= 0.125;
        }
        else
        {
            [unroll]
            for (int i = 0; i < 16; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, shadowDiskOffsets[i] * uvRadius, depthGradient, bounds);
            }
            shadow *= 0.0625;
        }
    }
    return shadow;
}

#endif
