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
            
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            
            half _Intensity;
            half _BlurIntensity;
            //x: samplerCount, y: 1 / sampleCount, z: 1 / (sampleCount - 1)
            half3 _SampleCount;
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

                half rcpSampleCount = _SampleCount.y;

                for(int i = 0; i < _SampleCount.x; i++)
                {
                    float2 uv = input.texcoord - _Center;

                    #if defined(_NOISE_GRADIENT_TEXTURE)
                    uv *= noise;
                    #endif
                    
                    #if defined(_DITHER)
                    uv *= lerp(1, 1 - _Intensity * _BlurIntensity, (i + random) * rcpSampleCount);
                    #else
                    uv *= lerp(1, 1 - _Intensity * _BlurIntensity, i * _SampleCount.z);
                    #endif

                    uv += _Center;
                    output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                }

                output *= rcpSampleCount;

                return output;
            }
            
            ENDHLSL
        }
    }
}