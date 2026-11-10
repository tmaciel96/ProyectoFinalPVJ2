// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// EnemyPrefabBuilder (M4-5): menú "Sepulturero › Crear prefabs de enemigos".
//   - Enemy_Zombie y Enemy_Skeleton: cambia el EnemyOrb del TP1 por ZombieEnemy (conserva modelo, Animator,
//     VFX de muerte y ajuste de altura), saca la cápsula de relleno y agrega un NavMeshAgent apagado
//     (EnemyBase lo prende solo si hay NavMesh cerca). Se guardan en el mismo archivo: las escenas que
//     ya los usan siguen apuntando a ellos.
//   - Enemy_Espectro: nuevo, con el fantasma de Poly Halloween, luz fría, estela de partículas y
//     SpectreEnemy.
// Si existe la capa "Enemy" (M5-3) se la asigna a los tres. Se puede correr de nuevo sin problema.
// =================================================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyPrefabBuilder
{
    private const string Folder = "Assets/_Project/Prefabs/Enemies/";
    private const string GhostModel = "Assets/ThirdParty/Poly Halloween/Prefabs/People/Man_Ghost_Halloween.prefab";
    private static readonly Color SpectreColor = new Color(0.55f, 0.85f, 1f);

    [MenuItem("Sepulturero/Crear prefabs de enemigos")]
    public static void Build()
    {
        ConvertToZombie(Folder + "Enemy_Zombie.prefab", skeleton: false);
        ConvertToZombie(Folder + "Enemy_Skeleton.prefab", skeleton: true);
        BuildSpectre();
        AssetDatabase.SaveAssets();
        Debug.Log("[Sepulturero] Prefabs de enemigos listos. Asignalos en \"Enemy Prefabs\" de cada CursedSpawner.");
    }

    // ---------------------------------------------------------------- Zombi (y esqueleto)

    private static void ConvertToZombie(string path, bool skeleton)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning($"[Sepulturero] No está {path}; se saltea.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ZombieEnemy z = root.GetComponent<ZombieEnemy>();
            if (z == null)
            {
                // Lo que se ajustó a mano en el orbe se conserva
                EnemyOrb orb = root.GetComponent<EnemyOrb>();
                Animator anim = orb != null ? orb.animator : null;
                GameObject vfx = orb != null ? orb.deathVFX : null;
                float groundOffset = orb != null ? orb.groundOffset : 0f;
                float deathDuration = orb != null ? orb.deathAnimDuration : 2f;
                if (orb != null) Object.DestroyImmediate(orb, true);

                z = root.AddComponent<ZombieEnemy>();
                z.animator = anim;
                z.deathVFX = vfx;
                z.groundOffset = groundOffset;
                z.deathAnimDuration = deathDuration;
            }

            // Valores del GDD: zombi lento y débil (20 de daño); el esqueleto un poco más ágil y frágil
            z.maxHealth = skeleton ? 45 : 60;
            z.attackDamage = skeleton ? 15 : 20;
            z.attackRange = 1.6f;
            z.attackCooldown = skeleton ? 1.1f : 1.4f;
            z.patrolSpeed = skeleton ? 1.1f : 0.8f;
            z.chaseSpeed = skeleton ? 2.8f : 2.2f;
            z.detectionRadius = 9f;
            z.loseAggroRadius = 15f;
            z.useNavMesh = true;
            z.dropChance = 0f;

            // La cápsula visible de los orbes ya no hace falta (el modelo es el hijo)
            MeshRenderer mr = root.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr, true);
            MeshFilter mf = root.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf, true);

            CapsuleCollider col = root.GetComponent<CapsuleCollider>();
            if (col == null) col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.45f;

            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            if (agent == null) agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.angularSpeed = 360f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.2f;
            agent.enabled = false;

            SetEnemyLayer(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[Sepulturero] {path}: ahora usa ZombieEnemy.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- Espectro

    private static void BuildSpectre()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GhostModel);
        if (model == null)
        {
            Debug.LogError($"[Sepulturero] No encontré el modelo {GhostModel}");
            return;
        }

        GameObject root = new GameObject("Enemy_Espectro");
        try
        {
            root.tag = "Enemy";

            GameObject m = QuestItemPrefabBuilder.AddModel(model, root.transform, 1.9f);
            m.name = "Modelo";

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.95f, 0f);
            col.height = 1.9f;
            col.radius = 0.45f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            QuestItemPrefabBuilder.AddLight(root.transform, SpectreColor, new Vector3(0f, 1.1f, 0f), 1.4f, 4.5f);
            QuestItemPrefabBuilder.CreateParticles(root.transform, "Estela", SpectreColor, new Vector3(0f, 0.5f, 0f),
                rate: 16f, lifetime: 1.4f, speed: 0.15f, size: new Vector2(0.08f, 0.24f), radius: 0.45f, gravity: -0.08f);

            SpectreEnemy s = root.AddComponent<SpectreEnemy>();
            s.maxHealth = 40;
            s.attackDamage = 35;
            s.attackRange = 1.5f;
            s.attackCooldown = 1.6f;
            s.patrolSpeed = 1.8f;
            s.chaseSpeed = 4.2f;
            s.detectionRadius = 11f;
            s.loseAggroRadius = 18f;
            s.hitPushback = 0.8f;
            s.hitStun = 0.2f;
            s.useNavMesh = false;
            s.terrainFollowSpeed = 6f;
            s.hoverHeight = 0.5f;
            s.knockbackForce = 9f;
            s.dropChance = 0f;

            SetEnemyLayer(root);
            QuestItemPrefabBuilder.Save(root, Folder + "Enemy_Espectro.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetEnemyLayer(GameObject root)
    {
        int layer = LayerMask.NameToLayer("Enemy");
        if (layer < 0) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}
