// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Basado en el PooledProjectile del TP1 (The Pumpkin Ritual, Tamara Maciel).
//
// Proyectil que vive en el ObjectPooler: la piedra del sepulturero (Projectile_Stone) y los proyectiles
// de los enemigos a distancia. Avanza en línea recta con un Rigidbody cinemático, ignora al propio tirador
// y a los triggers de interacción, y al chocar con algo que implementa IDamageable le resta damage.
// Al chocar o al cumplir lifeTime se desactiva para volver al pool.
// =================================================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
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
        if (isPlayerProjectile) HitFromPlayer(hitObject);
        else HitFromEnemy(hitObject);
    }

    /// <summary>Piedra del jugador: daña a los enemigos (IDamageable) y se rompe contra el escenario.</summary>
    private void HitFromPlayer(GameObject hitObject)
    {
        // El propio jugador
        if (hitObject.CompareTag("Player") || hitObject.GetComponentInParent<PlayerController>() != null)
            return;

        // SpawnPoint y zonas de reaparición: no romperse en el frame en que revive
        if (hitObject.CompareTag("Respawn") ||
            hitObject.name.IndexOf("spawn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        IDamageable target = hitObject.GetComponentInParent<IDamageable>();
        if (target == null) target = hitObject.GetComponentInChildren<IDamageable>();
        if (target == null && hitObject.CompareTag("Enemy"))
            target = hitObject.transform.root.GetComponentInChildren<IDamageable>();

        // Triggers de interacción que no son enemigos (altares, portales, pickups, montículos)
        Collider col = hitObject.GetComponent<Collider>();
        if (col != null && col.isTrigger && target == null && !hitObject.CompareTag("Enemy"))
            return;

        // Enemigo o escenario (árboles, lápidas, suelo): en los dos casos la piedra se rompe
        if (target != null) target.TakeDamage(damage, transform.position);
        SpawnImpactEffect();
        Deactivate();
    }

    /// <summary>Proyectil de un enemigo: daña al jugador y atraviesa a los otros enemigos.</summary>
    private void HitFromEnemy(GameObject hitObject)
    {
        if (hitObject.CompareTag("Enemy") || hitObject.GetComponentInParent<IDamageable>() != null)
            return;

        PlayerHealth playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null) playerHealth.TakeDamage(damage);
        SpawnImpactEffect();
        Deactivate();
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
