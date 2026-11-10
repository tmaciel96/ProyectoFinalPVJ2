// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Menú Sepulturero/Crear prefabs de ítems de misión (M3-8). Arma en Prefabs/Items:
//  - Item_Ingrediente_01 / 02 / 03: hongo mágico, mano cortada y libro del ojo, con QuestItem (Bruja).
//  - Item_Caldero: caldero vacío que pasa a lleno con burbujas al completar la misión (QuestStateSwap).
//  - Item_Vela_Altar: vela encendida/apagada con AltarCandle (Vampiro).
//  - Item_Farol: farol chico con su Point Light; además lo cuelga del Player.prefab con PlayerLantern.
//  - Item_Ofrenda_Gato: ofrenda luminosa con QuestItem (GatoNegro).
// Los ítems de misión brillan con una luz de color y partículas aditivas (no hay shader de contorno en
// el proyecto). Si los prefabs ya existen se sobrescriben conservando el GUID.
// =================================================================================================

using UnityEditor;
using UnityEngine;

public static class QuestItemPrefabBuilder
{
    private const string H = "Assets/ThirdParty/Poly Halloween/Prefabs/Halloween/";
    private const string Folder = "Assets/_Project/Prefabs/Items";
    private const string GlowMatPath = "Assets/_Project/Prefabs/Enemies/Mat_WispParticles.mat";
    private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";

    private static readonly Color Green = new Color(0.45f, 1f, 0.4f);
    private static readonly Color Purple = new Color(0.75f, 0.45f, 1f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.35f);
    private static readonly Color Warm = new Color(1f, 0.72f, 0.4f);

