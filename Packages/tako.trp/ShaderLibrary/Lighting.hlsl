#ifndef TRP_LIGHTING_INCLUDED
#define TRP_LIGHTING_INCLUDED

#include "Packages/tako.trp/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/tako.trp/ShaderLibrary/ForwardPlus.hlsl"

//Directional Light----------------
half3 GetDirectionalLightDirection(int index)
{
	return _DirectionalLightData1[index].xyz;
}

half3 GetDirectionalLightColor(int index)
{
	return _DirectionalLightData2[index].rgb;
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
	return light;
}
//---------------------------------



//Lightmap-------------------------
#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTES float2 lightmapUv : TEXCOORD1;
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


half3 ToonLighting(half3 normalWS, half3 color, half3 direction, half3 lightColor, half attenuation = 1)
{
	half3 output = color;
	half lambert = Lambert(normalWS, direction) * attenuation;
	half toonLambert1 = smoothstep(_ShadowThreshold1 - _ShadowSmoothness1, _ShadowThreshold1 + _ShadowSmoothness1, lambert);
	half toonLambert2 = smoothstep(_ShadowThreshold2 - _ShadowSmoothness2, _ShadowThreshold2 + _ShadowSmoothness2, lambert);
	half toonLambert3 = smoothstep(_ShadowThreshold3 - _ShadowSmoothness3, _ShadowThreshold3 + _ShadowSmoothness3, lambert);
	output = lerp(color * _ShadowColor1.rgb, output + lightColor * 0.1 * _LightEffect, toonLambert1);
	output = lerp(color * _ShadowColor2.rgb, output + lightColor * 0.066 * _LightEffect, toonLambert2);
	output = lerp(color * _ShadowColor3.rgb, output + lightColor * 0.033 * _LightEffect, toonLambert3);
	output *= lightColor;
	return output;
}

half3 PunctualLighting(int index, float3 positionWS, half3 normalWS, half3 color)
{
	PunctualLight light = GetPunctualLight(index);
	half3 direction = light.position - positionWS;
	half3 normalizedDirection = normalize(direction);
	//逆2乗則。
	half distanceSqr = max(dot(direction, direction), 0.00001);
	half rangeAttenuation = Pow2(max(0, 1 - Pow2(distanceSqr * light.rangeInverseSquare)));
	half2 spotAngles = light.spotAngles;
	half spotAttenuation = saturate(dot(light.direction, normalizedDirection) * spotAngles.x + spotAngles.y);
	half attenuation = rangeAttenuation * spotAttenuation;
	half3 output = ToonLighting(normalWS, color, normalizedDirection, light.color, attenuation);
	return output * attenuation;
}

half3 ToonLighting(float3 positionWS, half3 normalWS, half3 color, float2 screenUv)
{
	half3 output = Gi(normalWS) * color;

	for(int i = 0; i < _DirectionalLightCount; i++)
	{
		output += ToonLighting(normalWS, color, GetDirectionalLightDirection(i), GetDirectionalLightColor(i));
	}

	ForwardPlusTile tile = GetForwardPlusTile(screenUv);
	int lastIndex = tile.GetLastLightIndexInTile();
	for(int j = tile.GetFirstLightIndexInTile(); j <= lastIndex; j++)
	{
		//output += j * 0.1;
		output += PunctualLighting(tile.GetLightIndex(j), positionWS, normalWS, color);
	}

	return output;
}


#endif