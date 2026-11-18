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

    // シーン切替でカスケード数が減っても、有効なタイルの範囲で参照する。
    half lastCascade = clamp(half(_ShadowParams1.w) - 1.0h, 0.0h, half(MAX_CASCADE_COUNT - 1));
    half index = min(half(4.0) - dot(weights, half4(4, 3, 2, 1)), lastCascade);
    index += CascadeFade(
        distancesSqrs[index],
        _CullingSphereRadiusSqrs[index],
        _CullingSphereRanges[index],
        _ShadowParams1.x,
        dither);
    return min(index, lastCascade);
}

//directional shadowの範囲外のフェードアウトの設定。
half GetDirectionalShadowFade(float3 positionWS)
{
    float3 cameraToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceSquared = dot(cameraToPixel, cameraToPixel);
    // 最大距離が0でもフェード区間の幅0で除算しない。
    if (distanceSquared >= _ShadowParams1.z) return 1;
    return smoothstep(_ShadowParams1.z - _ShadowParams1.y, _ShadowParams1.z, distanceSquared);
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

// Cascade切り替え用に既に計算した画素ノイズを共有し、追加のノイズ生成を避ける。
// フレーム番号を使わないため、静止画面で回転角が時間変化しない。
float2 GetShadowSampleRotation(float enabled, half dither)
{
    float2 rotation = float2(1, 0);
    UNITY_BRANCH
    if (enabled > 0.5)
    {
        float angle = (float)dither * TWO_PI;
        float sine = 0, cosine = 1;
        sincos(angle, sine, cosine);
        rotation = float2(cosine, sine);
    }
    return rotation;
}

float2 GetShadowDiskOffset(int index, float2 scaledRotation)
{
    float2 offset = shadowDiskOffsets[index];
    // 回転と半径をまとめた基底を使い、サンプル中は分岐を行わない。
    return offset.x * scaledRotation + offset.y * float2(-scaledRotation.y, scaledRotation.x);
}

// 投影UVに対する受光面の深度勾配。探索とフィルターの両方で自己遮蔽を抑える。
float2 PunctualReceiverPlaneDepthGradient(float3 positionSTSdx, float3 positionSTSdy, float minDeterminant = 0.000001);

// Low/Medium/Highの既存品質を探索数にも使用し、追加サンプル数を一定に制限する。
int PcssSampleCount(float quality)
{
    return (int)(4u << (uint)clamp((int)quality, 0, 2));
}

float PcssDiskScale(int count)
{
    return count == 4 ? 2.0 : (count == 8 ? 1.41421356 : 1.0);
}

float ShadowDepthSeparation(float receiver, float blocker)
{
#if UNITY_REVERSED_Z
    return blocker - receiver;
#else
    return receiver - blocker;
#endif
}

half SampleDirectionalPcss(float3 positionSTS, float2 depthGradient, float2 minUv, float2 maxUv,
    float2 scales, float4 pcss, float quality, float2 sampleRotation)
{
    // 探索幅・半影幅はワールド単位。Cascadeが変わっても幅の単位を維持する。
    int count = PcssSampleCount(quality);
    float diskScale = PcssDiskScale(count);
    float searchRadius = min(pcss.x * pcss.z, pcss.y) * scales.x;
    float2 searchRotation = sampleRotation * (diskScale * searchRadius);
    float separationSum = 0;
    float blockerCount = 0;
    [loop]
    for (int i = 0; i < count; i++)
    {
        float2 uv = clamp(positionSTS.xy + GetShadowDiskOffset(i, searchRotation), minUv, maxUv);
        float receiver = positionSTS.z + dot(depthGradient, uv - positionSTS.xy);
        float depth = SAMPLE_TEXTURE2D_LOD(_DirectionalShadowMap, sampler_PointClamp, uv, 0).r;
        float separation = ShadowDepthSeparation(receiver, depth) * scales.y;
        if (separation > 0.0001)
        {
            separationSum += separation;
            blockerCount += 1;
        }
    }
    // 遮蔽物のない画素では比較サンプルを省略する。
    half result = 1;
    UNITY_BRANCH
    if (blockerCount > 0)
    {
        float separation = min(separationSum * rcp(blockerCount), pcss.z);
        float radius = min(pcss.x * separation, pcss.y) * scales.x;
        float2 filterRotation = sampleRotation * (diskScale * radius);
        half shadow = 0;
        [loop]
        for (int j = 0; j < count; j++)
        {
            float2 uv = clamp(positionSTS.xy + GetShadowDiskOffset(j, filterRotation), minUv, maxUv);
            float receiver = positionSTS.z + dot(depthGradient, uv - positionSTS.xy);
            shadow += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(uv, receiver));
        }
        result = shadow * rcp((float)count);
    }
    return result;
}

