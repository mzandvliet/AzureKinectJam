using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;

[RequireComponent(typeof(KinectButton))]
public class ParticleTrigger : MonoBehaviour {
    [SerializeField] private ParticleSystem _particles;

    private KinectButton _button;

    private void Awake() {
        _button = gameObject.GetComponent<KinectButton>();
        // _button.OnTouchEnter += OnTouchEnter;
    }
    
    private void OnDestroy() {
        // _button.OnTouchEnter -= OnTouchEnter;
    }

    private void OnTouchEnter(KinectButton button, JointId bone) {
        _particles.Play();
    }
}