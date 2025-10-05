Shader "TRP/ParticleUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Rgb ("Rgb", float) = 1
        _A ("A", float) = 1

        [Toggle(_SOFT_PARTICLE)] _SOFT_PARTICLE ("Soft Particle", float) = 0
        _Near ("Near", float) = 0
        _Far ("Far", float) = 1

        [Header(Common Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", int) = 0
        [Toggle(MULTIPLY_RGB_A)] _MultiplyRgbA ("Multiply RGB A", int) = 1
        [PerRendererData] _AlphaBlend ("Alpha Blend", int) = 3
        [PerRendererData] _VertexColorBlend ("Vertex Color Blend", int) = 0
        
        [Toggle] _ZWrite ("Z Write", int) = 0
        
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", int) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
            "Queue" = "Transparent"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Blend [_BlendSrc][_BlendDst]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma shader_feature_local _SOFT_PARTICLE

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/DepthFade.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float fogCoord : TEXCOORD1;
                float4 positionNDC : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseMap_ST;
            half _Rgb;
            half _A;
            float _Near;
            float _Far;
            half _MultiplyRgbA;
            int _VertexColorBlend;

            CBUFFER_END


            Varyings Vertex (Attributes input)
            {
                Varyings output;
                VertexInputs vertexInput = GetVertexInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.color.rgb *= _Rgb;
                output.color.a *= _A;
                output.fogCoord = vertexInput.positionVS.z;
                output.positionNDC = vertexInput.positionNDC;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 Fragment (Varyings input) : SV_Target
            {
                half4 output = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;

                #if defined(_SOFT_PARTICLE)
                output.a *= DepthFade(_Near, _Far, input.positionNDC, input.positionWS);
                #endif

                #if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                    float viewZ = -input.fogCoord;
                    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
                    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                #else
                    half fogFactor = 0;
                #endif
                output.rgb = MixFog(output.rgb, fogFactor);

                VERTEX_COLOR_BLEND(output, input.color);
                MULTIPLY_RGB_A(output);

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
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma multi_compile_instancing
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseMap_ST;
            half _Intensity;
            half _AlphaMultiplier;
            float _Near;
            float _Far;
            half _MultiplyRgbA;
            int _VertexColorBlend;

            CBUFFER_END

            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.ParticleUnlitGui"
}
