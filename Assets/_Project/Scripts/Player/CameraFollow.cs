// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador de cámara cenital / vista de pájaro con ángulo top-down (~65°). Sigue con suavizado
// Lerp al jugador en tiempo de ejecución e implementa SnapToTarget() vinculado al evento
// OnPlayerRespawned para teletransportar la cámara de inmediato al reaparecer y no romper el apuntado 3D.
// =================================================================================================

using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo a seguir")]
    public Transform target;

    [Header("Configuración")]
    [Tooltip("Distancia relativa entre la cámara y el jugador")]
    public Vector3 offset = new Vector3(0f, 15f, -10f);

    [Tooltip("Suavizado del movimiento (valores más altos = más rápido)")]
    public float smoothSpeed = 5f;

    [Tooltip("Si está activo, toma la distancia actual en la escena como offset")]
    public bool useCurrentOffset = true;

    void OnEnable()
    {
        PlayerHealth.OnPlayerRespawned += SnapToTarget;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerRespawned -= SnapToTarget;
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null && useCurrentOffset)
        {
            offset = transform.position - target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Teletransporta la cámara inmediatamente a la posición del jugador sin retraso Lerp.
    /// Crucial al reaparecer para que el cursor y el apuntado 3D no apunten hacia atrás en el mapa.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
}
