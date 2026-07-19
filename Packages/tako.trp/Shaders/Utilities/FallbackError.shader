// GPU Resident Drawerが有効な描画を生成できない場合に使用するフォールバック。
// 表示方法を変更したい場合は、このFragmentを差し替える。
Shader "Hidden/Trp/FallbackError"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "Trp"
            "ShaderModel" = "4.5"
        }

        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(1.0h, 0.0h, 1.0h, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
