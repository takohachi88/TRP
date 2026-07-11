Shader "TRP/Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor][HDR] _BaseColor ("Base Color", color) = (1, 1, 1, 1)


        [Header(Common Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", int) = 0
        [Toggle(MULTIPLY_RGB_A)] _MultiplyRgbA ("Multiply RGB A", int) = 1
        [PerRendererData] _AlphaBlend ("Alpha Blend", int) = 3
        [PerRendererData] _VertexColorBlend ("Vertex Color Blend", int) = 0
        
        [Toggle] _ZWrite ("Z Write", int) = 1
        
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
            "Queue" = "Geometry"
            "PreviewType" = "Sphere"
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
            Blend [_BlendSrc][_BlendDst]
            ZWrite [_ZWrite]
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _MultiplyRgbA;
            int _VertexColorBlend;

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
                return output;
            }

            half4 Fragment (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 output = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;

                #if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                    float viewZ = -input.fogCoord;
                    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
                    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                #else
                    half fogFactor = 0;
                #endif
                output.rgb = MixFog(output.rgb, fogFactor);

                return output;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #define UNITY_SETUP_DOTS_SH_COEFFS
            #define UNITY_SETUP_DOTS_RENDER_BOUNDS
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _MultiplyRgbA;
            int _VertexColorBlend;

            CBUFFER_END

            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.UnlitGui"
}
