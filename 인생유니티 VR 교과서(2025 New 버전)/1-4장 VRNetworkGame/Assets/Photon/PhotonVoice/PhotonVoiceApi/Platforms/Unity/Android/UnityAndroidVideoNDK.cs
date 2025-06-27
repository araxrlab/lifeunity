#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Photon.Voice.Unity
{
    public class AndroidNDKVideoEncoder : IEncoder
    {
        const string lib_name = "photon-native-video";
        [DllImport(lib_name)]
        private static extern IntPtr create_video_encoder(DataCallbackDelegate dataCallback);
        [DllImport(lib_name)]
        private static extern void configure_video_encoder(IntPtr instanceID, int width, int height, int bitRate, int frameRate, int keyFrameInterval);
        [DllImport(lib_name)]
        private static extern void release_video_encoder(IntPtr handle);
        [DllImport(lib_name)]
        protected static extern IntPtr get_encoder_surface(IntPtr Encoder);


        private IntPtr handle;
        private delegate void DataCallbackDelegate(IntPtr buf, UIntPtr len, Int32 flags);

        protected static Dictionary<IntPtr, AndroidNDKVideoEncoder> instancePerHandle = new Dictionary<IntPtr, AndroidNDKVideoEncoder>();


        [MonoPInvokeCallback(typeof(DataCallbackDelegate))]
        static void dataCallbackStatic(IntPtr buf, UIntPtr len, Int32 flags)
        {
            var x = instancePerHandle.First();
            FrameFlags f =
                ((flags & 1) != 0 ? FrameFlags.KeyFrame : 0) |
                ((flags & 2) != 0 ? FrameFlags.Config : 0);

            x.Value.output(buf, (int)len, f);
        }

        public AndroidNDKVideoEncoder(ILogger logger, int width, int height, VoiceInfo info)
        {
            handle = create_video_encoder(dataCallbackStatic);
            instancePerHandle[handle] = this;
            configure_video_encoder(handle, width, height, info.Bitrate, info.FPS, info.KeyFrameInt);
        }

        public AndroidJavaObject Surface
        {
            get
            {
                if (nativeSurfaceUtil == null)
                {
                    nativeSurfaceUtil = new AndroidJavaObject("com.exitgames.photon.video.NativeSurfaceUtil");
                }
                return nativeSurfaceUtil.Call<AndroidJavaObject>("nativeToSurface", (long)SurfaceNative);
            }
        }
        AndroidJavaObject nativeSurfaceUtil;

        public IntPtr SurfaceNative => get_encoder_surface(handle); // native ASurfaceTexture*

        byte[] bufManaged = new byte[0];

        void output(IntPtr buf, int len, FrameFlags flags)
        {
            if (Output != null)
            {
                // native code uses flags defined in FrameFlags
                if (bufManaged.Length < len)
                {
                    bufManaged = new byte[len];
                }
                Marshal.Copy(buf, bufManaged, 0, len);
                Output(new ArraySegment<byte>(bufManaged, 0, len), flags);
            }
        }

        public Action<ArraySegment<byte>, FrameFlags> Output { set; private get; }

        static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
        public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
        {
            flags = 0;
            return EmptyBuffer;
        }

        public string Error { get; protected set; }

        public void EndOfStream()
        {
        }

        public I GetPlatformAPI<I>() where I : class
        {
            return null;
        }

        public virtual void Dispose()
        {
            lock (instancePerHandle)
            {
                instancePerHandle.Remove(handle);
            }
            if (handle != IntPtr.Zero)
            {
                release_video_encoder(handle);
                handle = IntPtr.Zero;
            }
            if (nativeSurfaceUtil != null)
            {
                nativeSurfaceUtil.Dispose();
                nativeSurfaceUtil = null;
            }
        }
    }

    public class AndroidNDKCamera : IDisposable
    {
        const string lib_name = "photon-native-video";
        [DllImport(lib_name)]
        protected static extern IntPtr create_camera_manager(string deviceID, int width, int height);
        [DllImport(lib_name)]
        protected static extern IntPtr start_camera_session(IntPtr camera);
        [DllImport(lib_name)]
        protected static extern void stop_camera_session(IntPtr camera);
        [DllImport(lib_name)]
        protected static extern void is_camera_active(IntPtr camera);
        [DllImport(lib_name)]
        protected static extern void get_camera_error(IntPtr camera);
        [DllImport(lib_name)]
        protected static extern void add_surface_to_camera(IntPtr camera, IntPtr surface);
        [DllImport(lib_name)]
        protected static extern int get_camera_width(IntPtr camera);
        [DllImport(lib_name)]
        protected static extern int get_camera_height(IntPtr camera);

        protected static Dictionary<IntPtr, AndroidNDKVideoEncoder> instancePerHandle = new Dictionary<IntPtr, AndroidNDKVideoEncoder>();

        private IntPtr handle;

        public AndroidNDKCamera(ILogger logger, VoiceInfo info, string deviceID)
        {
            handle = create_camera_manager(deviceID, info.Width, info.Height);
            logger.Log(LogLevel.Info, "[PV] [AC] Unity.AndroidNDKCamera initialized");
        }

        public int Width => get_camera_width(handle);
        public int Height => get_camera_height(handle);
        public string Error { get; protected set; }

        public void AddSurface(IntPtr surface)
        {
            add_surface_to_camera(handle, surface);
        }

        public void Start()
        {
            start_camera_session(handle);
        }

        public void Dispose()
        {
            lock (instancePerHandle)
            {
                instancePerHandle.Remove(handle);
            }
            if (handle != IntPtr.Zero)
            {
                stop_camera_session(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    public class AndroidNDKVideoRecorderSurfaceView : IVideoRecorder
    {
        public IEncoder Encoder => encoder;
        public object PlatformView => preview.View;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;

        public int Width => camera.Width;
        public int Height => camera.Height;
        public string Error => Encoder.Error;

        AndroidNDKCamera camera;
        AndroidNDKVideoEncoder encoder;
        AndroidSurfaceView preview;
        public AndroidNDKVideoRecorderSurfaceView(ILogger logger, VoiceInfo info, string cameraID, Action<IVideoRecorder> onReady)
        {
            camera = new AndroidNDKCamera(logger, info, cameraID);
            encoder = new AndroidNDKVideoEncoder(logger, camera.Width, camera.Height, info);
            camera.AddSurface(encoder.SurfaceNative);
            preview = new AndroidSurfaceView(logger, info, (surfaceNative) => {
                camera.AddSurface(surfaceNative);
                camera.Start();
            });

            onReady(this);
        }

        public void Dispose()
        {
            preview.Dispose();
            camera.Dispose();
            encoder.Dispose();
        }
    }

    public class AndroidNDKVideoRecorderUnityTexture : IVideoRecorder
    {
        public IEncoder Encoder => encoder;
        public object PlatformView => preview.Texture;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;

        public int Width => camera.Width;
        public int Height => camera.Height;
        public string Error => Encoder.Error;

        AndroidNDKCamera camera;
        AndroidNDKVideoEncoder encoder;
        AndroidTextureView preview;
        public AndroidNDKVideoRecorderUnityTexture(ILogger logger, VoiceInfo info, string cameraID, Action<IVideoRecorder> onReady)
        {
            camera = new AndroidNDKCamera(logger, info, cameraID);
            encoder = new AndroidNDKVideoEncoder(logger, camera.Width, camera.Height, info);
            preview = new AndroidTextureView(logger, info);
            camera.AddSurface(encoder.SurfaceNative);
            camera.AddSurface(preview.SurfaceNative);
            camera.Start();

            onReady(this);
        }

        public void Dispose()
        {
            preview.Dispose();
            camera.Dispose();
            encoder.Dispose();
        }
    }
}
#endif