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
		[Toggle] _DistortDissolve ("Distort Dissolve", float) = 1
		_DissolveSmooth ("Dissolve Smooth", Range(0, 1)) = 0.1
		[Enum(Overwrite, 0, Add, 1, Multiply, 2)] _EdgeColorBlendMode ("Edge Color Blend Mode", int) = 0

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

        Pass
        {
            Blend [_BlendSrc][_BlendDst]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma shader_feature_local_fragment _SOFT_PARTICLE
			#pragma shader_feature_local _ALPHATEST
			#pragma shader_feature_local_fragment _BASEUVMODE_NORMAL _BASEUVMODE_POLAR
			#pragma shader_feature_local_fragment _DISTORTMODE_NODE _DISTORTMODE_NORMAL _DISTORTMODE_POLAR
			#pragma shader_feature_local_fragment _DISSOLVEMODE_NODE _DISSOLVEMODE_NORMAL _DISSOLVEMODE_POLAR

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/DepthFade.hlsl"
            #include "Packages/tako.trp/ShaderLibrary/Vfx.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
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
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

			// ShurikenはSRPBatcher無効なのでCBUFFERにする意味ない。
            float4 _BaseMap_ST;
            half _Rgb;
            half _A;
            float _Near;
            float _Far;
            half _MultiplyRgbA;
            int _VertexColorBlend;

			TEXTURE2D(_DissolveMap);
			SAMPLER(sampler_DissolveMap);
			float4 _DissolveMap_ST;

			TEXTURE2D(_DistortMap);
			SAMPLER(sampler_DistortMap);
			half _DistortDissolve;
			float4 _DistortMap_ST;
			half _DissolveSmooth;
			int _EdgeColorBlendMode;

            Varyings Vertex (Attributes input)
            {
                Varyings output;
                VertexInputs vertexInput = GetVertexInputs(input.positionOS.xyz);
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
                return output;
            }

			#define TRANSFORM_TEX_SCROLL(uv, st, time) ((uv) * (st).xy + (st).zw * (time))

            half4 Fragment (Varyings input) : SV_Target
            {
				const float2 uv = input.uv0.xy;
				const float time = input.uv0.z;
				const float distortStrength = input.uv0.w;
				const float dissolveProgress = input.uv1.x;
				const float edgeWidth = input.uv1.y;
				const half4 edgeColor = half4(input.uv1.zw, input.uv2.xy);

				const float2 positionSS = input.positionNDC.xy / input.positionNDC.w;
				const half dither = InterleavedGradientNoise(positionSS * _ScreenParams.xy, 0);
				
				half4 output = 1;

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

				//BaseColor
				float2 uvBase = uv;
				#if defined(_BASEUVMODE_NORMAL)
				uvBase = TRANSFORM_TEX_SCROLL(uv, _BaseMap_ST, time);
				#elif defined(_BASEUVMODE_POLAR)
				uvBase = Polar(uv - 0.5, _BaseMap_ST.xy, _BaseMap_ST.zw * time);
				#endif
				#if defined(_BASEUVMODE_NORMAL) || defined(_BASEUVMODE_POLAR)
                output = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase + distort);
				#endif

				//Dissolve
				#if defined(_DISSOLVEMODE_NORMAL)
				float2 uvDissolve = TRANSFORM_TEX_SCROLL(uv, _DissolveMap_ST, time);
				#elif defined(_DISSOLVEMODE_POLAR)
				float2 uvDissolve = Polar(uv - 0.5, _DissolveMap_ST.xy, _DissolveMap_ST.zw * time);
				#endif
				#if defined(_DISSOLVEMODE_NORMAL) || defined(_DISSOLVEMODE_POLAR)
				if(_DistortDissolve) uvDissolve += distort;
				output = Dissolve(TEXTURE2D_ARGS(_DissolveMap, sampler_DissolveMap), uvDissolve, output, dissolveProgress, _DissolveSmooth, edgeWidth, edgeColor, _EdgeColorBlendMode);
				#endif

				//AlphaTest
				#if defined(_ALPHATEST)
				clip(output.a * input.color.a - dither);
				#endif

				output *= input.color;

				#if defined(_SOFT_PARTICLE)
                output.a *= DepthFade(_Near, _Far, positionSS, input.positionWS);
                #endif

                output.rgb = MixFog(output.rgb, input.fogCoord);

                VERTEX_COLOR_BLEND(output, input.color);
                MULTIPLY_RGB_A(output);

                return output;
            }

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _BaseMap_ST;

            #include "Packages/tako.trp/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.ParticleGui"
}
