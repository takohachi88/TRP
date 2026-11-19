Shader "Hidden/Trp/PostFx/Uber"
{
    SubShader
    {
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        half _VignetteIntensity;
        half _VignetteSmoothness;
        float2 _VignetteCenter;
        half3 _VignetteColor;
        int _VignetteFitAspect;
        int _VignetteBlendMode;

        half _MosaicIntensity;
        half _MosaicCellDensity;

        half _PosterizationIntensity;
        int _ToneCount;

        int _UseLut;
        half3 _LutParams;//(1 / lut_width, 1 / lut_height, lut_height - 1)

        TEXTURE2D(_Lut);
        SAMPLER(sampler_Lut);

        TEXTURE2D(_AutoExposure);
        TEXTURE2D(_PreviousExposure);
        float4 _ExposureAdaptation; // 補正の下限・上限、露出を下げる追従率・上げる追従率。
        int _ExposureHasHistory;

        // AutoExposurePass.MeterSize と同じサイズで画面全体を等間隔に測光する。
        #define EXPOSURE_METER_SIZE 16

        float LogLuminance(float2 uv)
        {
            float3 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            return log2(max(dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722)), 1e-4));
        }

        float MeterExposure(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            // 各セルの4点を個別に対数化してから平均し、局所的な強い光の影響を抑える。
            float2 uv = input.texcoord;
            const float offset = 0.25 / EXPOSURE_METER_SIZE;
            return 0.25 * (LogLuminance(uv + float2(-offset, -offset)) +
                           LogLuminance(uv + float2( offset, -offset)) +
                           LogLuminance(uv + float2(-offset,  offset)) +
                           LogLuminance(uv + float2( offset,  offset)));
        }

        float AdaptExposure(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float sum = 0.0;
            [loop]
            for (uint y = 0; y < EXPOSURE_METER_SIZE; y++)
            {
                [unroll]
                for (uint x = 0; x < EXPOSURE_METER_SIZE; x++)
                    sum += LOAD_TEXTURE2D_X(_BlitTexture, uint2(x, y)).r;
            }
            float mean = sum * (1.0 / (EXPOSURE_METER_SIZE * EXPOSURE_METER_SIZE));
            float target = clamp(log2(0.18) - mean, _ExposureAdaptation.x, _ExposureAdaptation.y);
            if (_ExposureHasHistory == 0) return target;

            float previous = LOAD_TEXTURE2D(_PreviousExposure, uint2(0, 0)).r;
            float rate = target < previous ? _ExposureAdaptation.z : _ExposureAdaptation.w;
            return clamp(lerp(previous, target, rate), _ExposureAdaptation.x, _ExposureAdaptation.y);
        }


        half4 Vignette (float2 uv, half4 output)
        {
            half vignette = DotDistance(uv, _VignetteCenter, _VignetteIntensity, _VignetteSmoothness, _VignetteFitAspect);
            
            half3 color = 0;
            color += (_VignetteBlendMode == 0) * lerp(output.rgb, _VignetteColor, vignette);
            color += (_VignetteBlendMode == 1) * (output.rgb + vignette * _VignetteColor);
            color += (_VignetteBlendMode == 2) * lerp(output.rgb, output.rgb * _VignetteColor, vignette);
            color += (_VignetteBlendMode == 3) * lerp(output.rgb, (1 - output.rgb) * _VignetteColor, vignette);
            
            output.rgb = color;

            return output;
        }

        half4 Fragment (Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            //モザイク。
            if(0 < _MosaicIntensity)
            {
                float cellDensity = lerp(_ScreenParams.x, _MosaicCellDensity, _MosaicIntensity);
                uv -= 0.5;
                uv *= _AspectFit;
                uv = round(uv * cellDensity) * rcp(cellDensity);
                uv *= rcp(_AspectFit);
                uv += 0.5;
            }

            half4 output = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

            //ポスタライズ。
            if(0 < _PosterizationIntensity)
            {
                output = lerp(output, round(output * _ToneCount) * rcp(_ToneCount), _PosterizationIntensity);
            }

            // リニアHDRに露出を掛けてから、カラーグレーディングとトーンマッピングを行う。
            #if defined(_AUTO_EXPOSURE)
            float3 exposedColor = float3(output.rgb) * exp2(SAMPLE_TEXTURE2D_LOD(_AutoExposure, sampler_PointClamp, float2(0.5, 0.5), 0).r);
            #else
            half3 exposedColor = output.rgb;
            #endif

            #if defined(_LUT)
            output.rgb = ApplyLut2D(TEXTURE2D_ARGS(_Lut, sampler_Lut), saturate(LinearToLogC(exposedColor)), _LutParams.xyz);
            #elif defined(_AUTO_EXPOSURE)
            output.rgb = exposedColor;
            #endif

            //ビネット。
            #if defined(_VIGNETTE)
            output = Vignette(input.texcoord, output);
            #endif

            return output;
        }

        ENDHLSL

        ZWrite Off
        Cull Off
        ZTest Always


        Pass
        {            
            Name "Uber"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Fragment

            #pragma multi_compile_local _ _LUT
            #pragma multi_compile_local _ _VIGNETTE
            #pragma multi_compile_local_fragment _ _AUTO_EXPOSURE
            
            ENDHLSL
        }

        Pass
        {
            Name "Auto Exposure Meter"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment MeterExposure
            ENDHLSL
        }

        Pass
        {
            Name "Auto Exposure Adapt"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment AdaptExposure
            ENDHLSL
        }
    }
}
