#ifndef TRP_GPU_RESIDENT_DRAWER_INPUT
#define TRP_GPU_RESIDENT_DRAWER_INPUT

// URPのUniversalDOTSInstancing.hlslと同じ考え方で、GPU Resident Drawerが持つ
// BuiltinPropertyMetadataをTRPの組み込み変数へ結び付ける。
// UnityPerDrawとの二重宣言を避けることが、BatchRendererGroupのcbuffer不一致防止に必要となる。
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    #undef unity_ObjectToWorld
    #undef unity_WorldToObject
    #undef unity_MatrixPreviousM
    #undef unity_MatrixPreviousMI

    UNITY_DOTS_INSTANCING_START(BuiltinPropertyMetadata)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float3x4, unity_ObjectToWorld)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float3x4, unity_WorldToObject)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, unity_SpecCube0_HDR)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, unity_LightmapST)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, unity_LightmapIndex)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, unity_DynamicLightmapST)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float3x4, unity_MatrixPreviousM)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float3x4, unity_MatrixPreviousMI)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(SH, unity_SHCoefficients)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(uint2, unity_EntityId)
        UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(uint, unity_RendererUserValuesPropertyEntry)
    UNITY_DOTS_INSTANCING_END(BuiltinPropertyMetadata)

    // BRGのPickingではRenderer単位のEntity IDとSubMesh番号から選択IDを復元する。
    // _SelectionIDはPicking Pass側、unity_SubmeshIndexはUnityのBRG選択描画側から設定される。
    int unity_SubmeshIndex;
    #define unity_SelectionID UNITY_ACCESS_DOTS_INSTANCED_SELECTION_VALUE(unity_EntityId, unity_SubmeshIndex, _SelectionID)

    #define unity_LODFade LoadDOTSInstancedData_LODFade()
    #define unity_SpecCube0_HDR UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_CUSTOM_DEFAULT(float4, unity_SpecCube0_HDR, unity_DOTS_SpecCube0_HDR)
    #define unity_LightmapST UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, unity_LightmapST)
    #define unity_LightmapIndex UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, unity_LightmapIndex)
    #define unity_DynamicLightmapST UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, unity_DynamicLightmapST)
    #define unity_SHAr LoadDOTSInstancedData_SHAr()
    #define unity_SHAg LoadDOTSInstancedData_SHAg()
    #define unity_SHAb LoadDOTSInstancedData_SHAb()
    #define unity_SHBr LoadDOTSInstancedData_SHBr()
    #define unity_SHBg LoadDOTSInstancedData_SHBg()
    #define unity_SHBb LoadDOTSInstancedData_SHBb()
    #define unity_SHC LoadDOTSInstancedData_SHC()
    #define unity_ProbesOcclusion LoadDOTSInstancedData_ProbesOcclusion()
    #define unity_WorldTransformParams LoadDOTSInstancedData_WorldTransformParams()
    #define unity_RendererBounds_Min LoadDOTSInstancedData_RendererBounds_Min()
    #define unity_RendererBounds_Max LoadDOTSInstancedData_RendererBounds_Max()

    // 各シェーダーが空のマクロを先に定義していても、GRD向けの実装へ差し替える。
    #undef UNITY_SETUP_DOTS_SH_COEFFS
    #define UNITY_SETUP_DOTS_SH_COEFFS SetupDOTSSHCoeffs(UNITY_DOTS_INSTANCED_METADATA_NAME(SH, unity_SHCoefficients))
    #undef UNITY_SETUP_DOTS_RENDER_BOUNDS
    #define UNITY_SETUP_DOTS_RENDER_BOUNDS SetupDOTSRendererBounds(UNITY_DOTS_MATRIX_M)
#else
    // 通常のRendererによるPickingではUnityから渡された値をそのまま使用する。
    #define unity_SelectionID _SelectionID
#endif

#endif
