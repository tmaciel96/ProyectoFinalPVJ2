
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;

public class GraveyardSectorGenerator : EditorWindow
{
    [Serializable]
    public class PrefabGroup
    {
        public string name = "New Group";
        public List<GameObject> prefabs = new List<GameObject>();
        public int amount = 10;
        public float minDistance = 2f;
        public bool randomRotation = true;
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    }

    private Terrain targetTerrain;
    private Transform boundsMin;
    private Transform boundsMax;
    private Transform generatedParent;

    private List<PrefabGroup> groups = new List<PrefabGroup>();

    private int seed = 12345;
    private bool randomizeSeed = true;
    private Vector2 scroll;

    [MenuItem("Sepulturero/Procedural/Sector Generator")]
    public static void OpenWindow()
    {
        GetWindow<GraveyardSectorGenerator>(
            "Sector Generator"
        );
    }

    private void OnEnable()
    {
        if (groups.Count == 0)
        {
            groups.Add(new PrefabGroup
            {
                name = "Gravestones",
                amount = 20,
                minDistance = 2f
            });

            groups.Add(new PrefabGroup
            {
                name = "Coffins",
                amount = 5,
                minDistance = 3f
            });

            groups.Add(new PrefabGroup
            {
                name = "Trees",
                amount = 6,
                minDistance = 5f
            });

            groups.Add(new PrefabGroup
            {
                name = "Rocks",
                amount = 12,
                minDistance = 2f
            });

            groups.Add(new PrefabGroup
            {
                name = "Lanterns",
                amount = 6,
                minDistance = 4f
            });
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            "GRAVEYARD SECTOR GENERATOR",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Generates prefab instances inside a defined Terrain area.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField(
            "Target Terrain",
            targetTerrain,
            typeof(Terrain),
            true
        );

        boundsMin = (Transform)EditorGUILayout.ObjectField(
            "Bounds Min",
            boundsMin,
            typeof(Transform),
            true
        );

        boundsMax = (Transform)EditorGUILayout.ObjectField(
            "Bounds Max",
            boundsMax,
            typeof(Transform),
            true
        );

        generatedParent = (Transform)EditorGUILayout.ObjectField(
            "Generated Parent",
            generatedParent,
            typeof(Transform),
            true
        );

        EditorGUILayout.Space(10);

        randomizeSeed = EditorGUILayout.Toggle(
            "Randomize Seed",
            randomizeSeed
        );

        if (!randomizeSeed)
        {
            seed = EditorGUILayout.IntField("Seed", seed);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(
            "PREFAB GROUPS",
            EditorStyles.boldLabel
        );

        for (int i = 0; i < groups.Count; i++)
        {
            DrawGroup(groups[i], i);
        }

        if (GUILayout.Button("ADD PREFAB GROUP"))
        {
            groups.Add(new PrefabGroup());
        }

        EditorGUILayout.Space(15);

        GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);

        if (GUILayout.Button(
            "GENERATE SECTOR",
            GUILayout.Height(35)))
        {
            GenerateSector();
        }

        GUI.backgroundColor = Color.white;

        if (GUILayout.Button(
            "CLEAR GENERATED OBJECTS",
            GUILayout.Height(30)))
        {
            ClearGenerated();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawGroup(PrefabGroup group, int index)
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        EditorGUILayout.BeginHorizontal();

        group.name = EditorGUILayout.TextField(
            group.name,
            EditorStyles.boldLabel
        );

        if (GUILayout.Button("Remove", GUILayout.Width(65)))
        {
            groups.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        group.amount = Mathf.Max(
            0,
            EditorGUILayout.IntField(
                "Amount",
                group.amount
            )
        );

        group.minDistance = Mathf.Max(
            0,
            EditorGUILayout.FloatField(
                "Minimum Distance",
                group.minDistance
            )
        );

        group.randomRotation = EditorGUILayout.Toggle(
            "Random Rotation",
            group.randomRotation
        );

        group.scaleRange = EditorGUILayout.Vector2Field(
            "Scale Range",
            group.scaleRange
        );

        EditorGUILayout.LabelField(
            "Prefabs",
            EditorStyles.boldLabel
        );

        int newSize = Mathf.Max(
            0,
            EditorGUILayout.IntField(
                "Prefab Slots",
                group.prefabs.Count
            )
        );

        while (group.prefabs.Count < newSize)
            group.prefabs.Add(null);

        while (group.prefabs.Count > newSize)
            group.prefabs.RemoveAt(group.prefabs.Count - 1);

        for (int i = 0; i < group.prefabs.Count; i++)
        {
            group.prefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                "Prefab " + (i + 1),
                group.prefabs[i],
                typeof(GameObject),
                false
            );
        }

        EditorGUILayout.EndVertical();
    }

    private bool ValidateSetup()
    {
        if (targetTerrain == null ||
            boundsMin == null ||
            boundsMax == null ||
            generatedParent == null)
        {
            EditorUtility.DisplayDialog(
                "Missing references",
                "Assign Terrain, Bounds Min, Bounds Max and Generated Parent.",
                "OK"
            );

            return false;
        }

        if (generatedParent == boundsMin ||
            generatedParent == boundsMax ||
            generatedParent.IsChildOf(boundsMin) ||
            generatedParent.IsChildOf(boundsMax))
        {
            EditorUtility.DisplayDialog(
                "Invalid hierarchy",
                "Generated Parent cannot contain the bounds objects.",
                "OK"
            );

            return false;
        }

        TerrainData data = targetTerrain.terrainData;

        Vector3 terrainPosition = targetTerrain.transform.position;
        Vector3 terrainSize = data.size;

        float minX = Mathf.Min(boundsMin.position.x, boundsMax.position.x);
        float maxX = Mathf.Max(boundsMin.position.x, boundsMax.position.x);
        float minZ = Mathf.Min(boundsMin.position.z, boundsMax.position.z);
        float maxZ = Mathf.Max(boundsMin.position.z, boundsMax.position.z);

        if (minX < terrainPosition.x ||
            maxX > terrainPosition.x + terrainSize.x ||
            minZ < terrainPosition.z ||
            maxZ > terrainPosition.z + terrainSize.z)
        {
            EditorUtility.DisplayDialog(
                "Bounds outside Terrain",
                "The selected area extends outside the target Terrain.",
                "OK"
            );

            return false;
        }

        return true;
    }

    private void GenerateSector()
    {
        if (!ValidateSetup())
            return;

        int currentSeed = randomizeSeed
            ? UnityEngine.Random.Range(0, int.MaxValue)
            : seed;

        System.Random random = new System.Random(currentSeed);

        Undo.RegisterFullObjectHierarchyUndo(
            generatedParent.gameObject,
            "Generate Graveyard Sector"
        );

        List<Vector3> occupiedPositions = new List<Vector3>();

        int generatedCount = 0;

        foreach (PrefabGroup group in groups)
        {
            List<GameObject> validPrefabs = group.prefabs.FindAll(
                prefab => prefab != null
            );

            if (validPrefabs.Count == 0 || group.amount == 0)
                continue;

            GameObject groupObject = new GameObject(
                group.name + "_Generated"
            );

            Undo.RegisterCreatedObjectUndo(
                groupObject,
                "Create Generated Group"
            );

            groupObject.transform.SetParent(
                generatedParent,
                false
            );

            int placed = 0;
            int attempts = 0;
            int maxAttempts = group.amount * 40;

            while (placed < group.amount &&
                   attempts < maxAttempts)
            {
                attempts++;

                float x = Mathf.Lerp(
                    Mathf.Min(boundsMin.position.x, boundsMax.position.x),
                    Mathf.Max(boundsMin.position.x, boundsMax.position.x),
                    (float)random.NextDouble()
                );

                float z = Mathf.Lerp(
                    Mathf.Min(boundsMin.position.z, boundsMax.position.z),
                    Mathf.Max(boundsMin.position.z, boundsMax.position.z),
                    (float)random.NextDouble()
                );

                Vector3 position = new Vector3(x, 0, z);

                bool tooClose = false;

                foreach (Vector3 occupied in occupiedPositions)
                {
                    if (Vector2.Distance(
                        new Vector2(x, z),
                        new Vector2(occupied.x, occupied.z)
                    ) < group.minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                float terrainHeight =
                    targetTerrain.SampleHeight(position) +
                    targetTerrain.transform.position.y;

                position.y = terrainHeight;

                GameObject prefab = validPrefabs[
                    random.Next(validPrefabs.Count)
                ];

                GameObject instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(
                        prefab,
                        groupObject.transform
                    );

                if (instance == null)
                    continue;

                Undo.RegisterCreatedObjectUndo(
                    instance,
                    "Generate Graveyard Object"
                );

                instance.transform.position = position;

                if (group.randomRotation)
                {
                    instance.transform.rotation =
                        Quaternion.Euler(
                            0,
                            (float)random.NextDouble() * 360f,
                            0
                        );
                }

                float scale = Mathf.Lerp(
                    Mathf.Min(group.scaleRange.x, group.scaleRange.y),
                    Mathf.Max(group.scaleRange.x, group.scaleRange.y),
                    (float)random.NextDouble()
                );

                instance.transform.localScale *= scale;

                occupiedPositions.Add(position);

                placed++;
                generatedCount++;
            }

            Debug.Log(
                "[Graveyard] " + group.name +
                ": " + placed + "/" + group.amount +
                " objects generated."
            );
        }

        EditorSceneManager.MarkSceneDirty(
            targetTerrain.gameObject.scene
        );

        Debug.Log(
            "[Graveyard] Generation completed. Seed: " +
            currentSeed + ". Total: " + generatedCount
        );

        EditorUtility.DisplayDialog(
            "Generation completed",
            generatedCount + " objects generated.",
            "OK"
        );
    }

    private void ClearGenerated()
    {
        if (generatedParent == null)
        {
            EditorUtility.DisplayDialog(
                "Missing parent",
                "Assign the Generated Parent first.",
                "OK"
            );

            return;
        }

        if (!EditorUtility.DisplayDialog(
            "Clear generated objects",
            "Only the children of the assigned Generated Parent will be deleted. Continue?",
            "Delete",
            "Cancel"))
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(
            generatedParent.gameObject,
            "Clear Generated Objects"
        );

        for (int i = generatedParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(
                generatedParent.GetChild(i).gameObject
            );
        }

        EditorSceneManager.MarkSceneDirty(
            generatedParent.gameObject.scene
        );

        Debug.Log("[Graveyard] Generated objects cleared.");
    }
}
