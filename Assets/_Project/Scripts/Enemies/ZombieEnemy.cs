// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// ZombieEnemy (M4-2): zombi lento y débil. Patrulla arrastrando los pies, persigue al jugador cuando lo
// ve cerca y golpea por contacto (resta 20 de vida; 5 golpes para matar al sepulturero). Toda la IA está
// en EnemyBase; acá van los valores del zombi, un leve tambaleo en la velocidad y los gruñidos.
// =================================================================================================

using UnityEngine;

public class ZombieEnemy : EnemyBase
{
    [Header("Zombi")]
    [Tooltip("Variación de la velocidad al perseguir (tambaleo), en proporción")]
    [Range(0f, 0.5f)] public float lurch = 0.25f;
    [Tooltip("Gruñidos al azar mientras está vivo")]
    public AudioClip[] groans;
    [Tooltip("Segundos entre gruñidos (mínimo y máximo)")]
    public Vector2 groanInterval = new Vector2(4f, 9f);

    private float baseChaseSpeed;
    private float baseAnimSpeed = 1f;
    private float lurchSeed;
    private float nextGroan;

    void Reset()
    {
        maxHealth = 60;
        attackDamage = 20;
        attackRange = 1.6f;
        attackCooldown = 1.4f;
        patrolSpeed = 0.8f;
        chaseSpeed = 2.2f;
        detectionRadius = 9f;
        loseAggroRadius = 15f;
        useNavMesh = true;
    }

    protected override void Awake()
    {
        base.Awake();
        baseChaseSpeed = chaseSpeed;
        lurchSeed = Random.value * 100f;
        if (animator != null) baseAnimSpeed = animator.speed;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // Cada zombi un poco distinto: así un grupo no camina en fila al mismo paso
        lurchSeed = Random.value * 100f;
        if (animator != null) animator.speed = baseAnimSpeed * Random.Range(0.9f, 1.1f);
        nextGroan = Time.time + Random.Range(groanInterval.x, groanInterval.y);
    }

    protected override void Update()
    {
        chaseSpeed = baseChaseSpeed * (1f + lurch * (Mathf.PerlinNoise(lurchSeed, Time.time * 0.8f) * 2f - 1f));
        base.Update();

        if (!IsDead && groans != null && groans.Length > 0 && Time.time >= nextGroan)
        {
            nextGroan = Time.time + Random.Range(groanInterval.x, groanInterval.y);
            PlayClip(groans[Random.Range(0, groans.Length)]);
        }
    }
}
