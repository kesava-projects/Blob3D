using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only tool.
/// Menu: Blob3D ▶ Build Level 01  (shortcut: Ctrl/Cmd+Shift+B)
///
/// Procedurally creates the full Level01 scene as an enclosed interior room:
///
///   LEFT ROOM (x < 0)          │  RIGHT ROOM (x > 0)
///   ─────────────────────────────────────────────────
///   [B1][B2]  [PushCube]  [BTN]  ║[GATE]║  [obstacles]  [MERGE ZONE]
///                                 ║      ║
///   Beige walls + screens        ─╝      ╚─  dark floor + furniture
///
/// Interior: beige/cream walls, ceiling, wall-mounted screens/pictures,
/// furniture (desks, monitors, boxes), pink gate doorway, warm lighting.
/// </summary>
public static class SceneBuilder
{
    private const string RobotSpherePrefabPath =
        "Assets/RobotSphere/Assets/Prefab/robotSphere.prefab";

    [MenuItem("Blob3D/Build Level 01 %#b")]
    public static void BuildLevel()
    {
        // 1. Ensure custom tags exist in TagManager
        EnsureTag("Blob");
        EnsureTag("Pushable");

        // 2. Fresh empty scene
        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3. Render settings – warm indoor look
        RenderSettings.ambientLight = new Color(0.35f, 0.32f, 0.28f);
        RenderSettings.fog          = false;

        // ── Lighting (warm interior) ──────────────────────────────────────
        var sunGO = new GameObject("Sun");
        var sun   = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.1f;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Fill light for softer shadows
        var fillGO = new GameObject("FillLight");
        var fill   = fillGO.AddComponent<Light>();
        fill.type      = LightType.Directional;
        fill.color     = new Color(0.6f, 0.65f, 0.8f);
        fill.intensity = 0.35f;
        fillGO.transform.rotation = Quaternion.Euler(30f, 140f, 0f);

        // ── Floor ─────────────────────────────────────────────────────────
        BuildFloor();

        // ── Ceiling ───────────────────────────────────────────────────────
        BuildCeiling();

        // ── Outer boundary walls (beige interior) ─────────────────────────
        BuildWall("Wall_N", new Vector3( 0f, 2f,  9.5f), new Vector3(38f, 4f, 0.5f), WallBeige);
        BuildWall("Wall_S", new Vector3( 0f, 2f, -9.5f), new Vector3(38f, 4f, 0.5f), WallBeige);
        BuildWall("Wall_W", new Vector3(-19f, 2f, 0f),   new Vector3(0.5f, 4f, 20f), WallBeige);
        BuildWall("Wall_E", new Vector3( 19f, 2f, 0f),   new Vector3(0.5f, 4f, 20f), WallGrey);

        // ── Dividing wall (forces blobs to use the gate doorway) ──────────
        BuildWall("DivWall_N", new Vector3(0f, 2f,  5.5f), new Vector3(1.2f, 4f, 8f), DivWallColor);
        BuildWall("DivWall_S", new Vector3(0f, 2f, -5.5f), new Vector3(1.2f, 4f, 8f), DivWallColor);

        // ── Gate (pink doorway) ───────────────────────────────────────────
        BuildGate(new Vector3(0f, 2f, 0f));

        // ── Pressure button ───────────────────────────────────────────────
        BuildButton(new Vector3(-4f, 0.06f, -6f));

        // ── Pushable cube ─────────────────────────────────────────────────
        BuildPushable("PushCube", new Vector3(-9f, 0.45f, -5.5f));

        // ── Static obstacle cubes (right zone) ────────────────────────────
        BuildObstacle("Obs_1", new Vector3( 5f, 0.5f,  4f));
        BuildObstacle("Obs_2", new Vector3( 5f, 0.5f, -4f));
        BuildObstacle("Obs_3", new Vector3( 9f, 0.5f,  2.5f));
        BuildObstacle("Obs_4", new Vector3(10f, 0.5f, -2.5f));
        BuildObstacle("Obs_5", new Vector3(14f, 0.5f,  3.5f));

        // ── Wall-mounted screens / pictures ───────────────────────────────
        BuildWallScreens();

        // ── Interior furniture ────────────────────────────────────────────
        BuildFurniture();

        // ── Blobs ─────────────────────────────────────────────────────────
        var robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotSpherePrefabPath);
        var b1GO = BuildBlob("BlobOne", new Vector3(-14f, 0.425f,  1.2f), BlobOneColor, robotPrefab);
        var b2GO = BuildBlob("BlobTwo", new Vector3(-14f, 0.425f, -1.2f), BlobTwoColor, robotPrefab);

