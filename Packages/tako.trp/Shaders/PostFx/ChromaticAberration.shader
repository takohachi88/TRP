Shader "Hiddden/Trp/PostFx/ChromaticAberrarion"
{
    HLSLINCLUDE

    #pragma multi_compile_local_fragment _ _DITHER
    #pragma multi_compile_local_fragment _ _RADIAL _DIRECTION
    
    #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
    
    half _Intensity;
    half _Limit;
    float2 _Center;
    float2 _Direction;
    //x: samplerCount, y: 1 / sampleCount, z: 1 / (sampleCount - 1)
    half3 _SampleCount;
    half _UseIntensityMap;
    TEXTURE2D(_ChromaLut);
    TEXTURE2D(_IntensityMap);
    
    half4 Fragment (Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        half4 output = 0;
        half4 chromaSum = 0;

        #if defined(_RADIAL)
        half intensity = min(distance(uv, _Center) * _Intensity, _Limit);
        #else
        half intensity = _Intensity;
        #endif

        #if defined(_DIRECTION)
        half intensityMap = _UseIntensityMap ? SAMPLE_TEXTURE2D(_IntensityMap, sampler_LinearClamp, uv).r - 0.5 : 0.5;
        #endif

        #if defined(_DITHER)
        half random = InterleavedGradientNoise(input.positionCS.xy, 0);
        #endif

        //_SampleCountは3以上。
        for (int i = 0; i < _SampleCount.x; i++)
        {
            #if defined(_DITHER)
            half t = lerp(i, i + 1, random) * _SampleCount.y;
            #else
            half t = i * _SampleCount.z;
            #endif

            half4 chroma = SAMPLE_TEXTURE2D(_ChromaLut, sampler_LinearClamp, t);

            float2 st = uv;
            #if defined(_RADIAL)
            st = (uv - _Center) * (1 - intensity * t) + _Center;
            #elif defined(_DIRECTION)
            st += _Direction * intensity * (t - 0.5) * intensityMap;
            #endif

            output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, st) * chroma;
            chromaSum += chroma;
        }
        output *= rcp(chromaSum);
        return output;
    }

    ENDHLSL

    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always
        Pass
        {
            Name "ChromaticAberrarion"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Fragment
            
            ENDHLSL
        }
    }
}