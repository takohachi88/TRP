Shader "TRP/WbOit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor][HDR] _BaseColor ("Base Color", color) = (1, 1, 1, 1)
        _AlphaFactor ("Alpha Factor", float) = 1 //透けない完全な不透明を表現したい場合などに調整する。

        [Header(Common Settings)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", int) = 2
        _Stencil ("Stencil ID", int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp("Stencil Operation", int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", int) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail", int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil Z Fail", int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
            "Queue" = "Transparent"
            "PreviewType" = "Torus"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }

        Pass
        {
            Tags
            {
                "LightMode" = "WbOit"
            }
            Blend 0 One One
            Blend 1 Zero OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2

            #define UNITY_SETUP_DOTS_SH_COEFFS
            #define UNITY_SETUP_DOTS_RENDER_BOUNDS

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/WbOit.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float fogCoord : TEXCOORD1;
                float z : OIT_Z;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _AlphaFactor;

            CBUFFER_END


            Varyings Vertex (Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexInputs vertexInput = GetVertexInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogCoord = vertexInput.positionVS.z;
                output.z = abs(TransformWorldToView(vertexInput.positionWS).z);
                return output;
            }


            FragmentWbOitOutputs Fragment (Varyings input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FragmentWbOitOutputs output = (FragmentWbOitOutputs)0;

                float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;

                output.alpha = color.a;

                #if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                    float viewZ = -input.fogCoord;
                    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
                    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                #else
                    half fogFactor = 0;
                #endif
                color.rgb = MixFog(color.rgb, fogFactor);

                color.rgb *= color.a;
                color *= WbOitWeight2(input.z, color.a * _AlphaFactor);

                output.color = color;

                return output;
            }

            ENDHLSL
        }
    }
}
