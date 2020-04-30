
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine.Events;

public class KinectButton : MonoBehaviour {
    // public event System.Action<KinectButton, JointId> OnTouchEnter;
    
    [SerializeField] public UnityEvent OnTouchEnter;

    public void OnTouchEnter_Internal(JointId bone) {
        Debug.Log("Touched!");

        OnTouchEnter.Invoke();
    }
}