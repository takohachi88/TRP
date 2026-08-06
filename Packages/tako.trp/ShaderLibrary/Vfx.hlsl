#ifndef TRP_VFX
#define TRP_VFX

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"


float2 Distort(TEXTURE2D_PARAM( distortMap, sampler_DistortMap), float2 uv, float strength)
{
	return (SAMPLE_TEXTURE2D(distortMap, sampler_DistortMap, uv).xy - 0.5) * strength;
}

half4 Dissolve(TEXTURE2D_PARAM( dissolveMap, sampler_DissolveMap), float2 uv, half4 baseColor, float progress, half edgeSmooth, float edgeWidth, half4 edgeColor, int edgeColorBlendMode)
{
	half dissolveValue = SAMPLE_TEXTURE2D(dissolveMap, sampler_DissolveMap, uv).r;

    // 正の領域が表示部分、負の領域が消滅部分。
	half distanceFromEdge = dissolveValue - saturate(progress);

    clip(distanceFromEdge);

    edgeWidth  = max(edgeWidth, 0);
    edgeSmooth = max(edgeSmooth, 0.0001);

    // ディゾルブ境界から内側へ向かって減衰するマスク。
	half edgeMask = 1 - smoothstep(edgeWidth, edgeWidth + edgeSmooth, distanceFromEdge);

    baseColor.rgb = 
		lerp(baseColor.rgb, edgeColor.rgb, edgeMask * edgeColor.a) * (edgeColorBlendMode == 0) + //通常
		(baseColor.rgb + edgeColor.rgb * edgeMask * edgeColor.a) * (edgeColorBlendMode == 1) +   //加算
		lerp(baseColor.rgb, baseColor.rgb * edgeColor.rgb, edgeMask * edgeColor.a) * (edgeColorBlendMode == 2);    //乗算

    return baseColor;
}

#endif