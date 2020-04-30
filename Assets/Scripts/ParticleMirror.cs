using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;
using Unity.Mathematics;
using Rng = Unity.Mathematics.Random;

public class ParticleMirror : MonoBehaviour {
    [SerializeField] private KinectBodyTrackingInteraction _tracker;
    [SerializeField] private ParticleSystem _particles;
    [SerializeField] private Transform _mirror;

    private Rng _rng;

    private void Awake() {
        _rng = new Rng(1234);
    }

    private void Update() {
        var skeleton = _tracker.InterpolatedSkeleton;
        EmitAlongSkeleton(skeleton);
    }

    private void EmitAlongSkeleton(UnitySkeleton skeleton) {
        // Todo: maybe make a reusable bone enumerator pattern?

        // Draw spine
        EmitAlongJoint(skeleton, JointId.Pelvis, JointId.SpineNavel);
        EmitAlongJoint(skeleton, JointId.SpineNavel, JointId.SpineChest);
        EmitAlongJoint(skeleton, JointId.SpineChest, JointId.Neck);

        // Draw head
        EmitAlongJoint(skeleton, JointId.Neck, JointId.Head);
        EmitAlongJoint(skeleton, JointId.Head, JointId.Nose);
        EmitAlongJoint(skeleton, JointId.Head, JointId.EyeLeft);
        EmitAlongJoint(skeleton, JointId.Head, JointId.EyeRight);

        // Draw left arm
        EmitAlongJoint(skeleton, JointId.SpineChest, JointId.ClavicleLeft);
        EmitAlongJoint(skeleton, JointId.ClavicleLeft, JointId.ShoulderLeft);
        EmitAlongJoint(skeleton, JointId.ShoulderLeft, JointId.ElbowLeft);
        EmitAlongJoint(skeleton, JointId.ElbowLeft, JointId.WristLeft);
        EmitAlongJoint(skeleton, JointId.WristLeft, JointId.HandLeft);
        EmitAlongJoint(skeleton, JointId.HandLeft, JointId.HandTipLeft);
        EmitAlongJoint(skeleton, JointId.HandLeft, JointId.ThumbLeft);

        // Draw right arm
        EmitAlongJoint(skeleton, JointId.SpineChest, JointId.ClavicleRight);
        EmitAlongJoint(skeleton, JointId.ClavicleRight, JointId.ShoulderRight);
        EmitAlongJoint(skeleton, JointId.ShoulderRight, JointId.ElbowRight);
        EmitAlongJoint(skeleton, JointId.ElbowRight, JointId.WristRight);
        EmitAlongJoint(skeleton, JointId.WristRight, JointId.HandRight);
        EmitAlongJoint(skeleton, JointId.HandRight, JointId.HandTipRight);
        EmitAlongJoint(skeleton, JointId.HandRight, JointId.ThumbRight);

        // Draw left leg
        EmitAlongJoint(skeleton, JointId.Pelvis, JointId.HipLeft);
        EmitAlongJoint(skeleton, JointId.HipLeft, JointId.KneeLeft);
        EmitAlongJoint(skeleton, JointId.KneeLeft, JointId.AnkleLeft);
        EmitAlongJoint(skeleton, JointId.AnkleLeft, JointId.FootLeft);

        // Draw right leg
        EmitAlongJoint(skeleton, JointId.Pelvis, JointId.HipRight);
        EmitAlongJoint(skeleton, JointId.HipRight, JointId.KneeRight);
        EmitAlongJoint(skeleton, JointId.KneeRight, JointId.AnkleRight);
        EmitAlongJoint(skeleton, JointId.AnkleRight, JointId.FootRight);
    }

    private void EmitAlongJoint(UnitySkeleton skeleton, JointId a, JointId b) {
        var jointA = skeleton.GetJoint(a);
        var jointB = skeleton.GetJoint(b);

        var pos = math.lerp(jointA.position, jointB.position, _rng.NextFloat());

        pos = _mirror.InverseTransformPoint(pos);
        pos.z *= -1f;
        pos = _mirror.TransformPoint(pos);

        var vel = _rng.NextFloat3Direction() * 0.0001f;

        _particles.Emit(pos, vel, 0.1f, 4f, Color.green);
    }

}