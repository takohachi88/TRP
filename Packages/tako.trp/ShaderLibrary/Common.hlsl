#ifndef TRP_COMMON
#define TRP_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

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
#include "GpuResidentDrawerInput.hlsl"

#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.visualeffectgraph/Shaders/VFXMatricesOverride.hlsl"
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#include "Packages/takolib.common/ShaderLibrary/Common.hlsl"

//rcp(2 * PI)
#define PI_TWO_RCP 0.159155

//UnityEngine.LightTypeに準拠。
#define LIGHT_TYPE_SPOT 0
#define LIGHT_TYPE_DIRECTIONAL 1
#define LIGHT_TYPE_POINT 2

//UnityEngine.TextureWrapModeに準拠。
#define WRAP_MODE_REPEAT 0
#define WRAP_MODE_CLAMP 1

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

#ifndef TRP_TIME
#define TRP_TIME _Time.y
#endif

half DotDistance(float2 uv, float2 center, float sizeInv, float smoothness, bool fitAspect)
{
    float2 dist = abs(uv - center) * sizeInv;
    dist *= fitAspect ? 1 : _AspectFit;
    return smoothstep(0.5 - smoothness * 0.5, 0.5 + smoothness * 0.5, dot(dist, dist));
}

//powの整数乗はコンパイル時に乗算に変換されるようだが、されないケースもなくはないかもしれないので一応定義。
#define Pow2(a) ((a) * (a))
#define Pow3(a) ((a) * (a) * (a))
#define Pow4(a) ((a) * (a) * (a) * (a))
#define Pow5(a) ((a) * (a) * (a) * (a) * (a))
#define Pow6(a) ((a) * (a) * (a) * (a) * (a) * (a))

//schlickの近似式。powの代用だが、tの値域が0～1である点に注意。
float Schlick(float t, float k)
{
    return t / (k - k * t + t);
}

// Schlick近似によるFresnel反射率。
half3 SchlickFresnel(half3 f0, half f90, half cosTheta)
{
    half fresnel = Pow5(1.0h - saturate(cosTheta));
    return f0 + (f90.xxx - f0) * fresnel;
}

half SchlickFresnel(half f0, half f90, half cosTheta)
{
    half fresnel = Pow5(1.0h - saturate(cosTheta));
    return f0 + (f90 - f0) * fresnel;
}

float2 Rotate(float2 uv, float radian, float2 center)
{
    float2 trigs;
    sincos(radian, trigs.x, trigs.y);
    return mul(float2x2(trigs.y, -trigs.x,
                        trigs.x,  trigs.y), uv - center) + center;
}

float2 Polar(float2 uv, float2 tiling, float2 offset)
{
	float radius = length(uv) * 2.0;
	float theta = atan2(uv.y, uv.x) / (2.0 * PI) + 0.5;
	return float2(radius, theta) * tiling.yx + offset.yx;
}

// ハッシュ値から classic Perlin noise の勾配を選び、格子点からの相対座標との内積を返す。
// 固定された勾配集合を用いることで、三角関数と勾配の正規化を避ける。
float PerlinGradientDot3D(uint hash, float3 offset)
{
    uint gradient = hash & 15u;
    float u = gradient < 8u ? offset.x : offset.y;
    float v = gradient < 4u ? offset.y : ((gradient == 12u || gradient == 14u) ? offset.x : offset.z);
    u = (gradient & 1u) == 0u ? u : -u;
    v = (gradient & 2u) == 0u ? v : -v;
    return u + v;
}

// 3出力ハッシュから、互いに異なる3組の勾配内積をまとめて求める。
// 格子点ごとのハッシュ計算を成分ごとに繰り返さず、3次元出力の負荷を抑える。
float3 PerlinGradientDot3D(uint3 hash, float3 offset)
{
    return float3(PerlinGradientDot3D(hash.x, offset),
                  PerlinGradientDot3D(hash.y, offset),
                  PerlinGradientDot3D(hash.z, offset));
}

