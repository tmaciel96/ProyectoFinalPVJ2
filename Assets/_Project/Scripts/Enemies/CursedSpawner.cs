// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// (basado en el CursedSpawner de The Pumpkin Ritual, TP Programación 2)
//
// CursedSpawner (M4-6): hace aparecer un grupo de enemigos cuando el jugador se acerca (o cuando lo pide
// una misión con TriggerSpawner) y avisa cuando los mató a todos.
//   - Cantidad: "Enemies To Spawn", de a uno cada "Spawn Interval" segundos.
//   - Área: en los "Spawn Points" (en orden) o al azar dentro de "Spawn Radius" (sobre el NavMesh si hay).
//   - Tipo: "Enemy Prefabs" (Enemy_Zombie, Enemy_Espectro...). Cada prefab tiene su pool, que se crea
//     solo en el ObjectPooler y crece si hace falta: nunca se recicla un enemigo vivo. Si la lista
//     está vacía usa los pool tags de la escena o el prefab de respaldo, como en el TP1.
//   - Al morir el último: onAreaCleared (UnityEvent) y el evento estático OnAreaCleared.
// Los enemigos que crea llevan QuestEnemy, así las misiones de limpiar el área (AreaClearQuest) los cuentan.
// =================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class CursedSpawner : MonoBehaviour
{
    public enum PickMode { Random, InOrder }

    [Header("Activación")]
    [Tooltip("Aparecen cuando el jugador entra en este radio (desactivar para que solo los dispare una misión)")]
    public bool triggerOnProximity = true;
    public float triggerRadius = 7f;

    [Header("Cantidad")]
    public int enemiesToSpawn = 3;
    public float spawnInterval = 0.5f;

    [Header("Área")]
    [Tooltip("Puntos de aparición, en orden; vacío = al azar dentro de Spawn Radius")]
    public Transform[] spawnPoints;
    public float spawnRadius = 2.5f;

    [Header("Tipo")]
    [Tooltip("Prefabs de enemigos a crear (cada uno con su pool automático)")]
    public GameObject[] enemyPrefabs;
    public PickMode pickMode = PickMode.Random;
    [Tooltip("Tamaño inicial del pool de cada prefab (crece solo)")]
    public int poolSize = 6;

    [Header("Compatibilidad TP1 (si Enemy Prefabs está vacío)")]
    public List<string> enemyPoolTags = new List<string>() { "EnemyOrb_Fast", "EnemyOrb_Tank", "EnemyOrb_Shooter" };
    public GameObject fallbackEnemyPrefab;

    [Header("Efectos opcionales")]
    public ParticleSystem triggerVFX;
    public AudioSource triggerAudio;

    [Header("Eventos")]
    [Tooltip("Al morir el último enemigo de este spawner")]
    public UnityEvent onAreaCleared;

    /// <summary>Se dispara al morir el último enemigo de cualquier spawner.</summary>
    public static event Action<CursedSpawner> OnAreaCleared;

    public bool HasTriggered { get; private set; }
    public bool IsSpawning { get; private set; }
    public bool IsCleared { get; private set; }
    public int SpawnedCount => spawned.Count;
    public int AliveCount
    {
        get
        {
            int n = 0;
            foreach (GameObject e in spawned) if (IsAlive(e)) n++;
            return n;
        }
    }

    private readonly List<GameObject> spawned = new List<GameObject>();
    private Transform playerTransform;
    private int pickIndex;

    void Update()
    {
        if (!HasTriggered)
        {
            if (!triggerOnProximity) return;
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                playerTransform = player.transform;
            }

            if (Vector3.Distance(transform.position, playerTransform.position) <= triggerRadius) TriggerSpawner();
            return;
        }

        if (IsSpawning || IsCleared || spawned.Count == 0) return;
        if (AliveCount == 0)
        {
            IsCleared = true;
            Debug.Log($"<color=yellow>[CursedSpawner] {name}: área limpia ({spawned.Count} enemigos).</color>");
            onAreaCleared?.Invoke();
            OnAreaCleared?.Invoke(this);
        }
    }

    public void TriggerSpawner()
    {
        if (HasTriggered) return;
        HasTriggered = true;

        if (triggerVFX != null) triggerVFX.Play();
        if (triggerAudio != null) triggerAudio.Play();

        StartCoroutine(SpawnEnemiesRoutine());
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        IsSpawning = true;
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            GameObject enemy = SpawnOne(SpawnPosition(i));
            if (enemy != null)
            {
                MarkAsQuestEnemy(enemy);
                spawned.Add(enemy);
            }
            if (i < enemiesToSpawn - 1) yield return new WaitForSeconds(spawnInterval);
        }
        IsSpawning = false;

        if (spawned.Count == 0)
            Debug.LogWarning($"[CursedSpawner] {name}: no pudo crear ningún enemigo. Asigná Enemy Prefabs.", this);
    }

    private Vector3 SpawnPosition(int i)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform p = spawnPoints[i % spawnPoints.Length];
            if (p != null) return p.position;
        }

        Vector2 r = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 pos = transform.position + new Vector3(r.x, 0.5f, r.y);
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas)) pos = hit.position;
        return pos;
    }

    private GameObject SpawnOne(Vector3 pos)
    {
        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

        // 1. Prefabs propios, cada uno con su pool que crece solo
        GameObject prefab = PickPrefab();
        if (prefab != null)
        {
            if (ObjectPooler.Instance != null)
                return ObjectPooler.Instance.SpawnInactive("Spawn_" + prefab.name, prefab, pos, rot, poolSize);
            return Instantiate(prefab, pos, rot);
        }

        // 2. Pools de la escena (TP1)
        if (ObjectPooler.Instance != null && ObjectPooler.Instance.poolDictionary != null)
        {
            List<string> validTags = new List<string>();
            foreach (string tag in enemyPoolTags)
                if (ObjectPooler.Instance.HasPool(tag)) validTags.Add(tag);

            if (validTags.Count > 0)
            {
                GameObject obj = ObjectPooler.Instance.SpawnFromPool(validTags[UnityEngine.Random.Range(0, validTags.Count)], pos, rot);
                if (obj != null) return obj;
            }
        }

        // 3. Prefab de respaldo
        return fallbackEnemyPrefab != null ? Instantiate(fallbackEnemyPrefab, pos, rot) : null;
    }

    private GameObject PickPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;

        for (int tries = 0; tries < enemyPrefabs.Length; tries++)
        {
            GameObject p = pickMode == PickMode.InOrder
                ? enemyPrefabs[pickIndex++ % enemyPrefabs.Length]
                : enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
            if (p != null) return p;
        }
        return null;
    }

    private static bool IsAlive(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy) return false;
        EnemyBase eb = enemy.GetComponent<EnemyBase>();
        return eb == null || !eb.IsDead;
    }

    // M3-2 / M3-6: las misiones de limpiar el área cuentan los QuestEnemy. Si el prefab no la trae, se agrega
    // acá (al agregarla con el objeto activo se dispara su OnEnable y la misión la reclama en el momento).
    private static void MarkAsQuestEnemy(GameObject enemy)
    {
        if (enemy != null && enemy.GetComponent<QuestEnemy>() == null) enemy.AddComponent<QuestEnemy>();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.35f);
        if (triggerOnProximity) Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        if (spawnPoints == null || spawnPoints.Length == 0) Gizmos.DrawWireSphere(transform.position, spawnRadius);
        else foreach (Transform p in spawnPoints) if (p != null) Gizmos.DrawWireCube(p.position, Vector3.one * 0.4f);
    }
}
