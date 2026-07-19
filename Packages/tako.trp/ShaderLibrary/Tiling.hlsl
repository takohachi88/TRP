#ifndef TRP_TILING
#define TRP_TILING

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// Morten S. Mikkelsen, "Practical Real-Time Hex-Tiling" (JCGT 2022)
// https://jcgt.org/published/0011/03/05/
//
// 論文で推奨されている特徴量の寄与率。必要な場合は include より前で上書きできる。
#ifndef TRP_HEX_TILING_FALLOFF_CONTRAST
    #define TRP_HEX_TILING_FALLOFF_CONTRAST 0.6h
#endif

// 複数テクスチャで共有する Hex Tiling の座標情報。
// デバッグ専用のタイル ID は含めず、通常利用時のレジスタ負荷を抑える。
struct HexTilingContext
{
    float2 sampleUv1;
    float2 sampleUv2;
    float2 sampleUv3;
    float2 sampleUvDx1;
    float2 sampleUvDx2;
    float2 sampleUvDx3;
    float2 sampleUvDy1;
    float2 sampleUvDy2;
    float2 sampleUvDy3;
    float2 rotation1;
    float2 rotation2;
    float2 rotation3;
    half3 seventhPowerWeights;
};

// UV を三角格子へ写像し、現在の三角形を構成する3頂点と重心座標を返す。
void TrpGetHexTriangleGrid(float2 uv, out half3 barycentricWeights, out int2 vertex1, out int2 vertex2, out int2 vertex3)
{
    // 2 * sqrt(3) を掛け、正三角形の格子間隔へ合わせる。
    const float2 scaledUv = uv * 3.46410162;
    const float2x2 gridToSkewedGrid = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
    const float2 skewedCoordinate = mul(gridToSkewedGrid, scaledUv);
    const int2 baseId = int2(floor(skewedCoordinate));

    float3 triangleCoordinate = float3(frac(skewedCoordinate), 0.0);
    triangleCoordinate.z = 1.0 - triangleCoordinate.x - triangleCoordinate.y;

    // 対角線のどちら側にいるかで、使用する三角形を切り替える。
    const float upperTriangle = step(0.0, -triangleCoordinate.z);
    const float triangleSign = 2.0 * upperTriangle - 1.0;
    barycentricWeights = half3(-triangleCoordinate.z * triangleSign, upperTriangle - triangleCoordinate.y * triangleSign, upperTriangle - triangleCoordinate.x * triangleSign);

    const int upperTriangleId = int(upperTriangle);
    vertex1 = baseId + int2(upperTriangleId, upperTriangleId);
    vertex2 = baseId + int2(upperTriangleId, 1 - upperTriangleId);
    vertex3 = baseId + int2(1 - upperTriangleId, upperTriangleId);
}

// タイルごとに決定的なランダム UV オフセットを生成する。
float2 TrpHexTilingHash(int2 vertex)
{
    // Common.hlsl 経由で読み込まれる Random.hlsl の Jenkins hash を使用する。
    // int のビット列を保持したまま uint へ変換し、末尾要素を各軸の salt とする。
    const uint2 seed = asuint(vertex);
    return float2(GenerateHashedRandomFloat(uint3(seed, 0u)), GenerateHashedRandomFloat(uint3(seed, 1u)));
}

// 三角格子上の整数頂点を、元の UV 空間における六角タイル中心へ戻す。
float2 TrpGetHexTileCenter(int2 vertex)
{
    const float2x2 skewedGridToGrid = float2x2(1.0, 0.5, 0.0, 0.86602540);
    return mul(skewedGridToGrid, float2(vertex)) * 0.28867513;
}

// タイル ID から cos と sin を生成する。rotationStrength=0 なら回転しない。
float2 TrpGetHexTileRotation(int2 vertex, float rotationStrength)
{
    float angle = abs(vertex.x * vertex.y) + abs(vertex.x + vertex.y) + PI;
    angle = fmod(angle, TWO_PI);
    angle += angle < 0.0 ? TWO_PI : 0.0;
    angle -= angle > PI ? TWO_PI : 0.0;
    angle *= rotationStrength;

    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    return float2(cosine, sine);
}

// 行ベクトルと回転行列の積。UV とその微分の回転に使用する。
float2 TrpRotateHexUv(float2 value, float2 rotation)
{
    return float2(value.x * rotation.x + value.y * rotation.y, value.y * rotation.x - value.x * rotation.y);
}

