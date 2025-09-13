#ifndef TRP_TOON_INPUT_INCLUDED
#define TRP_TOON_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
#include "Packages/tako.trp/Shaders/LitInput.hlsl"

TEXTURE2D(_ControlMap1);
SAMPLER(sampler_ControlMap1);

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

half _ShadowThreshold1;
half _ShadowThreshold2;
half _ShadowThreshold3;
half _ShadowSmoothness1;
half _ShadowSmoothness2;
half _ShadowSmoothness3;
half4 _ShadowColor1;
half4 _ShadowColor2;
half4 _ShadowColor3;

half3 _RimLightColor;
half _RimLightStrength;
half _RimLightWidth;
half _RimLightSmoothness;

half _LightEffect;

CBUFFER_END

#endif