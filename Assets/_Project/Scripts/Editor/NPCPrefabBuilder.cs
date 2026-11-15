// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Menú Sepulturero/Crear prefabs de NPCs. Arma en Prefabs/NPCs un prefab por sector, listo para
// arrastrar a la escena:
//  1. NPC_Bruja            Woman_Witch_Halloween                     + QuestCollection (Ingredientes)
//  2. NPC_ZombiSepulturero Man_Killer_Halloween con la pala clavada al lado + AreaClearQuest (Zombis)
//  3. NPC_Vampiro          el zombi de traje del enemigo, con ojos rojos y bruma roja + CandleQuest
//  4. NPC_NinoFantasma     Boy_Ghost_Halloween                       + AreaClearQuest  (Espectros)
//  5. NPC_Espantapajaros   Scarecrow_A                               + QuestCollection (Ofrendas)
// Cada uno trae NPCInteractable con su misión, su nombre y sus 5 diálogos, un collider para no
// atravesarlo, la animación de reposo (los humanoides) y los indicadores "!" (misión disponible) y "?"
// (listo para entregar). Lo que depende de la escena se completa después de colocarlo: en
// AreaClearQuest los spawners de "Activate On Quest Start" y el "Area Center"; en las misiones, el
// "Progress Text".
// Si los prefabs ya existen se sobrescriben conservando el GUID. Los agregados (Pala, Ojos_Vampiro,
// Bruma_Vampiro, Luz_Vampiro y los marcadores) conservan la posición que se les haya dado a mano en el
// prefab; para volver a la ubicación automática hay que borrar el prefab antes de regenerarlo.
// Los IDs internos no cambian: el Zombi Sepulturero usa QuestId.Esqueleto y Dialogo_Esqueleto_*, y el
// Espantapájaros usa QuestId.GatoNegro y Dialogo_Gato_*.
// =================================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NPCPrefabBuilder
{
    private const string Folder = "Assets/_Project/Prefabs/NPCs";
    private const string DialogFolder = "Assets/_Project/Data/Dialogos";
    private const string ControllerFolder = "Assets/_Project/Animation/Controllers";
    private const string People = "Assets/ThirdParty/Poly Halloween/Prefabs/People/";
    private const string Halloween = "Assets/ThirdParty/Poly Halloween/Prefabs/Halloween/";
    private const string ZombieSuit = "Assets/ThirdParty/Zombies/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
    private const string HumanIdleFbx = "Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx";

    private static readonly Color MarkerAvailable = new Color(1f, 0.85f, 0.2f);
    private static readonly Color MarkerReady = new Color(0.45f, 1f, 0.4f);
    private static readonly Color VampireRed = new Color(0.9f, 0.08f, 0.08f);

    /// <summary>Objetos agregados cuya posición hecha a mano en el prefab se respeta al regenerar.</summary>
    private static readonly HashSet<string> KeepPose = new HashSet<string>
        { "Pala", "Ojos_Vampiro", "Bruma_Vampiro", "Luz_Vampiro", "Marcador_Disponible", "Marcador_Entrega" };

    [MenuItem("Sepulturero/Crear prefabs de NPCs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "NPCs");
        RuntimeAnimatorController idle = IdleController("NPC_Idle", HumanIdleFbx, "Idle");

        BuildNPC("NPC_Bruja", Load(People + "Woman_Witch_Halloween.prefab"), idle,
            QuestId.Bruja, "Bruja", "Bruja", root =>
            {
                QuestCollection c = root.AddComponent<QuestCollection>();
                c.quest = QuestId.Bruja;
                c.required = 0;
                c.itemLabel = "Ingredientes";
            });

        BuildNPC("NPC_ZombiSepulturero", Load(People + "Man_Killer_Halloween.prefab"), idle,
            QuestId.Esqueleto, "Zombi Sepulturero", "Esqueleto", root =>
            {
                AreaClearQuest a = root.AddComponent<AreaClearQuest>();
                a.quest = QuestId.Esqueleto;
                a.requiredKills = 0;
                a.enemyLabel = "Zombis";
            },
            (root, model) => AddShovel(root));

        BuildNPC("NPC_Vampiro", Load(ZombieSuit), idle,
            QuestId.Vampiro, "Vampiro", "Vampiro", root =>
            {
                CandleQuest c = root.AddComponent<CandleQuest>();
                c.quest = QuestId.Vampiro;
                c.required = 0;
            },
            DecorateVampire);

        BuildNPC("NPC_NinoFantasma", Load(People + "Boy_Ghost_Halloween.prefab"), idle,
            QuestId.NinoFantasma, "Niño Fantasma", "NinoFantasma", root =>
            {
                AreaClearQuest a = root.AddComponent<AreaClearQuest>();
                a.quest = QuestId.NinoFantasma;
                a.requiredKills = 0;
                a.enemyLabel = "Espectros";
            });

        BuildNPC("NPC_Espantapajaros", Load(Halloween + "Scarecrow_A.prefab"), null,
            QuestId.GatoNegro, "Espantapájaros", "Gato", root =>
            {
                QuestCollection c = root.AddComponent<QuestCollection>();
                c.quest = QuestId.GatoNegro;
                c.required = 0;
                c.itemLabel = "Ofrendas";
            });

        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- NPC

    private static void BuildNPC(string name, GameObject modelSrc, RuntimeAnimatorController idle, QuestId quest,
        string npcName, string dialogKey, Action<GameObject> addQuestComponent, Action<GameObject, GameObject> decorate = null)
    {
        if (modelSrc == null) return; // Load ya avisó en la consola
        string path = $"{Folder}/{name}.prefab";
        Dictionary<string, Pose3> kept = ReadPoses(path);

        GameObject root = new GameObject(name);
        try
        {
            GameObject model = AddModel(modelSrc, root.transform, idle);
            model.name = "Modelo";
            Bounds b = CalculateBounds(model);

            // Un solo collider en la raíz para que el sepulturero no lo atraviese (la charla es por distancia)
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.2f, 0.5f);
            col.height = Mathf.Max(b.size.y, col.radius * 2f);
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            NPCInteractable npc = root.AddComponent<NPCInteractable>();
            npc.quest = quest;
            npc.npcName = npcName;
            npc.lockedDialogue = Dialog(dialogKey, "Bloqueado");
            npc.offerDialogue = Dialog(dialogKey, "Pedido");
            npc.reminderDialogue = Dialog(dialogKey, "Recordatorio");
            npc.deliveryDialogue = Dialog(dialogKey, "Entrega");
            npc.thanksDialogue = Dialog(dialogKey, "Gracias");

            float top = b.max.y + 0.45f;
            npc.questAvailableMarker = CreateMarker(root.transform, "Marcador_Disponible", "!", MarkerAvailable, top);
            npc.readyToDeliverMarker = CreateMarker(root.transform, "Marcador_Entrega", "?", MarkerReady, top);
            npc.readyToDeliverMarker.SetActive(false);

            addQuestComponent(root);
            decorate?.Invoke(root, model);
            ApplyPoses(root, kept);
            QuestItemPrefabBuilder.Save(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>Instancia el modelo con su escala original, apoyado en el origen, sin colliders propios.</summary>
    private static GameObject AddModel(GameObject src, Transform parent, RuntimeAnimatorController idle)
    {
        GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        Bounds b = CalculateBounds(m);
        m.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

        Animator anim = m.GetComponentInChildren<Animator>(true);
        if (anim != null && idle != null)
        {
            anim.runtimeAnimatorController = idle;
            anim.applyRootMotion = false;
        }
        return m;
    }

    // ---------------------------------------------------------------- Zombi Sepulturero: la pala

    private static void AddShovel(GameObject root)
    {
        GameObject src = Load(Halloween + "Shovel_Gravedigger.prefab");
        if (src == null) return;
        GameObject shovel = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        shovel.name = "Pala";
        foreach (Collider c in shovel.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // Parada con la hoja para abajo, un poco inclinada y clavada en la tierra a la derecha del zombi
        StandUpright(shovel, wideEndDown: true);
        shovel.transform.rotation = Quaternion.Euler(0f, 0f, -10f) * shovel.transform.rotation;
        Bounds b = CalculateBounds(shovel);
        shovel.transform.position += new Vector3(0.55f - b.center.x, -0.15f - b.min.y, 0.15f - b.center.z);
    }

    // ---------------------------------------------------------------- Vampiro: ojos rojos y bruma

    private static void DecorateVampire(GameObject root, GameObject model)
    {
        Bounds b = CalculateBounds(model);
        Animator anim = model.GetComponentInChildren<Animator>(true);
        Transform head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;

        // Altura de los ojos: un poco por debajo de la coronilla; adelante, sobre la superficie de la cara
        float eyeY = b.max.y - b.size.y * 0.065f;
        float faceZ = FaceSurfaceZ(model, eyeY, 0.06f) ?? (head != null ? head.position.z + 0.1f : b.max.z);

        Material eyeMat = GetOrCreateMaterial("Mat_Vampiro_Ojos", VampireRed, VampireRed * 4f);
        GameObject eyes = new GameObject("Ojos_Vampiro");
        eyes.transform.SetParent(head != null ? head : root.transform, true);
        eyes.transform.position = new Vector3(0f, eyeY, faceZ + 0.005f);
        eyes.transform.rotation = root.transform.rotation;
        foreach (float x in new[] { -0.035f, 0.035f })
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = x < 0 ? "Ojo_I" : "Ojo_D";
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            // Se ubica en coordenadas del mundo y después se cuelga del hueso (que puede tener otra escala)
            eye.transform.position = eyes.transform.position + root.transform.right * x;
            eye.transform.localScale = Vector3.one * 0.028f;
            eye.transform.SetParent(eyes.transform, true);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
        }

        ParticleSystem mist = QuestItemPrefabBuilder.CreateParticles(root.transform, "Bruma_Vampiro", VampireRed, new Vector3(0f, 0.15f, 0f),
            rate: 8f, lifetime: 2.5f, speed: 0.1f, size: new Vector2(0.3f, 0.6f), radius: 0.6f, gravity: -0.01f);
        var shape = mist.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);

        Light l = QuestItemPrefabBuilder.AddLight(root.transform, VampireRed, new Vector3(0f, eyeY, 0.6f), 1.5f, 3f);
        l.name = "Luz_Vampiro";
    }

    /// <summary>Z del frente de la cara a la altura "y" (vértices de la malla en pose de reposo), o null.</summary>
    private static float? FaceSurfaceZ(GameObject model, float y, float halfWidth)
    {
        float? best = null;
        foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh baked = new Mesh();
            try
            {
                smr.BakeMesh(baked, true);
                Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (Mathf.Abs(w.y - y) > 0.03f || Mathf.Abs(w.x) > halfWidth) continue;
                    if (best == null || w.z > best.Value) best = w.z;
                }
            }
            finally { Object.DestroyImmediate(baked); }
        }
        return best;
    }

    // ---------------------------------------------------------------- Indicadores "!" y "?"

    private static GameObject CreateMarker(Transform parent, string name, string symbol, Color color, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = symbol;
        tmp.fontSize = 6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);

        go.AddComponent<Billboard>();
        return go;
    }

    // ---------------------------------------------------------------- Posiciones hechas a mano

    private struct Pose3 { public Vector3 pos; public Quaternion rot; public Vector3 scale; }

    private static Dictionary<string, Pose3> ReadPoses(string path)
    {
        var poses = new Dictionary<string, Pose3>();
        GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (old == null) return poses;
        foreach (Transform t in old.GetComponentsInChildren<Transform>(true))
            if (KeepPose.Contains(t.name) && !poses.ContainsKey(t.name))
                poses[t.name] = new Pose3 { pos = t.localPosition, rot = t.localRotation, scale = t.localScale };
        return poses;
    }

    private static void ApplyPoses(GameObject root, Dictionary<string, Pose3> poses)
    {
        if (poses.Count == 0) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!poses.TryGetValue(t.name, out Pose3 p)) continue;
            t.localPosition = p.pos;
            t.localRotation = p.rot;
            t.localScale = p.scale;
        }
    }

    // ---------------------------------------------------------------- Utilidades

    /// <summary>Gira el objeto para que su lado más largo quede vertical, con el extremo más ancho abajo o arriba.</summary>
    private static void StandUpright(GameObject go, bool wideEndDown)
    {
        List<Vector3> pts = WorldVertices(go);
        if (pts.Count == 0) return;

        Bounds b = new Bounds(pts[0], Vector3.zero);
        foreach (Vector3 p in pts) b.Encapsulate(p);
        int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
        Vector3 dir = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        // ¿Qué mitad es más ancha? (la hoja de la pala)
        float c = b.center[axis];
        float spreadPos = Spread(pts, axis, p => p[axis] > c), spreadNeg = Spread(pts, axis, p => p[axis] <= c);
        if ((spreadPos > spreadNeg) == wideEndDown) dir = -dir; // el extremo que va arriba

        go.transform.rotation = Quaternion.FromToRotation(dir, Vector3.up) * go.transform.rotation;
    }

    private static float Spread(List<Vector3> pts, int axis, Func<Vector3, bool> half)
    {
        int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
        float min1 = float.MaxValue, max1 = float.MinValue, min2 = float.MaxValue, max2 = float.MinValue;
        foreach (Vector3 p in pts.Where(half))
        {
            min1 = Mathf.Min(min1, p[a1]); max1 = Mathf.Max(max1, p[a1]);
            min2 = Mathf.Min(min2, p[a2]); max2 = Mathf.Max(max2, p[a2]);
        }
        return max1 < min1 ? 0f : (max1 - min1) * (max2 - min2);
    }

    private static List<Vector3> WorldVertices(GameObject go)
    {
        var pts = new List<Vector3>();
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            foreach (Vector3 v in mf.sharedMesh.vertices) pts.Add(mf.transform.TransformPoint(v));
        }
        return pts;
    }

    private static Material GetOrCreateMaterial(string name, Color color, Color emission)
    {
        string path = $"{Folder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static RuntimeAnimatorController IdleController(string name, string fbxPath, string clipName)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .FirstOrDefault(c => clipName == null || c.name == clipName);
        if (clip == null)
        {
            Debug.LogWarning($"[Sepulturero] No encontré la animación de reposo en {fbxPath}: los NPCs quedan sin animar.");
            return null;
        }

        string path = $"{ControllerFolder}/{name}.controller";
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);

        // Ya existe: se apunta el estado por defecto a la animación (por si cambió)
        AnimatorState state = ctrl.layers[0].stateMachine.defaultState;
        if (state != null && state.motion != clip)
        {
            state.motion = clip;
            EditorUtility.SetDirty(ctrl);
        }
        return ctrl;
    }

    private static DialogueData Dialog(string key, string state)
    {
        string path = $"{DialogFolder}/Dialogo_{key}_{state}.asset";
        DialogueData d = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (d == null) Debug.LogWarning($"[Sepulturero] No encontré {path}: el NPC usará el texto de respaldo.");
        return d;
    }

    private static GameObject Load(string path)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogError($"[Sepulturero] No encontré el modelo {path}");
        return go;
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
