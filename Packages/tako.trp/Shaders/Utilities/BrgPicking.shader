// 元のシェーダーにPicking/SceneSelection用Passがない場合に、
// GPU Resident DrawerがEditorの選択描画へ使用するフォールバック。
Shader "Hidden/Trp/BrgPicking"
{
    // 描画には使用しないが、元マテリアルと同じ定数バッファレイアウトを維持し、
    // SRP Batcher互換のBRGフォールバックシェーダーとして扱えるよう宣言する。
    Properties
    {
        [HideInInspector] [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [HideInInspector] [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

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
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma multi_compile DOTS_INSTANCING_ON
            #pragma vertex Vertex
            #pragma fragment Fragment

            #define SCENEPICKINGPASS
            float4 _SelectionID;

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return unity_SelectionID;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma multi_compile DOTS_INSTANCING_ON
            #pragma vertex Vertex
            #pragma fragment Fragment

            #define SCENESELECTIONPASS
            int _ObjectId;
            int _PassValue;

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_ObjectId, _PassValue, 1.0h, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
