using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class LinearPerspectiveTransform : MonoBehaviour
{
    [SerializeField] private Transform _close;
    [SerializeField] private Transform _far;

    void Start()
    {
        StartCoroutine(Animate());
    }

    private IEnumerator Animate() {
        const float duration = 10;

        float time = 0;
        while (time < duration) {
            float lerp = math.saturate(time / duration);
            float zlerp = math.pow(lerp, 4f);
            float3 position = math.lerp(_far.position, _close.position, zlerp);
            float3 scale = math.lerp(_far.localScale, _close.localScale, zlerp);

            transform.position = position;
            transform.localScale = scale;

            time += Time.deltaTime;

            yield return null;
        }
    }
}
