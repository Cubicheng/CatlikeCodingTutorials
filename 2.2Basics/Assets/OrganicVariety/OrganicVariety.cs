//优化前：使用instantiate，depth=8，cube，只有10fps
//优化后：使用compute shader，depth=8，cube，有190fps
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

using static Unity.Mathematics.math;
using float3x4 = Unity.Mathematics.float3x4;
//与UnityEngin.Quaternion相比，Mathematics.quaternion更适合高性能计算
using quaternion = Unity.Mathematics.quaternion;

public class OrganicVariety : MonoBehaviour
{
    [SerializeField, Range(3, 8)]
    int depth = 4;

    [SerializeField]
    Mesh mesh, leafMesh;

    [SerializeField]
    Material material;

    [SerializeField]
    Gradient gradientA, gradientB;

    [SerializeField]
    Color leafColorA, leafColorB;

    [SerializeField, Range(0f, 90f)]
    float maxSagAngleA = 15f, MaxSagAngleB = 25f;

    [SerializeField, Range(0f,90f)]
    float spinVelocityA = 20f, spinVelocityB = 25f;

    [SerializeField, Range(0f, 1f)]
    float reverseSpinChance = 0.25f;

    struct FractalPart {
        //float3和Vector3都表示三个浮点数，但是float3用于高性能计算
        public float3 worldPosition;
        public quaternion rotation, worldRotation;
        public float maxSagAngle, spineAngle, spinVelocity;
        //直接记录旋转角度，而不是使用累加法得到旋转角度，因为使用累加法计算，浮点数会有误差累计
    }

    static readonly int
        colorAId = Shader.PropertyToID("_ColorA"),
        colorBId = Shader.PropertyToID("_ColorB"),
        matricesId = Shader.PropertyToID("_Matrices"),
        sequenceNumbersId = Shader.PropertyToID("_SequenceNumbers");

    NativeArray<FractalPart>[] parts;

    NativeArray<float3x4>[] matrices;

    ComputeBuffer[] matrixBuffers;

    static MaterialPropertyBlock propertyBlock;

    static quaternion[] rotations = {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };

    Vector4[] sequenceNumbers;

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor {

        public float scale;
        public float deltaTime;
        [ReadOnly]
        public NativeArray<FractalPart> parents;
        public NativeArray<FractalPart> parts;
        [WriteOnly]
        public NativeArray<float3x4> matrices;

        public void Execute(int i) {
            FractalPart parentPart = parents[i / 5];
            FractalPart currentPart = parts[i];
            float3 upAxis = mul(mul(parentPart.worldRotation, currentPart.rotation), up());
            float3 sagAxis = cross(up(), upAxis);
            float sagMagnitude = length(sagAxis);
            quaternion baseRotation;
            if (sagMagnitude > 0f) {
                sagAxis /= sagMagnitude;
                quaternion sagRotation = quaternion.AxisAngle(sagAxis, currentPart.maxSagAngle * sagMagnitude);
                baseRotation = mul(sagRotation, parentPart.worldRotation);
            } else {
                baseRotation = parentPart.worldRotation;
            }
            currentPart.spineAngle += currentPart.spinVelocity * deltaTime;
            currentPart.worldRotation = mul(baseRotation, mul(currentPart.rotation, quaternion.RotateY(currentPart.spineAngle)));
            currentPart.worldPosition = parentPart.worldPosition + mul(currentPart.worldRotation, float3(0f, 1.5f*scale, 0f));

            parts[i] = currentPart;
            //matrices[i] = Matrix4x4.TRS(
            //        currentPart.worldPosition, currentPart.worldRotation, float3(scale)
            //    );
            float3x3 r = float3x3(currentPart.worldRotation) * scale;
            matrices[i] = float3x4(
                r.c0,
                r.c1,
                r.c2,
                currentPart.worldPosition
                );
                
        }
    }

    void SetupMemory() {
        if (matrixBuffers != null) {
            OnDisable();
        }

        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matrixBuffers = new ComputeBuffer[depth];
        sequenceNumbers = new Vector4[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matrixBuffers[i] = new ComputeBuffer(length, stride);
            sequenceNumbers[i] = new Vector4(Random.value, Random.value, Random.value, Random.value);
        }
    }

