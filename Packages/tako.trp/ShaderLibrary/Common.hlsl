#ifndef TRP_COMMON
#define TRP_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM

#include "UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.visualeffectgraph/Shaders/VFXMatricesOverride.hlsl"
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#include "Packages/takolib.common/ShaderLibrary/Common.hlsl"

//rcp(2 * PI)
#define PI_TWO_RCP 0.159155

//XR対応ないので「_X」は基本使わないが、LensFlareCommon.hlslなどで必要になってしまうので定義する。
#define TEXTURE2D_X(textureName) TEXTURE2D(textureName)
#define TEXTURE2D_X_LOD(textureName) TEXTURE2D_LOD(textureName)
#define TEXTURE2D_X_FLOAT(textureName) TEXTURE2D_FLOAT(textureName)
static uint unity_StereoEyeIndex;
#define SLICE_ARRAY_INDEX   unity_StereoEyeIndex
#define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2) SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
#define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod) SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod)
#define LOAD_TEXTURE2D_X(textureName, unCoord2) LOAD_TEXTURE2D_ARRAY(textureName, unCoord2, SLICE_ARRAY_INDEX)
#define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod) LOAD_TEXTURE2D_ARRAY_LOD(textureName, unCoord2, SLICE_ARRAY_INDEX, lod)

float2 _AspectFit;
float4 _AttachmentSize;

float4 unity_FogParams;
real4  unity_FogColor;

float _TanFov; // tan(FoV角度)
float4 _ScaledScreenParams;

float4 _Time;

half DotDistance(float2 uv, float2 center, float sizeInv, float smoothness, bool fitAspect)
{
    float2 dist = abs(uv - center) * sizeInv;
    dist *= fitAspect ? 1 : _AspectFit;
    return smoothstep(0.5 - smoothness * 0.5, 0.5 + smoothness * 0.5, dot(dist, dist));
}

half Pow2(half a)
{
    return a * a;
}

//schlickの近似式。powの代用だが、tの値域が0～1である点に注意。
float Schlick(float t, float k)
{
    return t / (k - k * t + t);
}

float2 Rotate(float2 uv, float radian, float2 center)
{
    float2 trigs;
    sincos(radian, trigs.x, trigs.y);
    return mul(float2x2(trigs.y, -trigs.x,
                        trigs.x,  trigs.y), uv - center) + center;
}

void AlphaClip(half alpha, half cutoff)
{
    #if defined(ALPHA_CLIP)
    clip(alpha - cutoff);
    #endif
}

float3 GetCurrentViewPosition()
{
    return _WorldSpaceCameraPos;
}

bool IsPerspectiveProjection()
{
    return (unity_OrthoParams.w == 0);
}

//URPから移植。
#if UNITY_REVERSED_Z
    // TODO: workaround. There's a bug where SHADER_API_GL_CORE gets erroneously defined on switch.
    #if (defined(SHADER_API_GLCORE) && !defined(SHADER_API_SWITCH)) || defined(SHADER_API_GLES3)
        //GL with reversed z => z clip range is [near, -far] -> remapping to [0, far]
        #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max((coord - _ProjectionParams.y)/(-_ProjectionParams.z-_ProjectionParams.y)*_ProjectionParams.z, 0)
    #else
        //D3d with reversed Z => z clip range is [near, 0] -> remapping to [0, far]
        //max is required to protect ourselves from near plane not being correct/meaningful in case of oblique matrices.
        #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(((1.0-(coord)/_ProjectionParams.y)*_ProjectionParams.z),0)
    #endif
#elif UNITY_UV_STARTS_AT_TOP
    //D3d without reversed z => z clip range is [0, far] -> nothing to do
    #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#else
    //Opengl => z clip range is [-near, far] -> remapping to [0, far]
    #define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(((coord + _ProjectionParams.y)/(_ProjectionParams.z+_ProjectionParams.y))*_ProjectionParams.z, 0)
#endif

