#ifndef TRP_PBR_LIGHTING_INCLUDED
#define TRP_PBR_LIGHTING_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
#include "Packages/tako.trp/Shaders/LitInput.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Pbr.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

#ifndef UNITY_SPECCUBE_LOD_STEPS
#define UNITY_SPECCUBE_LOD_STEPS 6
#endif

half3 SamplePbrSpecularIbl(
    PbrMaterialData material,
    half3 normalWS,
    half3 viewDirectionWS,
    half ambientOcclusion,
    half strength)
{
    half noV = saturate(dot(normalWS, viewDirectionWS));
    half3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);
    half mipLevel = material.perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;
    half4 encodedIbl = SAMPLE_TEXTURECUBE_LOD(
        unity_SpecCube0,
        samplerunity_SpecCube0,
        reflectionDirectionWS,
        mipLevel);
    half3 radiance = DecodeHDREnvironment(encodedIbl, unity_SpecCube0_HDR);
    half3 environmentBrdf = ApproximateEnvironmentBrdf(material, noV);
    half specularOcclusion = ComputeSpecularOcclusion(noV, ambientOcclusion, material.roughness);
    return radiance * environmentBrdf * specularOcclusion * strength;
}

half3 EvaluatePbrDirectionalLights(
    PbrMaterialData material,
    float3 positionWS,
    half3 normalWS,
    half3 shadowNormalWS,
    half3 viewDirectionWS,
    half cascadeIndex,
    bool useBurleyDiffuse)
{
    half3 lighting = 0;

    for (int i = 0; i < _DirectionalLightCount; i++)
    {
        DirectionalLight light = GetDirectionalLight(i);
        half3 cookie = 1;
        if (0 <= light.cookieIndex)
        {
            cookie = SampleDirectionalLightCookie(light.cookieIndex, positionWS);
        }

        half attenuation = 1;
        if (0 < light.attenuation)
        {
            attenuation = GetDirectionalShadow(
                cascadeIndex,
                positionWS,
                shadowNormalWS,
                light.normalBias,
                light.shadowMapTileStartIndex);
        }

        lighting += EvaluatePbrDirect(
            material,
            normalWS,
            viewDirectionWS,
            light.direction,
            useBurleyDiffuse) * light.color * cookie * attenuation;
    }

    return lighting;
}

half3 EvaluatePbrPunctualLight(
    int lightIndex,
    PbrMaterialData material,
    float3 positionWS,
    half3 normalWS,
    half3 shadowNormalWS,
    half3 viewDirectionWS,
    bool useBurleyDiffuse)
{
    PunctualLight light = GetPunctualLight(lightIndex);
    float3 surfaceToLight = light.position - positionWS;
    float distanceSquared = max(dot(surfaceToLight, surfaceToLight), 0.00001);
    half3 lightDirectionWS = surfaceToLight * rsqrt(distanceSquared);

    // Filament と同様にライト範囲の終端で滑らかに減衰させ、逆二乗減衰と組み合わせる。
    half rangeFactor = saturate(1.0h - Pow2(distanceSquared * light.rangeInverseSquare));
    half rangeAttenuation = Pow2(rangeFactor) * rcp(distanceSquared);
    half spotAttenuation = saturate(dot(light.direction, lightDirectionWS) * light.spotAngles.x + light.spotAngles.y);
    spotAttenuation = Pow2(spotAttenuation);

    half shadowAttenuation = 1;
    if (0 < light.attenuation)
    {
        shadowAttenuation = GetPunctualShadow(
            positionWS,
            shadowNormalWS,
            light.position,
            light.direction,
            lightDirectionWS,
            light.type,
            light.shadowMapTileStartIndex);
    }

    half3 cookie = 1;
    if (0 <= light.cookieIndex)
    {
        cookie = SamplePunctualLightCookie(light.cookieIndex, positionWS, light.type);
    }

    return EvaluatePbrDirect(
        material,
        normalWS,
        viewDirectionWS,
        lightDirectionWS,
        useBurleyDiffuse)
        * light.color
        * cookie
        * rangeAttenuation
        * spotAttenuation
        * shadowAttenuation;
}

// サーフェスの作り方に依存しない、TRP 共通の PBR 照明評価。
half3 EvaluatePbrLighting(
    PbrMaterialData material,
    float3 positionWS,
    half3 normalWS,
    half3 shadowNormalWS,
    half3 viewDirectionWS,
    float2 lightmapUv,
    half ambientOcclusion,
    half specularIblStrength,
    bool useBurleyDiffuse,
    float2 positionPx)
{
    // Lightmap／Light Probe には Lambert 積分が含まれるため、ここでは INV_PI を重ねない。
    half3 color = Gi(normalWS, lightmapUv) * material.diffuseColor * ambientOcclusion;
    color += SamplePbrSpecularIbl(
        material,
        normalWS,
        viewDirectionWS,
        ambientOcclusion,
        specularIblStrength);

    half dither = InterleavedGradientNoise(positionPx, 0);
    half cascadeIndex = ComputeCascadeIndex(positionWS, dither);
    color += EvaluatePbrDirectionalLights(
        material,
        positionWS,
        normalWS,
        shadowNormalWS,
        viewDirectionWS,
        cascadeIndex,
        useBurleyDiffuse);

    if (0 < _PunctualLightCount)
    {
        float2 screenUv = positionPx * _AttachmentSize.xy;
        ForwardPlusTile tile = GetForwardPlusTile(screenUv);
        int lastIndex = tile.GetLastLightIndexInTile();
        for (int i = tile.GetFirstLightIndexInTile(); i <= lastIndex; i++)
        {
            color += EvaluatePbrPunctualLight(
                tile.GetLightIndex(i),
                material,
                positionWS,
                normalWS,
                shadowNormalWS,
                viewDirectionWS,
                useBurleyDiffuse);
        }
    }

    return color;
}

#endif