    [MenuItem("Sepulturero/Crear prefabs de ítems de misión")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Items");

        BuildQuestItem("Item_Ingrediente_01", "Mushroom_Magical_Amanita_Mature", QuestId.Bruja, "hongo luminoso", Green, 0.45f);
        BuildQuestItem("Item_Ingrediente_02", "Hand_Severed", QuestId.Bruja, "mano cortada", Green, 0.4f);
        BuildQuestItem("Item_Ingrediente_03", "Book_Magical_Eye", QuestId.Bruja, "libro del ojo", Green, 0.45f);
        BuildQuestItem("Item_Ofrenda_Gato", "Candy_Pumpkin", QuestId.GatoNegro, "ofrenda", Purple, 0.4f);
        BuildCauldron();
        BuildCandle();
        GameObject lantern = BuildLantern();
        if (lantern != null) AttachLanternToPlayer(lantern);

        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- Ítems que se juntan

    private static void BuildQuestItem(string name, string model, QuestId quest, string itemName, Color glow, float size)
    {
        GameObject src = Load(model);
        if (src == null) return;
        GameObject root = new GameObject(name);
        try
        {
            // El modelo flota a ~0.5 m del piso (QuestItem hace girar y flotar la raíz)
            GameObject m = AddModel(src, root.transform, size);
            m.transform.localPosition += Vector3.up * 0.5f;

            AddGlow(root.transform, glow, new Vector3(0f, 0.6f, 0f), 2.5f, 3f);

            QuestItem item = root.AddComponent<QuestItem>();
            item.quest = quest;
            item.itemName = itemName;
            Save(root, $"{Folder}/{name}.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---------------------------------------------------------------- Caldero

    private static void BuildCauldron()
    {
        GameObject emptySrc = Load("Cauldron_Potion_Empty"), fullSrc = Load("Cauldron_Potion_Full");
        if (emptySrc == null || fullSrc == null) return;
        GameObject root = new GameObject("Item_Caldero");
        try
        {
            GameObject empty = AddModel(emptySrc, root.transform, 1.2f, keepColliders: true);
            GameObject full = AddModel(fullSrc, root.transform, 1.2f, keepColliders: true);
            full.SetActive(false);

            Bounds b = CalculateBounds(empty);
            ParticleSystem bubbles = CreateParticles(root.transform, "Burbujas", Green, new Vector3(0f, b.max.y - root.transform.position.y, 0f),
                rate: 14f, lifetime: 1.4f, speed: 0.6f, size: new Vector2(0.08f, 0.2f), radius: 0.35f, gravity: -0.1f);
            bubbles.gameObject.SetActive(false);
            Light l = AddLight(root.transform, Green, new Vector3(0f, b.max.y - root.transform.position.y + 0.3f, 0f), 3f, 2.5f);
            l.gameObject.SetActive(false);

            if (root.GetComponentInChildren<Collider>() == null)
            {
                CapsuleCollider c = root.AddComponent<CapsuleCollider>();
                c.radius = Mathf.Max(b.extents.x, b.extents.z);
                c.height = b.size.y;
                c.center = new Vector3(0f, b.size.y * 0.5f, 0f);
            }

            QuestStateSwap swap = root.AddComponent<QuestStateSwap>();
            swap.quest = QuestId.Bruja;
            swap.whenState = QuestState.Completada;
            swap.hide = new[] { empty };
            swap.show = new[] { full, l.gameObject };
            swap.play = new[] { bubbles };
            Save(root, $"{Folder}/Item_Caldero.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---------------------------------------------------------------- Vela del altar

    private static void BuildCandle()
    {
        GameObject litSrc = Load("Candle_Stick_A_Lit"), unlitSrc = Load("Candle_Stick_A_Unlit");
        if (litSrc == null || unlitSrc == null) return;
        GameObject root = new GameObject("Item_Vela_Altar");
        try
        {
            GameObject lit = AddModel(litSrc, root.transform, 0.6f);
            GameObject unlit = AddModel(unlitSrc, root.transform, 0.6f);
            unlit.SetActive(false);

            Bounds b = CalculateBounds(lit);
            float top = b.max.y - root.transform.position.y;
            // Si el modelo no trae luz propia se agrega una cálida sobre la llama (AltarCandle la apaga igual)
            if (lit.GetComponentInChildren<Light>(true) == null)
                AddLight(root.transform, Warm, new Vector3(0f, top + 0.1f, 0f), 4f, 1.8f);

            AltarCandle candle = root.AddComponent<AltarCandle>();
            candle.quest = QuestId.Vampiro;
            candle.litVisual = lit;
            candle.unlitVisual = unlit;
            Save(root, $"{Folder}/Item_Vela_Altar.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---------------------------------------------------------------- Farol

    private static GameObject BuildLantern()
    {
        GameObject src = Load("Lantern_Street_Movable_Halloween");
        if (src == null) return null;
        GameObject root = new GameObject("Item_Farol");
        try
        {
            GameObject m = AddModel(src, root.transform, 0.4f);
            foreach (Light own in m.GetComponentsInChildren<Light>(true)) own.enabled = false; // la maneja PlayerLantern
            Bounds b = CalculateBounds(m);
            Light l = AddLight(root.transform, Warm, new Vector3(0f, (b.center.y - root.transform.position.y), 0f), 4f, 9f);
            l.name = "Farol_Light";
            l.shadows = LightShadows.Soft;
            return Save(root, $"{Folder}/Item_Farol.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AttachLanternToPlayer(GameObject lanternPrefab)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
        if (contents == null)
        {
            Debug.LogWarning($"[Sepulturero] No encontré {PlayerPath}: agregá PlayerLantern e Item_Farol a mano.");
            return;
        }
        try
        {
            Transform old = contents.transform.Find("Item_Farol");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject farol = (GameObject)PrefabUtility.InstantiatePrefab(lanternPrefab, contents.transform);
            farol.transform.localPosition = new Vector3(0.35f, 0.9f, 0.25f);
            farol.transform.localRotation = Quaternion.identity;

            PlayerLantern pl = contents.GetComponent<PlayerLantern>();
            if (pl == null) pl = contents.AddComponent<PlayerLantern>();
            pl.lanternModel = farol;
            pl.lanternLight = farol.GetComponentInChildren<Light>(true);

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
            Debug.Log("[Sepulturero] Player.prefab: Item_Farol colgado y PlayerLantern configurado.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // ---------------------------------------------------------------- Brillo

    private static void AddGlow(Transform parent, Color color, Vector3 pos, float range, float intensity)
    {
        AddLight(parent, color, pos, intensity, range);
        CreateParticles(parent, "Brillo", color, pos, rate: 10f, lifetime: 1.2f, speed: 0.15f,
            size: new Vector2(0.06f, 0.16f), radius: 0.3f, gravity: -0.05f);
    }

    internal static Light AddLight(Transform parent, Color color, Vector3 localPos, float intensity, float range)
    {
        GameObject go = new GameObject("Glow_Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    internal static ParticleSystem CreateParticles(Transform parent, string name, Color color, Vector3 localPos,
        float rate, float lifetime, float speed, Vector2 size, float radius, float gravity)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new Color(color.r, color.g, color.b, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;

        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
        if (mat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        else Debug.LogWarning("[Sepulturero] Falta Mat_WispParticles: corré antes \"Crear prefab de tumba maldita\" o asigná un material de partículas.");
        return ps;
    }

    // ---------------------------------------------------------------- Utilidades

    private static GameObject Load(string model)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>($"{H}{model}.prefab");
        if (go == null) Debug.LogError($"[Sepulturero] No encontré el modelo {H}{model}.prefab");
        return go;
    }

    /// <summary>Instancia el modelo, lo escala a "size" (lado mayor) y lo apoya centrado sobre el origen.</summary>
    internal static GameObject AddModel(GameObject src, Transform parent, float size, bool keepColliders = false)
    {
        GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        if (!keepColliders) foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        Bounds b = CalculateBounds(m);
        float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (biggest > 0.001f) m.transform.localScale = Vector3.one * (size / biggest);
        b = CalculateBounds(m);
        Vector3 p = parent.position;
        m.transform.localPosition = new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
        return m;
    }

    internal static GameObject Save(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        if (ok) Debug.Log($"[Sepulturero] Prefab creado: {path}");
        else Debug.LogError($"[Sepulturero] No se pudo guardar {path}");
        return ok ? saved : null;
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
