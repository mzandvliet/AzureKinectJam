using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
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

        _captureWatch = new System.Diagnostics.Stopwatch();
    }

    private void OnDestroy() {
        _device.Dispose();
    }

    System.Diagnostics.Stopwatch _captureWatch;

    private void Update() {
        if (_captureWatch.IsRunning) {
            if (_captureWatch.ElapsedMilliseconds >= 1000 / 30) {
                _captureWatch.Reset();
            }
        } else {
            Capture();
            _captureWatch.Start();
        }
    }

    private void OnGUI() {
        GUI.DrawTexture(new Rect(0f, 0f, _colorWidth, _colorHeight), _colorTex, ScaleMode.ScaleToFit);
    }

    /*
    Todo: Try to use this interal method!

    /// <summary>
        /// Gets a native pointer to the underlying memory.
        /// </summary>
        /// <remarks>
        /// This property may only be accessed by unsafe code.
        ///
        /// This returns an unsafe pointer to the image's memory. It is important that the
        /// caller ensures the Image is not Disposed or garbage collected while this pointer is
        /// in use, since it may become invalid when the Image is disposed or finalized.
        ///
        /// If this method needs to be used in a context where the caller cannot guarantee that the
        /// Image will be disposed by another thread, the caller can call <see cref="Reference"/>
        /// to create a duplicate reference to the Image which can be disposed separately.
        ///
        /// For safe buffer access <see cref="Memory"/>.
        /// </remarks>
        /// <returns>A pointer to the native buffer.</returns>
        internal unsafe void* GetUnsafeBuffer()
        {
            if (this.buffer != IntPtr.Zero)
            {
                if (this.disposedValue)
                {
                    throw new ObjectDisposedException(nameof(Image));
                }

                return (void*)this.buffer;
            }

            lock (this)
            {
                if (this.disposedValue)
                {
                    throw new ObjectDisposedException(nameof(Image));
                }

                this.buffer = NativeMethods.k4a_image_get_buffer(this.handle);
                if (this.buffer == IntPtr.Zero)
                {
                    throw new AzureKinectException("Image has NULL buffer");
                }

                return (void*)this.buffer;
            }
        }

        Or from Transformation.cs

         using (Image pointCloudReference = pointCloud.Reference())
                {
                    // Ensure changes made to the managed memory are visible to the native layer
                    depthReference.FlushMemory();

                    AzureKinectException.ThrowIfNotSuccess(() => NativeMethods.k4a_transformation_depth_image_to_point_cloud(
                        this.handle,
                        depthReference.DangerousGetHandle(),
                        camera,
                        pointCloudReference.DangerousGetHandle()));

                    // Copy the native memory back to managed memory if required
                    pointCloudReference.InvalidateMemory();
                }


        https://forum.unity.com/threads/nativearrayunsafeutility-convertexistingdatatonativearray.693775/
        
        The length param for this function is in number of elements, not bytes

        https://forum.unity.com/threads/mesh-improvements.684688/
        
        Allocator.Invalid ?
        AtomicSafetyHandle!!
        
    */

    private unsafe void Capture() {
        Capture capture = _device.GetCapture();

        // _depthTransformer.DepthImageToColorCamera(capture); // Todo: needs to write to _transformedDepth Image?

        // var colorPixelMemory = capture.Color.GetPixels<BGRA>().Span;
        // var colorPixelMemory = capture.Color.Memory;
        // var depthPixels = capture.Depth.GetPixels<ushort>().Span;

        // capture.Color.FlushMemory();

        // var pin = capture.Color.Memory.Pin();
        void* unsafeBuffer = capture.Color.GetUnsafeBuffer();


        // ulong gcHandle;
        // var addr = UnsafeUtility.PinGCObjectAndGetAddress(capture.Color.Memory, out gcHandle);

        // (int)capture.Color.Size
        var colorInput = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BGRA>(unsafeBuffer, _colorWidth * _colorHeight, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var safetyHandle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref colorInput, safetyHandle);
#endif

        var colorOutput = _colorTex.GetRawTextureData<Color32>();

        var job = new ConvertColorDataJob
        {
            colorBGRA = colorInput,
            colorRGBA = colorOutput
        };
        job.Schedule(colorInput.Length, 64).Complete();

        // pin.Dispose();
        // UnsafeUtility.ReleaseGCObject(gcHandle);

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

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(safetyHandle);
#endif
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