        // ── Merge zone ────────────────────────────────────────────────────
        BuildMergeZone(new Vector3(15f, 0.06f, 0f));

        // ── Managers GameObject ───────────────────────────────────────────
        var managers = new GameObject("Managers");
        managers.AddComponent<GameManager>();
        managers.AddComponent<LevelWiring>();
        managers.AddComponent<BlobPhysicsTuner>();
        var bm  = managers.AddComponent<BlobManager>();
        bm.blobOne = b1GO.GetComponent<BlobController>();
        bm.blobTwo = b2GO.GetComponent<BlobController>();

        // ── Camera ────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.farClipPlane    = 250f;

        var cc  = camGO.AddComponent<CameraController>();
        cc.blobOne = b1GO.transform;
        cc.blobTwo = b2GO.transform;
        cc.tppHeightBase      = 6f;
        cc.tppDistance         = 5f;
        cc.tppLookHeightOffset = 1.2f;
        cc.tppSmoothSpeed      = 7f;
        cc.combinedHeightBase  = 10f;
        cc.combinedMaxHeight   = 20f;
        cc.combinedZOffset     = -5f;
        cc.velocityLookaheadScale = 0.2f;
        cc.maxLookaheadDistance   = 2.5f;
        camGO.transform.position = new Vector3(0f, 10f, -8f);
        camGO.transform.LookAt(Vector3.zero);

        // ── Save ──────────────────────────────────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level01.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Blob3D] ✓ Level01.unity built and saved.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Room structure
    // ════════════════════════════════════════════════════════════════════════

