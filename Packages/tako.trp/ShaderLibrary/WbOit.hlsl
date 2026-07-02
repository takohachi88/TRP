#ifndef TRP_WBOIT
#define TRP_WBOIT

#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

//以下を参考にしたOITの重み付け関数。
//https://jcgt.org/published/0002/02/09/paper-lowres.pdf
//(7)
float WbOitWeight1(float z, float alpha)
{
    return alpha * max(1e-2, min(3 * 1e3, 10 * rcp(1e-5 + Pow2(abs(z) / 5) + Pow6(abs(z) / 200))));
}

//(8)
float WbOitWeight2(float z, float alpha)
{
    return alpha * max(1e-2, min(3 * 1e3, 10 * rcp(1e-5 + Pow3(abs(z) / 10) + Pow6(abs(z) / 200))));
}

//(9)
float WbOitWeight3(float z, float alpha)
{
    return alpha * max(1e-2, min(3 * 1e3, 0.03 * rcp(1e-5 + Pow4(abs(z) / 200))));
}


struct FragmentWbOitOutputs
{
    float4 color : SV_Target0;
    float4 alpha : SV_Target1;
};

#endif