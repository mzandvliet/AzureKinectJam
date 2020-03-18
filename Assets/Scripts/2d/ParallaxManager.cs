using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Rng = Unity.Mathematics.Random;
using System.Collections.Generic;

[System.Serializable]
public class ParallaxLayer {
    [SerializeField] public GameObject parent;
    [SerializeField] public float multiplier = 1f;
}

public class ParallaxManager : MonoBehaviour {
    [SerializeField] private Transform _camera;
    [SerializeField] private List<ParallaxLayer> _layers;
    
    private void Awake() {
        
    }

    private void Update() {
        // float2 displace = new float2(
        //     math.sin(Time.time),
        //     math.cos(Time.time)
        // );

        float2 displace = new float2(
            _camera.transform.position.x,
            _camera.transform.position.y
        );

        for (int i = 0; i < _layers.Count; i++)
        {
            float motionScale = (1f / (float)(2 + i));
            _layers[i].parent.transform.position = new Vector3(
                displace.x * motionScale * _layers[i].multiplier,
                displace.y * motionScale * _layers[i].multiplier,
                0f
            );
        }
    }
}