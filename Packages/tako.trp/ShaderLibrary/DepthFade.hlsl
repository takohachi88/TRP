#ifndef TRP_DEPTH_FADE
#define TRP_DEPTH_FADE

#include "Packages/tako.trp/ShaderLibrary/DeclareDepthTexture.hlsl"

//SoftParticleなど、主にtransparent surfaceからscene depthに沿ってフェードアウトさせる表現に用いる。
float DepthFade(float near, float far, float2 positionSS, float3 positionWS)
{
    float fade = 1;
    if (near > 0.0 || far > 0.0)
    {
        float2 uv = positionSS;

        float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_LinearClamp, uv).r;
        float sceneZ = (unity_OrthoParams.w == 0) ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
        float thisZ = LinearEyeDepth(positionWS.xyz, GetWorldToViewMatrix());
        fade = saturate(far * ((sceneZ - near) - thisZ));
    }
    return fade;
}

#endif