half SampleDirectionalShadow(half cascadeIndex, float3 positionWS, half3 normalWS, half normalBias,
    int mapTileStartIndex, float4 filter, float4 pcss, float3 positionWSdx, float3 positionWSdy, half dither)
{
    float3 positionSTS = mul(_WorldToDirectionalShadows[mapTileStartIndex + cascadeIndex], float4(positionWS + normalWS * normalBias, 1.0)).xyz;
    
    // 半テクセル内側へ制限し、アトラスの隣接タイルを読まないようにする。
    uint tileIndex = (uint)(mapTileStartIndex + (int)cascadeIndex);
    uint split = (uint)_DirectionalShadowAtlasSplit;
    float scale = rcp((float)split);
    float2 origin = float2(tileIndex % split, tileIndex / split) * scale;
    float2 border = _DirectionalShadowMap_TexelSize.xy * 0.5;
    float2 uv = clamp(positionSTS.xy, origin + border, origin + scale - border);
    // Hard／光源サイズ0ではディザリングの回転角も計算しない。
    bool softFilter = pcss.w > 0.5 ? (pcss.x > 0 && pcss.y > 0) : filter.x > 0;
    float2 sampleRotation = GetShadowSampleRotation(softFilter ? filter.z : 0, dither);
    half shadow = 0;
    // ライト単位の明示的分岐により、無効時は深度探索も勾配計算も行わない。
    UNITY_BRANCH
    if (pcss.w > 0.5 && pcss.x > 0 && pcss.y > 0)
    {
        float3 dx = mul(_WorldToDirectionalShadows[tileIndex], float4(positionWSdx, 0)).xyz;
        float3 dy = mul(_WorldToDirectionalShadows[tileIndex], float4(positionWSdy, 0)).xyz;
        float2 gradient = PunctualReceiverPlaneDepthGradient(dx, dy, 0.000000000001);
        shadow = SampleDirectionalPcss(positionSTS, gradient, origin + border, origin + scale - border,
            _DirectionalShadowFilterScales[tileIndex].yz, pcss, filter.y, sampleRotation);
    }
    else if (filter.x <= 0 || pcss.w > 0.5)
    {
        shadow = SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_PointClampCompare, float3(uv, positionSTS.z));
    }
    else
    {
        int quality = clamp((int)filter.y, 0, 2);
        float uvRadius = filter.x * _DirectionalShadowMap_TexelSize.x * _DirectionalShadowFilterScales[tileIndex].x;
        float2 filterRotation = sampleRotation * (PcssDiskScale(PcssSampleCount(filter.y)) * uvRadius);
        if (quality == 0)
        {
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                float2 offset = GetShadowDiskOffset(i, filterRotation);
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
                float2 offset = GetShadowDiskOffset(i, filterRotation);
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
                float2 offset = GetShadowDiskOffset(i, filterRotation);
                uv = clamp(positionSTS.xy + offset, origin + border, origin + scale - border);
                shadow += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowMap, sampler_LinearClampCompare, float3(uv, positionSTS.z));
            }
            shadow *= 0.0625;
        }
    }
    return shadow;
}


