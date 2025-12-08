#ifndef TRP_LIT_INPUT_INCLUDED
#define TRP_LIT_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_DIRECTIONAL_SHADOW_COUNT 4
#define MAX_CASCADE_COUNT 4

#define MAX_PUNCTUAL_LIGHT_COUNT 8

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

TEXTURE2D_SHADOW(_DirectionalShadowMap);
TEXTURE2D_SHADOW(_PunctualShadowMap);
SAMPLER_CMP(sampler_LinearClampCompare);

struct DirectionalLightBuffer
{
    float4 data1, data2, data3;
};

#define LIGHT_TYPE_IS_POINT(type) ((type) == 2)

struct PunctualLightBuffer
{
	float4 data1, data2, data3, data4, data5;
};

struct PunctualShadowTileBuffer
{
    float4 tileData;
    float4x4 worldToShadow;
};

StructuredBuffer<PunctualShadowTileBuffer> _PunctualShadowTileBuffer;

CBUFFER_START(TrpLight)
int _DirectionalLightCount;
StructuredBuffer<DirectionalLightBuffer> _DirectionalLightBuffer;
float4x4 _WorldToDirectionalShadows[MAX_DIRECTIONAL_SHADOW_COUNT * MAX_CASCADE_COUNT];
float4 _CullingSphere0;
float4 _CullingSphere1;
float4 _CullingSphere2;
float4 _CullingSphere3;
float4 _CullingSphereRadiusSqrs;
float4 _CullingSphereRanges;
float4 _ShadowParams1;
float4 _DirectionalShadowMap_TexelSize;
float4 _PunctualShadowMap_TexelSize;

int _PunctualLightCount;
StructuredBuffer<PunctualLightBuffer> _PunctualLightBuffer;
StructuredBuffer<int> _TileBuffer;

//xy: Screen UV to tile coordinates
//z: 一行あたりのタイルの個数
//w: タイルデータのサイズ
float4 _ForwardPlusTileSettings;

CBUFFER_END

#endif