Shader "Hiddden/Trp/PostFx/RadialBlur"
{
    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {            
            Name "RadialBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_local _ _DITHER
            #pragma multi_compile_local _ _NOISE_GRADIENT_TEXTURE
            
            #include "Packages/takolib.rp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            
            half _Intensity;
            half _BlurIntensity;
            int _SampleCount;
            float2 _Center;
            float _NoiseTiling;
            float _NoiseIntensity;

            TEXTURE2D_X(_NoiseGradientTexture);
            SAMPLER(sampler_NoiseGradientTexture);

            half4 Fragment (Varyings input) : SV_Target
            {
                half4 output = 0;

                #if defined(_DITHER)
                half random = InterleavedGradientNoise(input.positionCS.xy, 0);
                #endif

                #if defined(_NOISE_GRADIENT_TEXTURE)
                float2 noiseUv = input.texcoord - _Center;
                noiseUv = float2((atan2(noiseUv.y, noiseUv.x) + PI) * PI_TWO_RCP * _NoiseTiling, frac(_Intensity * 5));
                float noise = 1 - SAMPLE_TEXTURE2D(_NoiseGradientTexture, sampler_NoiseGradientTexture, noiseUv).r * _Intensity * _NoiseIntensity;
                #endif

                half rcpSampleCount = rcp(_SampleCount);

                for(int i = 0; i < _SampleCount; i++)
                {
                    float2 uv = input.texcoord - _Center;

                    #if defined(_NOISE_GRADIENT_TEXTURE)
                    uv *= noise;
                    #endif
                    
                    #if defined(_DITHER)
                    uv *= lerp(1, 1 - _Intensity * _BlurIntensity, (i + random) * rcpSampleCount);
                    #else
                    uv *= lerp(1, 1 - _Intensity * _BlurIntensity, i * rcp(_SampleCount - 1));
                    #endif

                    uv += _Center;
                    output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                }

                output *= rcpSampleCount;

                return output;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "ChromaticAberrarion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/takolib.rp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            half _Intensity;
            half _ChromaticAberrationIntensity;
            half _ChromaticAberrationLimit;
            float2 _Center;

            half4 Fragment (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                half4 output = 0;
                float multiplier = min(_Intensity, _ChromaticAberrationLimit) * _ChromaticAberrationIntensity;

                output.r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).r;
                output.ga = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - _Center) * (1 - multiplier * 0.5) + _Center).ga;
                output.b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, (uv - _Center) * (1 - multiplier) + _Center).b;

                return output;
            }
            
            ENDHLSL
        }
    }
}