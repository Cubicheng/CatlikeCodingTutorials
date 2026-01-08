// 1. 声明位置缓冲区（只读）
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    StructuredBuffer<float3> _Positions;
#endif

// 2. 声明缩放属性（由 C# 脚本 material.SetFloat 设置）
float _Step;

// 3. 核心配置函数：在每个顶点渲染前，修改物体的变换矩阵
void ConfigureProcedural () {
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        // 获取当前正在绘制的实例 ID，并从缓冲区取出对应的坐标
        float3 position = _Positions[unity_InstanceID];

        // 初始化矩阵为 0
        unity_ObjectToWorld = 0.0;
        
        // 设置矩阵的第四列：控制位移 (x, y, z)
        unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
        
        // 设置矩阵的对角线：控制缩放
        unity_ObjectToWorld._m00_m11_m22 = _Step;
    #endif
}

// 4. Shader Graph 调用的虚函数（空实现，仅用于引入代码）
void ShaderGraphFunction_float (float3 In, out float3 Out) {
    Out = In;
}

void ShaderGraphFunction_half (half3 In, out half3 Out) {
    Out = In;
}