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
    Image _transformedDepth;
    Image _transformedSegment;
    private Transformation _depthTransformer;

    private int2 _colorDims;
    private int2 _depthDims;

    private Texture2D _segmentTex;
    private Texture2D _colorTex;

    private Tracker _tracker;

    private void Awake() {
        Application.targetFrameRate = 120;

        _device = Device.Open(0);

        _device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32, // Note: other formats would be hardware-native, faster
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.WFOV_2x2Binned, // Note: makes a large difference in latency!
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });

        var calibration = _device.GetCalibration();
        _colorDims = new int2(
             calibration.ColorCameraCalibration.ResolutionWidth,
             calibration.ColorCameraCalibration.ResolutionHeight
         );
        _depthDims = new int2(
            calibration.DepthCameraCalibration.ResolutionWidth,
            calibration.DepthCameraCalibration.ResolutionHeight
        );

        _segmentTex = new Texture2D(_colorDims.x, _colorDims.y, TextureFormat.RGBA32, false, true);
        _colorTex = new Texture2D(_colorDims.x, _colorDims.y, TextureFormat.RGBA32, false, true);

        _transformedDepth = new Image(
            ImageFormat.Depth16,
            _colorDims.x, _colorDims.y, _colorDims.x * sizeof(System.UInt16));

        _transformedSegment = new Image(
            ImageFormat.Custom8,
            _colorDims.x, _colorDims.y, _colorDims.x * sizeof(System.Byte));

        _depthTransformer = calibration.CreateTransformation();

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
        _transformedDepth.Dispose();
        _transformedSegment.Dispose();
        _tracker.Dispose();
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
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _colorTex, ScaleMode.ScaleToFit);

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

        var palette = new NativeArray<Color32>(3, Allocator.TempJob);
        palette[0] = new Color32(255, 0, 0, 255);
        palette[1] = new Color32(0, 255, 0, 255);
        palette[2] = new Color32(0, 0, 255, 255);

        if (_numBodies > 0) {
            uint bodyId = frame.GetBodyId(0);
            _skeleton = frame.GetBodySkeleton(0);
        }

        _depthTransformer.DepthImageToColorCameraCustom(
            capture.Depth,
            frame.BodyIndexMap,
            _transformedDepth,
            _transformedSegment);

        var colorPin = capture.Color.Memory.Pin();
        var bodyMapPin = _transformedSegment.Memory.Pin();
        var colorInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BGRA>(colorPin.Pointer, _colorDims.x * _colorDims.y, Allocator.None);
        var bodyMapInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(bodyMapPin.Pointer, _colorDims.x * _colorDims.y, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var colorSafetyHandle = AtomicSafetyHandle.Create();
        var bodyMapSafetyHandle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref bodyMapInput, bodyMapSafetyHandle);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref colorInput, colorSafetyHandle);
#endif

        var colorOutput = _colorTex.GetRawTextureData<Color32>();
        var segmentOutput = _segmentTex.GetRawTextureData<Color32>();

        var job = new ConvertSegmentMapToColorPreviewJob
        {
            dims = _colorDims,
            backgroundIndex = Frame.BodyIndexMapBackground,
            bodyPalette = palette,
            colorIn = colorInput,
            segmentIn = bodyMapInput,
            segmentOut = segmentOutput,
            colorOut = colorOutput
        };
        job.Schedule(bodyMapInput.Length, 64).Complete();

        _segmentTex.Apply(true);
        _colorTex.Apply(true);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(bodyMapSafetyHandle);
        AtomicSafetyHandle.Release(colorSafetyHandle);
#endif

        colorPin.Dispose();
        bodyMapPin.Dispose();

        frame.Dispose();
        palette.Dispose();
    }

    [BurstCompile]
    public struct ConvertSegmentMapToColorPreviewJob : IJobParallelFor {
        [ReadOnly]
        public int2 dims;

        [ReadOnly]
        public byte backgroundIndex;

        [ReadOnly]
        public NativeArray<Color32> bodyPalette;

        [ReadOnly]
        public NativeArray<BGRA> colorIn;

        [ReadOnly]
        public NativeArray<byte> segmentIn;


        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Color32> segmentOut;
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Color32> colorOut;

        public void Execute(int iIn) {
            int x = iIn % dims.x;
            int y = iIn / dims.x;

            // HACK: read offset applied to segmentation map, as the data returned by Tranformation is off?
            int sIn = y * dims.x + (math.min(x + 16, dims.x-1));

            var depthColor = new Color32(0,0,0,255);
            var color = new Color32(0, 0, 0, 255);
            if (segmentIn[sIn] != backgroundIndex) {
                depthColor = GetPaletted(segmentIn[sIn], bodyPalette);
                color = Convert32(colorIn[iIn]);
            }

            int iOut = (dims.y - 1 - y) * dims.x + x;
            segmentOut[iOut] = depthColor;
            colorOut[iOut] = color;
        }
    }

    private static Color32 GetPaletted(ushort value, NativeArray<Color32> palette) {
        return palette[value % palette.Length];
    }

    private static Color32 Convert32(BGRA c) {
        return new Color32(
            c.R,
            c.G,
            c.B,
            c.A
        );
    }
}
