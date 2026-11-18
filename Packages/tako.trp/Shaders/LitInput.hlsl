#ifndef TRP_LIT_INPUT_INCLUDED
#define TRP_LIT_INPUT_INCLUDED

#include "Packages/tako.trp/ShaderLibrary/LightingInput.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

#endif
