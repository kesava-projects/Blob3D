using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only tool.
/// Menu: Blob3D ▶ Build Level 01  (shortcut: Ctrl/Cmd+Shift+B)
///
/// Uses the Free LowPoly SciFi Pack’s Free_RoomA prefab scaled 2×.
/// Two-level room: upper y≈0, lower y≈-2, ceiling y≈6.
/// Room bounds: x ∈ [-30,10], z ∈ [-40,0].
///
/// Divide & Conquer: single merged blob at start → press X to split →
/// solve cross-wired dual gate puzzle → reunite at merge zone to win.
///
///   [MergedBlob]  →  X split  →  [Blob1]  [Blob2]
///   [Btn1→Gate2]  |  [Gate1]  [Gate2]  |  [Btn2→Gate1]
///   (ground)       |    chokepoint       |  (elevated/jump)
///                  ↓     ramps      ↓
///               [MergeZone] (lower level) → WIN
/// </summary>
public static class SceneBuilder
{
    // ── Asset paths ───────────────────────────────────────────────────
    private const string RoomPrefabPath =
        "Assets/Free LowPoly SciFi Pack/Prefabs/Rooms/Free_RoomA.prefab";
    private const string RobotSpherePrefabPath =
        "Assets/RobotSphere/Assets/Prefab/robotSphere.prefab";
    private const string SciFiMatDir =
        "Assets/Free LowPoly SciFi Pack/Meshes/Materials/";

    [MenuItem("Blob3D/Build Level 01 %#b")]
    public static void BuildLevel()
    {
        EnsureTag("Blob");
        EnsureTag("Pushable");

        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Render settings – match SciFi demo scene
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.33f, 0.26f, 0.37f);
        RenderSettings.fog          = false;

        // ── Load shared assets ───────────────────────────────────────────
        var roomPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
        var robotPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(RobotSpherePrefabPath);
        var darkGreyMat  = AssetDatabase.LoadAssetAtPath<Material>(SciFiMatDir + "DARK_GREY_mat.mat");
        var blueLightMat = AssetDatabase.LoadAssetAtPath<Material>(SciFiMatDir + "BLUE_LIGHT_mat.mat");
        var greyMat      = AssetDatabase.LoadAssetAtPath<Material>(SciFiMatDir + "GREY_mat.mat");

        if (roomPrefab == null)
        {
            Debug.LogError("[Blob3D] Free_RoomA prefab not found! Import the Free LowPoly SciFi Pack.");
            return;
        }

        // ── Room (scaled 2× for bigger environment) ────────────────────────
        var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
        room.transform.position   = Vector3.zero;
        room.transform.localScale = Vector3.one * 2f;
        // At 2×: x ∈ [-30,10], z ∈ [-40,0], upper y≈0, lower y≈-2, ceiling y≈6

        // ── Lighting (SciFi interior) ─────────────────────────────────────
        var sunGO = new GameObject("Sun");
        var sun   = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = new Color(1f, 0.99f, 0.93f);
        sun.intensity = 0.3f;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── Gate 1 (blocks left corridor → opened by Button 2) ───────────
        BuildGate("Gate_1",
            new Vector3(-16f, 1f, -18f),
            new Vector3(0.6f, 5f, 6f),
            darkGreyMat, blueLightMat);

        // ── Gate 2 (blocks right corridor → opened by Button 1) ──────────
        BuildGate("Gate_2",
            new Vector3(-4f, 1f, -18f),
            new Vector3(0.6f, 5f, 6f),
            darkGreyMat, blueLightMat);

        // ── Button 1 (ground level, left → cross-wired to Gate 2) ───────
        BuildPressureButton("PressureButton_1",
            new Vector3(-24f, 0.1f, -12f),
            greyMat, blueLightMat);

        // ── Button 2 (elevated, right → cross-wired to Gate 1) ─────────
        // Platform the blob must jump onto
        BuildPlatform("ElevatedPlatform",
            new Vector3(4f, 0.65f, -8f),
            new Vector3(3f, 1.3f, 3f),
            darkGreyMat);
        // Button on top of platform
        BuildPressureButton("PressureButton_2",
            new Vector3(4f, 1.4f, -8f),
            greyMat, blueLightMat);

        // ── Pushable cube (can hold a button pressed) ────────────────────
        BuildPushable("PushCube",
            new Vector3(-20f, 0.5f, -6f),
            darkGreyMat);

        // ── Blobs (single merged blob at start) ─────────────────────────
        // Both blobs at the same position; MergeZone.startMerged hides blob2.
        Vector3 startPos = new Vector3(-10f, 0.5f, -5f);
        var b1GO = BuildBlob("BlobOne", startPos,
            new Color(0.15f, 0.85f, 1f), robotPrefab);
        var b2GO = BuildBlob("BlobTwo", startPos,
            new Color(1f, 0.35f, 0.8f), robotPrefab);

        // ── Merge Zone (lower level, end goal) ───────────────────────────
        var mzGO = BuildMergeZone(new Vector3(-10f, -1.94f, -32f));
        var mz   = mzGO.GetComponent<MergeZone>();
        mz.startMerged = true;

        // ── Managers ─────────────────────────────────────────────────────
        var managers = new GameObject("Managers");
        managers.AddComponent<GameManager>();
        managers.AddComponent<LevelWiring>();
        managers.AddComponent<BlobPhysicsTuner>();
        var bm  = managers.AddComponent<BlobManager>();
        bm.blobOne = b1GO.GetComponent<BlobController>();
        bm.blobTwo = b2GO.GetComponent<BlobController>();