// 3次元の符号付き Perlin noise。整数格子上では0になり、おおむね [-1, 1] の範囲を返す。
// 格子座標のハッシュは整数演算のみで行い、負座標もビット表現を保ったまま安定して処理する。
float PerlinNoise3_1(float3 position)
{
    int3 cell = (int3)floor(position);
    uint3 lattice = asuint(cell);
    float3 offset = position - float3(cell);

    // C2連続な6t^5 - 15t^4 + 10t^3。格子境界で補間の1階・2階微分を0にする。
    float3 fade = offset * offset * offset * (offset * (offset * 6.0 - 15.0) + 10.0);

    uint hash;
    Hash_Tchou_3_1_uint(lattice + uint3(0u, 0u, 0u), hash);
    float n000 = PerlinGradientDot3D(hash, offset);
    Hash_Tchou_3_1_uint(lattice + uint3(1u, 0u, 0u), hash);
    float n100 = PerlinGradientDot3D(hash, offset - float3(1.0, 0.0, 0.0));
    Hash_Tchou_3_1_uint(lattice + uint3(0u, 1u, 0u), hash);
    float n010 = PerlinGradientDot3D(hash, offset - float3(0.0, 1.0, 0.0));
    Hash_Tchou_3_1_uint(lattice + uint3(1u, 1u, 0u), hash);
    float n110 = PerlinGradientDot3D(hash, offset - float3(1.0, 1.0, 0.0));
    Hash_Tchou_3_1_uint(lattice + uint3(0u, 0u, 1u), hash);
    float n001 = PerlinGradientDot3D(hash, offset - float3(0.0, 0.0, 1.0));
    Hash_Tchou_3_1_uint(lattice + uint3(1u, 0u, 1u), hash);
    float n101 = PerlinGradientDot3D(hash, offset - float3(1.0, 0.0, 1.0));
    Hash_Tchou_3_1_uint(lattice + uint3(0u, 1u, 1u), hash);
    float n011 = PerlinGradientDot3D(hash, offset - float3(0.0, 1.0, 1.0));
    Hash_Tchou_3_1_uint(lattice + uint3(1u, 1u, 1u), hash);
    float n111 = PerlinGradientDot3D(hash, offset - 1.0);

    float4 nx = lerp(float4(n000, n010, n001, n011), float4(n100, n110, n101, n111), fade.x);
    float2 nxy = lerp(nx.xz, nx.yw, fade.y);
    return lerp(nxy.x, nxy.y, fade.z);
}

// 3次元座標から、各成分がおおむね [-1, 1] の3次元 Perlin noise を返す。
// Hash_Tchou_3_3_uint により、3回のスカラー noise 評価に必要な24回のハッシュを8回に削減する。
float3 PerlinNoise3_3(float3 position)
{
    int3 cell = (int3)floor(position);
    uint3 lattice = asuint(cell);
    float3 offset = position - float3(cell);

    float3 fade = offset * offset * offset * (offset * (offset * 6.0 - 15.0) + 10.0);

    uint3 hash;
    Hash_Tchou_3_3_uint(lattice + uint3(0u, 0u, 0u), hash);
    float3 n000 = PerlinGradientDot3D(hash, offset);
    Hash_Tchou_3_3_uint(lattice + uint3(1u, 0u, 0u), hash);
    float3 n100 = PerlinGradientDot3D(hash, offset - float3(1.0, 0.0, 0.0));
    Hash_Tchou_3_3_uint(lattice + uint3(0u, 1u, 0u), hash);
    float3 n010 = PerlinGradientDot3D(hash, offset - float3(0.0, 1.0, 0.0));
    Hash_Tchou_3_3_uint(lattice + uint3(1u, 1u, 0u), hash);
    float3 n110 = PerlinGradientDot3D(hash, offset - float3(1.0, 1.0, 0.0));
    Hash_Tchou_3_3_uint(lattice + uint3(0u, 0u, 1u), hash);
    float3 n001 = PerlinGradientDot3D(hash, offset - float3(0.0, 0.0, 1.0));
    Hash_Tchou_3_3_uint(lattice + uint3(1u, 0u, 1u), hash);
    float3 n101 = PerlinGradientDot3D(hash, offset - float3(1.0, 0.0, 1.0));
    Hash_Tchou_3_3_uint(lattice + uint3(0u, 1u, 1u), hash);
    float3 n011 = PerlinGradientDot3D(hash, offset - float3(0.0, 1.0, 1.0));
    Hash_Tchou_3_3_uint(lattice + uint3(1u, 1u, 1u), hash);
    float3 n111 = PerlinGradientDot3D(hash, offset - 1.0);

    float3 nx00 = lerp(n000, n100, fade.x);
    float3 nx10 = lerp(n010, n110, fade.x);
    float3 nx01 = lerp(n001, n101, fade.x);
    float3 nx11 = lerp(n011, n111, fade.x);
    float3 nxy0 = lerp(nx00, nx10, fade.y);
    float3 nxy1 = lerp(nx01, nx11, fade.y);
    return lerp(nxy0, nxy1, fade.z);
}

// 3次元の整数格子座標を、安定した [0, 1] のスカラー値へ変換する。
float ValueNoiseHash3_1(uint3 lattice)
{
    uint hash;
    Hash_Tchou_3_1_uint(lattice, hash);
    return (hash >> 8u) * (1.0 / float(0x00ffffff));
}

