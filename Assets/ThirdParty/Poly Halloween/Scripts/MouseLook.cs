using UnityEngine;

namespace Polyperfect.Universal
{
    public class MouseLook : MonoBehaviour
    {
        public float mouseSensitivity = 3f;
        public Transform playerBody;
        float xRotation = 0f;

        void Start()
        {
        }

        void Update()
        {
            try
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -90f, 90f);

                transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
                if (playerBody != null) playerBody.Rotate(Vector3.up * mouseX);
            }
            catch (System.Exception)
            {
                // Desactivar script automáticamente si el proyecto usa el nuevo Input System
                enabled = false;
            }
        }
    }
}