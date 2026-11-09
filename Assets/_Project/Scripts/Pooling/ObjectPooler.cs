// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Implementación del patrón de diseño Object Pooling (Singleton) para optimización de rendimiento.
// Administra colas reutilizables de proyectiles del jugador, proyectiles enemigos y coleccionables,
// evitando la sobrecarga de memoria y caídas de frames asociadas al Instantiate/Destroy frecuente.
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
