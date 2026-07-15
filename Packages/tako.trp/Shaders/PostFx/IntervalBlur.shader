Shader "Hidden/Trp/PostFx/IntervalBlur"
{
    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "IntervalBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

			#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half _Intensity;
            TEXTURE2D(_PreviousFrameTexture);

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 current = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half4 previous = SAMPLE_TEXTURE2D(_PreviousFrameTexture, sampler_LinearClamp, input.texcoord);
                return lerp(current, previous, _Intensity);
            }
            ENDHLSL
        }
    }
}
