using UnityEngine;

namespace Retro.ThirdPersonCharacter
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Combat))]
    public class Jumping : MonoBehaviour
    {
        private Animator _animator;
        private PlayerInput _playerInput;
        private Combat _combat;

        private bool isGrouned;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _combat = GetComponent<Combat>();
        }

        private void Update()
        {
            // if(_playerInput.Jump)
        }
    }
}