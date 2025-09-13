#ifndef TRP_LIT_INPUT_INCLUDED
#define TRP_LIT_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_PUNCTUAL_LIGHT_COUNT 8

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

struct PunctualLightBuffer
{
	float4 data1, data2, data3, data4;
};

CBUFFER_START(TrpLight)
int _DirectionalLightCount;
half4 _DirectionalLightData1[MAX_DIRECTIONAL_LIGHT_COUNT];
half4 _DirectionalLightData2[MAX_DIRECTIONAL_LIGHT_COUNT];

int _PunctualLightCount;
StructuredBuffer<PunctualLightBuffer> _PunctualLightBuffer;
StructuredBuffer<int> _TileBuffer;

//xy: Screen UV to tile coordinates
//z: 一行あたりのタイルの個数
//w: タイルデータのサイズ
float4 _ForwardPlusTileSettings;

CBUFFER_END

#endif