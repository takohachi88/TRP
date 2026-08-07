#ifndef TRP_VFX
#define TRP_VFX

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

float2 Distort(TEXTURE2D_PARAM(distortMap, sampler_DistortMap), float2 uv, float strength)
{
	return (SAMPLE_TEXTURE2D(distortMap, sampler_DistortMap, uv).xy - 0.5) * strength;
}

half4 Dissolve(
	TEXTURE2D_PARAM(dissolveMap, sampler_DissolveMap),
	float2 uv,
	half4 baseColor,
	float progress,
	half edgeSmooth,
	float edgeWidth,
	half4 edgeColor,
	int edgeColorBlendMode,
	out half dissolveAlpha)
{
	half dissolveValue = SAMPLE_TEXTURE2D(dissolveMap, sampler_DissolveMap, uv).r;
	half distanceFromEdge = dissolveValue - saturate(progress);

	edgeWidth = max(edgeWidth, 0);
	edgeSmooth = max(edgeSmooth, 0.0001h);

	// 境界から内側へ smoothstep で可視率を上げ、透明描画では滑らかな feather として扱う。
	dissolveAlpha = smoothstep(0.0h, edgeSmooth, distanceFromEdge);
	baseColor.a *= dissolveAlpha;

	// エッジ色も同じ滑らかさで、境界から内側へ自然に減衰させる。
	half edgeMask = 1 - smoothstep(edgeWidth, edgeWidth + edgeSmooth, distanceFromEdge);

	baseColor.rgb =
		lerp(baseColor.rgb, edgeColor.rgb, edgeMask * edgeColor.a) * (edgeColorBlendMode == 0) +
		(baseColor.rgb + edgeColor.rgb * edgeMask * edgeColor.a) * (edgeColorBlendMode == 1) +
		lerp(baseColor.rgb, baseColor.rgb * edgeColor.rgb, edgeMask * edgeColor.a) * (edgeColorBlendMode == 2);

	return baseColor;
}

#endif
