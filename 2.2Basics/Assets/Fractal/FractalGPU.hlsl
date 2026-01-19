#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    StructuredBuffer<float3x4> _Matrices;
#endif

void ConfigureProcedural () {
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        float3x4 m = _Matrices[unity_InstanceID];
        
        // 手动补全 4x4 变换矩阵固定的的最后一行 (0, 0, 0, 1)
        unity_ObjectToWorld = float4x4(
            m._m00, m._m01, m._m02, m._m03,
            m._m10, m._m11, m._m12, m._m13,
            m._m20, m._m21, m._m22, m._m23,
            0.0,    0.0,    0.0,    1.0
        );
    #endif
}

void ShaderGraphFunction_float (float3 In, out float3 Out) {
    Out = In;
}

void ShaderGraphFunction_half (half3 In, out half3 Out) {
    Out = In;
}