// 回転行列と列ベクトルの積。法線由来の偏微分を元の向きへ戻すために使用する。
float2 TrpRotateHexDerivative(float2 rotation, float2 value)
{
    return float2(value.x * rotation.x - value.y * rotation.y, value.x * rotation.y + value.y * rotation.x);
}

// 論文の固定指数 gamma=7 を、汎用 pow より安価な3段の乗算で正確に求める。
half3 TrpGetHexSeventhPowerWeights(half3 barycentricWeights)
{
    const half3 squaredWeights = barycentricWeights * barycentricWeights;
    const half3 fourthPowerWeights = squaredWeights * squaredWeights;
    return fourthPowerWeights * squaredWeights * barycentricWeights;
}

// 複数テクスチャで再利用できるサンプリング座標、微分、回転を一度だけ生成する。
HexTilingContext TrpGetHexTilingContext(float2 uv, float rotationStrength, out int2 vertex1, out int2 vertex3)
{
    const float2 uvDx = ddx(uv);
    const float2 uvDy = ddy(uv);

    half3 barycentricWeights;
    int2 vertex2;
    TrpGetHexTriangleGrid(uv, barycentricWeights, vertex1, vertex2, vertex3);

    HexTilingContext context;
    context.rotation1 = TrpGetHexTileRotation(vertex1, rotationStrength);
    context.rotation2 = TrpGetHexTileRotation(vertex2, rotationStrength);
    context.rotation3 = TrpGetHexTileRotation(vertex3, rotationStrength);

    const float2 center1 = TrpGetHexTileCenter(vertex1);
    const float2 center2 = TrpGetHexTileCenter(vertex2);
    const float2 center3 = TrpGetHexTileCenter(vertex3);
    context.sampleUv1 = TrpRotateHexUv(uv - center1, context.rotation1) + center1 + TrpHexTilingHash(vertex1);
    context.sampleUv2 = TrpRotateHexUv(uv - center2, context.rotation2) + center2 + TrpHexTilingHash(vertex2);
    context.sampleUv3 = TrpRotateHexUv(uv - center3, context.rotation3) + center3 + TrpHexTilingHash(vertex3);

    context.sampleUvDx1 = TrpRotateHexUv(uvDx, context.rotation1);
    context.sampleUvDx2 = TrpRotateHexUv(uvDx, context.rotation2);
    context.sampleUvDx3 = TrpRotateHexUv(uvDx, context.rotation3);
    context.sampleUvDy1 = TrpRotateHexUv(uvDy, context.rotation1);
    context.sampleUvDy2 = TrpRotateHexUv(uvDy, context.rotation2);
    context.sampleUvDy3 = TrpRotateHexUv(uvDy, context.rotation3);
    context.seventhPowerWeights = TrpGetHexSeventhPowerWeights(barycentricWeights);
    return context;
}

// uv               : タイリング対象の UV。複数マップで同じ値を共有する。
// rotationStrength : タイルごとの回転量。0 で回転なし、1 で生成された角度をそのまま使用する。
// 戻り値           : 各テクスチャのサンプリングで再利用する座標情報。
HexTilingContext GetHexTilingContext(float2 uv, float rotationStrength = 0.0)
{
    int2 unusedVertex1;
    int2 unusedVertex3;
    return TrpGetHexTilingContext(uv, rotationStrength, unusedVertex1, unusedVertex3);
}

// Schlick の Bias 関数を対称化した Gain 近似を適用する。
// Perlin の式に含まれる log と可変指数 pow を避けつつ、gain=0.5 の恒等性を保つ。
half3 TrpApplyHexTilingGain(half3 weights, half gain)
{
    const half safeGain = clamp(gain, 0.0001h, 0.9999h);
    const half schlickFactor = safeGain * rcp(1.0h - safeGain);
    const half3 upperHalf = step(0.5h, weights);
    const half3 mirroredWeights = lerp(2.0h * weights, 2.0h * (1.0h - weights), upperHalf);
    const half3 denominator = schlickFactor - schlickFactor * mirroredWeights + mirroredWeights;
    const half3 lowerCurve = 0.5h * mirroredWeights * rcp(max(denominator, 0.0001h));
    const half3 curvedWeights = lerp(lowerCurve, 1.0h - lowerCurve, upperHalf);

    const half weightSum = curvedWeights.x + curvedWeights.y + curvedWeights.z;
    return curvedWeights * rcp(max(weightSum, 0.0001h));
}

