#ifndef TRP_TOON_LIGHTING_INCLUDED
#define TRP_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/tako.trp/ShaderLibrary/ForwardPlus.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Shadow.hlsl"
#include "Packages/tako.trp/ShaderLibrary/LightCookie.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"


half3 ToonLighting(half3 normalWS, half3 color, half3 direction, half3 lightColor, half attenuation, half3 cookie = 1)
{	
	half3 output = color;
	half lambert = Lambert(normalWS, direction) * attenuation;
	half toonLambert1 = smoothstep(_ShadowThreshold1 - _ShadowSmoothness1, _ShadowThreshold1 + _ShadowSmoothness1, lambert);
	half toonLambert2 = smoothstep(_ShadowThreshold2 - _ShadowSmoothness2, _ShadowThreshold2 + _ShadowSmoothness2, lambert);
	half toonLambert3 = smoothstep(_ShadowThreshold3 - _ShadowSmoothness3, _ShadowThreshold3 + _ShadowSmoothness3, lambert);
	output = lerp(color * _ShadowColor1.rgb, output + lightColor * 0.1 * _LightEffect, toonLambert1);
	output = lerp(color * _ShadowColor2.rgb, output + lightColor * 0.066 * _LightEffect, toonLambert2);
	output = lerp(color * _ShadowColor3.rgb, output + lightColor * 0.033 * _LightEffect, toonLambert3);
	output *= lightColor * cookie;
	return output;
}

half3 PunctualLighting(int index, float3 positionWS, half3 normalWS, half3 color)
{	
	PunctualLight light = GetPunctualLight(index);
	
    half3 cookie = 1;
    if (0 <= light.cookieIndex) cookie = SamplePunctualLightCookie(light.cookieIndex, positionWS, light.type);
	
    half3 direction = light.position - positionWS;
	half3 normalizedDirection = normalize(direction);
	//逆2乗則。
	half distanceSqr = max(dot(direction, direction), 0.00001);
	half rangeAttenuation = Pow2(max(0, 1 - Pow2(distanceSqr * light.rangeInverseSquare)));
	half2 spotAngles = light.spotAngles;
	half spotAttenuation = saturate(dot(light.direction, normalizedDirection) * spotAngles.x + spotAngles.y);
	half attenuation = rangeAttenuation * spotAttenuation;
    half shadowAttenuation = 1;
	//影の適用。
    if (0 < light.attenuation) shadowAttenuation = GetPunctualShadow(positionWS, normalWS, light.position, light.direction, normalizedDirection, light.type, light.shadowMapTileStartIndex);
	half3 output = ToonLighting(normalWS, color, normalizedDirection, light.color, attenuation * shadowAttenuation, cookie);
	
	return output * attenuation;
}

half3 ToonLighting(float3 positionWS, half3 normalWS, half3 color, float2 screenUv, half dither)
{
	half3 output = Gi(normalWS) * color;
	
	//cascadeはライトに関わらす一定。
    int cascadeIndex = ComputeCascadeIndex(positionWS, dither);
	
	for(int i = 0; i < _DirectionalLightCount; i++)
	{
        DirectionalLight light = GetDirectionalLight(i);
        half3 cookie = 1;
        if (0 <= light.cookieIndex) cookie = SampleDirectionalLightCookie(light.cookieIndex, positionWS);
        half attenuation = 1;
        if (0 < light.attenuation) attenuation = GetDirectionalShadow(cascadeIndex, positionWS, normalWS, light.normalBias, light.shadowMapTileStartIndex); //shadowの適用。
        output += ToonLighting(normalWS, color, light.direction, light.color, attenuation, cookie);
    }

	ForwardPlusTile tile = GetForwardPlusTile(screenUv);
	int lastIndex = tile.GetLastLightIndexInTile();
	for(int j = tile.GetFirstLightIndexInTile(); j <= lastIndex; j++)
	{
		output += PunctualLighting(tile.GetLightIndex(j), positionWS, normalWS, color);
	}

	return output;
}


#endif