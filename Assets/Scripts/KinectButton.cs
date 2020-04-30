
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;

public class KinectButton : MonoBehaviour {
    public event System.Action<KinectButton, JointId> OnTouchEnter;

    public void OnTouchEnter_Internal(JointId bone) {
        Debug.Log("Touched!");

        if (OnTouchEnter != null) {
            OnTouchEnter(this, bone);
        }
    }
}