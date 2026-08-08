Shader "TRP/PbrFilament"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // R: Metallic、G: Ambient Occlusion、A: Smoothness
        [NoScaleOffset] _MaskMap ("Mask Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(-3, 3)) = 1
        _ShadowNormalDistortion ("Shadow Normal Distortion", Range(0, 0.3)) = 0.05

        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        _SpecularIblStrength ("Specular IBL Strength", Range(0, 2)) = 1

        [Header(Hex Tiling)]
        [Toggle(HEX_TILING)] _HexTiling ("Hex Tiling", Float) = 0
        _HexTilingRotationStrength ("Rotation Strength", Range(0, 1)) = 0
        _HexTilingGain ("Blend Gain", Range(0.0001, 0.9999)) = 0.5

        [Toggle] _UseBurleyDiffuse ("Burley Diffuse", Float) = 0
        [Toggle(ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Vertex Sway)]
        [Toggle] _VertexSway ("Vertex Sway", Float) = 0
        _SwayAmplitude ("Sway Amplitude", Float) = 0.1
        _SwayPeriodScale ("Sway Period Scale", Float) = 1

        [Header(Common Settings)]
        [Toggle] _ZWrite ("Z Write", Int) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Int) = 2

        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", int) = 0
        [Toggle(MULTIPLY_RGB_A)] _MultiplyRgbA ("Multiply RGB A", int) = 0

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
            "RenderType" = "Opaque"
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

        HLSLINCLUDE
        #define UNITY_SETUP_DOTS_SH_COEFFS
        #define UNITY_SETUP_DOTS_RENDER_BOUNDS

        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/tako.trp/Shaders/LitInput.hlsl"
        #include "Packages/tako.trp/Shaders/PbrFilamentInput.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/PbrLighting.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Tiling.hlsl"

        struct PbrSurfaceSamples
        {
            half4 baseMap;
            half4 maskMap;
            half3 normalTS;
            half3 emission;
        };

        // Alpha Clip用パスでもForwardと同一のBase Mapタイリングを使用する。
        half4 SamplePbrBaseMap(float2 uv)
        {
            #if defined(HEX_TILING)
            return SampleHexTiledColor(
                uv,
                TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap),
                _HexTilingRotationStrength,
                _HexTilingGain);
            #else
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            #endif
        }

        // ContextとBase Map由来のウェイトを全マップで共有し、マテリアル情報の位置ずれを防ぐ。
        PbrSurfaceSamples SamplePbrSurface(float2 uv)
        {
            PbrSurfaceSamples samples;

            #if defined(HEX_TILING)
            const HexTilingContext context = GetHexTilingContext(uv, _HexTilingRotationStrength);
            half3 blendWeights;
            samples.baseMap = SampleHexTiledColor(context, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), _HexTilingGain, blendWeights);
            samples.maskMap = SampleHexTiledTexture(context, TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), blendWeights);
            // 法線は回転後の勾配から専用ウェイトを求め、タイルごとの向きを正しく合成する。
            samples.normalTS = SampleHexTiledNormal(context, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale, _HexTilingGain);
            samples.emission = SampleHexTiledTexture(context, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), blendWeights).rgb;
            #else
            samples.baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            samples.maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            samples.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
            samples.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
            #endif

            return samples;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "Lit" }


            ZWrite [_ZWrite]
            Cull [_Cull]
            Blend [_BlendSrc] [_BlendDst]
            BlendOp [_BlendOp]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma shader_feature_local_fragment HEX_TILING

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                GI_ATTRIBUTES
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : POSITION_WS;
                float2 uv : TEXCOORD0;
                GI_VARYINGS
                half3 normalWS : NORMAL;
                half3 tangentWS : TANGENT;
                half3 bitangentWS : BITANGENT;
                half3 directionVS : DIRECTION_VS;
                float fogCoord : FOG_COORD;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = ApplyPbrVertexSway(input.positionOS.xyz, input.color.r);
                VertexInputs vertexInputs = GetVertexInputs(positionOS, input.normalOS, input.tangentOS);
                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = vertexInputs.normalWS;
                output.tangentWS = vertexInputs.tangentWS;
                output.bitangentWS = vertexInputs.bitangentWS;
                output.directionVS = vertexInputs.directionVS;
                output.fogCoord = vertexInputs.positionVS.z;
                GI_TRANSFER(input, output);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                PbrSurfaceSamples surfaceSamples = SamplePbrSurface(input.uv);
                half4 baseMap = surfaceSamples.baseMap * _BaseColor;
                AlphaClip(baseMap.a, _Cutoff);

                half4 mask = surfaceSamples.maskMap;
                half metallic = saturate(mask.r * _Metallic);
                half smoothness = saturate(mask.a * _Smoothness);
                half ambientOcclusion = lerp(1.0h, mask.g, _OcclusionStrength);

                half3 geometryNormalWS = SafeNormalize(input.normalWS);
                half3x3 tangentToWorld = half3x3(SafeNormalize(input.tangentWS), SafeNormalize(input.bitangentWS), geometryNormalWS);
                half3 normalWS = SafeNormalize(mul(surfaceSamples.normalTS, tangentToWorld));
                // 影の輪郭そのものではなく、受影時の Normal Bias にだけ法線マップの凹凸を反映する。
                half3 shadowNormalWS = SafeNormalize(lerp(geometryNormalWS, normalWS, _ShadowNormalDistortion));
                half3 viewDirectionWS = SafeNormalize(-input.directionVS);

                PbrMaterialData material = CreatePbrMaterialData(baseMap.rgb, metallic, 1.0h - smoothness);

                half3 color = EvaluatePbrLighting(
                    material,
                    input.positionWS,
                    normalWS,
                    shadowNormalWS,
                    viewDirectionWS,
                    GI_FRAGMENT_UV(input),
                    ambientOcclusion,
                    _SpecularIblStrength,
                    _UseBurleyDiffuse > 0.5h,
                    input.positionCS.xy);

                half3 emission = surfaceSamples.emission * _EmissionColor.rgb;
                color += emission;
                color = MixFog(color, input.fogCoord);
                half4 output = half4(color, baseMap.a);
                MULTIPLY_RGB_A(output);
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma shader_feature_local_fragment HEX_TILING

            #if defined(ALPHA_CLIP)
            #define _ALPHATEST_ON
            #endif
            #define TRP_APPLY_VERTEX_DEFORMATION(positionOS, vertexColor) ApplyPbrVertexSway(positionOS, vertexColor.r)
            #define TRP_SAMPLE_BASE_MAP(uv) SamplePbrBaseMap(uv)
            #include "Packages/tako.trp/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma shader_feature_local_fragment HEX_TILING
            #define TRP_APPLY_VERTEX_DEFORMATION(positionOS, vertexColor) ApplyPbrVertexSway(positionOS, vertexColor.r)
            #define TRP_SAMPLE_BASE_MAP(uv) SamplePbrBaseMap(uv)
            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.PbrFilamentGui"
}
