#ifndef TRP_LIGHTING_INCLUDED
#define TRP_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/tako.trp/ShaderLibrary/ForwardPlus.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Shadow.hlsl"
#include "Packages/tako.trp/ShaderLibrary/LightCookie.hlsl"

//Directional Light----------------
struct DirectionalLight
{
    half3 direction;
    half3 color;
    half attenuation;
    int shadowMapTileStartIndex;
    half normalBias;
    int cookieIndex;
};

DirectionalLight GetDirectionalLight(int index)
{
    DirectionalLightBuffer buffer = _DirectionalLightBuffer[index];
    DirectionalLight light = (DirectionalLight)0;
    light.direction = buffer.data1.xyz;
    light.color = buffer.data2.xyz;
    light.attenuation = buffer.data3.x;
    light.shadowMapTileStartIndex = buffer.data3.y;
    light.normalBias = buffer.data3.z;
    light.cookieIndex = buffer.data3.w;
    return light;
}
//---------------------------------


//Punctual Light-------------------
struct PunctualLight
{
	half3 direction;
	half3 color;
	half3 position;
	half rangeInverseSquare;
	half2 spotAngles;
    int cookieIndex;
    half attenuation;
    int shadowMapTileStartIndex;
    int type;//spot:0, point:2（UnityEngine.LightTypeに準拠。）
};

PunctualLight GetPunctualLight(int index)
{
	PunctualLightBuffer buffer = _PunctualLightBuffer[index];
	PunctualLight light = (PunctualLight)0;
	light.direction = buffer.data1.xyz;
	light.color = buffer.data2.xyz;
	light.position = buffer.data3.xyz;
	light.rangeInverseSquare = buffer.data3.w;
	light.spotAngles = buffer.data4.xy;
    light.cookieIndex = buffer.data4.z;
    light.attenuation = buffer.data5.x;
    light.shadowMapTileStartIndex = buffer.data5.y;
    light.type = buffer.data5.z;
	return light;
}
//---------------------------------



//Lightmap-------------------------
#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTES float2 lightmapUv : TEXCOORD1;//LightMapはUnity仕様により絶対に1番で固定。
	#define GI_VARYINGS float2 lightmapUv : LIGHT_MAP_UV;
	#define GI_TRANSFER(input, output) output.lightmapUv = input.lightmapUv * unity_LightmapST.xy + unity_LightmapST.zw;
	#define GI_FRAGMENT_UV(input) input.lightmapUv
#else
	#define GI_ATTRIBUTES
	#define GI_VARYINGS
	#define GI_TRANSFER(input, output)
	#define GI_FRAGMENT_UV(input) 0
#endif

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

half3 SampleLightmap(float2 lightmapUv)
{
	#if defined(LIGHTMAP_ON)
		return SampleSingleLightmap(
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
			lightmapUv,
			float4(1, 1, 0, 0),
			#if defined(UNITY_LIGHTMAP_FULL_HDR)
			false,
			#else
			true,
			#endif
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0, 0));
	#else
		return 0;
	#endif
}
//----------------------------------


//LightProbe------------------------
float3 SampleLightProbe (half3 normalWS)
{
	#if defined(LIGHTMAP_ON)
		return 0;
	#else
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0, SampleSH9(coefficients, normalWS));
	#endif
}
//----------------------------------

half3 Gi(half3 normalWS, float2 lightmapUv = 0)
{
	return SampleLightmap(lightmapUv) + SampleLightProbe(normalWS);
}


half Fresnel(half3 normalWS, half3 directionVS)
{
	return 1 - saturate(dot(normalWS, -directionVS));
}

half3 RimLight(half3 normalWS, half3 directionVS, half strength, half width, half smoothness, half3 color)
{
	half fresnel = Fresnel(normalWS, directionVS);
	width = 1 - width;
	fresnel = smoothstep(width - smoothness, width + smoothness, fresnel);
	return fresnel * color * strength;
}

half Lambert(half3 normalWS, half3 lightDirection)
{
    return saturate(dot(normalWS, lightDirection));
}

#endif