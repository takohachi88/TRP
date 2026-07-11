Shader "TRP/Samples/Map Chip GPU Resident Benchmark"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _GlobalTextureScale ("Global Texture Scale", Float) = 0.25
        _WindAmplitude ("Vertex Color Wind Amplitude", Range(0, 1)) = 0.08
        _WindFrequency ("Wind Frequency", Float) = 1.5
        _WindDirection ("Wind Direction (XZ)", Vector) = (1, 0, 0, 0)
        [Toggle(MAPCHIP_GLOBAL_TEXTURE)] _UseGlobalTexture ("Use World-space Global Texture", Float) = 1
        [Toggle(MAPCHIP_VERTEX_WIND)] _UseVertexWind ("Use Vertex-color Wind", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Tags { "LightMode" = "Lit" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            // GPU Resident DrawerはDOTS Instancingのバリアントを使う。
            // Raw Bufferを使うため、ベンチマーク対象はSM 4.5以上に限定する。
            #pragma target 4.5
            #pragma exclude_renderers gles
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature_local MAPCHIP_GLOBAL_TEXTURE
            #pragma shader_feature_local MAPCHIP_VERTEX_WIND

            // 本サンプルはライトプローブとRender Boundsのカスタム処理を使わない。
            // UnityInstancing.hlslがDOTS Instancingセットアップ時に呼ぶフックを空実装する。
            #define UNITY_SETUP_DOTS_SH_COEFFS
            #define UNITY_SETUP_DOTS_RENDER_BOUNDS

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ワールド座標で共有するテクスチャ。MaterialPropertyBlockを使わず、
            // Shader.SetGlobalTextureで設定するためGRDの対象から外れない。
            TEXTURE2D(_MapChipGlobalTexture);
            SAMPLER(sampler_MapChipGlobalTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _GlobalTextureScale;
                float _WindAmplitude;
                float _WindFrequency;
                float4 _WindDirection;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // UNITY_MATRIX_MはDOTS Instancing時にGRDのfloat3x4行列を展開する。
                float3 positionWS = mul(UNITY_MATRIX_M, input.positionOS).xyz;

                #if defined(MAPCHIP_VERTEX_WIND)
                    // 頂点カラーAを揺れの重みとして使用する。インスタンス固有値ではないため、
                    // この頂点変形はGPU Resident Drawerのバッチングと両立する。
                    float windPhase = dot(positionWS.xz, float2(0.17, 0.23)) + _Time.y * _WindFrequency;
                    positionWS.xz += normalize(_WindDirection.xz) * (sin(windPhase) * _WindAmplitude * input.color.a);
                #endif

                output.positionWS = positionWS;
                output.positionCS = mul(unity_MatrixVP, float4(positionWS, 1.0));
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 color = _BaseColor.rgb * input.color.rgb;
                #if defined(MAPCHIP_GLOBAL_TEXTURE)
                    // マップ全体で共有するワールド座標テクスチャの参照例。
                    float2 globalUv = frac(input.positionWS.xz * _GlobalTextureScale);
                    color *= SAMPLE_TEXTURE2D(_MapChipGlobalTexture, sampler_MapChipGlobalTexture, globalUv).rgb;
                #endif
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
