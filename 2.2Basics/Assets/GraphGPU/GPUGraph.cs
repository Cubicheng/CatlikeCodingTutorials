using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class GPUGraph : MonoBehaviour {
    public enum KernelType { FunctionKernel, TransitionKernel };

    [SerializeField]
    Material material;

    [SerializeField]
    Mesh mesh;

    const int MAX_RESOLUTION = 1000;

    [SerializeField, Range(10, MAX_RESOLUTION)]
    int resolution = 10;

    [SerializeField]
    ComputeShader computeShader;

    static readonly int
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress"),
        functionA = Shader.PropertyToID("_FunctionA"),
        functionB = Shader.PropertyToID("_FunctionB");


    [SerializeField]
    FunctionLibrary.FunctionName functionName = FunctionLibrary.FunctionName.Wave;

    float step;
    Vector3 scale;
    int currentResolution;

    public enum TransitionMode { Cycle, Random };
    [SerializeField]
    TransitionMode transitionMode = TransitionMode.Cycle;

    [SerializeField, Min(0f)]
    float functionDuration = 1.0f;

    [SerializeField, Min(0f)]
    float transitionDuration = 1.0f;

    float duration = 0.0f;
    bool isTransition = false;

    FunctionLibrary.FunctionName transitionFunctionName;

    //在显存中的buffer，用来存储点坐标
    ComputeBuffer positionBuffer;

    private void Awake() {
        currentResolution = resolution;
    }

    private void OnEnable() {
        //开辟空间，3*4表示每个点坐标包含三个float，占据12字节的空间
        positionBuffer = new ComputeBuffer(MAX_RESOLUTION * MAX_RESOLUTION, 3 * 4);
    }

    private void OnDisable() {
        positionBuffer.Release();
        positionBuffer = null;
    }

    private void UpdateFunctionOnGPU() {
        var kernelIndex = isTransition ? (int)KernelType.TransitionKernel : (int)KernelType.FunctionKernel;
        float step = 2f / resolution;
        computeShader.SetInt(resolutionId, resolution);
        computeShader.SetInt(functionA, (int)functionName);
        computeShader.SetInt(functionB, (int)transitionFunctionName);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat(timeId, Time.time);
        computeShader.SetFloat(transitionProgressId, duration/ transitionDuration);
        computeShader.SetBuffer(kernelIndex, positionsId, positionBuffer);
        int groups = Mathf.CeilToInt(resolution/8f);
        //启动指定的线程组数量
        computeShader.Dispatch(kernelIndex, groups, groups, 1);
        material.SetBuffer(positionsId, positionBuffer);
        material.SetFloat(stepId, step);

        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution*resolution);
    }

    private void Update() {
        duration += Time.deltaTime;
        if (isTransition) {
            if (duration > transitionDuration) {
                duration -= transitionDuration;
                isTransition = false;
                functionName = transitionFunctionName;
            }
        } else if (duration > functionDuration) {
            duration -= functionDuration;
            PickNextFunction();
        }
        UpdateFunctionOnGPU();
    }
    private void PickNextFunction() {
        isTransition = true;
        if (transitionMode == TransitionMode.Cycle) {
            transitionFunctionName = FunctionLibrary.GetNextFunciton(functionName);
        } else {
            transitionFunctionName = FunctionLibrary.GetRandomFunctionNameOtherThan(functionName);
        }
    }

}
