﻿#ifndef TRP_UNITY_INPUT
#define TRP_UNITY_INPUT

//https://docs.unity3d.com/ja/2023.2/Manual/SL-UnityShaderVariables.html

//UnityPerDrawはこの組み合わせでなければならない。（こうでないとSRPBatcherは機能しない。）
CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4 unity_LODFade;
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

//x: width
//y: height
//z: 1 + 1 / width
//w: 1 + 1 / height
float4 _ScreenParams;

float4 _ProjectionParams;

//x: 1 - far / linear
//y: far / linear
//z: x / far
//w: y / far
float4 _ZBufferParams;

float4 _ScreenSize;
float4 _RTHandleScale;

float4x4 unity_WorldToCamera;
float4x4 unity_CameraToWorld;

float4 _FrustumPlanes[6]; // {(a, b, c) = N, d = -dot(N, P)} [L, R, T, B, N, F]

#endif