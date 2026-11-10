// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Basado en el ObjectPooler del TP1 (The Pumpkin Ritual, Tamara Maciel).
//
// Object Pooling (singleton): colas de objetos reutilizables para no hacer Instantiate/Destroy en cada
// disparo. Los pools se cargan desde la lista de la escena (proyectiles y enemigos) o se registran en
// runtime con RegisterPool, como las piedras del jugador (M1-5). A los pooled con NavMeshAgent les apaga
// el agente si no hay NavMesh horneado cerca.
// =================================================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    [System.Serializable]
    public struct Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools = new List<Pool>();
    public Dictionary<string, Queue<GameObject>> poolDictionary;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null) continue;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, transform);
                
                // Desactivar NavMeshAgent si no hay NavMesh horneado en la escena
                NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(obj.transform.position, out hit, 5f, NavMesh.AllAreas))
                    {
                        agent.enabled = false;
                    }
                }

                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    public bool HasPool(string tag)
    {
        return poolDictionary != null && !string.IsNullOrEmpty(tag) && poolDictionary.ContainsKey(tag);
    }

    /// <summary>
    /// Crea un pool en runtime si todavía no existe (por ejemplo las piedras del jugador, M1-5),
    /// así un sistema nuevo no obliga a editar la lista de pools de la escena.
    /// </summary>
    public bool RegisterPool(string tag, GameObject prefab, int size)
    {
        if (prefab == null || string.IsNullOrEmpty(tag) || size <= 0) return false;
        if (poolDictionary == null) poolDictionary = new Dictionary<string, Queue<GameObject>>();
        if (poolDictionary.ContainsKey(tag)) return false;

        Queue<GameObject> objectPool = new Queue<GameObject>();
        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            objectPool.Enqueue(obj);
        }
        poolDictionary.Add(tag, objectPool);
        return true;
    }

    /// <summary>
    /// Como SpawnFromPool, pero nunca recicla un objeto que sigue activo (un enemigo vivo): toma uno
    /// apagado y, si no queda ninguno, instancia otro del prefab y lo suma al pool. Si el pool no existe,
    /// lo crea con ese prefab (M4-6, spawners).
    /// </summary>
    public GameObject SpawnInactive(string tag, GameObject prefab, Vector3 position, Quaternion rotation, int initialSize = 4)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        if (!HasPool(tag) && !RegisterPool(tag, prefab, Mathf.Max(1, initialSize))) return null;

        Queue<GameObject> queue = poolDictionary[tag];
        GameObject obj = null;
        for (int i = 0; i < queue.Count; i++)
        {
            GameObject candidate = queue.Dequeue();
            queue.Enqueue(candidate);
            if (candidate != null && !candidate.activeSelf) { obj = candidate; break; }
        }

        if (obj == null)
        {
            if (prefab == null) return null;
            obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            queue.Enqueue(obj);
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning("ObjectPooler: No se encontró el pool con tag '" + tag + "'");
            return null;
        }

        Queue<GameObject> queue = poolDictionary[tag];
        if (queue.Count == 0) return null;

        GameObject obj = queue.Dequeue();
        if (obj == null) return null;

        // Si tiene NavMeshAgent, evitar que arroje error al activar si no hay NavMesh
        NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(position, out hit, 5f, NavMesh.AllAreas))
            {
                agent.enabled = false;
            }
        }

        obj.SetActive(false);
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        queue.Enqueue(obj);
        return obj;
    }
}
