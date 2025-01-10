Shader "Hiddden/Trp/PostProcess/Uber"
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

        bool _Nega;
        half _NegaIntensity;

        half4 Vignette (Varyings input, half4 destination)
        {
            half4 output = destination;

            float2 dist = abs(input.texcoord - _VignetteCenter) * _VignetteIntensity;
            dist *= _VignetteFitAspect ? _AspectFit : 1;
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
                uv.x *= rcp(_AspectFit);
                uv += 0.5;
            }

            half4 output = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

            //ポスタライズ。
            if(0 < _PosterizationIntensity)
            {
                output.rgb = lerp(output.rgb, round(output.rgb * _ToneCount) * rcp(_ToneCount), _PosterizationIntensity);
            }

            //ネガ。
            if(_Nega) output.rgb = max(0, lerp(output.rgb, (1 - output.rgb), _NegaIntensity));

            //ビネット。
            #if defined(VIGNETTE)
            output = Vignette(input, output);
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

            #pragma multi_compile_local _ VIGNETTE
            
            ENDHLSL
        }
    }
}