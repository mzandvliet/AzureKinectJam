
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class KinectButton : MonoBehaviour {
    [SerializeField] private ParticleSystem _particlesA;
    [SerializeField] private ParticleSystem _particlesB;

    private void Awake() {
        
    }
    private void Update() {
        
    }

    public void OnTouch() {
        Debug.Log("Touched!");

        _particlesA.Play();
        _particlesB.Play();

        gameObject.SetActive(false);
    }
}