Shader "TRP/Hidden/PpllOitResolve"
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
                        while (insertionIndex > 0u &&
                               (fragments[insertionIndex - 1u].depthSampleIndex >> 8u) < packedDepth)
                        {
                            fragments[insertionIndex] = fragments[insertionIndex - 1u];
                            insertionIndex--;
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
