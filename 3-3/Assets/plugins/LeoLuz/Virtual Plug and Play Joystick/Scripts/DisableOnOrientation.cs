using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LeoLuz.PlugAndPlayJoystick {
    public class DisableOnOrientation : MonoBehaviour {
        public enum Orientation { portrait, landscape }
        public Orientation DisableOn;
        private bool disabled;
        // Use this for initialization


        // Update is called once per frame
        void Update() {
            if (!disabled)
            {
                if (DisableOn == Orientation.portrait)
                {
                    if (IsPortrait())
                        disable();
                }
                else
                {
                    if (!IsPortrait())
                        disable();
                }
            }
            else
            {
                if (DisableOn == Orientation.portrait)
                {
                    if (!IsPortrait())
                        enable();
                }
                else
                {
                    if (IsPortrait())
                        enable();
                }
            }

        

        }

        bool IsPortrait()
        {
            return UnityEngine.Input.deviceOrientation == DeviceOrientation.Portrait || UnityEngine.Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown;
        }

        private void disable()
        {
            var btn = GetComponent<ButtonBase>();
            if (btn != null)
                btn.Disable();

            disabled = true;
        }

        private void enable()
        {
            var btn = GetComponent<ButtonBase>();
            if (btn != null)
                btn.Enable();

            disabled = false;
        }
    }
}