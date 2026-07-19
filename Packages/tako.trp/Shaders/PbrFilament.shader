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
        _ShadowNormalDistortion ("Shadow Normal Distortion", Range(0, 1)) = 0

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

        [Header(Common Settings)]
        [Toggle] _ZWrite ("Z Write", Int) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Int) = 2

        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 0

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
        #include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Pbr.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Tiling.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

        TEXTURECUBE(unity_SpecCube0);
        SAMPLER(samplerunity_SpecCube0);

        #ifndef UNITY_SPECCUBE_LOD_STEPS
        #define UNITY_SPECCUBE_LOD_STEPS 6
        #endif

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
                half fogFactor : FOG_FACTOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexInputs vertexInputs = GetVertexInputs(input.positionOS.xyz, input.normalOS, input.tangentOS);
                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = vertexInputs.normalWS;
                output.tangentWS = vertexInputs.tangentWS;
                output.bitangentWS = vertexInputs.bitangentWS;
                output.directionVS = vertexInputs.directionVS;
                output.fogFactor = vertexInputs.fogFactor;
                GI_TRANSFER(input, output);
                return output;
            }

            half3 SampleSpecularIbl(PbrMaterialData material, half3 normalWS, half3 viewDirectionWS, half ambientOcclusion)
            {
                half noV = saturate(dot(normalWS, viewDirectionWS));
                half3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);
                half mipLevel = material.perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;
                half4 encodedIbl = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionDirectionWS, mipLevel);
                half3 radiance = DecodeHDREnvironment(encodedIbl, unity_SpecCube0_HDR);
                half3 environmentBrdf = ApproximateEnvironmentBrdf(material, noV);
                half specularOcclusion = ComputeSpecularOcclusion(noV, ambientOcclusion, material.roughness);
                return radiance * environmentBrdf * specularOcclusion * _SpecularIblStrength;
            }

            half3 EvaluateDirectionalLights(PbrMaterialData material, float3 positionWS, half3 normalWS, half3 shadowNormalWS, half3 viewDirectionWS, half cascadeIndex)
            {
                half3 lighting = 0;

                for (int i = 0; i < _DirectionalLightCount; i++)
                {
                    DirectionalLight light = GetDirectionalLight(i);
                    half3 cookie = 1;
                    if (0 <= light.cookieIndex)
                    {
                        cookie = SampleDirectionalLightCookie(light.cookieIndex, positionWS);
                    }

                    half attenuation = 1;
                    if (0 < light.attenuation)
                    {
                        attenuation = GetDirectionalShadow(cascadeIndex, positionWS, shadowNormalWS, light.normalBias, light.shadowMapTileStartIndex);
                    }

                    lighting += EvaluatePbrDirect(material, normalWS, viewDirectionWS, light.direction, _UseBurleyDiffuse > 0.5h) * light.color * cookie * attenuation;
                }

                return lighting;
            }

            half3 EvaluatePunctualLight(int lightIndex, PbrMaterialData material, float3 positionWS, half3 normalWS, half3 shadowNormalWS, half3 viewDirectionWS)
            {
                PunctualLight light = GetPunctualLight(lightIndex);
                float3 surfaceToLight = light.position - positionWS;
                float distanceSquared = max(dot(surfaceToLight, surfaceToLight), 0.00001);
                half3 lightDirectionWS = surfaceToLight * rsqrt(distanceSquared);

                // 逆二乗則に、ライト範囲端で滑らかに0になるFilament型の減衰を組み合わせる。
                half rangeFactor = saturate(1.0h - Pow2(distanceSquared * light.rangeInverseSquare));
                half rangeAttenuation = Pow2(rangeFactor) * rcp(distanceSquared);
                half spotAttenuation = saturate(dot(light.direction, lightDirectionWS) * light.spotAngles.x + light.spotAngles.y);
                spotAttenuation = Pow2(spotAttenuation);

                half shadowAttenuation = 1;
                if (0 < light.attenuation)
                {
                    shadowAttenuation = GetPunctualShadow(positionWS, shadowNormalWS, light.position, light.direction, lightDirectionWS, light.type, light.shadowMapTileStartIndex);
                }

                half3 cookie = 1;
                if (0 <= light.cookieIndex)
                {
                    cookie = SamplePunctualLightCookie(light.cookieIndex, positionWS, light.type);
                }

                return EvaluatePbrDirect(material, normalWS, viewDirectionWS, lightDirectionWS, _UseBurleyDiffuse > 0.5h)
                    * light.color
                    * cookie
                    * rangeAttenuation
                    * spotAttenuation
                    * shadowAttenuation;
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

                // Lightmap/Light ProbeにはLambert積分が既に含まれるため、ここではINV_PIを重ねない。
                half3 color = Gi(normalWS, GI_FRAGMENT_UV(input)) * material.diffuseColor * ambientOcclusion;
                color += SampleSpecularIbl(material, normalWS, viewDirectionWS, ambientOcclusion);

                half dither = InterleavedGradientNoise(input.positionCS.xy, 0);
                half cascadeIndex = ComputeCascadeIndex(input.positionWS, dither);
                color += EvaluateDirectionalLights(material, input.positionWS, normalWS, shadowNormalWS, viewDirectionWS, cascadeIndex);

                if (0 < _PunctualLightCount)
                {
                    float2 screenUv = input.positionCS.xy * _AttachmentSize.xy;
                    ForwardPlusTile tile = GetForwardPlusTile(screenUv);
                    int lastIndex = tile.GetLastLightIndexInTile();
                    for (int i = tile.GetFirstLightIndexInTile(); i <= lastIndex; i++)
                    {
                        color += EvaluatePunctualLight(tile.GetLightIndex(i), material, input.positionWS, normalWS, shadowNormalWS, viewDirectionWS);
                    }
                }

                half3 emission = surfaceSamples.emission * _EmissionColor.rgb;
                color += emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, baseMap.a);
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
            #define TRP_SAMPLE_BASE_MAP(uv) SamplePbrBaseMap(uv)
            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
