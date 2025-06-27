﻿#if PUN_2_OR_NEWER

using Photon.Voice.PUN;
using UnityEngine;

namespace ExitGames.Demos.DemoPunVoice
{
    [RequireComponent(typeof(PhotonVoiceView))]
    public class PointersController : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        private GameObject pointerDown;
        [SerializeField]
        private GameObject pointerUp;
#pragma warning restore 649

        private PhotonVoiceView photonVoiceView;

        private void Awake()
        {
            this.photonVoiceView = this.GetComponent<PhotonVoiceView>();
            this.SetActiveSafe(this.pointerUp, false);
            this.SetActiveSafe(this.pointerDown, false);
        }

        private void Update()
        {
            this.SetActiveSafe(this.pointerDown, this.photonVoiceView.IsSpeaking);
            this.SetActiveSafe(this.pointerUp, this.photonVoiceView.IsRecording);
        }

        private void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
#endif