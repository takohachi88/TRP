Shader "TRP/Hidden/OitComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
        }

        Blend One Zero
        Cull Off
        ZWrite Off

        Pass
        {
            Name "OitComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE2D(_AccumulateTexture);
            TEXTURE2D_FLOAT(_RevealageTexture);
            
            half4 Fragment (Varyings input) : SV_Target
            {
                half4 output = 0;
                half4 src = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, input.texcoord);
                half4 accumulate = SAMPLE_TEXTURE2D(_AccumulateTexture, sampler_LinearClamp, input.texcoord);
                half revealage = SAMPLE_TEXTURE2D(_RevealageTexture, sampler_LinearClamp, input.texcoord).r;
                output = half4(accumulate.rgb * rcp(clamp(accumulate.a, 1e-4, 5e4)), revealage);
                output = lerp(output, src, output.a);
                return output;
            }

            ENDHLSL
        }
    }
}
