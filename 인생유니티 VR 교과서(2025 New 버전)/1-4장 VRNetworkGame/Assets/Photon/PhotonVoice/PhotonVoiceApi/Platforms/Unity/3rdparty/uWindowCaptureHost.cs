#if PHOTON_VOICE_VIDEO_ENABLE
// Basic support for https://github.com/hecomi/uWindowCapture

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && U_WINDOW_CAPTURE_RECORDER_ENABLE

using System;
using UnityEngine;
using uWindowCapture;

namespace Photon.Voice.Unity
{
    [AddComponentMenu("Photon Voice/uWindowCaptureHost")]
    public class uWindowCaptureHost : MonoBehaviour
    {
        public float encoderFPS = 30;
        float nextEncodingRealtime;

        public bool PrevBufferMode { get; private set; } // detect buffer mode change and re-initialize

        public bool waitingCapture = false;
        public event Action OnCaptured;

        void onCaptured()
        {
            if (!waitingCapture) return;
            waitingCapture = false;
            OnCaptured?.Invoke();
            // use window buffer if available
            PrevBufferMode = UseWindowBuffer && window.buffer != IntPtr.Zero;
        }

        [SerializeField]
        WindowTextureType type = WindowTextureType.Desktop;
        public WindowTextureType Type
        {
            get
            {
                return type;
            }
            set
            {
                shouldUpdateWindowOnParameterChanged = true;
                type = value;
            }
        }

        [SerializeField]
        bool altTabWindow = false;
        public bool AltTabWindow
        {
            get
            {
                return altTabWindow;
            }
            set
            {
                shouldUpdateWindowOnParameterChanged = true;
                altTabWindow = value;
            }
        }

        [SerializeField]
        string partialWindowTitle;
        public string PartialWindowTitle
        {
            get
            {
                return partialWindowTitle;
            }
            set
            {
                shouldUpdateWindowOnParameterChanged = true;
                partialWindowTitle = value;
            }
        }

        [SerializeField]
        int desktopIndex = 0;
        public int DesktopIndex
        {
            get
            {
                return desktopIndex;
            }
            set
            {
                shouldUpdateWindowOnParameterChanged = true;
                desktopIndex = (UwcManager.desktopCount > 0) ?
                    Mathf.Clamp(value, 0, UwcManager.desktopCount - 1) : 0;
            }
        }

        public CaptureMode captureMode = CaptureMode.Auto;
        // use window buffer if available
        public bool UseWindowBuffer;
        public CapturePriority capturePriority = CapturePriority.Auto;
        public bool drawCursor = true;

        UwcWindow window;
        public UwcWindow Window
        {
            get
            {
                return window;
            }
            set
            {
                if (window == value)
                {
                    return;
                }

                if (window != null)
                {
                    window.onCaptured.RemoveListener(onCaptured);
                }

                var old = window;
                window = value;
                onWindowChanged.Invoke(window, old);

                if (window != null)
                {
                    shouldUpdateWindowOnParameterChanged = false;
                    window.onCaptured.AddListener(onCaptured);
                    window.RequestCapture(CapturePriority.High);
                }
            }
        }

        UwcWindowChangeEvent onWindowChanged = new UwcWindowChangeEvent();
        public UwcWindowChangeEvent OnWindowChanged
        {
            get { return onWindowChanged; }
        }

        protected virtual void Update()
        {
            if (OnCaptured == null)
            {
                return;
            }

            if (SearchTiming == WindowSearchTiming.Always || (SearchTiming == WindowSearchTiming.OnlyWhenParameterChanged && shouldUpdateWindowOnParameterChanged))
            {
                switch (Type)
                {
                    case WindowTextureType.Window:
                        Window = UwcManager.Find(PartialWindowTitle, AltTabWindow);
                        break;
                    case WindowTextureType.Desktop:
                        Window = UwcManager.FindDesktop(DesktopIndex);
                        break;
                    case WindowTextureType.Child:
                        break;
                }
            }

            if (Window != null && Window.isValid)
            {

                Window.cursorDraw = drawCursor;
                Window.captureMode = captureMode;

                if (Time.realtimeSinceStartup < nextEncodingRealtime)
                {
                    return;
                }
                else
                {
                    nextEncodingRealtime = Time.realtimeSinceStartup + 1.0f / encoderFPS;
                    waitingCapture = true;

                    var priority = capturePriority;
                    if (priority == CapturePriority.Auto)
                    {
                        priority = CapturePriority.Low;
                        if (Window == UwcManager.cursorWindow)
                        {
                            priority = CapturePriority.High;
                        }
                        else if (Window.zOrder < UwcSetting.MiddlePriorityMaxZ)
                        {
                            priority = CapturePriority.Middle;
                        }
                    }

                    Window.RequestCapture(priority);
                }
            }
        }

        private void UWindowCaptureMB_onCaptured()
        {
            throw new NotImplementedException();
        }

        public void RequestWindowUpdate()
        {
            shouldUpdateWindowOnParameterChanged = true;
        }

        bool shouldUpdateWindowOnParameterChanged = true;

        [SerializeField]
        WindowSearchTiming searchTiming = WindowSearchTiming.OnlyWhenParameterChanged;
        public WindowSearchTiming SearchTiming
        {
            get
            {
                return searchTiming;
            }
            set
            {
                searchTiming = value;
                shouldUpdateWindowOnParameterChanged = true;
            }
        }

    }
}

#endif
#endif
