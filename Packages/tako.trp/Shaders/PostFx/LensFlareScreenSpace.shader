//URPのLensFlareScreenSpaceを移植・改造。
Shader "Hidden/Trp/PostFx/LensFlareScreenSpace"
{
    HLSLINCLUDE

    #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
    #include "Packages/tako.trp/ShaderLibrary/LensFlareScreenSpace.hlsl"

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "Trp" }
        
        Pass
        {
            Name "LensFlareScreenSpac Prefilter"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentPrefilter

            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Downsample"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentDownsample

            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Upsample"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentUpsample

            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Streaks Accumulate"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

            Blend SrcAlpha One

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentStreaksAccumulate

            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Composition"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentComposition

            ENDHLSL
        }
        
        Pass
        {
            Name "LensFlareScreenSpace Write to BloomTexture"
            Tags{ "LightMode" = "Forward"  "RenderQueue" = "Transparent" }
            
            Blend One One
            BlendOp Add
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment FragmentWrite

            ENDHLSL
        }

    }
}