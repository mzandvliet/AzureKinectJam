using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using System.Threading.Tasks;
using System.Reflection;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

/*

    !!!IMPORTANT USAGE NOTE!!!

    Official Azure Kinect SDK depends on:

    System.Runtime.CompilerServices.Unsafe

    Which also ships with the Unity Package: Native Collections, found in cache:

    Library\PackageCache\com.unity.collections@0.5.1-preview.11

    So as soon as you have imported both into your project, they conflict. My
    first temporary solution is to cut the .dll from the package library and
    move it to the AzureKinect folder in Plugins. Not great, but it works for
    now...


    Todo: 
    
    - non-block waits for new capture frames

    Can run in a TaskThread like the example does. Could edit
    SDK so it can run as a Burst thread that writes data
    directly into Unity texture memory?


*/

public class BasicKinectDataTest : MonoBehaviour {
    private Device _device;
    private Texture2D _depthTex;
    private Texture2D _colorTex;
    private Transformation _depthTransformer;

    private int _colorWidth;
    private int _colorHeight;

    private void Awake() {
        // string unsafeLibPath = "E:\\code\\unity\\KinectJam\\Library\\PackageCache\\com.unity.collections@0.5.1-preview.11\\System.Runtime.CompilerServices.Unsafe.dll";
        // if (System.IO.File.Exists(unsafeLibPath)) {
        //     Assembly.Load(unsafeLibPath);
        // } else {
        //     Debug.LogFormat("Path {0} does not exist", unsafeLibPath);
        // }

        _device = Device.Open(0);

        _device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.WFOV_2x2Binned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });

        var calibration = _device.GetCalibration();
        _colorWidth = calibration.ColorCameraCalibration.ResolutionWidth;
        _colorHeight = calibration.ColorCameraCalibration.ResolutionHeight;

        _depthTex = new Texture2D(_colorWidth, _colorHeight, TextureFormat.R16, false, true);
        _colorTex = new Texture2D(_colorWidth, _colorHeight, TextureFormat.RGBA32, false, true);

        _depthTransformer = calibration.CreateTransformation();
    }

    private void OnDestroy() {
        _device.Dispose();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            CaptureBlocking();
        }
    }

    private void OnGUI() {
        GUI.DrawTexture(new Rect(0f, 0f, _colorWidth, _colorHeight), _colorTex, ScaleMode.ScaleToFit);
    }

    private unsafe void CaptureBlocking() {
        Debug.Log("Capturing frame...");

        Capture capture = _device.GetCapture();

        // _depthTransformer.DepthImageToColorCamera(capture); // Todo: needs to write to _transformedDepth Image?

        var colorPixelMemory = capture.Color.GetPixels<BGRA>();
        // var depthPixels = capture.Depth.GetPixels<ushort>().Span;

        var pin = colorPixelMemory.Pin();
        var colorInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BGRA>(pin.Pointer, colorPixelMemory.Length, Allocator.Temp);
        var colorOutput = _colorTex.GetRawTextureData<Color32>();

        var job = new ConvertColorDataJob
        {
            colorBGRA = colorInput,
            colorRGBA = colorOutput
        };
        job.Schedule(colorInput.Length, 64).Complete();

        // for (int i = 0; i < _colorWidth * _colorHeight; i++) {
        // The output image will be the same as the input color image,
        // but colorized with Red where there is no depth data, and Green
        // where there is depth data at more than 1.5 meters

        // Color32 c = Convert(colorPixels[i]);
        // Color c = Convert(colorPixels[i]);

        // if (depthPixels[i] == 0) {
        //     c.r = 255;
        // } else if (depthPixels[i] > 1500) {
        //     c.g = 255;
        // }

        // colorData[i] = c;

        // Color c = new Color(0.12f, 0.23f, 0.88f, 1f);
        // _colorTex.SetPixel(i % _colorWidth, _colorHeight - i / _colorWidth, c);
        // }

        _colorTex.Apply(true);
    }


    [BurstCompile]
    public struct ConvertColorDataJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<BGRA> colorBGRA;

        [WriteOnly]
        public NativeArray<Color32> colorRGBA;

        public void Execute(int i) {
            colorRGBA[i] = Convert(colorBGRA[i]);
        }
    }

    // private async void CaptureAsync() {
    //     // Wait for a capture on a thread pool thread
    //     using (Capture capture = await Task.Run(() => { return _device.GetCapture(); }).ConfigureAwait(true)) {
    //         var colorPixels = capture.Color.GetPixels<BGRA>().Span;
    //         var depthPixels = capture.Depth.GetPixels<ushort>().Span;

    //         var colorBuffer = _outputColor.GetRawTextureData<Color32>();

    //         for (int i = 0; i < colorBuffer.Length; i++) {
    //             // The output image will be the same as the input color image,
    //             // but colorized with Red where there is no depth data, and Green
    //             // where there is depth data at more than 1.5 meters

    //             Color32 c = Convert(colorPixels[i]);

    //             if (depthPixels[i] == 0) {
    //                 c.r = 255;
    //             } else if (depthPixels[i] > 1500) {
    //                 c.g = 255;
    //             }

    //             colorBuffer[i] = c;
    //         }
    //     }
    // }

    private static Color32 Convert32(BGRA c) {
        return new Color32(
            c.R,
            c.G,
            c.B,
            c.A
        );
    }

    private static Color Convert(BGRA c) {
        return new Color(
            c.R / 255f,
            c.G / 255f,
            c.B / 255f,
            c.A / 255f
        );
    }
}
