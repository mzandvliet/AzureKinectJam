using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

/*
    The Body tracking API uses a neural network model running on CUDA:
    dnn_model_2_0

    It is crazy expensive to run, and introduces a lot of latency.
*/

public class BodyTrackingTest : MonoBehaviour {
    private Device _device;

    private int2 _dims;

    private Texture2D _segmentTex;

    private Tracker _tracker;

    private void Awake() {
        Application.targetFrameRate = 120;

        _device = Device.Open(0);

        _device.StartCameras(new DeviceConfiguration
        {
            ColorResolution = ColorResolution.Off,
            DepthMode = DepthMode.WFOV_2x2Binned, // Note: makes a large difference in latency!
            SynchronizedImagesOnly = false,
            CameraFPS = FPS.FPS30,
        });

        var calibration = _device.GetCalibration();
        _dims = new int2(
            calibration.DepthCameraCalibration.ResolutionWidth,
            calibration.DepthCameraCalibration.ResolutionHeight
        );

        _segmentTex = new Texture2D(_dims.x, _dims.y, TextureFormat.RGBA32, false, true);

        _tracker = Tracker.Create(calibration, new TrackerConfiguration {
            SensorOrientation = SensorOrientation.Default,
            ProcessingMode = TrackerProcessingMode.Gpu,
            GpuDeviceId = 0,
        });

        Capture();

        _captureWatch = new System.Diagnostics.Stopwatch();
        _captureWatch.Start();
    }

    private void OnDestroy() {
        _device.Dispose();
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
    }

    private void OnGUI() {
        float scale = 512f;
        GUI.DrawTexture(new Rect(scale, 0f, scale, scale), _segmentTex, ScaleMode.ScaleToFit);

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(string.Format("Number of bodies found: {0}", _numBodies));
        GUILayout.EndVertical();
    }

    private uint _numBodies;
    private Skeleton _skeleton;

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
            uint bodyId = frame.GetBodyId(0);
            _skeleton = frame.GetBodySkeleton(0);

            var palette = new NativeArray<Color32>(3, Allocator.TempJob);
            palette[0] = new Color32(255, 0, 0, 255);
            palette[1] = new Color32(0, 255, 0, 255);
            palette[2] = new Color32(0, 0, 255, 255);

            var bodyMapPin = frame.BodyIndexMap.Memory.Pin();
            var bodyMapInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(bodyMapPin.Pointer, _dims.x * _dims.y, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var bodyMapSafetyHandle = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref bodyMapInput, bodyMapSafetyHandle);
#endif

            var segmentOutput = _segmentTex.GetRawTextureData<Color32>();

            var job = new ConvertSegmentMapToColorPreviewJob
            {
                dims = _dims,
                backgroundIndex = Frame.BodyIndexMapBackground,
                palette = palette,
                segmentIn = bodyMapInput,
                colorOut = segmentOutput
            };
            job.Schedule(bodyMapInput.Length, 64).Complete();

            _segmentTex.Apply(true);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(bodyMapSafetyHandle);
#endif

            bodyMapPin.Dispose();
            palette.Dispose();
        }

        frame.Dispose();
    }

    [BurstCompile]
    public struct ConvertSegmentMapToColorPreviewJob : IJobParallelFor {
        [ReadOnly]
        public int2 dims;

        [ReadOnly]
        public byte backgroundIndex;

        [ReadOnly]
        public NativeArray<Color32> palette;

        [ReadOnly]
        public NativeArray<byte> segmentIn;


        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Color32> colorOut;

        public void Execute(int iIn) {
            int x = iIn % dims.x;
            int y = iIn / dims.x;
            int iOut = (dims.y - 1 - y) * dims.x + x;

            var c = new Color32(0,0,0,255);
            if (segmentIn[iIn] != backgroundIndex) {
                c = Convert32(segmentIn[iIn], palette);
            }

            colorOut[iOut] = c;
        }
    }

    private static Color32 Convert32(ushort value, NativeArray<Color32> palette) {
        return palette[value % palette.Length];
    }
}
