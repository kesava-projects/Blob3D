using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only tool.
/// Menu: Blob3D ▶ Build Level 01  (shortcut: Ctrl/Cmd+Shift+B)
///
/// Procedurally creates the full Level01 scene:
///
///   LEFT ZONE (x < 0)         │  RIGHT ZONE (x > 0)
///   ─────────────────────────────────────────────────
///   [B1][B2]  [PushCube]  [BTN]  ║[GATE]║  [obstacles]  [MERGE ZONE]
///                                 ║      ║
///   Dividing wall with gate in   ─╝      ╚─  the gap.
///
/// Puzzle: Push the purple cube onto the red button → gate rises →
///         guide both blobs to the merge zone → level complete.
/// </summary>
public static class SceneBuilder
{
    [MenuItem("Blob3D/Build Level 01 %#b")]
    public static void BuildLevel()
    {
        // 1. Ensure custom tags exist in TagManager
        EnsureTag("Blob");
        EnsureTag("Pushable");

        // 2. Fresh empty scene
        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3. Render settings – deep space look
        RenderSettings.ambientLight = new Color(0.04f, 0.04f, 0.14f);
        RenderSettings.fog          = false;

        // ── Lighting ──────────────────────────────────────────────────────
        var sunGO = new GameObject("Sun");
        var sun   = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = new Color(0.75f, 0.82f, 1f);
        sun.intensity = 0.85f;
        sunGO.transform.rotation = Quaternion.Euler(45f, -40f, 0f);

        // ── Floor ─────────────────────────────────────────────────────────
        BuildFloor();

        // ── Outer boundary walls ──────────────────────────────────────────
        // North / South walls span the full width including the dividing wall
        BuildWall("Wall_N", new Vector3( 0f, 1f,  9.5f), new Vector3(38f, 2f, 1f), WallColor);
        BuildWall("Wall_S", new Vector3( 0f, 1f, -9.5f), new Vector3(38f, 2f, 1f), WallColor);
        BuildWall("Wall_W", new Vector3(-19f, 1f, 0f),   new Vector3(1f, 2f, 20f), WallColor);
        BuildWall("Wall_E", new Vector3( 19f, 1f, 0f),   new Vector3(1f, 2f, 20f), WallColor);

        // ── Dividing wall (forces blobs to use the gate) ──────────────────
        // Gate opening occupies z = -1.5 … +1.5  (gate scale Z = 3)
        // Wall sections fill the rest of the dividing line at x = 0
        BuildWall("DivWall_N", new Vector3(0f, 1f,  5.5f), new Vector3(1.2f, 2f, 8f), DivColor);
        BuildWall("DivWall_S", new Vector3(0f, 1f, -5.5f), new Vector3(1.2f, 2f, 8f), DivColor);

        // ── Gate ──────────────────────────────────────────────────────────
        // Sits in the dividing-wall gap.  openHeight lifts it clear of blobs.
        var gateGO = BuildGate(new Vector3(0f, 1.5f, 0f));

        // ── Pressure button ───────────────────────────────────────────────
        // Positioned left of the gate; player pushes the cube here.
        var btnGO = BuildButton(new Vector3(-4f, 0.06f, -6f));

        // Button → Gate is wired at runtime by LevelWiring (avoids batch-mode
        // serialisation issues with UnityEventTools in headless mode).

        // ── Pushable cube ─────────────────────────────────────────────────
        // Purple cube the player rolls onto the button.
        BuildPushable("PushCube", new Vector3(-9f, 0.45f, -5.5f));

        // ── Static obstacle cubes (right zone, add some challenge) ────────
        BuildObstacle("Obs_1", new Vector3( 5f, 0.5f,  4f));
        BuildObstacle("Obs_2", new Vector3( 5f, 0.5f, -4f));
        BuildObstacle("Obs_3", new Vector3( 9f, 0.5f,  2.5f));
        BuildObstacle("Obs_4", new Vector3(10f, 0.5f, -2.5f));
        BuildObstacle("Obs_5", new Vector3(14f, 0.5f,  3.5f));

        // ── Blobs ─────────────────────────────────────────────────────────
        var b1GO = BuildBlob("BlobOne", new Vector3(-14f, 0.425f,  1.2f), BlobOneColor);
        var b2GO = BuildBlob("BlobTwo", new Vector3(-14f, 0.425f, -1.2f), BlobTwoColor);

        // ── Merge zone ────────────────────────────────────────────────────
        BuildMergeZone(new Vector3(15f, 0.06f, 0f));

        // ── Managers GameObject ───────────────────────────────────────────
        var managers = new GameObject("Managers");
        managers.AddComponent<GameManager>();
        managers.AddComponent<LevelWiring>();
        var bm  = managers.AddComponent<BlobManager>();
        bm.blobOne = b1GO.GetComponent<BlobController>();
        bm.blobTwo = b2GO.GetComponent<BlobController>();

        // ── Camera ────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.backgroundColor = new Color(0.01f, 0.01f, 0.10f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.farClipPlane    = 250f;

        var cc  = camGO.AddComponent<CameraController>();
        cc.blobOne = b1GO.transform;
        cc.blobTwo = b2GO.transform;
        camGO.transform.position = new Vector3(0f, 14f, -10f);
        camGO.transform.LookAt(Vector3.zero);

        // ── Starfield ─────────────────────────────────────────────────────
        BuildStarfield();

        // ── Save ──────────────────────────────────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level01.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Blob3D] ✓ Level01.unity built and saved.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Builder helpers
    // ════════════════════════════════════════════════════════════════════════

    static void BuildFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Floor";
        go.transform.position   = new Vector3(0f, -0.5f, 0f);
        go.transform.localScale = new Vector3(38f, 1f, 20f);
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat("FloorMat", new Color(0.07f, 0.07f, 0.18f), 0.5f, 0.65f);
    }

