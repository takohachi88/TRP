Shader "TRP/PpllOit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", color) = (1, 1, 1, 0.5)

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
            Tags { "LightMode" = "PpllOit" }
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2

            #define UNITY_SETUP_DOTS_SH_COEFFS
            #define UNITY_SETUP_DOTS_RENDER_BOUNDS

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/PpllOit.hlsl"

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
            // slot 0はGather用カラーRTが使用するため、Pixel Shader UAVはu1から割り当てる。
            RWStructuredBuffer<PpllOitNode> _PpllOitNodes : register(u1);
            RWByteAddressBuffer _PpllOitHeads : register(u2);
            int _PpllOitMaxNodeCount;
            float4 _PpllOitTextureSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings Vertex(Attributes input)
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

            [earlydepthstencil]
            half4 Fragment(Varyings input, uint sampleIndex : SV_SampleIndex) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                clip(color.a - 1e-5h);

                #if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                    float viewZ = -input.fogCoord;
                    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
                    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                #else
                    half fogFactor = 0;
                #endif
                color.rgb = MixFog(color.rgb, fogFactor);

                // 画面全体で共有するcounterから、このフラグメント専用のNodeを確保する。
                uint nodeIndex = _PpllOitNodes.IncrementCounter();
                // 総容量を使い切った場合は、このフラグメントだけを安全に破棄する。
                if (nodeIndex >= (uint)_PpllOitMaxNodeCount)
                    return color;

                uint2 pixel = min(uint2(input.positionCS.xy), uint2(_PpllOitTextureSize.xy) - 1u);
                uint headAddress = PpllOitHeadAddress(pixel, (uint)_PpllOitTextureSize.x);
                uint previousHead;
                // 新しいNodeをリスト先頭へ挿入し、以前の先頭をnextとして保存する。
                _PpllOitHeads.InterlockedExchange(headAddress, nodeIndex, previousHead);

                PpllOitNode node;
                node.pixelColor = PackPpllOitColor(color);
                node.depthSampleIndex = PackPpllOitDepthSample(Linear01Depth(input.positionCS.z, _ZBufferParams), sampleIndex);
                node.next = previousHead;
                _PpllOitNodes[nodeIndex] = node;
                return color;
            }
            ENDHLSL
        }
    }
}
