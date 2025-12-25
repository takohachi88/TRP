Shader "Hidden/Trp/DebugForwardPlusTile"
{	
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "Packages/tako.trp/Shaders/LitInput.hlsl"
		#include "Packages/tako.trp/Shaders/ToonInput.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"
		#include "../ShaderLibrary/ForwardPlus.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
		#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"
		ENDHLSL

		Pass
		{
			Name "DebugForwardPlusTile"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma vertex Vert
				#pragma fragment Fragment

				half _DebugForwardPlusTileOpacity;

				half4 Fragment(Varyings input) : SV_Target
				{
					float2 uv = input.positionCS.xy * _AttachmentSize.xy;
					if(0 <= _ProjectionParams.x)
					{
						uv.y = 1 - uv.y;
					}
					ForwardPlusTile tile = GetForwardPlusTile(uv);
					float3 color;
					if (tile.IsMinimumEdgePixel(uv))
					{
						color = 1.0;
					}
					else
					{
						color = OverlayHeatMap(
							uv * _AttachmentSize.zw,
							tile.GetScreenSize(),
							tile.GetLightCount(),
							tile.GetMaxLightsPerTile(),
							1.0).rgb;
					}
					return float4(color, _DebugForwardPlusTileOpacity);
				}


			ENDHLSL
		}
	}
}