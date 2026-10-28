using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float gravity = -15f;

        [Header("References")]
        private CharacterController characterController;
        private UnityEngine.Camera mainCamera;

        private Vector2 moveInput;
        private Vector3 verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            mainCamera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            ApplyGravity();
            MovePlayer();
        }

        /// <summary>
        /// Recibe el input desde el componente PlayerInput (Behavior: Send Messages)
        /// Acción esperada: Move (Vector2)
        /// </summary>
        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        private void MovePlayer()
        {
            // Convertimos el input 2D (WASD / Stick) en movimiento 3D en el plano X-Z
            Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

            // Si la cámara está inclinada en perspectiva isométrica / top-down,
            // podemos alinear el movimiento relativo al ángulo de la cámara o al mundo:
            if (mainCamera != null && direction.sqrMagnitude > 0.001f)
            {
                // Dirección relativa a la rotación horizontal de la cámara
                Vector3 cameraForward = mainCamera.transform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                Vector3 cameraRight = mainCamera.transform.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                Vector3 movement = (cameraRight * moveInput.x + cameraForward * moveInput.y);
                movement = Vector3.ClampMagnitude(movement, 1f);

                // Mover el personaje
                characterController.Move(movement * moveSpeed * Time.deltaTime);

                // Rotar el personaje hacia donde camina
                Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Aplicar gravedad / movimiento vertical
            characterController.Move(verticalVelocity * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f; // Mantenerlo pegado al suelo en desniveles
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
            }
        }
    }
}