// 重心座標、サンプル由来の特徴量、論文の Gain カーブから最終ウェイトを作る。
half3 TrpGetHexTilingWeights(half3 seventhPowerWeights, half3 featureWeights, half gain)
{
    half3 weights = featureWeights * seventhPowerWeights;
    const half weightSum = weights.x + weights.y + weights.z;
    weights *= rcp(max(weightSum, 0.0001h));

    UNITY_BRANCH
    if (gain != 0.5h)
    {
        weights = TrpApplyHexTilingGain(weights, gain);
    }
    return weights;
}

// 同じ六角タイルが常に同じ RGB チャンネルへ対応するようウェイトを並べ替える。
// 戻り値はタイル境界やブレンド状態のデバッグ表示に利用できる。
half3 TrpGetHexTilingDebugWeights(half3 weights, int2 vertex1, int2 vertex3)
{
    int firstChannel = (vertex1.x - vertex1.y) % 3;
    firstChannel += firstChannel < 0 ? 3 : 0;

    const int highChannel = firstChannel < 2 ? firstChannel + 1 : 0;
    const int lowChannel = firstChannel > 0 ? firstChannel - 1 : 2;
    const int secondChannel = vertex1.x < vertex3.x ? lowChannel : highChannel;
    const int thirdChannel = vertex1.x < vertex3.x ? highChannel : lowChannel;

    half3 debugWeights;
    debugWeights.x = thirdChannel == 0 ? weights.z : (secondChannel == 0 ? weights.y : weights.x);
    debugWeights.y = thirdChannel == 1 ? weights.z : (secondChannel == 1 ? weights.y : weights.x);
    debugWeights.z = thirdChannel == 2 ? weights.z : (secondChannel == 2 ? weights.y : weights.x);
    return debugWeights;
}

// コンテキストに保持した3組の座標と微分を使ってテクスチャをサンプリングする。
void TrpSampleHexTiledTexture(HexTilingContext context, TEXTURE2D_PARAM(textureName, samplerName), out half4 sample1, out half4 sample2, out half4 sample3)
{
    sample1 = SAMPLE_TEXTURE2D_GRAD(textureName, samplerName, context.sampleUv1, context.sampleUvDx1, context.sampleUvDy1);
    sample2 = SAMPLE_TEXTURE2D_GRAD(textureName, samplerName, context.sampleUv2, context.sampleUvDx2, context.sampleUvDy2);
    sample3 = SAMPLE_TEXTURE2D_GRAD(textureName, samplerName, context.sampleUv3, context.sampleUvDx3, context.sampleUvDy3);
}

// 指定された共通ウェイトで3サンプルを合成する。
half4 TrpBlendHexTiledTexture(half4 sample1, half4 sample2, half4 sample3, half3 blendWeights)
{
    return sample1 * blendWeights.x + sample2 * blendWeights.y + sample3 * blendWeights.z;
}

// context     : GetHexTilingContext で一度だけ生成した共有座標情報。
// textureName : サンプリングするテクスチャ。TEXTURE2D_ARGS で渡す。
// samplerName : textureName に対応するサンプラー。TEXTURE2D_ARGS で渡す。
// blendWeights: BaseMapなど代表テクスチャから取得した共通ウェイト。
// 戻り値      : 共通ウェイトで Hex Tiling した RGBA 値。
half4 SampleHexTiledTexture(HexTilingContext context, TEXTURE2D_PARAM(textureName, samplerName), half3 blendWeights)
{
    half4 sample1;
    half4 sample2;
    half4 sample3;
    TrpSampleHexTiledTexture(context, TEXTURE2D_ARGS(textureName, samplerName), sample1, sample2, sample3);
    return TrpBlendHexTiledTexture(sample1, sample2, sample3, blendWeights);
}

// カラーをサンプリングし、他のマテリアルマップでも共有できるウェイトを返す。
half4 SampleHexTiledColor(HexTilingContext context, TEXTURE2D_PARAM(textureName, samplerName), half gain, out half3 blendWeights)
{
    half4 color1;
    half4 color2;
    half4 color3;
    TrpSampleHexTiledTexture(context, TEXTURE2D_ARGS(textureName, samplerName), color1, color2, color3);

    // 論文の式(4)に従い、輝度をタイル境界の拡散指標として使う。
    const half3 luminanceWeights = half3(0.299h, 0.587h, 0.114h);
    half3 featureWeights = half3(dot(color1.rgb, luminanceWeights), dot(color2.rgb, luminanceWeights), dot(color3.rgb, luminanceWeights));
    featureWeights = lerp(1.0h, featureWeights, TRP_HEX_TILING_FALLOFF_CONTRAST);

    blendWeights = TrpGetHexTilingWeights(context.seventhPowerWeights, featureWeights, gain);
    return TrpBlendHexTiledTexture(color1, color2, color3, blendWeights);
}