    private void OnEnable() {
        SetupMemory();
        InitializeFractal();
        if (propertyBlock == null) {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void OnDisable() {
        if (matrixBuffers != null) {
            for (int i = 0; i < matrixBuffers.Length; i++) {
                parts[i].Dispose();
                matrices[i].Dispose();
                matrixBuffers[i].Release();
            }
        }
        parts = null;
        matrices = null;
        matrixBuffers = null;
        sequenceNumbers = null;
    }

    private void OnValidate() {
        if (parts != null && enabled) {
            OnDisable();
            OnEnable();
        }
    }

    private void InitializeFractal() {
        parts[0][0] = CreatePart(0);
        //因为结构体是值传递，所以要这样做
        FractalPart rootPart = parts[0][0];
        rootPart.worldPosition = Vector3.zero;
        rootPart.worldRotation = Quaternion.identity;
        rootPart.spinVelocity = radians(Random.Range(spinVelocityA, spinVelocityB));
        rootPart.spineAngle = 0f;
        parts[0][0] = rootPart;
        float3x3 r = float3x3(rootPart.worldRotation) * 1.0f;
        matrices[0][0] = float3x4(
            r.c0,
            r.c1,
            r.c2,
            parts[0][0].worldPosition
            );
        //matrices[0][0] = Matrix4x4.TRS(parts[0][0].worldPosition, parts[0][0].worldRotation, Vector3.one);
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++) {
            for (int i = 0; i < parts[levelIndex].Length; i += 5) {
                for (int childIndex = 0; childIndex < 5; childIndex++) {
                    parts[levelIndex][i + childIndex] = CreatePart(childIndex);
                }
            }
        }
    }

    //初始的时候，所有物体都在原点
    FractalPart CreatePart(int childIndex) => new FractalPart {
        worldPosition = Vector3.zero,
        rotation = rotations[childIndex],
        worldRotation = Quaternion.identity,
        maxSagAngle = radians(Random.Range(maxSagAngleA, MaxSagAngleB)),
        spineAngle = 0f,
        spinVelocity = (Random.value < reverseSpinChance ? -1f : 1f) * radians(Random.Range(spinVelocityA, spinVelocityB)),
    };

    void Update() {
        float deltaTime = Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spineAngle += rootPart.spinVelocity * deltaTime;
        rootPart.worldPosition = transform.position;
        rootPart.worldRotation = mul(transform.rotation, mul(rootPart.rotation, quaternion.RotateY(rootPart.spineAngle)));
        parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
        matrices[0][0] = float3x4(
            r.c0,
            r.c1,
            r.c2,
            parts[0][0].worldPosition
            );
        //matrices[0][0] = Matrix4x4.TRS(rootPart.worldPosition, rootPart.worldRotation, float3(objectScale));
        float scale = objectScale;
        JobHandle jobHandle = default;
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++) {
            scale *= 0.5f;
            jobHandle = new UpdateFractalLevelJob {
                deltaTime = deltaTime,
                scale = scale,
                parents = parts[levelIndex - 1],
                parts = parts[levelIndex],
                matrices = matrices[levelIndex]
            }.ScheduleParallel(parts[levelIndex].Length, 5, jobHandle);
        }
        var bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);

        //扔给unity的worker thread执行
        jobHandle.Complete();

        for (int i = 0; i < matrixBuffers.Length; i++) {
            ComputeBuffer buffer = matrixBuffers[i];
            //把matrices[i]放到显存matrixBuffers[i]中

            buffer.SetData(matrices[i]);
            Color colorA, colorB;
            Mesh instanceMesh;
            //propertyBlock，仅对当前draw批次有效，不会去修改material本身
            //绑定数据，一会儿渲染的时候，Shader 里的那个 _Matrices 变量，请去编号为 buffer 的显存地址找数据
            propertyBlock.SetBuffer(matricesId, buffer);
            if (i == matrixBuffers.Length - 1) {
                colorA = leafColorA;
                colorB = leafColorB;
                instanceMesh = leafMesh;
            } else {
                float gradientInterpolator = i / (matrixBuffers.Length - 1f);
                colorA = gradientA.Evaluate(gradientInterpolator);
                colorB = gradientB.Evaluate(gradientInterpolator);
                instanceMesh = mesh;
            }
            propertyBlock.SetColor(colorAId, colorA);
            propertyBlock.SetColor(colorBId, colorB);
            propertyBlock.SetVector(sequenceNumbersId, sequenceNumbers[i]);
            Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }
}
