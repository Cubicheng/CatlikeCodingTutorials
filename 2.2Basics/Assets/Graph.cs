using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public class Graph : MonoBehaviour {
    [SerializeField]
    Transform pointPrefab;
    [SerializeField, Range(10, 100)]
    int resolution = 10;

    [SerializeField]
    FunctionLibrary.FunctionName functionName = FunctionLibrary.FunctionName.Wave;

    Transform[] points;
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

    private void Awake() {
        currentResolution = resolution;
        f = FunctionLibrary.GetFunction(functionName);
        initPoints();
    }

    private void initPoints() {
        points = new Transform[currentResolution * currentResolution];
        step = 2f / currentResolution;
        scale = Vector3.one * step;
        float time = Time.time;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++) {
            if (x == currentResolution) {
                x = 0;
                z++;
            }
            Transform point = Instantiate(pointPrefab);
            points[i] = point;
            point.SetParent(transform, false);
            UpdatePoint(i, x, z, time);
        }
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
        if (resolution != currentResolution) {
            currentResolution = resolution;
            ClearPoints();
            initPoints();
        }
        f = FunctionLibrary.GetFunction(functionName);
    }

    private void PickNextFunction() {
        isTransition = true;
        if (transitionMode == TransitionMode.Cycle) {
            transitionFunctionName = FunctionLibrary.GetNextFunciton(functionName);
        } else {
            transitionFunctionName = FunctionLibrary.GetRandomFunctionNameOtherThan(functionName);
        }
    }

    private void ClearPoints() {
        for (int i = 0; i < points.Length; i++) {
            Destroy(points[i].gameObject);
            points[i] = null;
        }
        points = null;
    }

    private void LateUpdate() {
        float time = Time.time;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++) {
            if (x == currentResolution) {
                x = 0;
                z++;
            }
            Transform point = points[i];
            point.SetParent(transform, false);
            UpdatePoint(i, x, z, time);
        }
    }

    private void UpdatePoint(int i, int x, int z, float time) {
        Transform point = points[i];
        float u = (x + 0.5f) * step - 1f;
        float v = (z + 0.5f) * step - 1f;
        if (isTransition) {
            FunctionLibrary.Function from = FunctionLibrary.GetFunction(functionName);
            FunctionLibrary.Function to = FunctionLibrary.GetFunction(transitionFunctionName);
            float progress = duration / transitionDuration;
            point.localPosition = FunctionLibrary.Morph(u, v, time, from, to, progress);
        } else {
            point.localPosition = f(u, v, time);
        }
        point.localScale = scale;
    }
}
