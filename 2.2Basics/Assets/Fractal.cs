//优化前：使用instantiate，depth=8，cube，只有10fps
//优化后：使用compute shader，depth=8，cube，有190fps
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using float3x4 = Unity.Mathematics.float3x4;
//与UnityEngin.Quaternion相比，Mathematics.quaternion更适合高性能计算
using quaternion = Unity.Mathematics.quaternion;

public class FractalGPU : MonoBehaviour
{
    [SerializeField, Range(1, 8)]
    int depth = 4;

    [SerializeField]
    Mesh mesh;

    [SerializeField]
    Material material;

    struct FractalPart {
        //float3和Vector3都表示三个浮点数，但是float3用于高性能计算
        public float3 direction, worldPosition;
        public quaternion rotation, worldRotation;
        public float spineAngle;
        //直接记录旋转角度，而不是使用累加法得到旋转角度，因为使用累加法计算，浮点数会有误差累计
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor {

        public float spineAngleDelta;
        public float scale;
        [ReadOnly]
        public NativeArray<FractalPart> parents;
        public NativeArray<FractalPart> parts;
        [WriteOnly]
        public NativeArray<float3x4> matrices;

        public void Execute(int i) {
            FractalPart parentPart = parents[i / 5];
            FractalPart currentPart = parts[i];
            currentPart.spineAngle += spineAngleDelta;
            currentPart.worldRotation = mul(parentPart.worldRotation, mul(currentPart.rotation, quaternion.RotateY(currentPart.spineAngle)));
            currentPart.worldPosition = parentPart.worldPosition + mul(parentPart.worldRotation, 1.5f * scale * currentPart.direction);
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

    NativeArray<FractalPart>[] parts;

    NativeArray<float3x4>[] matrices;

    ComputeBuffer[] matrixBuffers;

    static MaterialPropertyBlock propertyBlock;

    static float3[] directions = {
        up(),right(),left(),forward(),back()
    };

    static quaternion[] rotations = {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };

    void SetupMemory() {
        if (matrixBuffers != null) {
            OnDisable();
        }

        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matrixBuffers = new ComputeBuffer[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matrixBuffers[i] = new ComputeBuffer(length, stride);
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
        direction = directions[childIndex],
        worldPosition = Vector3.zero,
        rotation = rotations[childIndex],
        worldRotation = Quaternion.identity,
        spineAngle = 0f,
    };

    static readonly int matricesId = Shader.PropertyToID("_Matrices");

    void Update() {
        float spineAngleDelta = 0.125f * PI * Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spineAngle += spineAngleDelta;
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
                spineAngleDelta = spineAngleDelta,
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
            //propertyBlock，仅对当前draw批次有效，不会去修改material本身
            //绑定数据，一会儿渲染的时候，Shader 里的那个 _Matrices 变量，请去编号为 buffer 的显存地址找数据
            propertyBlock.SetBuffer(matricesId, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }
}
