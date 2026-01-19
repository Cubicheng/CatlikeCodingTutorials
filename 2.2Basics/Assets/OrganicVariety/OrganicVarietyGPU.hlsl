#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    StructuredBuffer<float3x4> _Matrices;
#endif

float4 _SequenceNumbers;
float4 _ColorA, _ColorB;

float4 GetFractalColor(){
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    //Take the decimal part
        float4 color;
        color.rgb = lerp(_ColorA.rgb,_ColorB.rgb,frac(unity_InstanceID *_SequenceNumbers.x+_SequenceNumbers.y));
        color.a = lerp(_ColorA.a,_ColorB.a,frac(unity_InstanceID *_SequenceNumbers.z+_SequenceNumbers.w));
		return color;
	#else
		return _ColorA;
	#endif
}

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

void ShaderGraphFunction_float (float3 In, out float3 Out, out float4 FractalColor) {
    Out = In;
    FractalColor = GetFractalColor();
}

void ShaderGraphFunction_half (half3 In, out half3 Out, out float4 FractalColor) {
    Out = In;
    FractalColor = GetFractalColor();
}