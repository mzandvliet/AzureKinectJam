using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

/*

Todo: 

- Use multiple cams in sync
- Single and multi-camera calibration routines (port the OpenCV checkerboard sample?)

*/

public class KinectBodyTrackingInteraction : MonoBehaviour {
    [SerializeField] private Transform _kinectTransform;

    private Device _device;
    private int2 _colorDims;
    private Tracker _tracker;

    private uint _tick;

    private uint _numBodies;
    private UnitySkeleton[] _buffer;
    private int _bufIdx;

    private UnitySkeleton _interpolatedSkeleton;    

    private List<KinectButton> _buttons;

    private void Awake() {
        Application.targetFrameRate = 120;

        _device = Device.Open(0);

        _device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32, // Note: other formats would be hardware-native, faster
            ColorResolution = ColorResolution.Off,
            DepthMode = DepthMode.WFOV_2x2Binned, // Note: makes a large difference in latency!
            SynchronizedImagesOnly = false,
            CameraFPS = FPS.FPS30,
        });

        var calibration = _device.GetCalibration();

        _colorDims = new int2(
             calibration.ColorCameraCalibration.ResolutionWidth,
             calibration.ColorCameraCalibration.ResolutionHeight
         );
      
        _tracker = Tracker.Create(calibration, new TrackerConfiguration {
            SensorOrientation = SensorOrientation.Default,
            ProcessingMode = TrackerProcessingMode.Gpu,
            GpuDeviceId = 0,
        });

        _buffer = new UnitySkeleton[2];
        for (int i = 0; i < _buffer.Length; i++) {
            _buffer[i] = new UnitySkeleton(Allocator.Persistent);
        }

        _interpolatedSkeleton = new UnitySkeleton(Allocator.Persistent);
        
        var buttons = (KinectButton[])GameObject.FindObjectsOfTypeAll(typeof(KinectButton));
        _buttons = new List<KinectButton>(buttons);
    }

    private void OnDestroy() {
        _device.Dispose();

        for (int i = 0; i < _buffer.Length; i++) {
            _buffer[i].Dispose();
        }

        _interpolatedSkeleton.Dispose();
    }

    private void Start() {
        Capture();
        _captureWatch = new System.Diagnostics.Stopwatch();
        _captureWatch.Start();
    }

    System.Diagnostics.Stopwatch _captureWatch;

    private void Update() {
        if (_captureWatch.IsRunning) {
            // Call the blocking Capture call a little bit before we expect a new frame to arrive
            const int kinectFrameMillis = (1000 / 30);
            const int unityFrameMillis = (1000 / 120);

            if (_captureWatch.ElapsedMilliseconds >= kinectFrameMillis - unityFrameMillis * 2) {
                Capture();
                _captureWatch.Restart();
            }
        }

        // Interpolate(_buffer[_bufIdx], _buffer[_bufIdx], _interpolatedSkeleton, 0.5f);
        Interpolate(_interpolatedSkeleton, _buffer[_bufIdx], _interpolatedSkeleton, 4f * Time.deltaTime);

        for (int i = 0; i < _buttons.Count; i++)
        {
            if (math.distance(_interpolatedSkeleton.bones[(int)JointId.HandLeft].position, _buttons[i].transform.position) < 0.25f) {
                _buttons[i].OnTouch();
            }
            if (math.distance(_interpolatedSkeleton.bones[(int)JointId.HandRight].position, _buttons[i].transform.position) < 0.25f) {
                _buttons[i].OnTouch();
            }
        }
    }

    private void OnGUI() {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(string.Format("Number of bodies found: {0}", _numBodies));
        GUILayout.EndVertical();
    }

    private void OnDrawGizmos() {
        if (_numBodies > 0) {
            DrawSkeleton(_interpolatedSkeleton);
        }
    }

    private static readonly Color[] ConfidenceColors = new Color[] {
        Color.black,
        Color.red,
        Color.yellow,
        Color.green,
        Color.black
    };

    private static void DrawSkeleton(UnitySkeleton skeleton) {
        // Draw spine
        DrawJoint(skeleton, JointId.Pelvis, JointId.SpineNavel);
        DrawJoint(skeleton, JointId.SpineNavel, JointId.SpineChest);
        DrawJoint(skeleton, JointId.SpineChest, JointId.Neck);

        // Draw head
        DrawJoint(skeleton, JointId.Neck, JointId.Head);
        DrawJoint(skeleton, JointId.Head, JointId.Nose);
        DrawJoint(skeleton, JointId.Head, JointId.EyeLeft);
        DrawJoint(skeleton, JointId.Head, JointId.EyeRight);

        // Draw left arm
        DrawJoint(skeleton, JointId.SpineChest, JointId.ClavicleLeft);
        DrawJoint(skeleton, JointId.ClavicleLeft, JointId.ShoulderLeft);
        DrawJoint(skeleton, JointId.ShoulderLeft, JointId.ElbowLeft);
        DrawJoint(skeleton, JointId.ElbowLeft, JointId.WristLeft);
        DrawJoint(skeleton, JointId.WristLeft, JointId.HandLeft);
        DrawJoint(skeleton, JointId.HandLeft, JointId.HandTipLeft);
        DrawJoint(skeleton, JointId.HandLeft, JointId.ThumbLeft);

        // Draw right arm
        DrawJoint(skeleton, JointId.SpineChest, JointId.ClavicleRight);
        DrawJoint(skeleton, JointId.ClavicleRight, JointId.ShoulderRight);
        DrawJoint(skeleton, JointId.ShoulderRight, JointId.ElbowRight);
        DrawJoint(skeleton, JointId.ElbowRight, JointId.WristRight);
        DrawJoint(skeleton, JointId.WristRight, JointId.HandRight);
        DrawJoint(skeleton, JointId.HandRight, JointId.HandTipRight);
        DrawJoint(skeleton, JointId.HandRight, JointId.ThumbRight);

        // Draw left leg
        DrawJoint(skeleton, JointId.Pelvis, JointId.HipLeft);
        DrawJoint(skeleton, JointId.HipLeft, JointId.KneeLeft);
        DrawJoint(skeleton, JointId.KneeLeft, JointId.AnkleLeft);
        DrawJoint(skeleton, JointId.AnkleLeft, JointId.FootLeft);

        // Draw right leg
        DrawJoint(skeleton, JointId.Pelvis, JointId.HipRight);
        DrawJoint(skeleton, JointId.HipRight, JointId.KneeRight);
        DrawJoint(skeleton, JointId.KneeRight, JointId.AnkleRight);
        DrawJoint(skeleton, JointId.AnkleRight, JointId.FootRight);
    }

    private static void DrawJoint(UnitySkeleton skeleton, JointId a, JointId b) {
        var jointA = skeleton.GetJoint(a);
        var jointB = skeleton.GetJoint(b);

        Gizmos.color = ConfidenceColors[(int)jointA.confidenceLevel];
        Gizmos.DrawLine(jointA.position, jointB.position);
    }

    private unsafe void Capture() {
        Capture capture = _device.GetCapture();

        _tracker.EnqueueCapture(capture);

        // Todo: oh my god thread this
        Frame frame = _tracker.PopResult(); // System.TimeSpan.FromMilliseconds(4d)
        if (frame == null) {
            Debug.LogWarningFormat("Unable to get BodyTracking frame");
            return;
        }

        _numBodies = frame.NumberOfBodies;

        if (_numBodies > 0) {
            Increment();

            uint bodyId = frame.GetBodyId(0);
            _buffer[_bufIdx].CopyFrom(frame.GetBodySkeleton(0), _kinectTransform, _tick);
        }

        _tick++;
    }

    private void Increment() {
        _bufIdx = (_bufIdx + 1) % _buffer.Length;
    }

    /*
    Todo: 
    
    - make several variants
        - weighted interpolation based on confidence levels
        - extrapolation based on inferred derivatives from timeseries
    */
    private static void Interpolate(UnitySkeleton a, UnitySkeleton b, UnitySkeleton c, float lerp) {
        for (int i = 0; i < 32; i++) {
            var jointA = a.GetJoint((JointId)i);
            var jointB = b.GetJoint((JointId)i);

            var bone = new BoneState();
            bone.position = math.lerp(jointA.position, jointB.position, lerp);
            bone.rotation = math.slerp(jointA.rotation, jointB.rotation, lerp);
            bone.confidenceLevel = (JointConfidenceLevel)math.max((int)jointA.confidenceLevel, (int)jointB.confidenceLevel);
            c.bones[i] = bone;

            c.timestamp = (uint)math.round(math.lerp(a.timestamp, b.timestamp, lerp));
        }
    }
}

