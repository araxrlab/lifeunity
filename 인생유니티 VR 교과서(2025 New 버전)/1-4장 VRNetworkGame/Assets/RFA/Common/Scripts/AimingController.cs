using UnityEngine;
using NaughtyCharacter;

namespace Retro.ThirdPersonCharacter
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Aiming))]
    public class AimingController : MonoBehaviour
    {
        private Aiming _aiming;
        private PlayerInput _playerInput;

        #pragma warning disable 0649
        [SerializeField] private bool _isAiming;
        [SerializeField] private SpringArm _springArm;
        
        #pragma warning restore 0649

        [Header("Settings")]
        [SerializeField] private float _aimCameraDistance = 3;
        [SerializeField] private float _regularCameraDistance = 1f;

        private void Start()
        {
            _aiming = GetComponent<Aiming>();
            _playerInput = GetComponent<PlayerInput>();

            OnStateChanged();
        }

        private void Update()
        {
            if(_playerInput.ChangeCameraModeInput) SwitchAim();
        }

        private void SwitchAim()
        {
            _isAiming = !_isAiming;
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            if(_isAiming)
            {
                _springArm.TargetLength = _aimCameraDistance;
                _aiming.enabled = true;
            }
            else
            {
                _springArm.TargetLength = _regularCameraDistance;
                _aiming.enabled = false;
            }
        }
    }
}