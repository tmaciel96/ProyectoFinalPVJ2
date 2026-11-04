// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador de proyectiles reutilizables en el ObjectPooler (balas rúnicas del jugador y proyectiles
// de enemigos a distancia). Maneja velocidad de traslación, colisión con Rigidbody cinemático,
// exclusión de zonas de respawn y del propio tirador, cálculo de daño y efectos de impacto VFX.
// =================================================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PooledProjectile : MonoBehaviour
{
    [Header("Configuración del Proyectil")]
    public float speed = 22f;
    public int damage = 25;
    public float lifeTime = 3f;
    public bool isPlayerProjectile = true;

    [Header("Efectos de Impacto (Cartoon FX)")]
    public GameObject impactVFX;

    private float timer;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        // Esencial en Unity: Debe ser Kinematic y sin gravedad para que PhysX dispare OnTriggerEnter
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnEnable()
    {
        timer = 0f;
    }

    void Update()
    {
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));

        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Deactivate();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject hitObject)
    {
        // 1. Ignorar colisiones con el propio jugador si es bala del jugador
        if (isPlayerProjectile)
        {
            if (hitObject.CompareTag("Player") || 
                hitObject.GetComponent<PlayerController>() != null || 
                hitObject.GetComponentInParent<PlayerController>() != null)
            {
                return;
            }

            // Ignorar SpawnPoint y zonas de reaparición para no destruirse en el frame 0 al revivir
            if (hitObject.CompareTag("Respawn") || 
                hitObject.name.IndexOf("spawn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // Ignorar triggers de interacción que no sean enemigos (altares, portales, pickups)
            Collider col = hitObject.GetComponent<Collider>();
            if (col != null && col.isTrigger && !hitObject.CompareTag("Enemy") && hitObject.GetComponentInParent<EnemyOrb>() == null)
            {
                return;
            }

            // 2. Buscar si impactó a un enemigo (en el objeto, padres o hijos)
            EnemyOrb enemy = hitObject.GetComponent<EnemyOrb>();
            if (enemy == null) enemy = hitObject.GetComponentInParent<EnemyOrb>();
            if (enemy == null) enemy = hitObject.GetComponentInChildren<EnemyOrb>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                SpawnImpactEffect();
                Deactivate();
                return;
            }
            else if (hitObject.CompareTag("Enemy"))
            {
                // Si tiene el tag Enemy pero el script estaba en otro nivel
                EnemyOrb rootEnemy = hitObject.transform.root.GetComponentInChildren<EnemyOrb>();
                if (rootEnemy != null) rootEnemy.TakeDamage(damage);

                SpawnImpactEffect();
                Deactivate();
                return;
            }
            else
            {
                // Impacto contra obstáculos físicos del mapa (árboles, lápidas, suelo)
                SpawnImpactEffect();
                Deactivate();
            }
        }
        else // Proyectil disparado por enemigo
        {
            if (hitObject.CompareTag("Enemy") || 
                hitObject.GetComponent<EnemyOrb>() != null || 
                hitObject.GetComponentInParent<EnemyOrb>() != null)
            {
                return;
            }

            PlayerHealth playerHealth = hitObject.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = hitObject.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                SpawnImpactEffect();
                Deactivate();
                return;
            }
            else
            {
                SpawnImpactEffect();
                Deactivate();
            }
        }
    }

    void SpawnImpactEffect()
    {
        if (impactVFX != null)
        {
            GameObject vfx = Instantiate(impactVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }

    public void Deactivate()
    {
        gameObject.SetActive(false);
    }
}
