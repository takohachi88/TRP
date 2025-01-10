#ifndef TRP_UNITY_INPUT
#define TRP_UNITY_INPUT

CBUFFER_START(UnityPerDraw)

float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
real4 unity_WorldTransformParams;

CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

float4 unity_OrthoParams;
float4 _ScreenParams;
float4 _ProjectionParams;


#endif