#ifndef TRP_OUTLINE_PASS_INCLUDED
#define TRP_OUTLINE_PASS_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    half3 normalOS : NORMAL;
    half4 tangentOS : TANGENT;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    half4 smoothNormalTS : TEXCOORD2;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : POSITION_WS;
    half3 normalWS : NORMAL;
    float2 uv : TEXCOORD0;
    float fogFactor : FOG_FACTOR;
};

Varyings OutlineVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    VertexInputs vertexInputs = GetVertexInputs(input.positionOS.xyz, input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWS = vertexInputs.positionWS;
    output.normalWS = vertexInputs.normalWS;
    output.fogFactor = vertexInputs.fogFactor;

    half width = input.color.r;

#if defined(OUTLINE_SOFT_EDGE)

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 biTangentOS = normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
    float3 biTangentWS = TransformObjectToWorldDir(biTangentOS);
    float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
    
    float3x3 tangentTransform = float3x3(tangentWS, biTangentWS, normalWS);
    float3 world = TransformTangentToWorldDir(input.smoothNormalTS.xyz, tangentTransform, false).xyz;
    float3 smoothNormalOS = TransformWorldToObjectDir(world, true);
    smoothNormalOS *= _OutlineWidth * width * vertexInputs.positionCS.w * _TanFov;
    smoothNormalOS += input.positionOS.xyz;
    output.positionCS = TransformObjectToHClip(smoothNormalOS);

#else
    output.positionCS = vertexInputs.positionCS;
    half3 normalCS = TransformWorldToHClipDir(output.normalWS);
    output.positionCS.xy += normalCS.xy * _OutlineWidth * width * output.positionCS.w * _TanFov;
#endif

    return output;
}

half4 OutlineFragment(Varyings input) : SV_TARGET
{
    #if defined(OUTLINE_SINGLE_COLOR)

    half4 output = _OutlineColor;
    output.rgb = MixFog(output.rgb, input.fogFactor);
    return output;

    #else

    half4 output = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearClamp, input.uv);
    
    AlphaClip(output.a, _Cutoff);

    half3 lighting = ToonLighting(input.positionWS, input.normalWS, output.rgb, input.positionCS.xy * _AttachmentSize.xy);
    half luminance = Luminance(lighting);
    output.rgb = lighting;
    output.rgb *= _OutlineColor.rgb;
    half smoothness = 0.05;
    output.rgb += smoothstep(_OutlineLightStrengthThreshold - smoothness, _OutlineLightStrengthThreshold + smoothness, luminance) * lighting * _OutlineLightStrength;
    output.rgb = MixFog(output.rgb, input.fogFactor);
    return output;

    #endif
}
#endif