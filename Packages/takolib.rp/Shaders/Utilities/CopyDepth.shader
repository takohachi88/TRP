//URPのCopyDepthシェーダーを移植。
//XR系の処理は排除。
Shader "Hidden/Trp/CopyDepth"
{
    SubShader
    {
        Pass
        {
            Name "CopyDepth"
            ZTest Always
            ZWrite[_ZWrite]
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
            #pragma multi_compile _ _OUTPUT_DEPTH

            #include "Packages/takolib.rp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if defined(_DEPTH_MSAA_2)
                #define MSAA_SAMPLES 2
            #elif defined(_DEPTH_MSAA_4)
                #define MSAA_SAMPLES 4
            #elif defined(_DEPTH_MSAA_8)
                #define MSAA_SAMPLES 8
            #else
                #define MSAA_SAMPLES 1
            #endif
            
            #define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
            #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)

            //MSAAサンプルを行うマクロ。texture.Load()の第二引数にMSAAサンプルのインデックスを渡す。
            #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_CameraDepthAttachment, uv, sampleIndex)
            #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv)
            
            #if MSAA_SAMPLES == 1
                DEPTH_TEXTURE(_CameraDepthAttachment);
                SAMPLER(sampler_CameraDepthAttachment);
            #else
                DEPTH_TEXTURE_MS(_CameraDepthAttachment, MSAA_SAMPLES);
                float4 _CameraDepthAttachment_TexelSize;
            #endif
            
            #if UNITY_REVERSED_Z
                #define DEPTH_DEFAULT_VALUE 1.0
                #define DEPTH_OP min
            #else
                #define DEPTH_DEFAULT_VALUE 0.0
                #define DEPTH_OP max
            #endif
            
            float SampleDepth(float2 uv)
            {
            #if MSAA_SAMPLES == 1
                return SAMPLE(uv);
            #else
                int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);
                float outDepth = DEPTH_DEFAULT_VALUE;
            
                //最も奥の深度値を得る。
                //例えばDirectXやMetalではreversed zつまり最も近くが1で遠くが0となるので、MSAAサンプルのうち最も小さい値（min）すなわち遠くの値となる。
                UNITY_UNROLL
                for (int i = 0; i < MSAA_SAMPLES; ++i)
                    outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
                return outDepth;
            #endif
            }
            
            #if defined(_OUTPUT_DEPTH)
            float frag(Varyings input) : SV_Depth
            #else
            float frag(Varyings input) : SV_Target
            #endif
            {
                return SampleDepth(input.texcoord);
            }
            
            ENDHLSL
        }
    }
}
