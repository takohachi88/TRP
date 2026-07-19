#ifndef TRP_PBR_INCLUDED
#define TRP_PBR_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

// FilamentのStandard Modelに沿った、単一レイヤーのmetallic/roughnessマテリアル。
// テクスチャやUnityPerMaterialには依存させず、ParticleやVFXからも利用可能にする。
struct PbrMaterialData
{
    half3 diffuseColor;
    half3 f0;
    half perceptualRoughness;
    half roughness;
};

PbrMaterialData CreatePbrMaterialData(half3 baseColor, half metallic, half perceptualRoughness)
{
    PbrMaterialData material;
    metallic = saturate(metallic);

    // 完全な鏡面は数値不安定とスペキュラエイリアシングを起こすため、Filament同様に下限を設ける。
    material.perceptualRoughness = clamp(perceptualRoughness, 0.045h, 1.0h);
    material.roughness = Pow2(material.perceptualRoughness);
    material.diffuseColor = baseColor * (1.0h - metallic);
    material.f0 = lerp(0.04h.xxx, baseColor, metallic);
    return material;
}

// Trowbridge-Reitz GGX法線分布関数。
half DistributionGgx(half roughness, half noH)
{
    half roughnessSquared = Pow2(roughness);
    half denominator = noH * noH * (roughnessSquared - 1.0h) + 1.0h;
    return roughnessSquared * rcp(PI * denominator * denominator);
}

// Height-correlated Smith GGXの可視性項。
// G/(4 NoV NoL)までをまとめ、直接光評価側の乗算数を抑える。
half VisibilitySmithGgxCorrelated(half roughness, half noV, half noL)
{
    half roughnessSquared = Pow2(roughness);
    half visibilityV = noL * sqrt((noV - noV * roughnessSquared) * noV + roughnessSquared);
    half visibilityL = noV * sqrt((noL - noL * roughnessSquared) * noL + roughnessSquared);
    return 0.5h * rcp(max(visibilityV + visibilityL, 0.0001h));
}

half DiffuseLambert()
{
    return INV_PI;
}

// Disney/Burley diffuse。Lambertより高負荷だが、粗い表面の視線・光線依存を表現できる。
half DiffuseBurley(half roughness, half noV, half noL, half loH)
{
    half f90 = 0.5h + 2.0h * roughness * loH * loH;
    half lightScatter = SchlickFresnel(1.0h, f90, noL);
    half viewScatter = SchlickFresnel(1.0h, f90, noV);
    return lightScatter * viewScatter * INV_PI;
}

half3 EvaluatePbrDirect(
    PbrMaterialData material,
    half3 normalWS,
    half3 viewDirectionWS,
    half3 lightDirectionWS,
    bool useBurleyDiffuse)
{
    half3 halfDirectionWS = SafeNormalize(viewDirectionWS + lightDirectionWS);
    half noV = saturate(dot(normalWS, viewDirectionWS));
    half noL = saturate(dot(normalWS, lightDirectionWS));
    half noH = saturate(dot(normalWS, halfDirectionWS));
    half loH = saturate(dot(lightDirectionWS, halfDirectionWS));

    half distribution = DistributionGgx(material.roughness, noH);
    half visibility = VisibilitySmithGgxCorrelated(material.roughness, noV, noL);
    half3 fresnel = SchlickFresnel(material.f0, 1.0h, loH);
    half3 specular = distribution * visibility * fresnel;

    half diffuse = useBurleyDiffuse
        ? DiffuseBurley(material.roughness, noV, noL, loH)
        : DiffuseLambert();

    return (material.diffuseColor * diffuse + specular) * noL;
}

// UE4のEnvironment BRDF近似。DFG LUTを追加せず、Reflection Probeのsplit-sumを評価する。
half3 ApproximateEnvironmentBrdf(PbrMaterialData material, half noV)
{
    const half4 coefficient0 = half4(-1.0h, -0.0275h, -0.572h, 0.022h);
    const half4 coefficient1 = half4(1.0h, 0.0425h, 1.04h, -0.04h);
    half4 r = material.perceptualRoughness * coefficient0 + coefficient1;
    half a004 = min(r.x * r.x, exp2(-9.28h * noV)) * r.x + r.y;
    half2 dfg = half2(-1.04h, 1.04h) * a004 + r.zw;
    return material.f0 * dfg.x + dfg.y;
}

// 安価なspecular AO。遮蔽部でReflection Probeだけが浮く現象を抑える。
half ComputeSpecularOcclusion(half noV, half ambientOcclusion, half roughness)
{
    half exponent = exp2(-16.0h * roughness - 1.0h);
    return saturate(pow(abs(noV + ambientOcclusion), exponent) - 1.0h + ambientOcclusion);
}

#endif