    static void BuildWall(string name, Vector3 pos, Vector3 scale, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat(name + "Mat", col, 0.25f, 0.35f);
    }

    static GameObject BuildBlob(string name, Vector3 pos, Color col)
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

        return go;
    }

    static GameObject BuildGate(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = "Gate";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.5f, 3f, 3f);

        var mat = GetOrCreateMat("GateMat",
            new Color(1f, 0.15f, 0.15f), 0.6f, 0.85f,
            emissive: true, emCol: new Color(0.4f, 0.05f, 0.05f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Gate.cs requires Rigidbody via [RequireComponent] – added automatically
        go.AddComponent<Gate>();
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
        rb.mass        = 2f;
        rb.linearDamping        = 7f;
        rb.angularDamping = 12f;
        rb.constraints = RigidbodyConstraints.FreezeRotation |
                         RigidbodyConstraints.FreezePositionY;
    }

    static void BuildObstacle(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one;
        go.GetComponent<Renderer>().sharedMaterial =
            GetOrCreateMat("ObstacleMat", new Color(0.22f, 0.12f, 0.38f), 0.2f, 0.4f);
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

    static void BuildStarfield()
    {
        var go = new GameObject("Starfield");
        go.transform.position = new Vector3(0f, 50f, 0f);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = 200f;
        main.startSpeed      = 0f;
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.8f, 0.85f, 1f, 0.9f),
                                   Color.white);
        main.maxParticles    = 700;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 700) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(100f, 1f, 100f);
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

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

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

    // ── Colour palette ─────────────────────────────────────────────────────
    static readonly Color WallColor    = new Color(0.10f, 0.10f, 0.24f);
    static readonly Color DivColor     = new Color(0.18f, 0.08f, 0.32f);
    static readonly Color BlobOneColor = new Color(0.15f, 0.85f, 1.00f);   // Cyan
    static readonly Color BlobTwoColor = new Color(1.00f, 0.35f, 0.80f);   // Pink
}
