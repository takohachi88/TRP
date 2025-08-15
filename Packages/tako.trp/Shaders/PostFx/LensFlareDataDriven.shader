//URPÇ©ÇÁà⁄êAÅB
Shader "Hidden/Trp/PostFx/LensFlareDataDriven"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "Trp" }

        HLSLINCLUDE
        #include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/UnityInput.hlsl"
        #include "Packages/tako.trp/ShaderLibrary/DeclareDepthTexture.hlsl"
        ENDHLSL
        
        // Additive
        Pass
        {
            Name "LensFlareAdditive"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            Blend One One
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF

            #pragma multi_compile_vertex _ FLARE_OPENGL3_OR_OPENGLCORE

            #pragma multi_compile _ FLARE_HAS_OCCLUSION

            #define FLARE_ADDITIVE_BLEND

            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareCommon.hlsl"
            ENDHLSL
        }
        // Screen
        Pass
        {
            Name "LensFlareScreen"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            Blend One OneMinusSrcColor
            BlendOp Max
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF

            #pragma multi_compile_vertex _ FLARE_OPENGL3_OR_OPENGLCORE

            #pragma multi_compile _ FLARE_HAS_OCCLUSION

            #define FLARE_SCREEN_BLEND
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareCommon.hlsl"

            ENDHLSL
        }
        // Premultiply
        Pass
        {
            Name "LensFlarePremultiply"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            Blend One OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF

            #pragma multi_compile_vertex _ FLARE_OPENGL3_OR_OPENGLCORE

            #pragma multi_compile _ FLARE_HAS_OCCLUSION

            #define FLARE_PREMULTIPLIED_BLEND
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareCommon.hlsl"

            ENDHLSL
        }
        // Lerp
        Pass
        {
            Name "LensFlareLerp"
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ FLARE_INVERSE_SDF

            #pragma multi_compile_vertex _ FLARE_OPENGL3_OR_OPENGLCORE

            #pragma multi_compile _ FLARE_HAS_OCCLUSION

            #define FLARE_LERP_BLEND
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareCommon.hlsl"

            ENDHLSL
        }
        // OcclusionOnly
        Pass
        {
            Name "LensFlareOcclusion"

            Blend Off
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vertOcclusion
            #pragma fragment fragOcclusion
            #pragma exclude_renderers gles

            #define FLARE_COMPUTE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareCommon.hlsl"

            ENDHLSL
        }
    }
}
