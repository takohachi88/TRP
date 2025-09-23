//_CameraDepthTextureと_CameraNormalsTextureを生成するためのパス。
#ifndef TRP_DEPTH_NORMALS_PASS_INCLUDED
#define TRP_DEPTH_NORMALS_PASS_INCLUDED

struct Attributes
{
    float4 positionOS : POSITION;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD1;
    float3 normalWS : TEXCOORD2;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        
    VertexInputs vertexInputs = GetVertexInputs(input.positionOS.xyz, input.normal, input.tangentOS);
    output.positionCS = vertexInputs.positionCS;
    output.normalWS = vertexInputs.normalWS;
    output.normalWS = normalize(vertexInputs.normalWS);
    return output;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(ALPHA_CLIP)
        AlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    outNormalWS = half4(normalWS, 0.0);

    #ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}
#endif
