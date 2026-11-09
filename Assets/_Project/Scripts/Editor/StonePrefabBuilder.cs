// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Menú Sepulturero/Crear prefabs de piedras (M1-5, M1-6):
//  - Prefabs/Player/Projectile_Stone.prefab: piedra chica de Poly Halloween con Projectile.
//  - Prefabs/Pickups/Item_PilaPiedras.prefab: montículo (Rock_A + piedras sueltas) con StonePile.
//  - Asigna Projectile_Stone en el campo stoneProjectilePrefab del Player.prefab (y le suma StoneInventory).
// Si los prefabs ya existen los sobrescribe (conserva el GUID, así no se rompen las referencias).
// =================================================================================================

using UnityEditor;
using UnityEngine;

public static class StonePrefabBuilder
{
    private const string StoneModelPath = "Assets/ThirdParty/Poly Halloween/Prefabs/Nature/Rocks/Stone_F.prefab";
    private const string RockModelPath = "Assets/ThirdParty/Poly Halloween/Prefabs/Nature/Rocks/Rock_A.prefab";
    private const string ProjectilePath = "Assets/_Project/Prefabs/Player/Projectile_Stone.prefab";
    private const string PilePath = "Assets/_Project/Prefabs/Pickups/Item_PilaPiedras.prefab";
    private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";

    [MenuItem("Sepulturero/Crear prefabs de piedras")]
    public static void Build()
    {
        GameObject stoneModel = AssetDatabase.LoadAssetAtPath<GameObject>(StoneModelPath);
        GameObject rockModel = AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath);
        if (stoneModel == null || rockModel == null)
        {
            Debug.LogError($"[Sepulturero] Faltan los modelos de piedra ({StoneModelPath} / {RockModelPath})");
            return;
        }

        GameObject projectile = BuildProjectile(stoneModel);
        BuildPile(rockModel, stoneModel);
        if (projectile != null) AssignToPlayer(projectile);

        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- Proyectil

    private static GameObject BuildProjectile(GameObject stoneModel)
    {
        GameObject root = new GameObject("Projectile_Stone");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(stoneModel, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            RemoveColliders(model);

            // Escalar el modelo a ~0.3 m de diámetro, sea cual sea su tamaño original
            Bounds b = CalculateBounds(model);
            float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (biggest > 0.001f) model.transform.localScale = Vector3.one * (0.3f / biggest);
            b = CalculateBounds(model);
            model.transform.localPosition = -(b.center - root.transform.position);

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.2f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Projectile proj = root.AddComponent<Projectile>();
            proj.speed = 18f;
            proj.damage = 25;
            proj.lifeTime = 1.5f;
            proj.isPlayerProjectile = true;

            return Save(root, ProjectilePath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- Montículo

    private static void BuildPile(GameObject rockModel, GameObject stoneModel)
    {
        GameObject root = new GameObject("Item_PilaPiedras");
        try
        {
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(rockModel, root.transform);
            rock.transform.localPosition = Vector3.zero;
            rock.transform.localRotation = Quaternion.identity;

            // Que la roca base mida ~1 m de ancho
            Bounds rb = CalculateBounds(rock);
            float width = Mathf.Max(rb.size.x, rb.size.z);
            if (width > 0.001f) rock.transform.localScale = Vector3.one * (1f / width);
            rb = CalculateBounds(rock);

            // Piedras sueltas encima: se ocultan cuando el montículo está vacío
            GameObject loose = new GameObject("PiedrasSueltas");
            loose.transform.SetParent(root.transform, false);
            Vector3[] offsets =
            {
                new Vector3(0.25f, 0f, 0.15f), new Vector3(-0.2f, 0f, 0.25f),
                new Vector3(0.05f, 0f, -0.3f), new Vector3(-0.3f, 0f, -0.1f), new Vector3(0.3f, 0f, -0.2f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject s = (GameObject)PrefabUtility.InstantiatePrefab(stoneModel, loose.transform);
                RemoveColliders(s);
                Bounds sb = CalculateBounds(s);
                float biggest = Mathf.Max(sb.size.x, sb.size.y, sb.size.z);
                if (biggest > 0.001f) s.transform.localScale = Vector3.one * (0.25f / biggest);
                s.transform.localRotation = Quaternion.Euler(0f, i * 73f, i * 17f);
                s.transform.localPosition = offsets[i] + Vector3.up * (rb.max.y - root.transform.position.y) * 0.6f;
            }

            // Collider sólido para que no se atraviese (la interacción es por distancia, no por trigger)
            if (rock.GetComponentInChildren<Collider>() == null)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, rb.size.y * 0.5f, 0f);
                box.size = new Vector3(Mathf.Max(rb.size.x, 0.5f), Mathf.Max(rb.size.y, 0.3f), Mathf.Max(rb.size.z, 0.5f));
            }

            StonePile pile = root.AddComponent<StonePile>();
            pile.fullVisual = loose;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            pile.audioSource = audio;

            Save(root, PilePath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- Player

    private static void AssignToPlayer(GameObject projectile)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
        if (contents == null)
        {
            Debug.LogWarning($"[Sepulturero] No encontré {PlayerPath}: asigná Projectile_Stone a mano en el PlayerController.");
            return;
        }
        try
        {
            PlayerController pc = contents.GetComponent<PlayerController>();
            if (pc != null) pc.stoneProjectilePrefab = projectile;
            if (contents.GetComponent<StoneInventory>() == null) contents.AddComponent<StoneInventory>();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
            Debug.Log("[Sepulturero] Player.prefab: stoneProjectilePrefab asignado y StoneInventory agregado.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------------------------------------------------------------- Utilidades

    private static GameObject Save(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        if (ok) Debug.Log($"[Sepulturero] Prefab creado: {path}");
        else Debug.LogError($"[Sepulturero] No se pudo guardar {path}");
        return ok ? saved : null;
    }

    private static void RemoveColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
