#ifndef TRP_TOON_LIGHTING_INCLUDED
#define TRP_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/tako.trp/ShaderLibrary/ForwardPlus.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Shadow.hlsl"
#include "Packages/tako.trp/ShaderLibrary/LightCookie.hlsl"
#include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"


half3 ToonLighting(
	half3 normalWS,
	half3 color,
	half3 direction,
	half3 lightColor,
	half lightAttenuation,
	half shadowAttenuation,
	half3 cookie = 1,
	bool punctualLight = false,
	half3 gi = 0)
{
	// 法線陰影と落ち影を別々のRampで評価する。
	half lambert = saturate(Lambert(normalWS, direction) * lightAttenuation);
	half castShadow = saturate(shadowAttenuation);
	// ライトループ内で暗黙のミップ勾配を要求しないよう、LOD 0 を明示してサンプリングする。
	half3 shadingRamp = SAMPLE_TEXTURE2D_LOD(_ShadingRamp, sampler_ShadingRamp, half2(lambert, 0.5), 0).rgb;
	half3 castShadowRamp = SAMPLE_TEXTURE2D_LOD(_ShadowRamp, sampler_ShadowRamp, half2(castShadow, 0.5), 0).rgb;
	
	// Punctual Light専用の2種類のRampを使う場合。
	if (punctualLight && 0.5 < _PunctualLightRamp)
	{
		shadingRamp = SAMPLE_TEXTURE2D_LOD(_PunctualShadingRamp, sampler_PunctualShadingRamp, half2(lambert, 0.5), 0).rgb;
		castShadowRamp = SAMPLE_TEXTURE2D_LOD(_PunctualShadowRamp, sampler_PunctualShadowRamp, half2(castShadow, 0.5), 0).rgb;
	}
	// 乗算せず、より暗い原因のRampだけを採用する。
	half3 ramp = castShadow < lambert ? castShadowRamp : shadingRamp;
	return color * (gi + lightColor * cookie * ramp);
}

half3 PunctualLighting(
	int index,
	float3 positionWS,
	float3 positionWSdx,
	float3 positionWSdy,
	half3 normalWS,
	half3 color,
	half3 gi)
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
    if (0 < light.attenuation) shadowAttenuation = GetPunctualShadow(positionWS, positionWSdx, positionWSdy, normalWS, light.position, light.direction, normalizedDirection, light.type, light.shadowMapTileStartIndex, light.shadowFilter);
	// 距離・スポット減衰は直接光へ適用し、GIは減衰させずRampだけを共有する。
	return ToonLighting(
		normalWS,
		color,
		normalizedDirection,
		light.color * attenuation,
		attenuation,
		shadowAttenuation,
		cookie,
		true,
		gi);
}

half3 ToonLighting(float3 positionWS, half3 normalWS, half3 color, float2 screenUv, half dither)
{
	// 勾配命令は可変回数のPunctual Lightループへ入れず、フラグメントごとに一度だけ評価する。
	float3 positionWSdx = ddx(positionWS);
	float3 positionWSdy = ddy(positionWS);
	half3 gi = Gi(normalWS);
	half3 output = 0;
	
	//cascadeはライトに関わらす一定。
    int cascadeIndex = ComputeCascadeIndex(positionWS, dither);
	
	for(int i = 0; i < _DirectionalLightCount; i++)
	{
        DirectionalLight light = GetDirectionalLight(i);
        half3 cookie = 1;
        if (0 <= light.cookieIndex) cookie = SampleDirectionalLightCookie(light.cookieIndex, positionWS);
		half shadowAttenuation = 1;
		if (0 < light.attenuation) shadowAttenuation = GetDirectionalShadow(cascadeIndex, positionWS, normalWS, light.normalBias, light.shadowMapTileStartIndex, light.shadowFilter); //shadowの適用。
		output += ToonLighting(
			normalWS,
			color,
			light.direction,
			light.color,
			1,
			shadowAttenuation,
			cookie,
			false,
			gi);
		// GIは複数ライトへ重複加算せず、最初に評価したRampだけへ含める。
		gi = 0;
    }

	if (_PunctualLightCount > 0)
	{
		ForwardPlusTile tile = GetForwardPlusTile(screenUv);
		int lastIndex = tile.GetLastLightIndexInTile();
		for(int j = tile.GetFirstLightIndexInTile(); j <= lastIndex; j++)
		{
			output += PunctualLighting(tile.GetLightIndex(j), positionWS, positionWSdx, positionWSdy, normalWS, color, gi);
			// Directional Lightがない場合は、最初のPunctual LightがGIを引き継ぐ。
			gi = 0;
		}
	}

	// ライトが一つもない場合は、Rampを適用できないためGIのみを表示する。
	return output + gi * color;
}


#endif
