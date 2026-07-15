Shader "Hidden/Trp/PostFx/CameraMotionBlur"
{
    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "CameraMotionBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_local _ _DITHER

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            float2 _BlurVector;
            int _SampleCount;

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = 0;

                #if defined(_DITHER)
                float sampleOffset = InterleavedGradientNoise(input.positionCS.xy, 0);
                #else
                float sampleOffset = 0;
                #endif

                // 現在位置から過去フレーム側へ読み進め、カメラ移動方向に応じた片方向の軌跡を作る。
                float denominator = max(1, _SampleCount - 1);
                for (int i = 0; i < _SampleCount; i++)
                {
                    float t = saturate((i + sampleOffset) * rcp(denominator));
                    color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + _BlurVector * t);
                }

                return color * rcp(_SampleCount);
            }
            ENDHLSL
        }
    }
}
