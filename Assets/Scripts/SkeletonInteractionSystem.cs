using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;
using Unity.Mathematics;

public class SkeletonInteractionSystem : MonoBehaviour {
    [SerializeField] private KinectBodyTrackingInteraction _tracker;
    private List<KinectButton> _buttons;
    private List<KinectButton> _touchedButtons;

    private void Awake() {
        var buttons = (KinectButton[])GameObject.FindObjectsOfTypeAll(typeof(KinectButton));
        _buttons = new List<KinectButton>(buttons);
        _touchedButtons = new List<KinectButton>(buttons);
    }

    private void Update() {
        var skeleton = _tracker.InterpolatedSkeleton;

        for (int i = 0; i < _buttons.Count; i++) {
            if (IsTouching(skeleton.bones[(int)JointId.HandLeft], _buttons[i]) && !_touchedButtons.Contains(_buttons[i])) {
                _buttons[i].OnTouchEnter_Internal(JointId.HandLeft);
                _touchedButtons.Add(_buttons[i]);
            } else
            if (IsTouching(skeleton.bones[(int)JointId.HandRight], _buttons[i]) && !_touchedButtons.Contains(_buttons[i])) {
                _buttons[i].OnTouchEnter_Internal(JointId.HandRight);
                _touchedButtons.Add(_buttons[i]);
            }
            else {
                if (_touchedButtons.Contains(_buttons[i])) {
                    _touchedButtons.Remove(_buttons[i]);
                }
            }
        }   
    }

    private static bool IsTouching(BoneState bone, KinectButton button) {
        const float threshold = 0.4f;
        return math.distance(bone.position, button.transform.position) < threshold;
    }
}