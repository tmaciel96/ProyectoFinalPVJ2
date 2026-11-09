// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Generador de emboscadas basado en calderos malditos. Detecta la proximidad del jugador mediante
// un radio de activación para invocar oleadas de enemigos espectrales utilizando el ObjectPooler
// (o prefabs de respaldo), activando partículas de caldero y sonidos de invocación.
// =================================================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursedSpawner : MonoBehaviour
{
    [Header("Configuración de Trampa / Emboscada")]
    public float triggerRadius = 7f;
    public int enemiesToSpawn = 3;
    public float spawnInterval = 0.5f;
    public Transform[] spawnPoints;

    [Header("Tags de Pool para Enemigos")]
    public List<string> enemyPoolTags = new List<string>() { "EnemyOrb_Fast", "EnemyOrb_Tank", "EnemyOrb_Shooter" };

    [Header("Prefab de Respaldo")]
    public GameObject fallbackEnemyPrefab;

    [Header("Efectos Opcionales")]
    public ParticleSystem triggerVFX;
    public AudioSource triggerAudio;

    private bool hasTriggered = false;
    private Transform playerTransform;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (hasTriggered) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            if (playerTransform == null) return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= triggerRadius)
        {
            TriggerSpawner();
        }
    }

    public void TriggerSpawner()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        if (triggerVFX != null) triggerVFX.Play();
        if (triggerAudio != null) triggerAudio.Play();

        StartCoroutine(SpawnEnemiesRoutine());
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 1.5f;
            spawnPos.y = transform.position.y + 0.5f;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                spawnPos = spawnPoints[i % spawnPoints.Length].position;
            }

            bool spawned = false;

            // 1. Intentar spawnear desde ObjectPooler
            if (ObjectPooler.Instance != null && ObjectPooler.Instance.poolDictionary != null)
            {
                // Buscar si alguno de los tags existe en el diccionario del pool
                List<string> validTags = new List<string>();
                foreach (var tag in enemyPoolTags)
                {
                    if (ObjectPooler.Instance.poolDictionary.ContainsKey(tag))
                    {
                        validTags.Add(tag);
                    }
                }

                if (validTags.Count > 0)
                {
                    string chosenTag = validTags[Random.Range(0, validTags.Count)];
                    GameObject obj = ObjectPooler.Instance.SpawnFromPool(chosenTag, spawnPos, Quaternion.identity);
                    if (obj != null)
                    {
                        MarkAsQuestEnemy(obj);
                        spawned = true;
                    }
                }
            }

            // 2. Si el pool no tenía los tags, usar prefab de respaldo asignado
            if (!spawned && fallbackEnemyPrefab != null)
            {
                MarkAsQuestEnemy(Instantiate(fallbackEnemyPrefab, spawnPos, Quaternion.identity));
                spawned = true;
            }

            yield return new WaitForSeconds(spawnInterval);
        }
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
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