// 共有ウェイトが不要な場合のコンテキスト版オーバーロード。
half4 SampleHexTiledColor(HexTilingContext context, TEXTURE2D_PARAM(textureName, samplerName), half gain = 0.5h)
{
    half3 unusedBlendWeights;
    return SampleHexTiledColor(context, TEXTURE2D_ARGS(textureName, samplerName), gain, unusedBlendWeights);
}

// デバッグウェイトも取得する場合のオーバーロード。
half4 SampleHexTiledColor(float2 uv, TEXTURE2D_PARAM(textureName, samplerName), float rotationStrength, half gain, out half3 debugWeights)
{
    int2 vertex1;
    int2 vertex3;
    const HexTilingContext context = TrpGetHexTilingContext(uv, rotationStrength, vertex1, vertex3);
    half3 blendWeights;
    const half4 color = SampleHexTiledColor(context, TEXTURE2D_ARGS(textureName, samplerName), gain, blendWeights);
    debugWeights = TrpGetHexTilingDebugWeights(blendWeights, vertex1, vertex3);
    return color;
}

// カラーテクスチャを3つの六角タイルからサンプリングして合成する実用向け関数。
// uv               : タイリング対象の UV。値を拡大すると模様の繰り返し密度が上がる。
// textureName      : サンプリングするカラーテクスチャ。TEXTURE2D_ARGS で渡す。
// samplerName      : textureName に対応するサンプラー。TEXTURE2D_ARGS で渡す。
// rotationStrength : タイルごとの回転量。0 で回転なし、1 で生成された角度をそのまま使用する。
// gain             : 境界のコントラスト。0.5 で無変形、値を上げるほど境界が鋭くなる。
//                    カラー用途では 0.65～0.75 程度が保守的な目安。
// 戻り値           : Hex Tiling 適用後の RGBA カラー。
// デバッグウェイトを計算しないため、通常はこちらを使用する。
half4 SampleHexTiledColor(float2 uv, TEXTURE2D_PARAM(textureName, samplerName), float rotationStrength = 0.0, half gain = 0.5h)
{
    const HexTilingContext context = GetHexTilingContext(uv, rotationStrength);
    return SampleHexTiledColor(context, TEXTURE2D_ARGS(textureName, samplerName), gain);
}

// タンジェント空間法線を、暗黙的な高さ場の偏微分へ変換する。
float2 TrpTangentNormalToDerivative(half3 tangentNormal)
{
    const half3 absoluteNormal = abs(tangentNormal);
    const half minimumZ = (1.0h / 128.0h) * max(absoluteNormal.x, absoluteNormal.y);
    const half safeZ = max(absoluteNormal.z, minimumZ);
    return -float2(tangentNormal.xy) * rcp(float(safeZ));
}

// 法線マップを明示的な勾配で読み、ブレンド可能な高さ場の偏微分へ変換する。
float2 TrpSampleNormalDerivative(float2 uv, float2 uvDx, float2 uvDy, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale)
{
    const half4 packedNormal = SAMPLE_TEXTURE2D_GRAD(normalMap, sampler_normalMap, uv, uvDx, uvDy);
    return TrpTangentNormalToDerivative(UnpackNormalScale(packedNormal, normalScale));
}

// コンテキストの座標で法線を読み、各タイルの回転を反映した偏微分を返す。
void TrpSampleHexTiledNormalDerivatives(HexTilingContext context, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale, out float2 derivative1, out float2 derivative2, out float2 derivative3)
{
    // 回転後の UV で法線を読み、偏微分を元のタンジェント空間へ戻す。
    derivative1 = TrpRotateHexDerivative(context.rotation1, TrpSampleNormalDerivative(context.sampleUv1, context.sampleUvDx1, context.sampleUvDy1, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale));
    derivative2 = TrpRotateHexDerivative(context.rotation2, TrpSampleNormalDerivative(context.sampleUv2, context.sampleUvDx2, context.sampleUvDy2, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale));
    derivative3 = TrpRotateHexDerivative(context.rotation3, TrpSampleNormalDerivative(context.sampleUv3, context.sampleUvDx3, context.sampleUvDy3, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale));
}

