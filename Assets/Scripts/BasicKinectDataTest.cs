using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

/*
    Based on this sample code from the SDK:
    https://github.com/microsoft/Azure-Kinect-Samples/blob/master/build2019/csharp/1 - AcquiringImages/MainWindow.xaml.cs


    !!!IMPORTANT USAGE NOTE!!!

    Official Azure Kinect SDK depends on:

    System.Runtime.CompilerServices.Unsafe

    Which also ships with the Unity Package: Native Collections, found in cache:

    Library\PackageCache\com.unity.collections@0.5.1-preview.11

    So as soon as you have imported both into your project, they conflict. My
    first temporary solution is to cut the .dll from the package library and
    move it to the AzureKinect folder in Plugins. Not great, but it works for
    now...

    --

    Todo: 
    
    - non-block waits for new capture frames

    Can run in a TaskThread like the example does. Could edit
    SDK so it can run as a Burst thread that writes data
    directly into Unity texture memory?
*/

public class BasicKinectDataTest : MonoBehaviour {
    private Device _device;
    Image _transformedDepth;
    private Transformation _depthTransformer;

    private int2 _colorDims;
    private int2 _depthDims;

    private Texture2D _depthTex;
    private Texture2D _colorTex;

    private void Awake() {
        Application.targetFrameRate = 120;

        _device = Device.Open(0);

        _device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.WFOV_2x2Binned, // Note: makes a large difference in latency!
            SynchronizedImagesOnly = true, // Todo: might make interaction faster if we allow desync
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

        _transformedDepth = new Image(
            ImageFormat.Depth16,
            _colorDims.x, _colorDims.y, _colorDims.x * sizeof(System.UInt16));
        _depthTransformer = calibration.CreateTransformation();

        _colorTex = new Texture2D(_colorDims.x, _colorDims.y, TextureFormat.RGBA32, false, true);
        _depthTex = new Texture2D(_colorDims.x, _colorDims.y, TextureFormat.R16, false, true);

        Capture();

        _captureWatch = new System.Diagnostics.Stopwatch();
        _captureWatch.Start();
    }

    private void OnDestroy() {
        _device.Dispose();
        _transformedDepth.Dispose();
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
        GUI.DrawTexture(new Rect(0f, 0f, scale, scale), _colorTex, ScaleMode.ScaleToFit);
        GUI.DrawTexture(new Rect(scale, 0f, scale, scale), _depthTex, ScaleMode.ScaleToFit);
    }

    private unsafe void Capture() {
        Capture capture = _device.GetCapture();
        
        _depthTransformer.DepthImageToColorCamera(capture, _transformedDepth);

        var colorPin = capture.Color.Memory.Pin();
        var depthPin = _transformedDepth.Memory.Pin();

        var colorInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BGRA>(colorPin.Pointer, _colorDims.x * _colorDims.y, Allocator.None);
        var depthInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(depthPin.Pointer, _colorDims.x * _colorDims.y, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var colorSafetyHandle = AtomicSafetyHandle.Create();
        var depthSafetyHandle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref colorInput, colorSafetyHandle);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref depthInput, depthSafetyHandle);
#endif

        var colorOutput = _colorTex.GetRawTextureData<Color32>();
        var depthOutput = _depthTex.GetRawTextureData<ushort>();

        var job = new ConvertColorDataJob
        {
            dims = _colorDims,
            colorIn = colorInput,
            depthIn = depthInput,
            depthOut =  depthOutput,
            colorOut = colorOutput
        };
        job.Schedule(colorInput.Length, 64).Complete();

        _colorTex.Apply(true);
        _depthTex.Apply(true);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(colorSafetyHandle);
        AtomicSafetyHandle.Release(depthSafetyHandle);
#endif

        colorPin.Dispose();
        depthPin.Dispose();
    }

    [BurstCompile]
    public struct ConvertColorDataJob : IJobParallelFor {
        [ReadOnly]
        public int2 dims;

        [ReadOnly]
        public NativeArray<BGRA> colorIn;

        [ReadOnly]
        public NativeArray<ushort> depthIn;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<ushort> depthOut;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Color32> colorOut;

        public void Execute(int i) {
            int x = i % dims.x;
            int y = i / dims.x;
            int iOut = (dims.y - 1 - y) * dims.x + x;

            var c = Convert32(colorIn[i]);
            var d = depthIn[i];

            colorOut[iOut] = c;
            depthOut[iOut] = d;
        }
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