public struct BoneState {
    public float3 position;
    public quaternion rotation;
    public JointConfidenceLevel confidenceLevel;
}

public struct UnitySkeleton : System.IDisposable {
    public NativeArray<BoneState> bones;
    public uint timestamp;

    public UnitySkeleton(Allocator allocator) {
        this.timestamp = 0;
        this.bones = new NativeArray<BoneState>(32, allocator);
    }

    public void Dispose() {
        bones.Dispose();
    }

    public void CopyFrom(Skeleton skeleton, Transform kinect, uint timestamp) {
        const float mm2m = 0.01f; // When using 0.001, we get a dwarf. Do they mean cm?

        for (int i = 0; i < 32; i++) {
            var joint = skeleton.GetJoint(i);

            var bone = new BoneState();
            bone.position = kinect.TransformPoint(NumericsUtil.Convert(joint.Position) * mm2m);
            bone.rotation = kinect.rotation * NumericsUtil.Convert(joint.Quaternion);
            bone.confidenceLevel = joint.ConfidenceLevel;
            bones[i] = bone;
        }

        this.timestamp = timestamp;
    }

    public BoneState GetJoint(JointId id) {
        return bones[(int)id];
    }
}

public static class NumericsUtil {
    public static Unity.Mathematics.float3 Convert(System.Numerics.Vector3 v) {
        return new Unity.Mathematics.float3(
            v.X,
            -v.Y,
            v.Z
        );
    }

    public static Unity.Mathematics.quaternion Convert(System.Numerics.Quaternion v) {
        // Todo: UNTESTED
        return new Unity.Mathematics.quaternion(v.X, v.Y, v.Z, v.W);
    }
}