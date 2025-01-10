Shader "Hidden/Trp/CameraBlit"
{
	
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "../ShaderLibrary/Blit.hlsl"
		#include "CameraBlitPasses.hlsl"
		ENDHLSL

		Pass
		{
			Name "Copy Color"

			Blend [_CameraSrcBlend] [_CameraDstBlend]

			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex BlitPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
		
		Pass
		{
			Name "Copy Depth"

			ColorMask 0
			ZWrite On
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex BlitPassVertex
				#pragma fragment CopyDepthPassFragment
			ENDHLSL
		}
	}
}