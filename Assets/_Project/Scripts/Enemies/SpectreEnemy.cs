// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// SpectreEnemy (M4-3): Espectro Maldito. Más rápido y más frágil que el zombi, flota con un vaivén sobre
// las tumbas (patrulla entre los "Patrol Points", que conviene poner sobre las tumbas malditas) y al
// golpear resta 35 de vida (3 golpes) y empuja al sepulturero hacia atrás.
// Por defecto no usa NavMesh: atraviesa lápidas y rejas bajas como un fantasma. Al morir se desvanece
// subiendo y achicándose (el modelo de Poly Halloween no trae animaciones).
// =================================================================================================

using System.Collections;
using UnityEngine;

public class SpectreEnemy : EnemyBase
{
    [Header("Espectro: flotar")]
    [Tooltip("Altura sobre el piso")]
    public float hoverHeight = 0.5f;
    public float bobAmplitude = 0.2f;
    public float bobFrequency = 1.4f;

    [Header("Espectro: retroceso al golpear")]
    [Tooltip("Velocidad inicial del empujón al jugador (m/s)")]
    public float knockbackForce = 9f;

    [Header("Espectro: muerte")]
    public float fadeDuration = 0.8f;
    public float fadeRise = 1.2f;

    private Vector3 baseScale;
    private float bobSeed;

    void Reset()
    {
        maxHealth = 40;
        attackDamage = 35;
        attackRange = 1.5f;
        attackCooldown = 1.6f;
        patrolSpeed = 1.8f;
        chaseSpeed = 4.2f;
        detectionRadius = 11f;
        loseAggroRadius = 18f;
        hitPushback = 0.8f;
        hitStun = 0.2f;
        useNavMesh = false;
        terrainFollowSpeed = 6f;
    }

    protected override void Awake()
    {
        base.Awake();
        baseScale = transform.localScale;
    }

    protected override void OnEnable()
    {
        transform.localScale = baseScale;
        bobSeed = Random.value * 10f;
        base.OnEnable();
    }

    protected override float HeightAboveGround(float groundY)
    {
        return groundY + hoverHeight + groundOffset + Mathf.Sin((Time.time + bobSeed) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
    }

    protected override void OnAttackLanded()
    {
        if (player == null || knockbackForce <= 0f) return;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        pc.ApplyKnockback(dir.normalized, knockbackForce);
    }

    protected override IEnumerator DeathRoutine()
    {
        Vector3 start = transform.position;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            transform.position = start + Vector3.up * (fadeRise * k);
            transform.localScale = baseScale * (1f - k * k);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
