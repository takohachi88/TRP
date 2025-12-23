//URPのLightCookieを移植。
#ifndef TRP_LIGHT_COOKIE_INCLUDED
#define TRP_LIGHT_COOKIE_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

float2 ComputeLightCookieUVSpot(float4x4 worldToLightPerspective, float3 samplePositionWS, float4 atlasUVRect)
{
    // Translate, rotate and project 'positionWS' into the light clip space.
    float4 positionCS = mul(worldToLightPerspective, float4(samplePositionWS, 1));
    float2 positionNDC = positionCS.xy / positionCS.w;

    // Remap NDC to the texture coordinates, from NDC [-1, 1]^2 to [0, 1]^2.
    float2 positionUV = saturate(positionNDC * 0.5 + 0.5);

    // Remap into rect in the atlas texture
    float2 positionAtlasUV = atlasUVRect.xy * float2(positionUV) + atlasUVRect.zw;

    return positionAtlasUV;
}

float2 ComputeLightCookieUVPoint(float4x4 worldToLight, float3 samplePositionWS, float4 atlasUVRect)
{
    // Translate and rotate 'positionWS' into the light space.
    float4 positionLS = mul(worldToLight, float4(samplePositionWS, 1));

    float3 sampleDirLS = normalize(positionLS.xyz / positionLS.w);

    // Project direction to Octahederal quad UV.
    float2 positionUV = saturate(PackNormalOctQuadEncode(sampleDirLS) * 0.5 + 0.5);

    // Remap to atlas texture
    float2 positionAtlasUV = atlasUVRect.xy * float2(positionUV) + atlasUVRect.zw;

    return positionAtlasUV;
}

float2 ComputeLightCookieUVDirectional(float4x4 worldToLight, float3 samplePositionWS, float4 atlasUVRect, int wrapMode)
{
    // Translate and rotate 'positionWS' into the light space.
    // Project point to light "view" plane, i.e. discard Z.
    float2 positionLS = mul(worldToLight, float4(samplePositionWS, 1)).xy;

    // Remap [-1, 1] to [0, 1]
    // (implies the transform has ortho projection mapping world space box to [-1, 1])
    float2 positionUV = positionLS * 0.5 + 0.5;

    // Tile texture for cookie in repeat mode
    //TRPではXY軸ごとに判定しない。
    positionUV = (wrapMode == WRAP_MODE_REPEAT) ? frac(positionUV) : positionUV;
    positionUV = (wrapMode == WRAP_MODE_CLAMP) ? saturate(positionUV) : positionUV;

    // Remap to atlas texture
    float2 positionAtlasUV = atlasUVRect.xy * float2(positionUV) + atlasUVRect.zw;

    return positionAtlasUV;
}

half3 SampleDirectionalLightCookie(int perObjectLightIndex, float3 samplePositionWS)
{
    LightCookieBuffer cookie = _LightCookieBuffer[perObjectLightIndex];
    float4x4 worldToLight = cookie.worldToLight;
    float4 uvRect = cookie.uvScaleOffset;
    float2 uv = ComputeLightCookieUVDirectional(worldToLight, samplePositionWS, uvRect, cookie.wrapMode);
    return SAMPLE_TEXTURE2D_LOD(_LightCookieAtlas, sampler_LinearClamp, uv, 0).rgb;
}

half3 SamplePunctualLightCookie(int perObjectLightIndex, float3 samplePositionWS, int lightType)
{
    LightCookieBuffer cookie = _LightCookieBuffer[perObjectLightIndex];
    float4x4 worldToLight = cookie.worldToLight;
    float4 uvRect = cookie.uvScaleOffset;

    float2 uv = 0;
    if (lightType == LIGHT_TYPE_SPOT)
    {
        uv = ComputeLightCookieUVSpot(worldToLight, samplePositionWS, uvRect);
    }
    else
    {
        uv = ComputeLightCookieUVPoint(worldToLight, samplePositionWS, uvRect);
    }

    return SAMPLE_TEXTURE2D_LOD(_LightCookieAtlas, sampler_LinearClamp, uv, 0).rgb;
}

#endif