using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba NO destructiva: guarda una COPIA de la escena activa en YAML (texto) como "<nombre>_YAML.unity".
/// Antes mueve el TerrainData incrustado en la escena a un .asset aparte (Assets/_Project/Terrain).
/// Uso: abrir la escena y ejecutar Tools > Scene -> YAML (copia de prueba). Despues se puede borrar este script.
/// </summary>
public static class SaveSceneAsYamlTest
{
    [MenuItem("Tools/Scene -> YAML (copia de prueba)")]
    public static void Run()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[YAMLTest] Abri y selecciona una escena guardada antes de ejecutar.");
            return;
        }

        const string terrainDir = "Assets/_Project/Terrain";
        if (!AssetDatabase.IsValidFolder(terrainDir))
            AssetDatabase.CreateFolder("Assets/_Project", "Terrain");

        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            TerrainData td = terrain.terrainData;
            if (td == null || AssetDatabase.Contains(td)) continue;

            TerrainData copy = Object.Instantiate(td);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{terrainDir}/{scene.name}_{terrain.name}_TerrainData.asset");
            AssetDatabase.CreateAsset(copy, path);
            terrain.terrainData = copy;

            TerrainCollider col = terrain.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = copy;

            EditorUtility.SetDirty(terrain);
            Debug.Log($"[YAMLTest] TerrainData de '{terrain.name}' guardado en {path}");
        }
        AssetDatabase.SaveAssets();

        string outPath = scene.path.Replace(".unity", "_YAML.unity");
        bool ok = EditorSceneManager.SaveScene(scene, outPath, true); // saveAsCopy: no toca la escena original
        AssetDatabase.Refresh();

        string first = ok && File.Exists(outPath) ? File.ReadLines(outPath).FirstOrDefault() ?? "" : "";
        Debug.Log($"[YAMLTest] Copia: {outPath} (ok={ok}). Primera linea: '{first}'. " +
                  (first.StartsWith("%YAML") ? "ES YAML." : "SIGUE EN BINARIO."));
    }
}
