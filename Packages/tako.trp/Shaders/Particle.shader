Shader "TRP/Particle"
{
    Properties
    {
        [KeywordEnum(NORMAL, POLAR)] _BASEUVMODE ("Base UV Mode", int) = 0
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Rgb ("Rgb", float) = 1
        _A ("A", float) = 1

        [Toggle(_SOFT_PARTICLE)] _SOFT_PARTICLE ("Soft Particle", float) = 0
        _Near ("Near", float) = 0
        _Far ("Far", float) = 1

        [KeywordEnum(OFF, NORMAL, POLAR)] _DISTORTMODE ("Distort Mode", int) = 0
        _DistortMap ("Distort Map", 2D) = "white" {}

        [KeywordEnum(OFF, NORMAL, POLAR)] _DISSOLVEMODE ("Dissolve Mode", int) = 0
        _DissolveMap ("Dissolve Map", 2D) = "white" {}
        [Toggle] _DistortDissolve ("Distort Dissolve", int) = 0
        _DissolveSmooth ("Dissolve Smooth", Range(0, 1)) = 0.1
        [Enum(Overwrite, 0, Add, 1, Multiply, 2)] _EdgeColorBlendMode ("Edge Color Blend Mode", int) = 0

        [Toggle(_LIT)] _LIT ("Lit", int) = 0
        // R: Metallic、G: Ambient Occlusion、A: Smoothness
        [NoScaleOffset] _MaskMap ("Mask Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [NoScaleOffset] [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(-3, 3)) = 1

        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

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

        HLSLINCLUDE
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/tako.trp/Shaders/LitInput.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/DepthFade.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Vfx.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/PbrLighting.hlsl"

        // Shuriken は SRP Batcher 対象外のため、マテリアル値は CBUFFER に格納しない。
        float4 _BaseMap_ST;
        half _Rgb;
        half _A;
        float _Near;
        float _Far;
        half _MultiplyRgbA;
        int _VertexColorBlend;
        int _AlphaBlend;
        half _Cutoff;

        TEXTURE2D(_DissolveMap);
        SAMPLER(sampler_DissolveMap);
        float4 _DissolveMap_ST;
        half _DissolveSmooth;
        int _EdgeColorBlendMode;

        TEXTURE2D(_DistortMap);
        SAMPLER(sampler_DistortMap);
        float4 _DistortMap_ST;
        half _DistortDissolve;

        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        half _Metallic;
        half _Smoothness;
        half _OcclusionStrength;
        half _BumpScale;

        #define TRANSFORM_TEX_SCROLL(uv, st, time) ((uv) * (st).xy + (st).zw * (time))

        struct ParticleSurfaceSamples
        {
            half4 baseColor;
            half baseAlpha;
            float2 materialUv;
            half dissolveAlpha;
        };

        // Forward、ShadowCaster、DepthNormals で同一の UV・歪み・ディゾルブ結果を使用する。
        ParticleSurfaceSamples SampleParticleSurface(float4 uv0, float4 uv1, float4 uv2)
        {
            const float2 uv = uv0.xy;
            const float time = uv0.z;
            const float distortStrength = uv0.w;
            const float dissolveProgress = uv1.x;
            const float edgeWidth = uv1.y;
            const half4 edgeColor = half4(uv1.zw, uv2.xy);

			//Distort
            float2 distort = 0;
            #if defined(_DISTORTMODE_NORMAL)
            float2 uvDistort = TRANSFORM_TEX_SCROLL(uv, _DistortMap_ST, time);
            #elif defined(_DISTORTMODE_POLAR)
            float2 uvDistort = Polar(uv - 0.5, _DistortMap_ST.xy, _DistortMap_ST.zw * time);
            #endif
            #if defined(_DISTORTMODE_NORMAL) || defined(_DISTORTMODE_POLAR)
            distort = Distort(TEXTURE2D_ARGS(_DistortMap, sampler_DistortMap), uvDistort, distortStrength);
            #endif

			//BaseMap
            float2 uvBase = uv;
            #if defined(_BASEUVMODE_NORMAL)
            uvBase = TRANSFORM_TEX_SCROLL(uv, _BaseMap_ST, time);
            #elif defined(_BASEUVMODE_POLAR)
            uvBase = Polar(uv - 0.5, _BaseMap_ST.xy, _BaseMap_ST.zw * time);
            #endif

            ParticleSurfaceSamples samples;
            samples.materialUv = uvBase + distort;
            samples.baseColor = 1;
            samples.dissolveAlpha = 1;
            #if defined(_BASEUVMODE_NORMAL) || defined(_BASEUVMODE_POLAR)
            samples.baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, samples.materialUv);
            #endif
            samples.baseAlpha = samples.baseColor.a;

			//Dissolve
            #if defined(_DISSOLVEMODE_NORMAL)
            float2 uvDissolve = TRANSFORM_TEX_SCROLL(uv, _DissolveMap_ST, time);
            #elif defined(_DISSOLVEMODE_POLAR)
            float2 uvDissolve = Polar(uv - 0.5, _DissolveMap_ST.xy, _DissolveMap_ST.zw * time);
            #endif
            #if defined(_DISSOLVEMODE_NORMAL) || defined(_DISSOLVEMODE_POLAR)
            if (_DistortDissolve) uvDissolve += distort;
            samples.baseColor = Dissolve(
                TEXTURE2D_ARGS(_DissolveMap, sampler_DissolveMap),
                uvDissolve,
                samples.baseColor,
                dissolveProgress,
                _DissolveSmooth,
                edgeWidth,
                edgeColor,
                _EdgeColorBlendMode,
                samples.dissolveAlpha);
            #endif

            return samples;
        }

        // AlphaTest は feather をディザリングへ変換し、Opaque はディゾルブ境界を硬く切る。
        void ParticleAlphaClip(half baseAlpha, half dissolveAlpha, half vertexAlpha, float2 positionPx)
        {
            #if defined(_ALPHATEST)
            const half dither = InterleavedGradientNoise(positionPx, 0);
            // Base Map は Cutoff、ディゾルブとパーティクルのフェードはディザリングで判定する。
            clip(baseAlpha - _Cutoff);
            clip(saturate(dissolveAlpha * vertexAlpha) - dither);
            #else
            if (_AlphaBlend == 1)
            {
                clip(dissolveAlpha - 0.0001h);
            }
            #endif
        }

        half3 GetParticleNormalWS(
            float2 uv,
            half3 normalWS,
            half3 tangentWS,
            half3 bitangentWS)
        {
            half3 geometryNormalWS = SafeNormalize(normalWS);
            #if defined(_LIT)
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
            half3x3 tangentToWorld = half3x3(
                SafeNormalize(tangentWS),
                SafeNormalize(bitangentWS),
                geometryNormalWS);
            return SafeNormalize(mul(normalTS, tangentToWorld));
            #else
            return geometryNormalWS;
            #endif
        }

        half3 EvaluateParticleLighting(
            half3 baseColor,
            float2 materialUv,
            float3 positionWS,
            half3 normalWS,
            half3 tangentWS,
            half3 bitangentWS,
            half3 directionVS,
            float2 positionPx)
        {
            half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, materialUv);
            half metallic = saturate(mask.r * _Metallic);
            half smoothness = saturate(mask.a * _Smoothness);
            half ambientOcclusion = lerp(1.0h, mask.g, _OcclusionStrength);

            half3 surfaceNormalWS = GetParticleNormalWS(
                materialUv,
                normalWS,
                tangentWS,
                bitangentWS);
            half3 viewDirectionWS = SafeNormalize(-directionVS);
            PbrMaterialData material = CreatePbrMaterialData(baseColor, metallic, 1.0h - smoothness);
            return EvaluatePbrLighting(
                material,
                positionWS,
                surfaceNormalWS,
                surfaceNormalWS,
                viewDirectionWS,
                0,
                ambientOcclusion,
                1.0h,
                false,
                positionPx);
        }
        ENDHLSL

        Pass
        {
            Blend [_BlendSrc][_BlendDst]
            BlendOp [_BlendOp]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma shader_feature_local_fragment _SOFT_PARTICLE
            #pragma shader_feature_local_fragment _ALPHATEST
            #pragma shader_feature_local_fragment _BASEUVMODE_NORMAL _BASEUVMODE_POLAR
            #pragma shader_feature_local_fragment _ _DISTORTMODE_NORMAL _DISTORTMODE_POLAR
            #pragma shader_feature_local_fragment _ _DISSOLVEMODE_NORMAL _DISSOLVEMODE_POLAR
            #pragma shader_feature_local_fragment _LIT

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half4 color : COLOR;
                float fogCoord : TEXCOORD3;
                float4 positionNDC : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
                half3 normalWS : TEXCOORD6;
                half3 tangentWS : TEXCOORD7;
                half3 bitangentWS : TEXCOORD8;
                half3 directionVS : TEXCOORD9;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexInputs vertexInput = GetVertexInputs(input.positionOS.xyz, input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;
                output.color.rgb *= _Rgb;
                output.color.a *= _A;
                output.fogCoord = vertexInput.positionVS.z;
                output.positionNDC = vertexInput.positionNDC;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = vertexInput.normalWS;
                output.tangentWS = vertexInput.tangentWS;
                output.bitangentWS = vertexInput.bitangentWS;
                output.directionVS = vertexInput.directionVS;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                ParticleSurfaceSamples surface = SampleParticleSurface(input.uv0, input.uv1, input.uv2);
                half4 output = surface.baseColor;

                // 頂点色は照明前に一度だけ適用し、PBR のベースカラーにも反映する。
                VERTEX_COLOR_BLEND(output, input.color);
                ParticleAlphaClip(surface.baseAlpha, surface.dissolveAlpha, input.color.a, input.positionCS.xy);

                #if defined(_LIT)
                output.rgb = EvaluateParticleLighting(
                    output.rgb,
                    surface.materialUv,
                    input.positionWS,
                    input.normalWS,
                    input.tangentWS,
                    input.bitangentWS,
                    input.directionVS,
                    input.positionCS.xy);
                #endif

                const float2 positionSS = input.positionNDC.xy * rcp(input.positionNDC.w);
                #if defined(_SOFT_PARTICLE)
                output.a *= DepthFade(_Near, _Far, positionSS, input.positionWS);
                #endif

                output.rgb = MixFog(output.rgb, input.fogCoord);

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
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local_fragment _ALPHATEST
            #pragma shader_feature_local_fragment _BASEUVMODE_NORMAL _BASEUVMODE_POLAR
            #pragma shader_feature_local_fragment _DISTORTMODE_NORMAL _DISTORTMODE_POLAR
            #pragma shader_feature_local_fragment _DISSOLVEMODE_NORMAL _DISSOLVEMODE_POLAR

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half4 color : COLOR;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half alpha : TEXCOORD3;
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Shadow pancaking により、シャドウマップの有効な深度範囲へ収める。
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.alpha = input.color.a * _A;
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                ParticleSurfaceSamples surface = SampleParticleSurface(input.uv0, input.uv1, input.uv2);
                ParticleAlphaClip(surface.baseAlpha, surface.dissolveAlpha, input.alpha, input.positionCS.xy);
                return 0;
            }
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
            #pragma shader_feature_local_fragment _ALPHATEST
            #pragma shader_feature_local_fragment _BASEUVMODE_NORMAL _BASEUVMODE_POLAR
            #pragma shader_feature_local_fragment _DISTORTMODE_NORMAL _DISTORTMODE_POLAR
            #pragma shader_feature_local_fragment _DISSOLVEMODE_NORMAL _DISSOLVEMODE_POLAR
            #pragma shader_feature_local_fragment _LIT

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half4 color : COLOR;
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                half alpha : TEXCOORD3;
                half3 normalWS : TEXCOORD4;
                half3 tangentWS : TEXCOORD5;
                half3 bitangentWS : TEXCOORD6;
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                VertexInputs vertexInput = GetVertexInputs(input.positionOS.xyz, input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.alpha = input.color.a * _A;
                output.normalWS = vertexInput.normalWS;
                output.tangentWS = vertexInput.tangentWS;
                output.bitangentWS = vertexInput.bitangentWS;
                return output;
            }

            void DepthNormalsFragment(
                DepthNormalsVaryings input,
                out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                ParticleSurfaceSamples surface = SampleParticleSurface(input.uv0, input.uv1, input.uv2);
                ParticleAlphaClip(surface.baseAlpha, surface.dissolveAlpha, input.alpha, input.positionCS.xy);

                half3 normalWS = GetParticleNormalWS(
                    surface.materialUv,
                    input.normalWS,
                    input.tangentWS,
                    input.bitangentWS);
                outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0h);

                #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.ParticleGui"
}
