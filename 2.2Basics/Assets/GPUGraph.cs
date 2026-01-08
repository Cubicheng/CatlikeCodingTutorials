using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public class GPUGraph : MonoBehaviour {
    [SerializeField]
    Material material;

    [SerializeField]
    Mesh mesh;

    [SerializeField, Range(10, 500)]
    int resolution = 10;

    [SerializeField]
    ComputeShader computeShader;

    static readonly int
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        timeId = Shader.PropertyToID("_Time");

    [SerializeField]
    FunctionLibrary.FunctionName functionName = FunctionLibrary.FunctionName.Wave;

    float step;
    Vector3 scale;
    int currentResolution;
    FunctionLibrary.Function f;

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

    ComputeBuffer positionBuffer;

    private void Awake() {
        currentResolution = resolution;
        f = FunctionLibrary.GetFunction(functionName);
    }

    private void OnEnable() {
        positionBuffer = new ComputeBuffer(resolution * resolution, 3 * 4);
    }

    private void OnDisable() {
        positionBuffer.Release();
        positionBuffer = null;
    }

    private void UpdateFunctionOnGPU() {
        float step = 2f / resolution;
        computeShader.SetInt(resolutionId, resolution);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat(timeId, Time.time);
        computeShader.SetBuffer(0, positionsId, positionBuffer);
        int groups = Mathf.CeilToInt(resolution/8f);
        computeShader.Dispatch(0, groups, groups, 1);

        material.SetBuffer(positionsId, positionBuffer);
        material.SetFloat(stepId, step);

        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, positionBuffer.count);
    }

    private void Update() {
        UpdateFunctionOnGPU();
    }

}
