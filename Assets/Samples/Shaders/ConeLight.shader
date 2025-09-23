Shader "Custom/ConeLight"
{   
    Properties
    {
        [HDR] _Color ("Color", color) = (1, 1, 1, 1)
        [Toggle(_DEPTH_FADE)] _DepthFade ("Depth Fade", int) = 0
        _Near ("Near", float) = 0
        _Far ("Far", float) = 1
        _EdgeSmoothness ("Edge", Range(0, 1)) = 1

        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 1
    }


    SubShader
    {
        HLSLINCLUDE
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/DepthFade.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/DeclareNormalsTexture.hlsl"
        ENDHLSL

        Tags
        {
            "Queue" = "Transparent"
        }

        ZWrite Off
        Cull Off
        Blend [_BlendSrc][_BlendDst]
        
        Pass
        {
            Name "ConeLight"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _DEPTH_FADE

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 positionNDC : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half3 directionVS : TEXCOORD4;
            };

            float _Near;
            float _Far;
            half _EdgeSmoothness;
            half4 _Color;

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexInputs vertexInputs = GetVertexInputs(input.positionOS.xyz, input.normalOS);
                output.positionCS = vertexInputs.positionCS;
                output.positionNDC = vertexInputs.positionNDC;
                output.positionWS = vertexInputs.positionWS;
                output.uv = input.uv;
                output.normalWS = vertexInputs.normalWS;
                output.directionVS = vertexInputs.directionVS;
                return output;
            }


            half4 Frag (Varyings input) : SV_Target
            {
                float depth = SampleSceneDepth(input.positionNDC.xy * rcp(input.positionNDC.w));

                half4 output = 1;

                #if defined(_DEPTH_FADE)
                output.a *= DepthFade(_Near, _Far, input.positionNDC, input.positionWS);
                #endif

                output.a *= input.uv.y * input.uv.y * input.uv.y;
                output.a *= smoothstep(0, _EdgeSmoothness * (1 - output.a), saturate(abs(dot(normalize(input.normalWS), normalize(input.directionVS)))));
                output *= _Color;

                return output;
            }
            
            ENDHLSL
        }
    }
}
