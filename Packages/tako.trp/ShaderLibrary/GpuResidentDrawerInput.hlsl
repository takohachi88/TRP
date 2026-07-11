#ifndef TRP_GPU_RESIDENT_DRAWER_INPUT
#define TRP_GPU_RESIDENT_DRAWER_INPUT

// UnityInstancing.hlslはDOTS Instancing時にUNITY_MATRIX_Mなどを
// GPU Resident Drawerのfloat3x4インスタンスデータへ差し替える。
// その置き換え先のメタデータは、SpaceTransforms.hlslより前に宣言する必要がある。
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    UNITY_DOTS_INSTANCING_START(BuiltinPropertyMetadata)
        UNITY_DOTS_INSTANCED_PROP(float3x4, unity_ObjectToWorld)
        UNITY_DOTS_INSTANCED_PROP(float3x4, unity_WorldToObject)
        UNITY_DOTS_INSTANCED_PROP(float3x4, unity_MatrixPreviousM)
        UNITY_DOTS_INSTANCED_PROP(float3x4, unity_MatrixPreviousMI)
    UNITY_DOTS_INSTANCING_END(BuiltinPropertyMetadata)
#endif

#endif
