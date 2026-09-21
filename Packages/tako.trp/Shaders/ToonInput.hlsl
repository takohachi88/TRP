#ifndef TRP_TOON_INPUT_INCLUDED
#define TRP_TOON_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

TEXTURE2D(_ControlMap1);
SAMPLER(sampler_ControlMap1);
TEXTURE2D(_ShadingRamp);
SAMPLER(sampler_ShadingRamp);
TEXTURE2D(_ShadowRamp);
SAMPLER(sampler_ShadowRamp);
TEXTURE2D(_PunctualShadingRamp);
SAMPLER(sampler_PunctualShadingRamp);
TEXTURE2D(_PunctualShadowRamp);
SAMPLER(sampler_PunctualShadowRamp);

CBUFFER_START(UnityPerMaterial)

half4 _OutlineColor;
half _OutlineWidth;
half _OutlineLightStrength;
half _OutlineLightStrengthThreshold;

float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;

half _PunctualLightRamp;

half3 _RimLightColor;
half _RimLightStrength;
half _RimLightWidth;
half _RimLightSmoothness;

half _MultiplyRgbA;

CBUFFER_END

#endif
