
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ProceduralGraveyardGenerator : EditorWindow
{
    // ==================================================
    // CATEGORIES
    // ==================================================

    private enum Category
    {
        Architecture,
        Structures,
        Characters,
        Vegetation,
        Environment,
        CemeteryProps,
        MissionItems,
        Gameplay
    }

    private class MapObject
    {
        public GameObject root;
        public string objectName;
        public string groupName;
        public Category category;
        public Vector3 position;
    }

    // ==================================================
    // VARIABLES
    // ==================================================

    private Terrain targetTerrain;

    private Texture2D mapPreview;
    private Texture2D baseHeightmap;

    private readonly List<MapObject> detectedObjects =
        new List<MapObject>();

    private readonly Dictionary<Category, bool> layerVisibility =
        new Dictionary<Category, bool>();

    private Vector2 scrollPosition;
    private Vector2 objectListScroll;

    private bool showObjectList = true;
    private bool includeHeightmap = true;
    private bool showCoordinates = false;

    private float minHeight;
    private float maxHeight;
    private float averageHeight;

    private Bounds terrainBounds;

    // ==================================================
    // COLORS
    // ==================================================

    private static readonly Color[] categoryColors =
    {
        new Color(0.95f, 0.20f, 0.20f), // Architecture
        new Color(1.00f, 0.65f, 0.10f), // Structures
        new Color(0.15f, 0.75f, 1.00f), // Characters
        new Color(0.20f, 0.85f, 0.30f), // Vegetation
        new Color(0.75f, 0.85f, 0.35f), // Environment
        new Color(0.85f, 0.35f, 0.85f), // CemeteryProps
        new Color(1.00f, 0.90f, 0.20f), // MissionItems
        new Color(1.00f, 0.35f, 0.65f)  // Gameplay
    };

    // ==================================================
    // HIERARCHY GROUPS
    // ==================================================

    private static readonly HashSet<string> mainGroups =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WALLS",
            "PEOPLE",
            "NATURE",
            "PROPS"
        };

    private static readonly HashSet<string> subGroups =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TREES",
            "MUSHROOMS",
            "PUMPKINS",
            "STONES",
            "GRASS",
            "COFFINS",
            "LANTERNS",
            "GRAVESTONES"
        };

    // ==================================================
    // MENU
    // ==================================================

    [MenuItem("Sepulturero/Procedural/Graveyard Generator")]
    public static void OpenWindow()
    {
        GetWindow<ProceduralGraveyardGenerator>(
            "Graveyard Generator"
        );
    }

    // ==================================================
    // INITIALIZATION
    // ==================================================

    private void OnEnable()
    {
        foreach (Category category in Enum.GetValues(
                     typeof(Category)))
        {
            if (!layerVisibility.ContainsKey(category))
                layerVisibility.Add(category, true);
        }

        if (Selection.activeGameObject != null)
        {
            targetTerrain =
                Selection.activeGameObject.GetComponent<Terrain>();
        }

        if (targetTerrain == null)
        {
            targetTerrain = FindFirstObjectByType<Terrain>();
        }
    }

    private void OnDisable()
    {
        DestroyPreviewTextures();
    }

    private void DestroyPreviewTextures()
    {
        if (mapPreview != null)
        {
            DestroyImmediate(mapPreview);
            mapPreview = null;
        }

        if (baseHeightmap != null)
        {
            DestroyImmediate(baseHeightmap);
            baseHeightmap = null;
        }
    }

    // ==================================================
    // MAIN GUI
    // ==================================================

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition
        );

        DrawHeader();

        if (!ValidateTerrain())
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawTerrainInformation();

        DrawGenerationControls();

        DrawMapPreview();

        DrawLayerControls();

        DrawObjectStatistics();

        DrawObjectList();

        DrawFooter();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        GUILayout.Space(8);

        EditorGUILayout.LabelField(
            "PROCEDURAL GRAVEYARD",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Terrain analysis and hierarchical scene mapping.",
            MessageType.Info
        );
    }

    private bool ValidateTerrain()
    {
        EditorGUILayout.Space(8);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField(
            "Target Terrain",
            targetTerrain,
            typeof(Terrain),
            true
        );

        if (targetTerrain == null ||
            targetTerrain.terrainData == null)
        {
            EditorGUILayout.HelpBox(
                "Select a valid Terrain.",
                MessageType.Warning
            );

            return false;
        }

        TerrainData data = targetTerrain.terrainData;

        terrainBounds = new Bounds(
            targetTerrain.transform.position +
            data.size * 0.5f,
            data.size
        );

        return true;
    }

    // ==================================================
    // TERRAIN INFORMATION
    // ==================================================

    private void DrawTerrainInformation()
    {
        TerrainData data = targetTerrain.terrainData;

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "TERRAIN INFORMATION",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "Width",
            data.size.x.ToString("F2")
        );

        EditorGUILayout.LabelField(
            "Length",
            data.size.z.ToString("F2")
        );

        EditorGUILayout.LabelField(
            "Height",
            data.size.y.ToString("F2")
        );

        EditorGUILayout.LabelField(
            "Heightmap Resolution",
            data.heightmapResolution.ToString()
        );

        if (baseHeightmap != null)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Minimum Height",
                minHeight.ToString("F2")
            );

            EditorGUILayout.LabelField(
                "Maximum Height",
                maxHeight.ToString("F2")
            );

            EditorGUILayout.LabelField(
                "Average Height",
                averageHeight.ToString("F2")
            );
        }
    }

    // ==================================================
    // GENERATION CONTROLS
    // ==================================================

    private void DrawGenerationControls()
    {
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField(
            "MAP GENERATION",
            EditorStyles.boldLabel
        );

        if (GUILayout.Button(
            "1. GENERATE HEIGHTMAP",
            GUILayout.Height(30)))
        {
            GenerateHeightmap();
        }

        if (GUILayout.Button(
            "2. SCAN HIERARCHY GROUPS",
            GUILayout.Height(35)))
        {
            ScanHierarchy();
        }

        if (GUILayout.Button(
            "3. UPDATE OBJECT MAP",
            GUILayout.Height(35)))
        {
            UpdateObjectMap();
        }
    }

    // ==================================================
    // MAP PREVIEW
    // ==================================================

    private void DrawMapPreview()
    {
        if (mapPreview == null)
            return;

        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField(
            "GRAVEYARD MAP",
            EditorStyles.boldLabel
        );

        float width = Mathf.Min(
            position.width - 35,
            500
        );

        float height = width *
            (float)mapPreview.height /
            mapPreview.width;

        GUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            mapPreview,
            GUILayout.Width(width),
            GUILayout.Height(height)
        );

        GUILayout.FlexibleSpace();

        GUILayout.EndHorizontal();

        if (GUILayout.Button("EXPORT MAP PNG"))
        {
            ExportMap();
        }
    }

    // ==================================================
    // LAYER CONTROLS
    // ==================================================

    private void DrawLayerControls()
    {
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField(
            "MAP LAYERS",
            EditorStyles.boldLabel
        );

        includeHeightmap = EditorGUILayout.Toggle(
            "Show Heightmap",
            includeHeightmap
        );

        foreach (Category category in Enum.GetValues(
                     typeof(Category)))
        {
            layerVisibility[category] =
                EditorGUILayout.Toggle(
                    "Show " + category,
                    layerVisibility[category]
                );
        }

        if (GUILayout.Button("REFRESH MAP"))
        {
            UpdateObjectMap();
        }

        showCoordinates = EditorGUILayout.Toggle(
            "Show Coordinates in List",
            showCoordinates
        );
    }

    // ==================================================
    // STATISTICS
    // ==================================================

    private void DrawObjectStatistics()
    {
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField(
            "OBJECT STATISTICS",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "Total Instances",
            detectedObjects.Count.ToString()
        );

        foreach (Category category in Enum.GetValues(
                     typeof(Category)))
        {
            int count = detectedObjects.FindAll(
                o => o.category == category
            ).Count;

            EditorGUILayout.BeginHorizontal();

            Rect rect = GUILayoutUtility.GetRect(
                14,
                14,
                GUILayout.Width(14)
            );

            EditorGUI.DrawRect(
                rect,
                categoryColors[(int)category]
            );

            EditorGUILayout.LabelField(
                category.ToString()
            );

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                count.ToString(),
                GUILayout.Width(40)
            );

            EditorGUILayout.EndHorizontal();
        }
    }

    // ==================================================
    // OBJECT LIST
    // ==================================================

    private void DrawObjectList()
    {
        EditorGUILayout.Space(10);

        showObjectList = EditorGUILayout.Foldout(
            showObjectList,
            "DETECTED OBJECTS",
            true
        );

        if (!showObjectList)
            return;

        objectListScroll = EditorGUILayout.BeginScrollView(
            objectListScroll,
            GUILayout.MinHeight(100),
            GUILayout.MaxHeight(280)
        );

        foreach (Category category in Enum.GetValues(
                     typeof(Category)))
        {
            List<MapObject> objects =
                detectedObjects.FindAll(
                    o => o.category == category
                );

            if (objects.Count == 0)
                continue;

            EditorGUILayout.LabelField(
                category + " (" + objects.Count + ")",
                EditorStyles.boldLabel
            );

            foreach (MapObject obj in objects)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(
                    obj.objectName
                );

                if (showCoordinates)
                {
                    EditorGUILayout.LabelField(
                        "X: " + obj.position.x.ToString("F1") +
                        " Z: " + obj.position.z.ToString("F1"),
                        GUILayout.Width(125)
                    );
                }

                if (obj.root != null &&
                    GUILayout.Button(
                        "Select",
                        GUILayout.Width(50)))
                {
                    Selection.activeGameObject = obj.root;

                    EditorGUIUtility.PingObject(obj.root);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Read-only analysis. No Terrain or scene modifications.",
            MessageType.Info
        );
    }

    // ==================================================
    // NAME NORMALIZATION
    // ==================================================

    private string NormalizeName(string value)
    {
        StringBuilder result = new StringBuilder();

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(
                    char.ToUpperInvariant(c)
                );
            }
        }

        return result.ToString();
    }

    // ==================================================
    // HEIGHTMAP GENERATION
    // ==================================================

    private void GenerateHeightmap()
    {
        TerrainData data = targetTerrain.terrainData;

        int resolution = data.heightmapResolution;

        float[,] heights = data.GetHeights(
            0,
            0,
            resolution,
            resolution
        );

        if (baseHeightmap != null)
            DestroyImmediate(baseHeightmap);

        baseHeightmap = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGB24,
            false
        );

        minHeight = float.MaxValue;
        maxHeight = float.MinValue;

        float sum = 0f;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float h = heights[z, x];

                float worldHeight =
                    targetTerrain.transform.position.y +
                    h * data.size.y;

                minHeight = Mathf.Min(
                    minHeight,
                    worldHeight
                );

                maxHeight = Mathf.Max(
                    maxHeight,
                    worldHeight
                );

                sum += worldHeight;

                Color color;

                if (h < 0.30f)
                {
                    color = Color.Lerp(
                        new Color(0.12f, 0.20f, 0.14f),
                        new Color(0.38f, 0.43f, 0.27f),
                        h / 0.30f
                    );
                }
                else if (h < 0.65f)
                {
                    color = Color.Lerp(
                        new Color(0.38f, 0.43f, 0.27f),
                        new Color(0.58f, 0.40f, 0.27f),
                        (h - 0.30f) / 0.35f
                    );
                }
                else
                {
                    color = Color.Lerp(
                        new Color(0.58f, 0.40f, 0.27f),
                        new Color(0.90f, 0.85f, 0.70f),
                        (h - 0.65f) / 0.35f
                    );
                }

                baseHeightmap.SetPixel(
                    x,
                    z,
                    color
                );
            }
        }

        averageHeight =
            sum / (resolution * resolution);

        baseHeightmap.Apply();

        UpdateObjectMap();

        Repaint();

        Debug.Log(
            "[Graveyard] Heightmap generated."
        );
    }

    // ==================================================
    // HIERARCHY SCANNING
    // ==================================================

    private void ScanHierarchy()
    {
        detectedObjects.Clear();

        if (targetTerrain == null)
            return;

        GameObject[] roots =
            targetTerrain.gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            FindAndScanGroups(root.transform);
        }

        foreach (GameObject root in roots)
        {
            ScanGameplayObjects(root.transform);
        }

        Debug.Log(
            "[Graveyard] Hierarchy scan completed. " +
            "Total instances: " + detectedObjects.Count
        );

        foreach (Category category in Enum.GetValues(
                     typeof(Category)))
        {
            int count = detectedObjects.FindAll(
                o => o.category == category
            ).Count;

            Debug.Log(
                "[Graveyard] " + category + ": " + count
            );
        }

        UpdateObjectMap();

        Repaint();
    }

    // ==================================================
    // RECURSIVE GROUP SEARCH
    // ==================================================

    private void FindAndScanGroups(Transform current)
    {
        if (current == null)
            return;

        if (IsTerrainOrTerrainChild(current))
            return;

        string normalizedName =
            NormalizeName(current.name);

        if (mainGroups.Contains(normalizedName))
        {
            ScanGroupChildren(
                current,
                normalizedName,
                null
            );

            return;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            FindAndScanGroups(
                current.GetChild(i)
            );
        }
    }

    // ==================================================
    // GROUP CHILDREN
    // ==================================================

    private void ScanGroupChildren(
        Transform parent,
        string rootGroup,
        string subgroup)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (!child.gameObject.activeInHierarchy)
                continue;

            string normalizedName =
                NormalizeName(child.name);

            if (subGroups.Contains(normalizedName))
            {
                ScanGroupChildren(
                    child,
                    rootGroup,
                    normalizedName
                );
            }
            else
            {
                Category category = ClassifyGroupObject(
                    rootGroup,
                    subgroup,
                    child.name
                );

                RegisterObject(
                    child.gameObject,
                    subgroup ?? rootGroup,
                    category
                );
            }
        }
    }

    // ==================================================
    // GAMEPLAY OBJECT SEARCH
    // ==================================================

    private void ScanGameplayObjects(Transform current)
    {
        if (current == null)
            return;

        if (IsTerrainOrTerrainChild(current))
            return;

        if (!current.gameObject.activeInHierarchy)
            return;

        string normalizedName =
            NormalizeName(current.name);

        // Do not scan children of recognized groups again.
        if (mainGroups.Contains(normalizedName))
            return;

        // Player is registered once, without its children.
        if (normalizedName == "PLAYER")
        {
            RegisterObject(
                current.gameObject,
                "Gameplay",
                Category.Gameplay
            );

            return;
        }

        if (normalizedName.Contains("SPAWN") ||
            normalizedName.Contains("CHECKPOINT") ||
            normalizedName.Contains("EXITPOINT") ||
            normalizedName.Contains("ENEMY") ||
            normalizedName.Contains("MISSION"))
        {
            RegisterObject(
                current.gameObject,
                "Gameplay",
                Category.Gameplay
            );

            return;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            ScanGameplayObjects(
                current.GetChild(i)
            );
        }
    }

    // ==================================================
    // TERRAIN EXCLUSION
    // ==================================================

    private bool IsTerrainOrTerrainChild(Transform current)
    {
        Transform terrainTransform =
            targetTerrain.transform;

        while (current != null)
        {
            if (current == terrainTransform)
                return true;

            current = current.parent;
        }

        return false;
    }

    // ==================================================
    // CLASSIFICATION
    // ==================================================

    private Category ClassifyGroupObject(
        string rootGroup,
        string subgroup,
        string objectName)
    {
        string root = NormalizeName(rootGroup);

        string group = NormalizeName(
            subgroup ?? ""
        );

        string name = NormalizeName(objectName);

        // WALLS
        if (root == "WALLS")
            return Category.Architecture;

        // PEOPLE
        if (root == "PEOPLE")
            return Category.Characters;

        // NATURE
        if (root == "NATURE")
        {
            if (group == "TREES" ||
                group == "GRASS" ||
                group == "MUSHROOMS")
            {
                return Category.Vegetation;
            }

            return Category.Environment;
        }

        // PROPS
        if (root == "PROPS")
        {
            if (group == "COFFINS" ||
                name.Contains("GIBBET") ||
                name.Contains("GUILLOTINE"))
            {
                return Category.Structures;
            }

            if (group == "LANTERNS" ||
                group == "GRAVESTONES")
            {
                return Category.CemeteryProps;
            }

            if (name.Contains("BOOK") ||
                name.Contains("POTION") ||
                name.Contains("SCYTHE") ||
                name.Contains("SHOVEL") ||
                name.Contains("WITCHHAT"))
            {
                return Category.MissionItems;
            }

            if (name.Contains("SCARECROW") ||
                name.Contains("PUMPKIN") ||
                name.Contains("STONE") ||
                name.Contains("ROCK"))
            {
                return Category.Environment;
            }

            return Category.CemeteryProps;
        }

        return Category.Environment;
    }

    // ==================================================
    // REGISTER OBJECT
    // ==================================================

    private void RegisterObject(
        GameObject root,
        string groupName,
        Category category)
    {
        if (root == null)
            return;

        Bounds bounds;

        bool hasBounds = TryGetCombinedBounds(
            root,
            out bounds
        );

        if (!hasBounds)
        {
            bounds = new Bounds(
                root.transform.position,
                Vector3.zero
            );
        }

        Vector3 position = bounds.center;

        if (!terrainBounds.Intersects(bounds))
            return;

        if (!terrainBounds.Contains(position))
        {
            position.x = Mathf.Clamp(
                position.x,
                terrainBounds.min.x,
                terrainBounds.max.x
            );

            position.z = Mathf.Clamp(
                position.z,
                terrainBounds.min.z,
                terrainBounds.max.z
            );
        }

        // Avoid duplicate registration.
        foreach (MapObject existing in detectedObjects)
        {
            if (existing.root == root)
                return;
        }

        detectedObjects.Add(new MapObject
        {
            root = root,
            objectName = root.name,
            groupName = groupName,
            category = category,
            position = position
        });
    }

    // ==================================================
    // COMBINED BOUNDS
    // ==================================================

    private bool TryGetCombinedBounds(
        GameObject root,
        out Bounds bounds)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);

        bool initialized = false;

        bounds = new Bounds(
            root.transform.position,
            Vector3.zero
        );

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!initialized)
        {
            foreach (Collider collider in colliders)
            {
                if (!collider.enabled)
                    continue;

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        return initialized;
    }

    // ==================================================
    // MAP GENERATION
    // ==================================================

    private void UpdateObjectMap()
    {
        if (includeHeightmap && baseHeightmap == null)
        {
            Debug.LogWarning(
                "[Graveyard] Generate the heightmap first."
            );

            return;
        }

        if (mapPreview != null)
        {
            DestroyImmediate(mapPreview);
            mapPreview = null;
        }

        if (baseHeightmap != null)
        {
            mapPreview = new Texture2D(
                baseHeightmap.width,
                baseHeightmap.height,
                TextureFormat.RGB24,
                false
            );

            if (includeHeightmap)
            {
                mapPreview.SetPixels(
                    baseHeightmap.GetPixels()
                );
            }
            else
            {
                Color[] emptyPixels = new Color[
                    baseHeightmap.width *
                    baseHeightmap.height
                ];

                for (int i = 0; i < emptyPixels.Length; i++)
                    emptyPixels[i] = Color.white;

                mapPreview.SetPixels(emptyPixels);
            }
        }
        else
        {
            return;
        }

        foreach (MapObject obj in detectedObjects)
        {
            if (!layerVisibility[obj.category])
                continue;

            Vector3 terrainPosition =
                targetTerrain.transform.position;

            Vector3 terrainSize =
                targetTerrain.terrainData.size;

            float normalizedX =
                (obj.position.x - terrainPosition.x) /
                terrainSize.x;

            float normalizedZ =
                (obj.position.z - terrainPosition.z) /
                terrainSize.z;

            int x = Mathf.RoundToInt(
                normalizedX * (mapPreview.width - 1)
            );

            int z = Mathf.RoundToInt(
                normalizedZ * (mapPreview.height - 1)
            );

            DrawMarker(
                x,
                z,
                categoryColors[(int)obj.category],
                3
            );
        }

        mapPreview.Apply();

        Repaint();
    }

    // ==================================================
    // DRAW MARKERS
    // ==================================================

    private void DrawMarker(
        int centerX,
        int centerZ,
        Color color,
        int radius)
    {
        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + z * z > radius * radius)
                    continue;

                int px = centerX + x;
                int pz = centerZ + z;

                if (px < 0 || px >= mapPreview.width ||
                    pz < 0 || pz >= mapPreview.height)
                    continue;

                mapPreview.SetPixel(
                    px,
                    pz,
                    color
                );
            }
        }
    }

    // ==================================================
    // EXPORT MAP
    // ==================================================

    private void ExportMap()
    {
        if (mapPreview == null)
            return;

        string path = EditorUtility.SaveFilePanel(
            "Export Graveyard Map",
            Application.dataPath,
            "Graveyard_Design_Map",
            "png"
        );

        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllBytes(
            path,
            mapPreview.EncodeToPNG()
        );

        Debug.Log(
            "[Graveyard] Map exported: " + path
        );

        EditorUtility.DisplayDialog(
            "Export completed",
            "Map exported successfully.",
            "OK"
        );
    }
}
