#ifndef TRP_COMMON
#define TRP_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"


#define TEXTURE2D_X(textureName) TEXTURE2D(textureName)
#define TEXTURE2D_X_LOD(textureName) TEXTURE2D_LOD(textureName)
#define TEXTURE2D_X_FLOAT(textureName) TEXTURE2D_FLOAT(textureName)

//rcp(2 * PI)
#define PI_TWO_RCP 0.159155

#define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod) SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod)

float2 _AspectFit;

half DotDistance(float2 uv, float2 center, float sizeInv, float smoothness, bool fitAspect)
{
    float2 dist = abs(uv - center) * sizeInv;
    dist *= fitAspect ? 1 : _AspectFit;
    return smoothstep(0.5 - smoothness * 0.5, 0.5 + smoothness * 0.5, dot(dist, dist));
}

#endif