    static void BuildFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Floor";
        go.transform.position   = new Vector3(0f, -0.5f, 0f);
        go.transform.localScale = new Vector3(38f, 1f, 20f);
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat("FloorMat", FloorColor, 0.3f, 0.55f);
    }

    static void BuildCeiling()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Ceiling";
        go.transform.position   = new Vector3(0f, 4.25f, 0f);
        go.transform.localScale = new Vector3(38f, 0.5f, 20f);
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat("CeilingMat", CeilingColor, 0f, 0.2f);
    }

    static void BuildWall(string name, Vector3 pos, Vector3 scale, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat(name + "Mat", col, 0.05f, 0.25f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Wall-mounted screens / pictures (like the reference image)
    // ════════════════════════════════════════════════════════════════════════

    static void BuildWallScreens()
    {
        Material screenDark  = GetOrCreateMat("ScreenDarkMat",
            new Color(0.12f, 0.12f, 0.15f), 0.6f, 0.8f);
        Material screenLight = GetOrCreateMat("ScreenLightMat",
            new Color(0.65f, 0.68f, 0.72f), 0.4f, 0.6f);
        Material screenBlue  = GetOrCreateMat("ScreenBlueMat",
            new Color(0.2f, 0.3f, 0.5f), 0.5f, 0.7f,
            emissive: true, emCol: new Color(0.05f, 0.08f, 0.15f));

        // ── West wall screens (left room, main wall the blobs face) ──────
        BuildScreen("Screen_W1", new Vector3(-18.6f, 2.8f, 4f),
            new Vector3(0.08f, 1.2f, 1.8f), screenDark);
        BuildScreen("Screen_W2", new Vector3(-18.6f, 2.2f, 1f),
            new Vector3(0.08f, 0.8f, 1.2f), screenLight);
        BuildScreen("Screen_W3", new Vector3(-18.6f, 3.2f, -1.5f),
            new Vector3(0.08f, 0.6f, 0.9f), screenDark);
        BuildScreen("Screen_W4", new Vector3(-18.6f, 1.5f, -3.5f),
            new Vector3(0.08f, 1.0f, 1.4f), screenBlue);
        BuildScreen("Screen_W5", new Vector3(-18.6f, 3.0f, -5.5f),
            new Vector3(0.08f, 0.7f, 1.0f), screenLight);
        BuildScreen("Screen_W6", new Vector3(-18.6f, 2.0f, 6.5f),
            new Vector3(0.08f, 0.9f, 1.3f), screenBlue);
        BuildScreen("Screen_W7", new Vector3(-18.6f, 3.5f, 2.5f),
            new Vector3(0.08f, 0.5f, 0.7f), screenDark);

        // ── North wall screens ────────────────────────────────────────────
        BuildScreen("Screen_N1", new Vector3(-8f, 2.5f, 9.15f),
            new Vector3(1.6f, 1.0f, 0.08f), screenDark);
        BuildScreen("Screen_N2", new Vector3(-4f, 3.0f, 9.15f),
            new Vector3(1.0f, 0.7f, 0.08f), screenLight);
        BuildScreen("Screen_N3", new Vector3(6f, 2.3f, 9.15f),
            new Vector3(1.4f, 0.9f, 0.08f), screenBlue);
        BuildScreen("Screen_N4", new Vector3(12f, 3.2f, 9.15f),
            new Vector3(0.8f, 0.6f, 0.08f), screenDark);

        // ── South wall screens ────────────────────────────────────────────
        BuildScreen("Screen_S1", new Vector3(-12f, 2.8f, -9.15f),
            new Vector3(1.8f, 1.1f, 0.08f), screenLight);
        BuildScreen("Screen_S2", new Vector3(-6f, 1.8f, -9.15f),
            new Vector3(1.2f, 0.8f, 0.08f), screenDark);
        BuildScreen("Screen_S3", new Vector3(8f, 2.6f, -9.15f),
            new Vector3(1.5f, 1.0f, 0.08f), screenBlue);

        // ── East wall screens ─────────────────────────────────────────────
        BuildScreen("Screen_E1", new Vector3(18.6f, 2.5f, 3f),
            new Vector3(0.08f, 1.0f, 1.5f), screenDark);
        BuildScreen("Screen_E2", new Vector3(18.6f, 3.0f, -3f),
            new Vector3(0.08f, 0.8f, 1.2f), screenLight);
    }

    static void BuildScreen(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.isStatic             = true;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Interior furniture
    // ════════════════════════════════════════════════════════════════════════

    static void BuildFurniture()
    {
        Material deskMat    = GetOrCreateMat("DeskMat",
            new Color(0.45f, 0.38f, 0.30f), 0.1f, 0.3f);
        Material monitorMat = GetOrCreateMat("MonitorMat",
            new Color(0.15f, 0.15f, 0.18f), 0.5f, 0.7f);
        Material boxMat     = GetOrCreateMat("BoxMat",
            new Color(0.55f, 0.52f, 0.48f), 0.05f, 0.2f);
        Material shelfMat   = GetOrCreateMat("ShelfMat",
            new Color(0.50f, 0.45f, 0.38f), 0.1f, 0.25f);

        // ── Left room desks ───────────────────────────────────────────────
        // Desk 1: near west wall
        BuildFurniturePiece("Desk_1", new Vector3(-16f, 0.4f, 5f),
            new Vector3(2.5f, 0.8f, 1.2f), deskMat);
        // Monitor on desk 1
        BuildFurniturePiece("Monitor_1", new Vector3(-16f, 1.1f, 5f),
            new Vector3(0.1f, 0.7f, 0.9f), monitorMat);

        // Desk 2: near south wall
        BuildFurniturePiece("Desk_2", new Vector3(-10f, 0.4f, -7.5f),
            new Vector3(2.0f, 0.8f, 1.0f), deskMat);
        // Monitor on desk 2
        BuildFurniturePiece("Monitor_2", new Vector3(-10f, 1.1f, -7.5f),
            new Vector3(0.1f, 0.6f, 0.8f), monitorMat);

        // ── Right room furniture ──────────────────────────────────────────
        // Desk 3: right side near east wall
        BuildFurniturePiece("Desk_3", new Vector3(16f, 0.4f, -5f),
            new Vector3(2.0f, 0.8f, 1.0f), deskMat);
        BuildFurniturePiece("Monitor_3", new Vector3(16f, 1.1f, -5f),
            new Vector3(0.1f, 0.65f, 0.85f), monitorMat);

        // ── Scattered boxes ───────────────────────────────────────────────
        BuildFurniturePiece("Box_1", new Vector3(-15f, 0.3f, -3f),
            new Vector3(0.6f, 0.6f, 0.6f), boxMat);
        BuildFurniturePiece("Box_2", new Vector3(-15.5f, 0.3f, -3.5f),
            new Vector3(0.5f, 0.5f, 0.5f), boxMat);
        BuildFurniturePiece("Box_3", new Vector3(12f, 0.3f, 6f),
            new Vector3(0.7f, 0.7f, 0.7f), boxMat);
        BuildFurniturePiece("Box_4", new Vector3(7f, 0.3f, -7f),
            new Vector3(0.55f, 0.55f, 0.55f), boxMat);

        // ── Shelf-like ledges on walls ─────────────────────────────────────
        BuildFurniturePiece("Shelf_W1", new Vector3(-18.5f, 1.2f, 0f),
            new Vector3(0.4f, 0.1f, 3f), shelfMat);
        BuildFurniturePiece("Shelf_N1", new Vector3(-2f, 1.2f, 9.1f),
            new Vector3(3f, 0.1f, 0.4f), shelfMat);
    }

    static void BuildFurniturePiece(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.isStatic             = true;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Gameplay objects
    // ════════════════════════════════════════════════════════════════════════

    static GameObject BuildBlob(string name, Vector3 pos, Color col, GameObject robotPrefab)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.tag  = "Blob";
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.85f;

        var mat = GetOrCreateMat(name + "Mat", col, 0f, 0.85f, emissive: true, emCol: col * 0.35f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<Rigidbody>();           // BlobController.Awake() configures it
        var ctrl = go.AddComponent<BlobController>();
        ctrl.activeColor   = col;
        ctrl.inactiveColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f);

        // BUG FIX: assign the RobotSphere prefab so blobs get their avatar
        if (robotPrefab != null)
        {
            ctrl.robotSpherePrefab = robotPrefab;
            ctrl.avatarRoot        = go.transform;
            ctrl.hideBlobMeshWhenAvatarPresent = true;
        }
        else
        {
            Debug.LogWarning($"[Blob3D] RobotSphere prefab not found at {RobotSpherePrefabPath}. " +
                $"Blobs will use fallback sphere mesh. Run Blob3D > Setup > Assign RobotSphere To Blobs after importing the asset.");
        }

        return go;
    }

    static GameObject BuildGate(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = "Gate";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.5f, 4f, 3f);

        var mat = GetOrCreateMat("GateMat",
            GateColor, 0.3f, 0.7f,
            emissive: true, emCol: GateColor * 0.3f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        var gate = go.AddComponent<Gate>();
        gate.openHeight = 4.5f;  // taller walls need higher open
        return go;
    }

    static GameObject BuildButton(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name                 = "PressureButton";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(2f, 0.08f, 2f);

        var mat = GetOrCreateMat("ButtonMat",
            new Color(1f, 0.15f, 0.15f), 0.2f, 0.9f,
            emissive: true, emCol: new Color(0.5f, 0.05f, 0.05f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.GetComponent<Collider>().isTrigger = true;
        go.AddComponent<PressureButton>();
        return go;
    }

    static void BuildPushable(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.tag  = "Pushable";
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.9f;

        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat(name + "Mat", new Color(0.5f, 0.25f, 0.85f), 0.3f, 0.55f);

        var rb = go.AddComponent<Rigidbody>();
        rb.mass           = 2f;
        rb.linearDamping  = 7f;
        rb.angularDamping = 12f;
        rb.constraints    = RigidbodyConstraints.FreezeRotation |
                            RigidbodyConstraints.FreezePositionY;
    }

    static void BuildObstacle(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one;
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat("ObstacleMat", new Color(0.35f, 0.28f, 0.45f), 0.2f, 0.4f);
    }

    static void BuildMergeZone(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name                 = "MergeZone";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);

        var mat = GetOrCreateMat("MergeZoneMat",
            Color.yellow, 0f, 1f, emissive: true, emCol: Color.yellow * 0.7f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.GetComponent<Collider>().isTrigger = true;
        go.AddComponent<MergeZone>();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Asset helpers
    // ════════════════════════════════════════════════════════════════════════

    static Material GetOrCreateMat(string name, Color col,
        float metallic, float smooth,
        bool emissive = false, Color emCol = default)
    {
        Directory.CreateDirectory("Assets/Materials");
        string path = $"Assets/Materials/{name}.mat";

        // Always recreate so palette changes take effect on rebuild
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

    // ════════════════════════════════════════════════════════════════════════
    //  Colour palette — warm interior tones to match reference
    // ════════════════════════════════════════════════════════════════════════

    // Walls: beige / cream interior
    static readonly Color WallBeige    = new Color(0.82f, 0.76f, 0.66f);
    static readonly Color WallGrey     = new Color(0.45f, 0.44f, 0.48f);
    static readonly Color DivWallColor = new Color(0.60f, 0.55f, 0.50f);

    // Floor: dark grey
    static readonly Color FloorColor   = new Color(0.12f, 0.12f, 0.15f);

    // Ceiling: off-white
    static readonly Color CeilingColor = new Color(0.75f, 0.73f, 0.70f);

    // Gate: pink/rose doorway
    static readonly Color GateColor    = new Color(0.85f, 0.45f, 0.55f);

    // Blobs
    static readonly Color BlobOneColor = new Color(0.15f, 0.85f, 1.00f);   // Cyan
    static readonly Color BlobTwoColor = new Color(1.00f, 0.35f, 0.80f);   // Pink
}
