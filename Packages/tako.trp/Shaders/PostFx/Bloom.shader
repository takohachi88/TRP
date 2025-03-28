//URPのBloomを移植。
Shader "Hidden/Trp/PostFx/Bloom"
{
    HLSLINCLUDE
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"

        TEXTURE2D(_SrcTextureLowMip);
        float4 _SrcTextureLowMip_TexelSize;

        float4 _Params; // x: scatter, y: clamp, z: threshold (linear), w: threshold knee

        #define Scatter             _Params.x
        #define ClampMax            _Params.y
        #define Threshold           _Params.z
        #define ThresholdKnee       _Params.w

        float4 _CompositeParams;
        float4 _LensDirtParams;
        float _LensDirtIntensity;

        #define BloomIntensity          _CompositeParams.x
        #define BloomTint               _CompositeParams.yzw
        #define LensDirtScale           _LensDirtParams.xy
        #define LensDirtOffset          _LensDirtParams.zw
        #define LensDirtIntensity       _LensDirtIntensity.x

        TEXTURE2D(_BloomTexture);
        float4 _BloomTexture_TexelSize;
        TEXTURE2D(_LensDirtTexture);
        SAMPLER(sampler_LensDirtTexture);

        half4 EncodeHDR(half3 color)
        {
        #if UNITY_COLORSPACE_GAMMA
            color = sqrt(color); // linear to γ
        #endif

            return half4(color, 1.0);
        }

        half3 DecodeHDR(half4 data)
        {
            half3 color = data.xyz;

        #if UNITY_COLORSPACE_GAMMA
            color *= color; // γ to linear
        #endif

            return color;
        }

        half3 SamplePrefilter(float2 uv,  float2 offset)
        {
            float2 texelSize = _BlitTexture_TexelSize.xy;
            half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texelSize * offset);
            //#if _ENABLE_ALPHA_OUTPUT
                // When alpha is enabled, regions with zero alpha should not generate any bloom / glow. Therefore we pre-multipy the color with the alpha channel here and the rest
                // of the computations remain float3. Still, when bloom is applied to the final image, bloom will still be spread on regions with zero alpha (see UberPost.compute)
                color.xyz *= color.w;
            //#endif
            return color.xyz;
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

        #if _BLOOM_HQ
            half3 A = SamplePrefilter(uv, float2(-1.0, -1.0));
            half3 B = SamplePrefilter(uv, float2( 0.0, -1.0));
            half3 C = SamplePrefilter(uv, float2( 1.0, -1.0));
            half3 D = SamplePrefilter(uv, float2(-0.5, -0.5));
            half3 E = SamplePrefilter(uv, float2( 0.5, -0.5));
            half3 F = SamplePrefilter(uv, float2(-1.0,  0.0));
            half3 G = SamplePrefilter(uv, float2( 0.0,  0.0));
            half3 H = SamplePrefilter(uv, float2( 1.0,  0.0));
            half3 I = SamplePrefilter(uv, float2(-0.5,  0.5));
            half3 J = SamplePrefilter(uv, float2( 0.5,  0.5));
            half3 K = SamplePrefilter(uv, float2(-1.0,  1.0));
            half3 L = SamplePrefilter(uv, float2( 0.0,  1.0));
            half3 M = SamplePrefilter(uv, float2( 1.0,  1.0));

            half2 div = (1.0 / 4.0) * half2(0.5, 0.125);

            half3 color = (D + E + I + J) * div.x;
            color += (A + B + G + F) * div.y;
            color += (B + C + H + G) * div.y;
            color += (F + G + L + K) * div.y;
            color += (G + H + M + L) * div.y;
        #else
            half3 color = SamplePrefilter(uv, float2(0,0));
        #endif

            // User controlled clamp to limit crazy high broken spec
            color = min(ClampMax, color);

            // Thresholding
            half brightness = Max3(color.r, color.g, color.b);
            half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
            softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
            half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
            color *= multiplier;

            // Clamp colors to positive once in prefilter. Encode can have a sqrt, and sqrt(-x) == NaN. Up/Downsample passes would then spread the NaN.
            color = max(color, 0);
            return EncodeHDR(color);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            float2 texelSize = _BlitTexture_TexelSize.xy * 2.0;
            float2 uv = input.texcoord;

            //return texelSize.xyxx *100;
            //return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(texelSize.x * 4.0, 0.0);

            // 9-tap gaussian blur on the downsampled source
            half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(texelSize.x * 4.0, 0.0))));
            half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(texelSize.x * 3.0, 0.0))));
            half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(texelSize.x * 2.0, 0.0))));
            half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(texelSize.x * 1.0, 0.0))));
            half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv                                 )));
            half3 c5 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(texelSize.x * 1.0, 0.0))));
            half3 c6 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(texelSize.x * 2.0, 0.0))));
            half3 c7 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(texelSize.x * 3.0, 0.0))));
            half3 c8 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(texelSize.x * 4.0, 0.0))));

            half3 color = c0 * 0.01621622 + c1 * 0.05405405 + c2 * 0.12162162 + c3 * 0.19459459
                        + c4 * 0.22702703
                        + c5 * 0.19459459 + c6 * 0.12162162 + c7 * 0.05405405 + c8 * 0.01621622;

            return EncodeHDR(color);
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            float2 texelSize = _BlitTexture_TexelSize.xy;
            float2 uv = input.texcoord;

            // Optimized bilinear 5-tap gaussian on the same-sized source (9-tap equivalent)
            half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(0.0, texelSize.y * 3.23076923))));
            half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - float2(0.0, texelSize.y * 1.38461538))));
            half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv                                        )));
            half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(0.0, texelSize.y * 1.38461538))));
            half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv + float2(0.0, texelSize.y * 3.23076923))));

            half3 color = c0 * 0.07027027 + c1 * 0.31621622
                        + c2 * 0.22702703
                        + c3 * 0.31621622 + c4 * 0.07027027;

            return EncodeHDR(color);
        }

        half3 Upsample(float2 uv)
        {
            half3 highMip = DecodeHDR(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv));

        #if _BLOOM_HQ
            half3 lowMip = DecodeHDR(SampleTexture2DBicubic(TEXTURE2D_ARGS(_SrcTextureLowMip, sampler_LinearClamp), uv, _SrcTextureLowMip_TexelSize.zwxy, (1.0).xx, 0));
        #else
            half3 lowMip = DecodeHDR(SAMPLE_TEXTURE2D(_SrcTextureLowMip, sampler_LinearClamp, uv));
        #endif

            return lerp(highMip, lowMip, Scatter);
        }

        half4 FragUpsample(Varyings input) : SV_Target
        {
            half3 color = Upsample(input.texcoord);
            return EncodeHDR(color);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            half4 output = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            #if _BLOOM_HQ
            half3 bloom = SampleTexture2DBicubic(TEXTURE2D_ARGS(_BloomTexture, sampler_LinearClamp), uv, _BloomTexture_TexelSize.zwxy, (1.0).xx, 0).xyz;
            #else
            half3 bloom = SAMPLE_TEXTURE2D(_BloomTexture, sampler_LinearClamp, uv).xyz;
            #endif

            bloom *= BloomIntensity;
            output.rgb += bloom * BloomTint;

            #if defined(_BLOOM_DIRT)
            {
                half3 dirt = SAMPLE_TEXTURE2D(_LensDirtTexture, sampler_LensDirtTexture, uv + LensDirtOffset).xyz;
                dirt *= LensDirtIntensity;
                output.rgb += dirt * bloom.xyz;
            }
            #endif

            return output;
        }

    ENDHLSL

    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragPrefilter
                #pragma multi_compile_local_fragment _ _BLOOM_HQ
                //#pragma multi_compile_fragment _ _ENABLE_ALPHA_OUTPUT
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Blur Horizontal"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Blur Vertical"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Upsample"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragUpsample
                #pragma multi_compile_local_fragment _ _BLOOM_HQ
            ENDHLSL
        }

        //Uberに統合することでパフォーマンス改善の余地があるが、柔軟性が損なわれる。
        Pass
        {
            Name "Bloom Composite"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragComposite
                #pragma multi_compile_local_fragment _ _BLOOM_LQ _BLOOM_HQ
                #pragma multi_compile_local_fragment _ _BLOOM_DIRT
            ENDHLSL
        }
    }
}