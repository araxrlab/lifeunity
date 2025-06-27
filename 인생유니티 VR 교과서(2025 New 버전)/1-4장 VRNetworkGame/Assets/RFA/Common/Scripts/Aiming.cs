using UnityEngine;

namespace Retro.ThirdPersonCharacter
{
    public class Aiming : MonoBehaviour
    {
        public float turnspeed = 15;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;   
        }

        private void LateUpdate()
        {
            float yawCamera = mainCamera.transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yawCamera, 0), turnspeed * Time.deltaTime);
        }
    }
}