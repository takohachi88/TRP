Shader "Hidden/Trp/WbOitComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        ZWrite Off

        Pass
        {
            Name "WbOitComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE2D_FLOAT(_RevealageTexture);
            
            half4 Fragment (Varyings input) : SV_Target
            {
                half4 accumulate = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half revealage = SAMPLE_TEXTURE2D(_RevealageTexture, sampler_LinearClamp, input.texcoord).r;
                half3 averageColor = accumulate.rgb * rcp(clamp(accumulate.a, 1e-4, 5e4));
                return half4(averageColor, 1.0h - revealage);
            }

            ENDHLSL
        }
    }
}
