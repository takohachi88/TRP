Shader "Hidden/TRP/LiquidGlassComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "Trp"
        }

        Cull Off
        ZWrite Off

        HLSLINCLUDE

        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_AttachmentColor);
        TEXTURE2D(_LiquidData);
        TEXTURE2D(_LiquidNormal);
        TEXTURE2D(_MatCapTexture);

        float4 _Params1;
        float4 _Params2;
        float4 _Params3;
        float4 _Params4;
        float4 _Params5;
        float4 _Params6;
        half4 _SpecularColor;
        half4 _MatCapColor;

        half4 Blur(float2 uv, float2 direction, float4 params1)
        {
            int count = params1.x;
            float countRcp = params1.y;
            float countMinusOneRcp = params1.z;
            float strength = params1.w;

            direction *= _AspectFit.yx * strength;

            half4 output = 0;

            for(int i = 0; i < count; i++)
            {
                float2 offset = lerp(-direction, direction, i * (countMinusOneRcp));
                output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset);
            }
            output *= countRcp;

            return output;
        }

        half4 FragmentShapeBlurH(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(1, 0), _Params1);
        }
        
        half4 FragmentShapeBlurV(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(0, 1), _Params1);
        }

        half4 FragmentBlurH(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(1, 0), _Params2);
        }
        
        half4 FragmentBlurV(Varyings input) : SV_Target
        {
            return Blur(input.texcoord, float2(0, 1), _Params2);
        }

        half4 FragmentCreateNormalGradient(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            // 出力先が1/2解像度なので、隣接する低解像度ピクセルに対応する2ピクセル幅で勾配を取る。
            float2 texelSize = _Params5.xy * 2;
            half alphaTopLeft = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(-1, 1)).a;
            half alphaTop = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(0, 1)).a;
            half alphaTopRight = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(1, 1)).a;
            half alphaLeft = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(-1, 0)).a;
            half alphaRight = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(1, 0)).a;
            half alphaBottomLeft = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(-1, -1)).a;
            half alphaBottom = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(0, -1)).a;
            half alphaBottomRight = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv + texelSize * float2(1, -1)).a;

            half gradientX = alphaTopRight + 2.0h * alphaRight + alphaBottomRight
                - alphaTopLeft - 2.0h * alphaLeft - alphaBottomLeft;
            half gradientY = alphaTopLeft + 2.0h * alphaTop + alphaTopRight
                - alphaBottomLeft - 2.0h * alphaBottom - alphaBottomRight;
            return half4(half2(gradientX, gradientY) * 0.25h, 0, 1);
        }

        half4 BlurNormalGradient(float2 uv, float2 direction)
        {
            direction *= _AspectFit.yx * _Params6.x;

            // 正規化前の勾配を5タップGaussianでぼかし、法線方向の細かな揺れを除去する。
            half4 output = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv) * 0.375h;
            output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + direction) * 0.25h;
            output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - direction) * 0.25h;
            output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + direction * 2) * 0.0625h;
            output += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - direction * 2) * 0.0625h;
            return output;
        }

        half4 FragmentNormalBlurH(Varyings input) : SV_Target
        {
            return BlurNormalGradient(input.texcoord, float2(1, 0));
        }

        half4 FragmentNormalBlurV(Varyings input) : SV_Target
        {
            return BlurNormalGradient(input.texcoord, float2(0, 1));
        }

        half4 FragmentComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            float edgeDistortStrength = _Params3.x;
            float uiColor = _Params3.y;

            half4 liquidData = SAMPLE_TEXTURE2D(_LiquidData, sampler_LinearClamp, uv);
            half4 base = SAMPLE_TEXTURE2D(_AttachmentColor, sampler_LinearClamp, uv);

            float chromaticAberrationEdgeStrength = _Params3.z;
            float chromaticAberrationBaseStrength = _Params3.w;
            half distort = liquidData.a;

            half blurR = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength + chromaticAberrationEdgeStrength * edgeDistortStrength, distort) + float2(chromaticAberrationBaseStrength, 0)).r;
            half blurG = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength, distort)).g;
            half blurB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv * smoothstep(0, edgeDistortStrength - chromaticAberrationEdgeStrength * edgeDistortStrength, distort) + float2(-chromaticAberrationBaseStrength, 0)).b;

            half3 blur = half3(blurR, blurG, blurB);

            half shape = smoothstep(0.48, 0.52, liquidData.a);

            half4 output = 1;

            liquidData.rgb = liquidData.a <= 0.001 ? liquidData.rgb : liquidData.rgb * rcp(liquidData.a);

            output.rgb = lerp(base.rgb, blur * lerp(1, liquidData.rgb, uiColor), shape);

            // 両方の効果が無効な場合は、法線テクスチャ参照を含む一連の計算を省略する。
            [branch]
            if (_Params4.y > 0 || _Params4.w > 0)
            {
                // 低解像度バッファで平滑化した勾配をバイリニア拡大し、最後に法線へ変換する。
                half2 shapeGradient = SAMPLE_TEXTURE2D(_LiquidNormal, sampler_LinearClamp, uv).rg;
                half3 surfaceNormal = normalize(half3(-shapeGradient * _Params4.x, 1));

                // 画面上のライト方向と正面向きの視線から、軽量なBlinn-Phongスペキュラーを計算する。
                half3 lightDirection = normalize(half3(_Params5.zw, 1));
                half3 halfDirection = normalize(lightDirection + half3(0, 0, 1));
                half specular = pow(saturate(dot(surfaceNormal, halfDirection)), _Params4.z);
                output.rgb += specular * _Params4.y * _SpecularColor.rgb * shape;

                // MatCap未指定時はC#側で強度を0にするため、既存の表示には一切影響しない。
                half2 matCapUv = surfaceNormal.xy * 0.5h + 0.5h;
                half3 matCap = SAMPLE_TEXTURE2D(_MatCapTexture, sampler_LinearClamp, matCapUv).rgb * _MatCapColor.rgb;
                // 法線が正面を向く平坦な内側を除外し、形状勾配のある縁だけにMatCapを適用する。
                half matCapEdgeMask = smoothstep(0.05h, 0.35h, length(surfaceNormal.xy));
                half matCapBlend = saturate(_Params4.w * shape * matCapEdgeMask);
                output.rgb = lerp(output.rgb, output.rgb * matCap * 2, matCapBlend);
            }

            return output;
        }

        ENDHLSL

        Pass
        {
            Name "ShapeBlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentShapeBlurH
            ENDHLSL
        }
        
        Pass
        {
            Name "ShapeBlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentShapeBlurV
            ENDHLSL
        }
        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlurH
            ENDHLSL
        }
        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlurV
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentComposite
            ENDHLSL
        }
        Pass
        {
            Name "CreateNormalGradient"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentCreateNormalGradient
            ENDHLSL
        }
        Pass
        {
            Name "NormalBlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentNormalBlurH
            ENDHLSL
        }
        Pass
        {
            Name "NormalBlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentNormalBlurV
            ENDHLSL
        }
    }
}