// 独自シェーダーから呼ぶ場合も距離フェードと範囲外の省略を適用する。
half GetDirectionalShadow(half cascadeIndex, float3 positionWS, half3 normalWS, half normalBias,
    int mapTileStartIndex, float4 filter, float4 pcss, float3 positionWSdx, float3 positionWSdy, half dither)
{
    half shadowFade = GetDirectionalShadowFade(positionWS);
    half shadow = 1;
    UNITY_BRANCH
    if (shadowFade < 1)
    {
        shadow = SampleDirectionalShadow(cascadeIndex, positionWS, normalWS, normalBias,
            mapTileStartIndex, filter, pcss, positionWSdx, positionWSdy, dither);
        shadow = lerp(shadow, 1.0h, shadowFade);
    }
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
float2 PunctualReceiverPlaneDepthGradient(float3 positionSTSdx, float3 positionSTSdy, float minDeterminant)
{
    float determinant = positionSTSdx.x * positionSTSdy.y - positionSTSdx.y * positionSTSdy.x;
    // PCSSでは小さなUV微分でも平面勾配を保つ。既存PCFは従来の下限を使用する。
    float determinantRcp = rcp(max(abs(determinant), minDeterminant));
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

// 透視投影の深度をライトの投影面に対する距離へ戻す。深度処理にはfloatを使用する。
float PunctualShadowDistance(float depth, float4 filterData)
{
    return abs(filterData.z) * rcp(max(abs(depth - filterData.y), 0.0000001));
}

half SamplePunctualPcss(float4 positionSTS, float2 gradient, float3 bounds, float4 filterData,
    float4 pcss, float quality, float2 sampleRotation)
{
    int count = PcssSampleCount(quality);
    float diskScale = PcssDiskScale(count);
    float receiverDistance = PunctualShadowDistance(positionSTS.z, filterData);
    float distanceRcp = rcp(max(receiverDistance, 0.0001));
    float maxDistance = min(pcss.z, receiverDistance * 0.9);
    float searchWidth = pcss.x * maxDistance * rcp(max(receiverDistance - maxDistance, 0.0001));
    float uvScale = filterData.w * distanceRcp;
    float searchRadius = min(searchWidth, pcss.y) * uvScale;
    float2 searchRotation = sampleRotation * (diskScale * searchRadius);
    float blockerDistanceSum = 0;
    float separationSum = 0;
    float blockerCount = 0;
    [loop]
    for (int i = 0; i < count; i++)
    {
        float2 uv = clamp(positionSTS.xy + GetShadowDiskOffset(i, searchRotation), bounds.xy, bounds.xy + bounds.z);
        float receiver = positionSTS.z + dot(gradient, uv - positionSTS.xy);
        float depth = SAMPLE_TEXTURE2D_LOD(_PunctualShadowMap, sampler_PointClamp, uv, 0).r;
        if (ShadowDepthSeparation(receiver, depth) > 0)
        {
            float distance = PunctualShadowDistance(depth, filterData);
            float separation = PunctualShadowDistance(receiver, filterData) - distance;
            if (separation > max(0.0001, receiverDistance * 0.00001))
            {
                blockerDistanceSum += distance;
                separationSum += separation;
                blockerCount += 1;
            }
        }
    }
    half result = 1;
    UNITY_BRANCH
    if (blockerCount > 0)
    {
        float blockerRcp = rcp(blockerCount);
        float separation = min(separationSum * blockerRcp, pcss.z);
        float blockerDistance = max(blockerDistanceSum * blockerRcp, 0.0001);
        float width = min(pcss.x * separation * rcp(blockerDistance), pcss.y);
        float2 filterRotation = sampleRotation * (diskScale * width * uvScale);
        half shadow = 0;
        [loop]
        for (int j = 0; j < count; j++)
        {
            float2 uv = clamp(positionSTS.xy + GetShadowDiskOffset(j, filterRotation), bounds.xy, bounds.xy + bounds.z);
            float receiver = positionSTS.z + dot(gradient, uv - positionSTS.xy);
            shadow += SAMPLE_TEXTURE2D_SHADOW(_PunctualShadowMap, sampler_LinearClampCompare, float3(uv, receiver));
        }
        result = shadow * rcp((float)count);
    }
    return result;
}

// PBR専用。無効時は従来のスカラー減衰を返し、Tintの色計算を実行しない。
half3 GetPbrShadowColor(half shadow, half4 tint)
{
    half3 color = shadow;
    UNITY_BRANCH
    if (tint.w > 0.5h)
    {
        UNITY_BRANCH
        if (tint.w > 1.5h)
        {
            // HDRPと同じ三次式で、完全な影を暗く保ちながら中間領域を着色する。
            half shadow3 = shadow * shadow * shadow;
            color = shadow3 * (1.0h - tint.rgb) + shadow * tint.rgb;
        }
        else color = lerp(tint.rgb, half3(1, 1, 1), shadow);
    }
    return color;
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
    float4 filter,
    float4 pcss,
    half dither)
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
    bool softFilter = pcss.w > 0.5 ? (pcss.x > 0 && pcss.y > 0) : filter.x > 0;
    float2 sampleRotation = GetShadowSampleRotation(softFilter ? filter.z : 0, dither);
    half shadow = 0;
    UNITY_BRANCH
    if (pcss.w > 0.5 && pcss.x > 0 && pcss.y > 0)
    {
        float2 gradient = PunctualReceiverPlaneDepthGradient(positionSTSdx.xyz, positionSTSdy.xyz, 0.000000000001);
        shadow = SamplePunctualPcss(positionSTS, gradient, bounds, shadowTile.filterData, pcss, filter.y, sampleRotation);
    }
    else if (filter.x <= 0 || pcss.w > 0.5)
    {
        positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
        shadow = SAMPLE_TEXTURE2D_SHADOW(_PunctualShadowMap, sampler_PointClampCompare, positionSTS);
    }
    else
    {
        int quality = clamp((int)filter.y, 0, 2);
        float uvRadius = filter.x * _PunctualShadowMap_TexelSize.x * shadowTile.filterData.x * clipWRcp;
        float2 filterRotation = sampleRotation * (PcssDiskScale(PcssSampleCount(filter.y)) * uvRadius);
        float2 depthGradient = PunctualReceiverPlaneDepthGradient(positionSTSdx.xyz, positionSTSdy.xyz);
        if (quality == 0)
        {
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, GetShadowDiskOffset(i, filterRotation), depthGradient, bounds);
            }
            shadow *= 0.25;
        }
        else if (quality == 1)
        {
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, GetShadowDiskOffset(i, filterRotation), depthGradient, bounds);
            }
            shadow *= 0.125;
        }
        else
        {
            [unroll]
            for (int i = 0; i < 16; i++)
            {
                shadow += SamplePunctualShadow(positionSTS, GetShadowDiskOffset(i, filterRotation), depthGradient, bounds);
            }
            shadow *= 0.0625;
        }
    }
    return shadow;
}

#endif
