Shader "Hiddden/Trp/PostFx/Uber"
{
    SubShader
    {
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
        #include "Packages/takolib.rp/ShaderLibrary/Common.hlsl"
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


        half4 Vignette (float2 uv, half4 output)
        {
            float2 dist = abs(uv - _VignetteCenter) * _VignetteIntensity;
            dist *= _VignetteFitAspect ? 1 : _AspectFit;
            half vignette = smoothstep(0.5 - _VignetteSmoothness * 0.5, 0.5 + _VignetteSmoothness * 0.5, dot(dist, dist));
            
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

            //LUTの適用。
            #if defined(_LUT)
            output.rgb = ApplyLut2D(TEXTURE2D_ARGS(_Lut, sampler_Lut), output.rgb, _LutParams.xyz);
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
            
            ENDHLSL
        }
    }
}