//URPから移植。
real ComputeFogFactorZ0ToFar(float z)
{
    #if defined(FOG_LINEAR)
    // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
    float fogFactor = saturate(z * unity_FogParams.z + unity_FogParams.w);
    return real(fogFactor);
    #elif defined(FOG_EXP) || defined(FOG_EXP2)
    // factor = exp(-(density*z)^2)
    // -density * z computed at vertex
    return real(unity_FogParams.x * z);
    #else
        return real(0.0);
    #endif
}

//URPから移植。
real ComputeFogFactor(float zPositionCS)
{
    float clipZ_0Far = UNITY_Z_0_FAR_FROM_CLIPSPACE(zPositionCS);
    return ComputeFogFactorZ0ToFar(clipZ_0Far);
}

//URPから移植。
half ComputeFogIntensity(half fogFactor)
{
    half fogIntensity = half(0.0);
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        #if defined(FOG_EXP)
            // factor = exp(-density*z)
            // fogFactor = density*z compute at vertex
            fogIntensity = saturate(exp2(-fogFactor));
        #elif defined(FOG_EXP2)
            // factor = exp(-(density*z)^2)
            // fogFactor = density*z compute at vertex
            fogIntensity = saturate(exp2(-fogFactor * fogFactor));
        #elif defined(FOG_LINEAR)
            fogIntensity = fogFactor;
        #endif
    #endif
    return fogIntensity;
}


//URPから移植。
half3 MixFog(half3 fragColor, half fogFactor)
{
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        half fogIntensity = ComputeFogIntensity(fogFactor);
        // Workaround for UUM-61728: using a manual lerp to avoid rendering artifacts on some GPUs when Vulkan is used
        fragColor = fragColor * fogIntensity + unity_FogColor.rgb * (half(1.0) - fogIntensity);
    #endif
    return fragColor;
}

struct VertexInputs
{
    float3 positionWS;
    float3 positionVS;
    float4 positionCS;
    float4 positionNDC;
    half3 directionVS;
    half3 tangentWS;
    half3 bitangentWS;
    half3 normalWS;
    float fogFactor;
};

VertexInputs GetVertexInputs(float3 positionOS, half3 normalOS = half3(0, 0, 1), half4 tangentOS = half4(1, 0, 0, 1))
{
    VertexInputs output;
    output.positionWS = TransformObjectToWorld(positionOS);
    output.positionVS = TransformWorldToView(output.positionWS);
    output.positionCS = TransformWorldToHClip(output.positionWS);

    float4 ndc = output.positionCS * 0.5f;
    output.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    output.positionNDC.zw = output.positionCS.zw;

    output.directionVS = output.positionWS - GetCurrentViewPosition();

    // mikkts space compliant. only normalize when extracting normal at frag.
    real sign = real(tangentOS.w) * GetOddNegativeScale();
    output.normalWS = TransformObjectToWorldNormal(normalOS);
    output.tangentWS = real3(TransformObjectToWorldDir(tangentOS.xyz));
    output.bitangentWS = real3(cross(output.normalWS, float3(output.tangentWS))) * sign;

    output.fogFactor = ComputeFogFactor(output.positionCS.z);

    return output;
}

//UI標準シェーダーのボイラープレート。
half UiAlphaRoundUp(half alpha)
{
    //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
    //The incoming alpha could have numerical instability, which makes it very sensible to
    //HDR color transparency blend, when it blends with the world's texture.
    const half alphaPrecision = half(0xff);
    const half invAlphaPrecision = half(1.0 / alphaPrecision);
    return round(alpha * alphaPrecision) * invAlphaPrecision;
}

//LensFlareCommonで必要な関数。
float4 GetScaledScreenParams()
{
    return _ScaledScreenParams;
}


half3 NormalizeNormalPerPixel(half3 normalWS)
{
// With XYZ normal map encoding we sporadically sample normals with near-zero-length causing Inf/NaN
#if defined(UNITY_NO_DXT5nm) && defined(_NORMALMAP)
    return SafeNormalize(normalWS);
#else
    return normalize(normalWS);
#endif
}

float LinearDepthToEyeDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
        return _ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
#else
    return _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
#endif
}

#endif