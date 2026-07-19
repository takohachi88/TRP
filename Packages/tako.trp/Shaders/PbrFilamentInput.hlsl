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
float _HexTilingRotationStrength;
half _HexTilingGain;
half _ShadowNormalDistortion;
half _Padding1;
CBUFFER_END

#endif
