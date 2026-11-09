// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Menú Sepulturero/Crear prefab Tumba Maldita: arma Prefabs/Enemies/Enemy_CursedTomb.prefab con la torre
// de lápida de Poly Halloween, una luz flotante (Point Light + partículas) arriba y el script CursedTomb.
// Si el prefab ya existe lo sobrescribe (conserva el GUID, así no se rompen las referencias).
// =================================================================================================

using UnityEditor;
using UnityEngine;

public static class CursedTombPrefabBuilder
{
    private const string TombModelPath = "Assets/ThirdParty/Poly Halloween/Prefabs/Halloween/Gravestone_Old_Tower.prefab";
    private const string BulletPath = "Assets/_Project/Prefabs/Enemies/Bullet_DarkOrb.prefab";
    private const string OutputPath = "Assets/_Project/Prefabs/Enemies/Enemy_CursedTomb.prefab";

    [MenuItem("Sepulturero/Crear prefab Tumba Maldita")]
    public static void Build()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TombModelPath);
        if (model == null)
        {
            Debug.LogError($"[Sepulturero] No encontré el modelo de la tumba en {TombModelPath}");
            return;
        }

        GameObject root = new GameObject("Enemy_CursedTomb");
        try
        {
            root.tag = "Enemy";

            // Modelo
            GameObject tomb = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            tomb.transform.localPosition = Vector3.zero;
            tomb.transform.localRotation = Quaternion.identity;

            Bounds bounds = CalculateBounds(tomb);
            float top = bounds.size.y > 0.01f ? bounds.max.y - root.transform.position.y : 2.5f;

            // Collider para la pala, la piedra y para que el jugador no la atraviese
            if (tomb.GetComponentInChildren<Collider>() == null)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                box.size = new Vector3(Mathf.Max(bounds.size.x, 0.6f), Mathf.Max(bounds.size.y, 1f), Mathf.Max(bounds.size.z, 0.6f));
            }

            // Luz flotante
            GameObject wisp = new GameObject("Wisp");
            wisp.transform.SetParent(root.transform, false);
            wisp.transform.localPosition = new Vector3(0f, top + 0.6f, 0f);

            Light light = wisp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2f;
            light.color = new Color(0.45f, 1f, 0.75f);
            light.shadows = LightShadows.None;

            ParticleSystem ps = CreateWispParticles(wisp.transform);

            // Lógica
            CursedTomb logic = root.AddComponent<CursedTomb>();
            logic.wisp = wisp.transform;
            logic.wispLight = light;
            logic.wispParticles = ps;
            logic.projectileFallbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            logic.audioSource = audio;

            PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out bool ok);
            if (ok)
            {
                Debug.Log($"[Sepulturero] Prefab creado: {OutputPath}");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath));
            }
            else
            {
                Debug.LogError("[Sepulturero] No se pudo guardar el prefab de la tumba.");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    private static ParticleSystem CreateWispParticles(Transform parent)
    {
        GameObject go = new GameObject("WispParticles");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new Color(0.45f, 1f, 0.75f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        // Material de partículas URP (el mismo shader que se pidió para FireMaterial)
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            const string matPath = "Assets/_Project/Prefabs/Enemies/Mat_WispParticles.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.SetFloat("_Surface", 1f); // transparente
                mat.SetFloat("_Blend", 2f);   // aditivo
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            renderer.sharedMaterial = mat;
        }

        return ps;
    }
}
