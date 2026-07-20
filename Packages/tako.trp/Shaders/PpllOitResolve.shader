Shader "Hidden/Trp/PpllOitResolve"
{
    SubShader
    {
        Tags { "RenderPipeline" = "Trp" }
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "PpllOitResolve"

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/PpllOit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define PPLL_OIT_MAX_FRAGMENTS_PER_PIXEL 32

            ByteAddressBuffer _PpllOitHeads;
            StructuredBuffer<PpllOitNode> _PpllOitNodes;
            float4 _PpllOitTextureSize;

            half4 Fragment(Varyings input, uint sampleIndex : SV_SampleIndex) : SV_Target
            {
                uint2 pixel = min(uint2(input.positionCS.xy), uint2(_PpllOitTextureSize.xy) - 1u);
                uint headAddress = PpllOitHeadAddress(pixel, (uint)_PpllOitTextureSize.x);
                uint nodeIndex = _PpllOitHeads.Load(headAddress);

                [branch]
                if (nodeIndex == 0u)
                    return 0.0h;

                PpllOitNode fragments[PPLL_OIT_MAX_FRAGMENTS_PER_PIXEL];
                uint fragmentCount = 0u;

                [loop]
                while (nodeIndex != 0u && fragmentCount < PPLL_OIT_MAX_FRAGMENTS_PER_PIXEL)
                {
                    PpllOitNode node = _PpllOitNodes[nodeIndex];
                    nodeIndex = node.next;

                    if (UnpackPpllOitSampleIndex(node.depthSampleIndex) == sampleIndex)
                    {
                        // Insert while traversing so no second sorting pass is needed.
                        uint packedDepth = node.depthSampleIndex >> 8u;
                        uint insertionIndex = fragmentCount;
                        // ローカル配列を動的インデックスで移動するため、最大回数を固定して展開する。
                        // 配列参照をループ条件に含めるとDX12で範囲外と判定されるため、本体内で評価する。
                        [unroll(PPLL_OIT_MAX_FRAGMENTS_PER_PIXEL)]
                        for (uint sortStep = 0u; sortStep < PPLL_OIT_MAX_FRAGMENTS_PER_PIXEL; sortStep++)
                        {
                            if (insertionIndex == 0u)
                                break;

                            uint previousIndex = insertionIndex - 1u;
                            if ((fragments[previousIndex].depthSampleIndex >> 8u) >= packedDepth)
                                break;

                            fragments[insertionIndex] = fragments[previousIndex];
                            insertionIndex = previousIndex;
                        }
                        fragments[insertionIndex] = node;
                        fragmentCount++;
                    }
                }

                // Produce premultiplied color; fixed-function blending composites it over camera color.
                float4 color = 0.0;
                [loop]
                for (uint i = 0u; i < fragmentCount; i++)
                {
                    float4 fragmentColor = UnpackPpllOitColor(fragments[i].pixelColor);
                    color.rgb = fragmentColor.rgb * fragmentColor.a + color.rgb * (1.0 - fragmentColor.a);
                    color.a = fragmentColor.a + color.a * (1.0 - fragmentColor.a);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
