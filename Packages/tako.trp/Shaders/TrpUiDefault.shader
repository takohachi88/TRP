Shader "TRP/UI/Default"
{
    Properties
    {
        [PerRendererData][MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Common Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend Src", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend Dst", int) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", int) = 0
        [Toggle(MULTIPLY_RGB_A)] _MultiplyRgbA ("Multiply RGB A", int) = 1
        [PerRendererData] _AlphaBlend ("Alpha Blend", int) = 3
        [PerRendererData] _VertexColorBlend ("Vertex Color Blend", int) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "Trp"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_BlendSrc][_BlendDst]
        BlendOp [_BlendOp]
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 2.0

            #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP


            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv  : TEXCOORD0;
                float4 positionWS : TEXCOORD1;
                float4  mask : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            //uGUIはダイナミックバッチングのためSRPBactherが使えない。
            half _MultiplyRgbA;
            int _VertexColorBlend;
            half4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 vPosition = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = input.positionOS;
                output.positionCS = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (input.positionOS.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                output.uv = TRANSFORM_TEX(input.uv.xy, _MainTex);
                output.mask = float4(input.positionOS.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                input.color.a = UiAlphaRoundUp(input.color.a);

                half4 output = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd);
                VERTEX_COLOR_BLEND(output, input.color);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                output.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (output.a - 0.001);
                #endif

                MULTIPLY_RGB_A(output);

                return output;
            }
        ENDHLSL
        }
    }
    CustomEditor "TrpEditor.ShaderGui.UiDefaultGui"
}
