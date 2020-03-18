using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Rng = Unity.Mathematics.Random;
using System.Collections.Generic;

public class LoopingScroll : MonoBehaviour {
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _depth = 1f;

    private void Update() {
        float3 position = transform.position;
        position.x += (_speed / math.pow(2, _depth)) * Time.deltaTime;
        transform.position = position;
    }
}