// 3次元の整数格子座標を、安定した [0, 1] の3成分へ一度のハッシュで変換する。
float3 ValueNoiseHash3_3(uint3 lattice)
{
    uint3 hash;
    Hash_Tchou_3_3_uint(lattice, hash);
    return (hash >> 8u) * (1.0 / float(0x00ffffff));
}

// 3次元座標から [0, 1] のスカラー Value noise を返す。
// 低負荷なC1連続の cubic fade を使用し、勾配の選択と内積を行わない。
float ValueNoise3_1(float3 position)
{
    int3 cell = (int3)floor(position);
    uint3 lattice = asuint(cell);
    float3 fade = position - float3(cell);
    fade = fade * fade * (3.0 - 2.0 * fade);

    float4 nx = lerp(
        float4(ValueNoiseHash3_1(lattice + uint3(0u, 0u, 0u)),
               ValueNoiseHash3_1(lattice + uint3(0u, 1u, 0u)),
               ValueNoiseHash3_1(lattice + uint3(0u, 0u, 1u)),
               ValueNoiseHash3_1(lattice + uint3(0u, 1u, 1u))),
        float4(ValueNoiseHash3_1(lattice + uint3(1u, 0u, 0u)),
               ValueNoiseHash3_1(lattice + uint3(1u, 1u, 0u)),
               ValueNoiseHash3_1(lattice + uint3(1u, 0u, 1u)),
               ValueNoiseHash3_1(lattice + uint3(1u, 1u, 1u))),
        fade.x);
    float2 nxy = lerp(nx.xz, nx.yw, fade.y);
    return lerp(nxy.x, nxy.y, fade.z);
}

// 3次元座標から各成分が [0, 1] の3次元 Value noise を返す。
// 3成分を個別評価せず、8回の Hash_Tchou_3_3_uint でまとめて生成する。
float3 ValueNoise3_3(float3 position)
{
    int3 cell = (int3)floor(position);
    uint3 lattice = asuint(cell);
    float3 fade = position - float3(cell);
    fade = fade * fade * (3.0 - 2.0 * fade);

    float3 n000 = ValueNoiseHash3_3(lattice + uint3(0u, 0u, 0u));
    float3 n100 = ValueNoiseHash3_3(lattice + uint3(1u, 0u, 0u));
    float3 n010 = ValueNoiseHash3_3(lattice + uint3(0u, 1u, 0u));
    float3 n110 = ValueNoiseHash3_3(lattice + uint3(1u, 1u, 0u));
    float3 n001 = ValueNoiseHash3_3(lattice + uint3(0u, 0u, 1u));
    float3 n101 = ValueNoiseHash3_3(lattice + uint3(1u, 0u, 1u));
    float3 n011 = ValueNoiseHash3_3(lattice + uint3(0u, 1u, 1u));
    float3 n111 = ValueNoiseHash3_3(lattice + uint3(1u, 1u, 1u));

    float3 nx00 = lerp(n000, n100, fade.x);
    float3 nx10 = lerp(n010, n110, fade.x);
    float3 nx01 = lerp(n001, n101, fade.x);
    float3 nx11 = lerp(n011, n111, fade.x);
    float3 nxy0 = lerp(nx00, nx10, fade.y);
    float3 nxy1 = lerp(nx01, nx11, fade.y);
    return lerp(nxy0, nxy1, fade.z);
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
half ComputeFogIntensity(half fogFactor)
{
    half fogIntensity = half(0.0);
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        #if defined(FOG_EXP)
            // factor = exp(-density*z)
            // fogFactor = density*z
            fogIntensity = saturate(exp2(-fogFactor));
        #elif defined(FOG_EXP2)
            // factor = exp(-(density*z)^2)
            // fogFactor = density*z
            fogIntensity = saturate(exp2(-fogFactor * fogFactor));
        #elif defined(FOG_LINEAR)
            fogIntensity = fogFactor;
        #endif
    #endif
    return fogIntensity;
}

// 頂点から補間した視空間Zを使用し、フラグメント単位でFog強度を求める。
half ComputeFogIntensityFromPositionVS(float positionVSZ)
{
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        float viewZ = -positionVSZ;
        // カメラ位置を0とする視空間Zを、ニア平面からの距離へ変換する。
        float nearToFarZ = max(viewZ - _ProjectionParams.y, 0.0);
        return ComputeFogIntensity(ComputeFogFactorZ0ToFar(nearToFarZ));
    #else
        return half(0.0);
    #endif
}

// 視空間ZからFog強度を計算し、フラグメントカラーへ適用する。
half3 MixFog(half3 fragColor, float positionVSZ)
{
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        half fogIntensity = ComputeFogIntensityFromPositionVS(positionVSZ);
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
