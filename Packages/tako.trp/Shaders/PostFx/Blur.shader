Shader "Hidden/Trp/PostFx/Blur"
{
    HLSLINCLUDE
            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define MAX_SAMPLE_PAIRS 16

        float2 _BlurDirection;
        int _SamplePairCount;
        float _DownSampleBlend;

        TEXTURE2D(_ControlTexture);
        SAMPLER(sampler_ControlTexture);
        TEXTURE2D_X(_BlurTexture);

        float GetControl(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_ControlTexture, sampler_ControlTexture, uv).r;
        }

        half4 Blur(Varyings input) : SV_Target
        {
            float control = saturate(GetControl(input.texcoord));
            float2 radius = _BlurDirection * control;

            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            float totalWeight = 1.0;

            [unroll]
            for (int i = 1; i <= MAX_SAMPLE_PAIRS; ++i)
            {
                if (i > _SamplePairCount) break;

                float normalizedDistance = i * rcp(_SamplePairCount);
                float weight = exp2(-3.0 * normalizedDistance * normalizedDistance);
                float2 offset = radius * normalizedDistance * _BlitTexture_TexelSize.xy;

                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + offset) * weight;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - offset) * weight;
                totalWeight += 2.0 * weight;
            }

            return color * rcp(totalWeight);
        }

        half4 Composite(Varyings input) : SV_Target
        {
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            half4 blurred = SAMPLE_TEXTURE2D_X(_BlurTexture, sampler_LinearClamp, input.texcoord);
            float blend = lerp(1.0, saturate(GetControl(input.texcoord)), _DownSampleBlend);
            return lerp(source, blurred, blend);
        }

        half4 Downsample(Varyings input) : SV_Target
        {
            float2 halfTexel = _BlitTexture_TexelSize.xy * 0.5;

            // 各縮小段で 2x2 テントフィルターを使い、縮小時のエイリアシングを抑える。
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-halfTexel.x, -halfTexel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( halfTexel.x, -halfTexel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-halfTexel.x,  halfTexel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( halfTexel.x,  halfTexel.y));
            return color * 0.25;
        }

        half4 Upsample(Varyings input) : SV_Target
        {
            float2 texel = _BlitTexture_TexelSize.xy;

            // 拡大時は 3x3 テントフィルターを使い、Mip境界の段差を滑らかにする。
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * 4.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, 0.0)) * 2.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x, 0.0)) * 2.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0.0, -texel.y)) * 2.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0.0,  texel.y)) * 2.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, -texel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x, -texel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x,  texel.y));
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x,  texel.y));
            return color * (1.0 / 16.0);
        }
    ENDHLSL

    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Blur Horizontal"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Blur
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vertical"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Blur
            ENDHLSL
        }

        Pass
        {
            Name "Blur Composite"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Composite
            ENDHLSL
        }

        Pass
        {
            Name "Blur Pyramid Downsample"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Downsample
            ENDHLSL
        }

        Pass
        {
            Name "Blur Pyramid Upsample"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Upsample
            ENDHLSL
        }
    }
}