        // ── Camera (clamped to room bounds) ─────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.farClipPlane    = 200f;

        var cc = camGO.AddComponent<CameraController>();
        cc.blobOne = b1GO.transform;
        cc.blobTwo = b2GO.transform;
        // Heights for 2× room (ceiling at y≈6)
        cc.tppHeightBase       = 3.5f;
        cc.tppDistance          = 6f;
        cc.tppLookHeightOffset = 0.8f;
        cc.tppSmoothSpeed      = 7f;
        cc.combinedHeightBase  = 4f;
        cc.combinedMaxHeight   = 5f;
        cc.combinedZOffset     = -8f;
        cc.velocityLookaheadScale = 0.2f;
        cc.maxLookaheadDistance   = 3f;
        // Room bounds clamping
        cc.clampToRoom = true;
        cc.roomMin = new Vector3(-28f, 0.5f, -38f);
        cc.roomMax = new Vector3(8f, 5.5f, -2f);
        camGO.transform.position = new Vector3(-10f, 3.5f, -8f);
        camGO.transform.LookAt(new Vector3(-10f, 0f, -15f));

        // ── Save ──────────────────────────────────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level01.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Blob3D] ✓ Level01 built: 2× SciFi room, single-blob start, dual gates, camera clamped.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Gameplay builders
    // ════════════════════════════════════════════════════════════════════════

    static void BuildGate(string name, Vector3 pos, Vector3 scale,
        Material bodyMat, Material trimMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;

        if (bodyMat != null)
            go.GetComponent<Renderer>().sharedMaterial = bodyMat;

        var gate = go.AddComponent<Gate>();
        gate.openHeight  = 3.5f;
        gate.closedColor = new Color(0.8f, 0.15f, 0.15f);
        gate.openColor   = new Color(0f, 1f, 0.96f);

        // Emissive trim strip
        var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trim.name = name + "_Trim";
        trim.transform.SetParent(go.transform);
        trim.transform.localPosition = Vector3.zero;
        trim.transform.localScale    = new Vector3(1.05f, 0.08f, 1.02f);
        if (trimMat != null)
            trim.GetComponent<Renderer>().sharedMaterial = trimMat;
        Object.DestroyImmediate(trim.GetComponent<Collider>());
    }

    static void BuildPressureButton(string name, Vector3 pos,
        Material baseMat, Material glowMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(1.8f, 0.06f, 1.8f);

        if (baseMat != null)
            go.GetComponent<Renderer>().sharedMaterial = baseMat;

        go.GetComponent<Collider>().isTrigger = true;

        var btn = go.AddComponent<PressureButton>();
        btn.pressedColor  = new Color(0f, 1f, 0.96f);
        btn.releasedColor = new Color(0.8f, 0.15f, 0.15f);

        // Glow ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = name + "_Ring";
        ring.transform.SetParent(go.transform);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale    = new Vector3(1.15f, 0.5f, 1.15f);
        if (glowMat != null)
            ring.GetComponent<Renderer>().sharedMaterial = glowMat;
        Object.DestroyImmediate(ring.GetComponent<Collider>());
    }

    static void BuildPlatform(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void BuildPushable(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.tag  = "Pushable";
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.9f;

        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass           = 2f;
        rb.linearDamping  = 7f;
        rb.angularDamping = 12f;
        rb.constraints    = RigidbodyConstraints.FreezeRotation |
                            RigidbodyConstraints.FreezePositionY;
    }

    static GameObject BuildBlob(string name, Vector3 pos, Color col,
        GameObject robotPrefab)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.tag  = "Blob";
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.85f;

        var mat = CreateMat(name + "Mat", col, 0f, 0.85f,
            emissive: true, emCol: col * 0.35f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<Rigidbody>();
        var ctrl = go.AddComponent<BlobController>();
        ctrl.activeColor   = col;
        ctrl.inactiveColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f);

        if (robotPrefab != null)
        {
            ctrl.robotSpherePrefab = robotPrefab;
            ctrl.avatarRoot        = go.transform;
            ctrl.hideBlobMeshWhenAvatarPresent = true;
        }
        else
        {
            Debug.LogWarning("[Blob3D] RobotSphere prefab not found. " +
                "Run Blob3D > Setup > Assign RobotSphere To Blobs.");
        }

        return go;
    }

    static GameObject BuildMergeZone(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name                 = "MergeZone";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(4f, 0.05f, 4f);

        var mat = CreateMat("MergeZoneMat",
            Color.yellow, 0f, 1f, emissive: true, emCol: Color.yellow * 0.7f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.GetComponent<Collider>().isTrigger = true;
        go.AddComponent<MergeZone>();
        return go;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Asset helpers
    // ════════════════════════════════════════════════════════════════════════

    static Material CreateMat(string name, Color col,
        float metallic, float smooth,
        bool emissive = false, Color emCol = default)
    {
        Directory.CreateDirectory("Assets/Materials");
        string path = $"Assets/Materials/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        var mat = new Material(Shader.Find("Standard"));
        mat.color = col;
        mat.SetFloat("_Metallic",   metallic);
        mat.SetFloat("_Glossiness", smooth);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emCol);
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureTag(string tag)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
