#ifndef TRP_BLIT
#define TRP_BLIT

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"

struct Varyings
{
	float4 positionCS_SS : SV_POSITION;
	float2 uv : SCREEN_UV;
};

float4 _BlitScaleBias;

uniform float4 _BlitScaleBiasRt;
uniform float4 _BlitTexture_TexelSize;
uniform float _BlitMipLevel;
uniform float2 _BlitTextureSize;
uniform uint _BlitPaddingSize;
uniform int _BlitTexArraySlice;
uniform float4 _BlitDecodeInstructions;

Varyings BlitPassVertex(uint vertexId : SV_VertexID)
{
	Varyings output;

	output.positionCS_SS = GetFullScreenTriangleVertexPosition(vertexId);
	output.uv = GetFullScreenTriangleTexCoord(vertexId);
	output.uv = DYNAMIC_SCALING_APPLY_SCALEBIAS(output.uv);
	
	return output;
}

TEXTURE2D(_BlitTexture);

#endif