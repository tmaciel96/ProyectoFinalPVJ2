using UnityEngine;

namespace Project.Cameras
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target to Follow")]
        [SerializeField] private Transform target;

        [Header("Position Offset")]
        [Tooltip("Distancia y altura respecto al jugador (ej: X=0, Y=10, Z=-7 para vista cenital inclinada)")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);

        [Header("Smooth Settings")]
        [SerializeField] private float smoothTime = 0.2f;

        private Vector3 currentVelocity = Vector3.zero;

        private void Start()
        {
            // Si no se asignó el target manualmente, intentamos buscar al Player por Tag
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            // Aplicar rotación cenital inicial apuntando hacia abajo
            LookAtTargetPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Posición deseada
            Vector3 targetPosition = target.position + offset;

            // Movimiento suave con SmoothDamp
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        }

        private void LookAtTargetPosition()
        {
            if (target != null)
            {
                transform.rotation = Quaternion.LookRotation(target.position - (target.position + offset));
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}