// 共有ウェイトで偏微分を合成し、タンジェント空間法線へ戻す。
half3 TrpBlendHexTiledNormal(float2 derivative1, float2 derivative2, float2 derivative3, half3 blendWeights)
{
    const float2 derivative = derivative1 * blendWeights.x + derivative2 * blendWeights.y + derivative3 * blendWeights.z;
    return normalize(half3(-derivative.x, -derivative.y, 1.0h));
}

// context      : GetHexTilingContext で一度だけ生成した共有座標情報。
// normalMap    : Unity の法線マップ。TEXTURE2D_ARGS で渡す。
// normalScale  : 法線の強度。1 がテクスチャ本来の強さ。
// blendWeights : BaseMapなど代表テクスチャから取得した共通ウェイト。
// 戻り値       : 共通ウェイトで Hex Tiling したタンジェント空間法線。
half3 SampleHexTiledNormal(HexTilingContext context, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale, half3 blendWeights)
{
    float2 derivative1;
    float2 derivative2;
    float2 derivative3;
    TrpSampleHexTiledNormalDerivatives(context, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale, derivative1, derivative2, derivative3);
    return TrpBlendHexTiledNormal(derivative1, derivative2, derivative3, blendWeights);
}

// 法線自身の傾斜から個別ウェイトを生成するコンテキスト版。
half3 SampleHexTiledNormal(HexTilingContext context, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale, half gain, out half3 blendWeights)
{
    float2 derivative1;
    float2 derivative2;
    float2 derivative3;
    TrpSampleHexTiledNormalDerivatives(context, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale, derivative1, derivative2, derivative3);

    // 論文の式(3): 法線と Z 軸の角度の sin を境界拡散の指標にする。
    const float3 derivativeLengthSquared = float3(dot(derivative1, derivative1), dot(derivative2, derivative2), dot(derivative3, derivative3));
    half3 featureWeights = half3(sqrt(derivativeLengthSquared * rcp(1.0 + derivativeLengthSquared)));
    featureWeights = lerp(1.0h, featureWeights, TRP_HEX_TILING_FALLOFF_CONTRAST);

    blendWeights = TrpGetHexTilingWeights(context.seventhPowerWeights, featureWeights, gain);
    return TrpBlendHexTiledNormal(derivative1, derivative2, derivative3, blendWeights);
}

// 個別ウェイトの出力が不要な場合のコンテキスト版オーバーロード。
half3 SampleHexTiledNormal(HexTilingContext context, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale = 1.0h, half gain = 0.5h)
{
    half3 unusedBlendWeights;
    return SampleHexTiledNormal(context, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale, gain, unusedBlendWeights);
}

// デバッグウェイトも取得する場合のオーバーロード。
half3 SampleHexTiledNormal(float2 uv, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale, float rotationStrength, half gain, out half3 debugWeights)
{
    int2 vertex1;
    int2 vertex3;
    const HexTilingContext context = TrpGetHexTilingContext(uv, rotationStrength, vertex1, vertex3);
    half3 blendWeights;
    const half3 normal = SampleHexTiledNormal(context, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale, gain, blendWeights);
    debugWeights = TrpGetHexTilingDebugWeights(blendWeights, vertex1, vertex3);
    return normal;
}

// 法線マップを高さ場の偏微分として Hex Tiling し、タンジェント空間法線を返す実用向け関数。
// uv               : タイリング対象の UV。値を拡大すると法線模様の繰り返し密度が上がる。
// normalMap        : Unity の法線マップ。TEXTURE2D_ARGS で渡す。
// sampler_normalMap: normalMap に対応するサンプラー。TEXTURE2D_ARGS で渡す。
// normalScale      : 法線の強度。1 がテクスチャ本来の強さ、値を上げるほど凹凸が強くなる。
// rotationStrength : タイルごとの回転量。0 で回転なし、1 で生成された角度をそのまま使用する。
// gain             : 境界のコントラスト。法線では通常 0.5 の無変形を推奨する。
// 戻り値           : Hex Tiling 適用後のタンジェント空間法線。
// デバッグウェイトを計算しないため、通常はこちらを使用する。
half3 SampleHexTiledNormal(float2 uv, TEXTURE2D_PARAM(normalMap, sampler_normalMap), half normalScale = 1.0h, float rotationStrength = 0.0, half gain = 0.5h)
{
    const HexTilingContext context = GetHexTilingContext(uv, rotationStrength);
    return SampleHexTiledNormal(context, TEXTURE2D_ARGS(normalMap, sampler_normalMap), normalScale, gain);
}

#endif
