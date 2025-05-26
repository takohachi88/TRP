#include "Packages/tako.trp/ShaderLibrary/Common.hlsl"

#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
#define USE_FOG 1
#endif

#define CULL_VERTEX(o) { o.VFX_VARYING_POSCS.x = VFX_NAN; return o; }

#if HAS_STRIPS
#define HAS_STRIPS_DATA 1
#endif

#define VFX_PROP(input) (graphValues.input##_a)