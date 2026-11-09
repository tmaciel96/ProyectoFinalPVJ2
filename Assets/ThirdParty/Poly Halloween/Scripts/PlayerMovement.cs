using UnityEngine;

namespace Polyperfect.Universal
{
    public class PlayerMovement : MonoBehaviour
    {
        public CharacterController controller;
        public float speed = 12f;
        public float gravity = -9.81f;
        public float jumpHeight = 3f;

        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask;

        Vector3 velocity;
        bool isGrounded;

        void Update()
        {
            try
            {
                controller = GetComponent<CharacterController>();
                if (groundCheck != null)
                {
                    isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
                }

                if (isGrounded && velocity.y < 0)
                {
                    if (controller != null) controller.slopeLimit = 45.0f;
                    velocity.y = -2f;
                }

                float x = Input.GetAxis("Horizontal");
                float z = Input.GetAxis("Vertical");

                Vector3 move = transform.right * x + transform.forward * z;
                if (move.magnitude > 1)
                    move /= move.magnitude;

                if (controller != null) controller.Move(move * speed * Time.deltaTime);

                if (Input.GetButtonDown("Jump") && isGrounded)
                {
                    if (controller != null) controller.slopeLimit = 100.0f;
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }

                velocity.y += gravity * Time.deltaTime;

                if (controller != null) controller.Move(velocity * Time.deltaTime);
            }
            catch (System.Exception)
            {
                // Desactivar script automáticamente si el proyecto usa el nuevo Input System
                enabled = false;
            }
        }
    }
}