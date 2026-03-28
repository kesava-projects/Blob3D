using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class DarkChamberSetupUtility
{
    [MenuItem("Blob3D/Scene/Setup Dark Chamber")]
    public static void SetupDarkChamber()
    {
        Debug.Log("[DarkChamber] Starting scene setup...");

        // 1. Configure ambient lighting
        ConfigureLighting();

        // 2. Create chamber builder
        var chamberGO = new GameObject("DarkChamber");
        var builder = chamberGO.AddComponent<DarkChamberBuilder>();
        EditorUtility.SetDirty(builder);

        // 3. Create physics tuner
        var physicsTunerGO = new GameObject("PhysicsTuner");
        var tuner = physicsTunerGO.AddComponent<BlobPhysicsTuner>();
        EditorUtility.SetDirty(tuner);

        // 4. Create merge system
        var mergeGO = new GameObject("BlobMergeSystem");
        var mergeSystem = mergeGO.AddComponent<BlobMergeSystem>();
        EditorUtility.SetDirty(mergeSystem);

        // 5. Setup camera
        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 8f, -12f);
            camera.transform.LookAt(new Vector3(0f, 2f, 0f));
            camera.fieldOfView = 55f;
            EditorUtility.SetDirty(camera.transform);
        }

        // 6. Mark scene dirty
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[DarkChamber] Setup complete! Press Play to initialize the chamber.");
        Debug.LogWarning("[DarkChamber] Note: Make sure BlobManager with blobOne and blobTwo are in the scene.");
    }

    static void ConfigureLighting()
    {
        RenderSettings.ambientLight = new Color(0.1f, 0.12f, 0.15f) * 0.3f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = false;

        var existingLight = Object.FindAnyObjectByType<Light>();
        if (existingLight != null)
            Object.DestroyImmediate(existingLight.gameObject);

        Debug.Log("[DarkChamber] Lighting configured.");
    }

    [MenuItem("Blob3D/Scene/Setup Dark Chamber", true)]
    private static bool ValidateSetupDarkChamber()
    {
        return !EditorApplication.isPlaying;
    }

    [MenuItem("Blob3D/Scene/Create Dark Materials")]
    public static void CreateDarkMaterials()
    {
        var path = "Assets/Materials/";

        // Dark Floor Material
        CreateMaterial(path + "DarkFloor.mat", new Color(0.08f, 0.08f, 0.08f), 0.3f, 0f);

        // Dark Wall Material
        CreateMaterial(path + "DarkWall.mat", new Color(0.12f, 0.12f, 0.12f), 0.2f, 0.1f);

        // Dark Ceiling Material
        CreateMaterial(path + "DarkCeiling.mat", new Color(0.06f, 0.06f, 0.08f), 0.1f, 0f);

        Debug.Log("[DarkChamber] Dark materials created in Assets/Materials/");
    }

    static void CreateMaterial(string path, Color baseColor, float smoothness, float metallic)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = baseColor;
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metallic);

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
    }
}
