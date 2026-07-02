#ifndef TRP_PPLL_OIT_INCLUDED
#define TRP_PPLL_OIT_INCLUDED

struct PpllOitNode
{
    // 透明色をRGBA8へ圧縮した値。
    uint pixelColor;
    // 上位24bitが線形深度、下位8bitがMSAA sample index。
    uint depthSampleIndex;
    // 同じピクセルにある次のNode index。0ならリスト終端。
    uint next;
};

// Node Bufferの帯域とメモリを抑えるためfloat4をRGBA8へ圧縮する。
uint PackPpllOitColor(float4 color)
{
    uint4 value = (uint4)(saturate(color) * 255.0 + 0.5);
    return (value.w << 24u) | (value.z << 16u) | (value.y << 8u) | value.x;
}

float4 UnpackPpllOitColor(uint packedColor)
{
    uint4 value = uint4(
        packedColor & 0xffu,
        (packedColor >> 8u) & 0xffu,
        (packedColor >> 16u) & 0xffu,
        packedColor >> 24u);
    return (float4)value / 255.0;
}

uint PackPpllOitDepthSample(float linearDepth, uint sampleIndex)
{
    uint depth = (uint)(saturate(linearDepth) * 16777215.0);
    return (depth << 8u) | (sampleIndex & 0xffu);
}

float UnpackPpllOitDepth(uint packedDepthSample)
{
    return (float)(packedDepthSample >> 8u) / 16777215.0;
}

uint UnpackPpllOitSampleIndex(uint packedDepthSample)
{
    return packedDepthSample & 0xffu;
}

uint PpllOitHeadAddress(uint2 pixel, uint width)
{
    // Head BufferはRaw Bufferなので、要素indexではなくbyte offsetを返す。
    return 4u * (pixel.y * width + pixel.x);
}

#endif
