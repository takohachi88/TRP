Shader "TRP/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor][HDR] _BaseColor ("Base Color", color) = (1, 1, 1, 1)
        [Toggle(ALPHA_CLIP)] _AlphaClip("Alpha Clip", int) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        //R:RimLightStrength
        //G:
        //B:
        //A:
        [MainTexture][NoScaleOffset] _ControlMap1 ("Control Map 1", 2D) = "white" {}
        
        _ShadowThreshold1 ("Shadow Threshold 1", Range(0, 1)) = 0.2
        _ShadowThreshold2 ("Shadow Threshold 2", Range(0, 1)) = 0.3
        _ShadowThreshold3 ("Shadow Threshold 3", Range(0, 1)) = 0.5
        _ShadowSmoothness1 ("Shadow Smoothness 1", Range(0.001, 0.5)) = 0.1
        _ShadowSmoothness2 ("Shadow Smoothness 2", Range(0.001, 0.5)) = 0.1
        _ShadowSmoothness3 ("Shadow Smoothness 3", Range(0.001, 0.5)) = 0.1
        _ShadowColor1 ("Shadow Color 1", color) = (0.7, 0.7, 0.7, 1)
        _ShadowColor2 ("Shadow Color 2", color) = (0.6, 0.6, 0.6, 1)
        _ShadowColor3 ("Shadow Color 3", color) = (0.3, 0.3, 0.3, 1)
        _LightEffect ("Light Effect", Range(0, 15)) = 1
        [Toggle(PUNCTUAL_LIGHT_IS_TOON)] PUNCTUAL_LIGHT_IS_TOON ("Punctual Light Is Toon", int) = 1

        [Toggle(RIM_LIGHT)] RIM_LIGHT ("Rim Light", int) = 0
        [HDR] _RimLightColor ("Rim Light Color", color) = (1, 1, 1, 1)
        _RimLightStrength ("Rim Light Strength", float) = 1
        _RimLightWidth ("Rim Light Width", Range(0, 1)) = 0.2
        _RimLightSmoothness ("Rim Light Smoothness", Range(0, 1)) = 0.2

        [Toggle(OUTLINE_SINGLE_COLOR)] OUTLINE_SINGLE_COLOR ("Outline Single Color", int) = 0
        [HDR] _OutlineColor ("Outline Color", color) = (0.5, 0.5, 0.5, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.01
        _OutlineLightStrength ("Outline Light Strength", Range(0, 10)) = 1
        _OutlineLightStrengthThreshold ("Outline Light Strength Threshold", Range(0, 4)) = 0.1
        [Toggle(OUTLINE_SOFT_EDGE)] _OUTLINE_SOFT_EDGE ("Outlien Soft Edge", int) = 0

        [Toggle(TOON_PUNCTUAL_LIGHT)] TOON_PUNCTUAL_LIGHT ("Toon Punctual Light", int) = 0

        [Header(Common Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 0
        
        [Toggle] _ZWrite ("Z Write", int) = 1
        
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", int) = 2

        _Stencil ("Stencil ID", int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp("Stencil Operation", int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", int) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail", int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil Z Fail", int) = 0
    }

    SubShader
    {
        HLSLINCLUDE

        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/tako.trp/Shaders/ToonInput.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Lighting.hlsl"

        ENDHLSL

        Tags
        {
            "RenderPipeline" = "Trp"
            "Queue" = "Geometry"
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

        Pass
        {
            Tags
            {
                "LightMode" = "Lit"
            }

            Blend [_BlendSrc][_BlendDst]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local PUNCTUAL_LIGHT_IS_TOON

            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2SA
            #pragma shader_feature_local_fragment TOON_PUNCTUAL_LIGHT
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma shader_feature_local_fragment RIM_LIGHT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                GI_ATTRIBUTES
                half3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                GI_VARYINGS
                half3 normalWS : NORMAL;
                half4 color : COLOR;
                float fogFactor : FOG_FACTOR;
                float3 positionWS : POSITION_WS;
                half3 directionVS : DIRECTION_VS;
            };

            Varyings Vertex (Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexInputs vertexInputs = GetVertexInputs(input.positionOS.xyz, input.normalOS);
                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                GI_TRANSFER(input, output);
                output.color = input.color;
                output.fogFactor = vertexInputs.fogFactor;
                output.normalWS = vertexInputs.normalWS;
                output.directionVS = vertexInputs.directionVS;

                return output;
            }

            half4 Fragment (Varyings input) : SV_Target
            {
                half4 output = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearClamp, input.uv);

                AlphaClip(output.a, _Cutoff);
                
                half4 control1 = SAMPLE_TEXTURE2D(_ControlMap1, sampler_ControlMap1, input.uv);
                float3 positionWS = input.positionWS;
                half3 normalWS = SafeNormalize(input.normalWS);
                half3 directionVS = SafeNormalize(input.directionVS);
                half rimStrength = control1.x * _RimLightStrength;
                float2 screenUv = input.positionCS.xy * _AttachmentSize.xy;
                half dither = InterleavedGradientNoise(input.positionCS.xy, 0);

                output.rgb = ToonLighting(positionWS, normalWS, output.rgb, screenUv, dither);

                #if defined(RIM_LIGHT)
                output.rgb += RimLight(normalWS, directionVS, rimStrength, _RimLightWidth, _RimLightSmoothness, _RimLightColor);
                #endif

                output *= _BaseColor;

                output.rgb = MixFog(output.rgb, input.fogFactor);

                return output;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode" = "Outline"
            }

            ZWrite On
            Cull Front

            HLSLPROGRAM
            
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2SA
            #pragma shader_feature_local_fragment ALPHA_CLIP
            #pragma shader_feature_local_fragment OUTLINE_SINGLE_COLOR
            #pragma shader_feature_local_vertex OUTLINE_SOFT_EDGE

            #include "Packages/tako.trp/Shaders/OutlinePass.hlsl"
            
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/Shaders/ShadowCasterPass.hlsl"
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
            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
