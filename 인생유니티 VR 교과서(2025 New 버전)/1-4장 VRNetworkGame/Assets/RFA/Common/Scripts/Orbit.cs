using UnityEngine;

namespace Retro.ThirdPersonCharacter
{
    public class Orbit : MonoBehaviour
    {
        public Transform target;
        public float distance;
        public float rotationSpeed;
        public Vector3 targetOffcet;
        public bool lockCursor = true;

        private Vector3 TargetPostion => target.position + targetOffcet;
        
        private void Start()
        {
            transform.position = transform.forward * distance + target.position;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        private void LateUpdate()
        {
            float xAngle = Input.GetAxis("Mouse X") * rotationSpeed;
        
            float yAngle = -Input.GetAxis("Mouse Y") * rotationSpeed;
        
            Quaternion xRotation = Quaternion.AngleAxis(xAngle, target.up);
        
            Quaternion yRotation = Quaternion.AngleAxis(yAngle, target.right);
        
            Quaternion newRotation = transform.rotation * xRotation * yRotation;
        
            transform.rotation = transform.rotation * xRotation * yRotation;
        
            transform.position = transform.forward * (-distance) + TargetPostion;

            transform.LookAt(TargetPostion);
        }
    }
}