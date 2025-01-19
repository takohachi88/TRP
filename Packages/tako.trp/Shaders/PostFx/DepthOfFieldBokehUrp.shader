﻿//URPのDepthOfFieldのBokehモードを移植。
Shader "Hiddden/Trp/PostFx/DepthOfFieldBokehUrp"
{
    SubShader
    {
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/DeclareDepthTexture.hlsl"

        #define SAMPLE_COUNT 42

        //オンにするとflickeringが減る？
        #define COC_LUMA_WEIGHTING 0

        TEXTURE2D(_DofTexture);
        TEXTURE2D(_FullCoCTexture);

        half4 _DownSampleScaleFactor;
        half4 _CoCParams;
        half4 _BokehKernel[SAMPLE_COUNT];
        half4 _BokehConstants;

        half4 _SrcSize;

        #define FocusDist _CoCParams.x
        #define MaxCoC _CoCParams.y
        #define MaxRadius _CoCParams.z
        #define RcpAspect _CoCParams.w


        half4 FragCoC (Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            half depth = LoadSceneDepth(uv * _SrcSize.xy);
            half linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
            half coc = (1 - FocusDist / linearEyeDepth) * MaxCoC;
            half nearCoC = clamp(coc, -1, 0);
            half farCoC = saturate(coc);
            return saturate((farCoC + nearCoC + 1) * 0.5);
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            #if SHADER_TARGET >= 45 && defined(PLATFORM_SUPPORT_GATHER)

            half4 cr = GATHER_RED_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            half4 cg = GATHER_GREEN_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            half4 cb = GATHER_BLUE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

            half3 c0 = half3(cr.x, cg.x, cb.x);
            half3 c1 = half3(cr.y, cg.y, cb.y);
            half3 c2 = half3(cr.z, cg.z, cb.z);
            half3 c3 = half3(cr.w, cg.w, cb.w);

            //COCの値域を-1～1に。
            half4 cocs = GATHER_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv) * 2 - 1;
            half coc0 = cocs.x;
            half coc1 = cocs.y;
            half coc2 = cocs.z;
            half coc3 = cocs.w;

            #else

            half3 duv = _SrcSize.zwz * float3(0.5, 0.5, -0.5);
            float2 uv0 = uv - duv.xy;
            float2 uv1 = uv - duv.zy;
            float2 uv2 = uv + duv.zy;
            float2 uv3 = uv + duv.xy;

            half3 c0 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv0).xyz;
            half3 c1 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv1).xyz;
            half3 c2 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv2).xyz;
            half3 c3 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv3).xyz;

            half coc0 = SAMPLE_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv0).x * 2.0 - 1.0;
            half coc1 = SAMPLE_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv1).x * 2.0 - 1.0;
            half coc2 = SAMPLE_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv2).x * 2.0 - 1.0;
            half coc3 = SAMPLE_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv3).x * 2.0 - 1.0;

            #endif

            #if COC_LUMA_WEIGHTING

            // bleedingとflickeringを減らすためにCoCと輝度の重みを適用。
            half w0 = abs(coc0) / (Max3(c0.x, c0.y, c0.z) + 1.0);
            half w1 = abs(coc1) / (Max3(c1.x, c1.y, c1.z) + 1.0);
            half w2 = abs(coc2) / (Max3(c2.x, c2.y, c2.z) + 1.0);
            half w3 = abs(coc3) / (Max3(c3.x, c3.y, c3.z) + 1.0);

            // 色の加重平均。
            half3 avg = c0 * w0 + c1 * w1 + c2 * w2 + c3 * w3;
            avg /= max(w0 + w1 + w2 + w3, 1e-5);

            #else

            half3 avg = (c0 + c1 + c2 + c3) / 4.0;

            #endif

            // Select the largest CoC value
            half cocMin = min(coc0, Min3(coc1, coc2, coc3));
            half cocMax = max(coc0, Max3(coc1, coc2, coc3));
            half coc = (-cocMin > cocMax ? cocMin : cocMax) * MaxRadius;

            // Premultiply CoC
            //TODO: 黒フリンジが出るため一旦コメントアウト。
            //avg *= smoothstep(0, _SrcSize.w * 2.0, abs(coc));

            return half4(avg, coc);
        }

        void Accumulate(half4 samp0, float2 uv, half4 disp, inout half4 farAcc, inout half4 nearAcc)
        {
            half4 samp = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + disp.wy);

            // Compare CoC of the current sample and the center sample and select smaller one
            half farCoC = max(min(samp0.a, samp.a), 0.0);

            // Compare the CoC to the sample distance & add a small margin to smooth out
            half farWeight = saturate((farCoC - disp.z + _BokehConstants.y) / _BokehConstants.y);
            half nearWeight = saturate((-samp.a - disp.z + _BokehConstants.y) / _BokehConstants.y);

            // Cut influence from focused areas because they're darkened by CoC premultiplying. This is only
            // needed for near field
            nearWeight *= step(_BokehConstants.x, -samp.a);

            // Accumulation
            farAcc += half4(samp.rgb, 1.0h) * farWeight;
            nearAcc += half4(samp.rgb, 1.0h) * nearWeight;
        }

        half4 FragBlur(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            half4 samp0 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

            half4 farAcc = 0.0;  // Background: far field bokeh
            half4 nearAcc = 0.0; // Foreground: near field bokeh

            // Center sample isn't in the kernel array, accumulate it separately
            Accumulate(samp0, uv, 0.0, farAcc, nearAcc);

            UNITY_LOOP
            for (int si = 0; si < SAMPLE_COUNT; si++)
            {
                Accumulate(samp0, uv, _BokehKernel[si], farAcc, nearAcc);
            }

            // Get the weighted average
            farAcc.rgb /= farAcc.a + (farAcc.a == 0.0);     // Zero-div guard
            nearAcc.rgb /= nearAcc.a + (nearAcc.a == 0.0);

            // Normalize the total of the weights for the near field
            nearAcc.a *= PI / (SAMPLE_COUNT + 1);

            // Alpha premultiplying (total near field accumulation weight)
            half alpha = saturate(nearAcc.a);
            half3 rgb = lerp(farAcc.rgb, nearAcc.rgb, alpha);

            return half4(rgb, alpha);
        }

        half4 FragPostBlur(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            // 9-tap tent filter with 4 bilinear samples
            float4 duv = _SrcSize.zwzw * _DownSampleScaleFactor.zwzw * float4(0.5, 0.5, -0.5, 0);
            half4 acc;
            acc  = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - duv.xy);
            acc += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - duv.zy);
            acc += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + duv.zy);
            acc += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + duv.xy);
            return acc * 0.25;
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float dofDownSample = 2.0f;
            half4 dof = SAMPLE_TEXTURE2D(_DofTexture, sampler_LinearClamp, uv);
            half coc = SAMPLE_TEXTURE2D(_FullCoCTexture, sampler_LinearClamp, uv).r;
            coc = (coc - 0.5) * 2.0 * MaxRadius;

            // Convert CoC to far field alpha value
            float ffa = smoothstep(_SrcSize.w * 2.0, _SrcSize.w * 4.0, coc);
            half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            half alpha = Max3(dof.r, dof.g, dof.b);
            half4 outColor = lerp(color, half4(dof.rgb, alpha), ffa + dof.a - ffa * dof.a);
            outColor.rgb = color.a > 0 ? outColor.rgb : color.rgb;
            outColor.a = color.a;
            return outColor;
        }

        ENDHLSL

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {            
            Name "CoC"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragCoC

            ENDHLSL
        }
        Pass
        {            
            Name "Prefilter"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragPrefilter

            ENDHLSL
        }
        Pass
        {            
            Name "Blur"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragBlur

            ENDHLSL
        }
        Pass
        {            
            Name "PostBlur"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragPostBlur

            ENDHLSL
        }
        Pass
        {            
            Name "Composite"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment FragComposite

            ENDHLSL
        }
    }
}