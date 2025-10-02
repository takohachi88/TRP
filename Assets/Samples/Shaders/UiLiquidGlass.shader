Shader "Hidden/TRP/Ui/UiLiquidGlass"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
        }

        Cull Off
        ZWrite Off

        HLSLINCLUDE

        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_AttachmentColor);
        TEXTURE2D(_LiquidData);

        float4 _Params1;
        float4 _Params2;
        float4 _Params3;

        half4 Blur(float2 uv, float2 direction, float4 params1)
        {
            int count = params1.x;
            float countRcp = params1.y;
            float countMinusOneRcp = params1.z;
            float strength = params1.w;

            direction *= _AspectFit.yx * strength;

            half4 output = 0;

            for(int i = 0; i < count; i++)
            {
                float2 offset = lerp(-direction, direction, i * (countMinusOneRcp));
                output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset);
            }
            output *= countRcp;

            return output;
        }

        half4 FragmentShapeBlurH(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(1, 0), _Params1);
        }
        
        half4 FragmentShapeBlurV(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(0, 1), _Params1);
        }

        half4 FragmentBlurH(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(1, 0), _Params2);
        }
        
        half4 FragmentBlurV(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(0, 1), _Params2);
        }

        half4 FragmentComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            float edgeDistortStrength = _Params3.x;
            float uiColor = _Params3.y;

            half4 liquidData = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv);
            half4 base = SAMPLE_TEXTURE2D(_AttachmentColor, sampler_LinearClamp, uv);

            float chromaticAberrationEdgeStrength = _Params3.z;
            float chromaticAberrationBaseStrength = _Params3.w;
            half distort = liquidData.a;

            half blurR = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength + chromaticAberrationEdgeStrength * edgeDistortStrength, distort) + float2(chromaticAberrationBaseStrength, 0)).r;
            half blurG = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength, distort)).g;
            half blurB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength - chromaticAberrationEdgeStrength * edgeDistortStrength, distort) + float2(-chromaticAberrationBaseStrength, 0)).b;

            half3 blur = half3(blurR, blurG, blurB);

            half shape = smoothstep(0.48, 0.52, liquidData.a);

            half4 output = 1;

            liquidData.rgb = liquidData.a <= 0.001 ? liquidData.rgb : liquidData.rgb * rcp(liquidData.a);

            output.rgb = lerp(base.rgb, blur * lerp(1, liquidData.rgb, uiColor), shape);

            return output;
        }

        ENDHLSL

        Pass
        {
            Name "ShapeBlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentShapeBlurH
            ENDHLSL
        }
        
        Pass
        {
            Name "ShapeBlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentShapeBlurV
            ENDHLSL
        }
        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlurH
            ENDHLSL
        }
        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlurV
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentComposite
            ENDHLSL
        }
    }
}
