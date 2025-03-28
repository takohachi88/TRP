Shader "Hidden/Trp/PostFx/CreateLut"
{
    SubShader
    {
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        
        //(lut_height, 0.5 / lut_width, 0.5 / lut_height, lut_height / lut_height - 1)
        half4 _LutParams;

        half3 _LggLift;
		half3 _LggGamma;
		half3 _LggGain;

		half3 _SmhShadows;
		half3 _SmhMidtones;
		half3 _SmhHighlights;
		half4 _SmhRange;
        
        //Contrast、Hue、Saturation。
        half4 _ColorAdjustmentParams;
        
        half3 _ColorFilter;
        half3 _SaturationFactor;

        float3 _ChannelMixerRed;
        float3 _ChannelMixerGreen;
        float3 _ChannelMixerBlue;

        //None
		//Neutral
		//Reinhard
		//Aces
        int _TonemappingMode;
        #define TONEMAPPING_NONE (_TonemappingMode == 0)
        #define TONEMAPPING_NEUTRAL (_TonemappingMode == 1)
        #define TONEMAPPING_REINHARD (_TonemappingMode == 2)
        #define TONEMAPPING_ACES (_TonemappingMode == 3)

        bool _Nega;
        half _NegaIntensity;

        //輝度を取得する。
        half GetLuminance(half3 colorLinear)
        {
            return TONEMAPPING_ACES ? AcesLuminance(colorLinear) : dot(colorLinear, _SaturationFactor);
        }


        half4 Fragment (Varyings input) : SV_Target
        {
            half3 lut = GetLutStripValue(input.texcoord, _LutParams);

            half3 colorLog = 0;
            if (TONEMAPPING_ACES) colorLog = ACES_to_ACEScc(unity_to_ACES(lut));
            else colorLog = LinearToLogC(lut);
            
            //Contrast
            colorLog = (colorLog - ACEScc_MIDGRAY) * _ColorAdjustmentParams.x + ACEScc_MIDGRAY;

            if (TONEMAPPING_ACES) lut = ACES_to_ACEScg(ACEScc_to_ACES(colorLog));
            else lut = LogCToLinear(colorLog);

            //ColorFilter
            lut *= _ColorFilter;

            //ChannelMixer
            lut = half3(
                dot(lut, _ChannelMixerRed.xyz),
                dot(lut, _ChannelMixerGreen.xyz),
                dot(lut, _ChannelMixerBlue.xyz));

            //ShadowsMidtonesHighlights
            half luminance = GetLuminance(lut);
            half shadowsFactor = 1 - smoothstep(_SmhRange.x, _SmhRange.y, luminance);
            half highlightFactor = smoothstep(_SmhRange.z, _SmhRange.w, luminance);
            half midtonesFactor = 1 - shadowsFactor - highlightFactor;
            lut = lut * _SmhShadows * shadowsFactor
                + lut * _SmhMidtones * midtonesFactor
                + lut * _SmhHighlights * highlightFactor;

            //LiftGammaGain
            lut = lut * _LggGain + _LggLift;
            lut = sign(lut) * pow(abs(lut), _LggGamma);//LUTなので多少の重たい処理は許される。

            //Hue
            half3 hsv = RgbToHsv(lut);
            hsv.x += _ColorAdjustmentParams.y;
            lut = HsvToRgb(hsv);

            //Saturation
            luminance = GetLuminance(lut);
            lut = (lut - luminance) * _ColorAdjustmentParams.z + luminance;

            //Nega
            lut = max(0, lerp(lut, (1 - lut), _NegaIntensity));

            //Tonemapping
            if(TONEMAPPING_NEUTRAL) //Neutral
            {
                lut = NeutralTonemap(lut);
            }
            else if(TONEMAPPING_REINHARD) //Reinhard
            {
                lut = lut * rcp(1 + lut);
            }
            else if(TONEMAPPING_ACES) //ACES
            {
                lut = AcesTonemap(ACEScg_to_ACES(lut));
            }

            return half4(lut, 1);
        }

        ENDHLSL

        ZWrite Off
        Cull Off
        ZTest Always


        Pass
        {            
            Name "CreatePostProcessLut"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Fragment

            ENDHLSL
        }
    }
}