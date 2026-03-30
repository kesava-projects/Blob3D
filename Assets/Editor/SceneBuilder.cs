using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Blob3D ▶ Build Level 01 (Ctrl/Cmd+Shift+B)
///
/// Free_RoomA × 2.  Room x∈[-30,10] z∈[-40,0]  upper y≈0  lower y≈-2  ceiling y≈6.
///
/// ENTRANCE → split → narrow gap + Gate 1 → push block onto Button 1 → Gate 1 opens
/// → climb shaft (merged can't fit) → Button 2 → Gate 2 opens
/// → one walks through, climber jumps down → EXIT DOOR = WIN
/// </summary>
public static class SceneBuilder
{
    const string RoomPrefab  = "Assets/Free LowPoly SciFi Pack/Prefabs/Rooms/Free_RoomA.prefab";
    const string RobotPrefab = "Assets/RobotSphere/Assets/Prefab/robotSphere.prefab";
    const string MatDir      = "Assets/Free LowPoly SciFi Pack/Meshes/Materials/";

    [MenuItem("Blob3D/Build Level 01 %#b")]
    public static void BuildLevel()
    {
        EnsureTag("Blob");
        EnsureTag("Pushable");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.33f, 0.26f, 0.37f);
        RenderSettings.fog = false;

        // ── Assets ──────────────────────────────────────────────────────────
        var roomPfb   = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab);
        var robotPfb  = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefab);
        var dkGrey    = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "DARK_GREY_mat.mat");
        var bluGlow   = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "BLUE_LIGHT_mat.mat");
        var grey      = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "GREY_mat.mat");
        if (roomPfb == null) { Debug.LogError("[Blob3D] Free_RoomA not found!"); return; }

        // ── Room 2× ─────────────────────────────────────────────────────────
        var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPfb);
        room.transform.position   = Vector3.zero;
        room.transform.localScale = Vector3.one * 2f;

        // ── Light ───────────────────────────────────────────────────────────
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.99f, 0.93f);
        sun.intensity = 0.35f;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ════════════════════════════════════════════════════════════════════
        //  ZONE A – Spawn (near entrance gate at z≈0)
        // ════════════════════════════════════════════════════════════════════
        Vector3 spawn = new Vector3(-18f, 0.5f, -4f);

        // ════════════════════════════════════════════════════════════════
        //  ZONE B – Gate 1 + Narrow gap (z = -12)
        //  Corridor-sized gate (not room-spanning).
        //  Gate_1:   x ∈ [-16, -8]  (8 units, blocks main corridor)
        //  GapCap:   x ∈ [-7,  -4]  (3 units, cap beside gap)
        //  Gap:      x ∈ [-8,  -7]  = 1.0 unit
        //  Split blob ø0.85 fits.  Merged blob ø1.275 doesn’t.
        // ════════════════════════════════════════════════════════════════
        // Gate material: red emissive closed, cyan when open
        var gateMat = Mat("GateMat", new Color(0.15f, 0.02f, 0.02f), 0.6f, 0.7f,
            true, new Color(0.6f, 0.08f, 0.08f));

        Box("Gate_1",  V(-12f, 1.5f, -12f), V(8f, 4f, 0.4f), gateMat, gate: true, gOpen: 4.5f);
        Box("GapCap",  V(-5.5f, 1.5f, -12f), V(3f, 4f, 0.4f), dkGrey);

        // ════════════════════════════════════════════════════════════════════
        //  ZONE C – Block + Button 1 (behind Gate 1, z ≈ -16)
        // ════════════════════════════════════════════════════════════════════
        BuildButton("PressureButton_1", V(-10f, 0.1f, -16f), grey, bluGlow);
        BuildPushable("PushCube", V(-18f, 0.5f, -16f), dkGrey);

        // ════════════════════════════════════════════════════════════════
        //  ZONE D – Climb steps + Button 2 (z ≈ -22)
        //  Open staircase (no shaft walls) with 0.5-unit height steps.
        //  Merged blob is too big to fit on the narrow steps.
        // ════════════════════════════════════════════════════════════════
        float sx = -8f, sz = -22f;

        // Steps: 0.5 m height each, staircase going up
        Box("Step_1", V(sx, 0.25f, sz),       V(1.2f, 0.5f, 1.5f), grey);
        Box("Step_2", V(sx, 0.75f, sz - 0.3f), V(1.2f, 0.5f, 1.5f), grey);
        Box("Step_3", V(sx, 1.25f, sz - 0.6f), V(1.2f, 0.5f, 1.5f), grey);
        Box("Step_4", V(sx, 1.75f, sz - 0.9f), V(1.2f, 0.5f, 1.5f), grey);

        // Top platform + Button 2
        Box("ShaftTop", V(sx, 2.25f, sz - 0.6f), V(2.5f, 0.5f, 3f), dkGrey);
        BuildButton("PressureButton_2", V(sx, 2.56f, sz - 0.6f), grey, bluGlow);

        // ════════════════════════════════════════════════════════════════
        //  ZONE E – Gate 2 (z = -26, corridor-sized, leads to lower level)
        // ════════════════════════════════════════════════════════════════
        Box("Gate_2", V(-10f, 0.5f, -26f), V(10f, 4f, 0.4f), gateMat, gate: true, gOpen: 4.5f);

        // Descent steps from shaft top (y≈2.5) to lower floor (y≈-2)
        // 0.7 m drops — easy to land on.
        Box("Desc_1", V(sx, 1.55f, -27.5f), V(2f, 0.4f, 2f), grey);
        Box("Desc_2", V(sx, 0.85f, -29f),   V(2f, 0.4f, 2f), grey);
        Box("Desc_3", V(sx, 0.15f, -30.5f), V(2f, 0.4f, 2f), grey);
        Box("Desc_4", V(sx, -0.55f,-32f),   V(2f, 0.4f, 2f), grey);
        Box("Desc_5", V(sx, -1.25f,-33.5f), V(2f, 0.4f, 2f), grey);

        // ════════════════════════════════════════════════════════════════
        //  ZONE F – Exit trigger at the back wall (z≈-40).
        //  Covers full back-wall width so walking through ANY back door
        //  into outer space triggers the win.
        // ════════════════════════════════════════════════════════════════
        var exitGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitGO.name = "ExitZone";
        exitGO.transform.position   = new Vector3(-10f, -1f, -41f);
        exitGO.transform.localScale = new Vector3(42f, 6f, 2f);
        exitGO.GetComponent<Collider>().isTrigger = true;
        exitGO.GetComponent<Renderer>().enabled = false; // invisible
        exitGO.AddComponent<ExitZone>();

        // ════════════════════════════════════════════════════════════════════
        //  Blobs (both at spawn; blob2 hidden by startMerged)
        // ════════════════════════════════════════════════════════════════════
        var b1 = BuildBlob("BlobOne", spawn, new Color(0.15f, 0.85f, 1f), robotPfb);
        var b2 = BuildBlob("BlobTwo", spawn, new Color(1f, 0.35f, 0.8f), robotPfb);

        // ════════════════════════════════════════════════════════════════════
        //  Managers (MergeZone lives here — no yellow pad, just merge logic)
        // ════════════════════════════════════════════════════════════════════
        var mgr = new GameObject("Managers");
        mgr.AddComponent<GameManager>();
        mgr.AddComponent<LevelWiring>();
        mgr.AddComponent<BlobPhysicsTuner>();

        var mz = mgr.AddComponent<MergeZone>();
        mz.startMerged = true;

        var bm = mgr.AddComponent<BlobManager>();
        bm.blobOne = b1.GetComponent<BlobController>();
        bm.blobTwo = b2.GetComponent<BlobController>();

        // ════════════════════════════════════════════════════════════════════
        //  Camera
        // ════════════════════════════════════════════════════════════════════
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.farClipPlane = 200f;

        var cc = camGO.AddComponent<CameraController>();
        cc.blobOne = b1.transform;
        cc.blobTwo = b2.transform;
        cc.tppHeightBase       = 3.5f;
        cc.tppDistance          = 6f;
        cc.tppLookHeightOffset = 0.8f;
        cc.tppSmoothSpeed      = 7f;
        cc.combinedHeightBase  = 4f;
        cc.combinedMaxHeight   = 5f;
        cc.combinedZOffset     = -8f;
        cc.velocityLookaheadScale = 0.2f;
        cc.maxLookaheadDistance   = 3f;
        cc.clampToRoom = true;
        cc.roomMin = new Vector3(-28f, -1.5f, -38f);
        cc.roomMax = new Vector3(8f, 5.5f, -2f);
        camGO.transform.position = new Vector3(-15f, 3f, -6f);
        camGO.transform.LookAt(new Vector3(-10f, 0f, -15f));

        // ════════════════════════════════════════════════════════════════════
        //  Save
        // ════════════════════════════════════════════════════════════════════
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level01.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Blob3D] ✓ Level01 built: no wall gaps, easy steps, exit door win.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════════════

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat,
        bool gate = false, float gOpen = 4f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        if (gate)
        {
            var g = go.AddComponent<Gate>();
            g.openHeight  = gOpen;
            g.closedColor = new Color(0.8f, 0.15f, 0.15f);
            g.openColor   = new Color(0f, 1f, 0.96f);
        }
        return go;
    }

    static void BuildButton(string name, Vector3 pos, Material baseMat, Material glowMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(1.0f, 0.06f, 1.0f); // compact button
        if (baseMat != null) go.GetComponent<Renderer>().sharedMaterial = baseMat;
        go.GetComponent<Collider>().isTrigger = true;
        var btn = go.AddComponent<PressureButton>();
        btn.pressedColor  = new Color(0f, 1f, 0.96f);
        btn.releasedColor = new Color(0.8f, 0.15f, 0.15f);

        // Small glow ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = name + "_Ring";
        ring.transform.SetParent(go.transform);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale    = new Vector3(1.2f, 0.5f, 1.2f);
        if (glowMat != null) ring.GetComponent<Renderer>().sharedMaterial = glowMat;
        Object.DestroyImmediate(ring.GetComponent<Collider>());
    }

    static void BuildPushable(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.tag = "Pushable";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.9f;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f; rb.linearDamping = 7f; rb.angularDamping = 12f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    static GameObject BuildBlob(string name, Vector3 pos, Color col, GameObject robotPfb)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name; go.tag = "Blob";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.85f;
        go.GetComponent<Renderer>().sharedMaterial =
            Mat(name + "Mat", col, 0f, 0.85f, true, col * 0.35f);
        go.AddComponent<Rigidbody>();
        var c = go.AddComponent<BlobController>();
        c.activeColor = col;
        c.inactiveColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f);
        if (robotPfb != null)
        {
            c.robotSpherePrefab = robotPfb;
            c.avatarRoot = go.transform;
            c.hideBlobMeshWhenAvatarPresent = true;
        }
        return go;
    }

    static Material Mat(string name, Color col, float metal, float smooth,
        bool emit = false, Color emCol = default)
    {
        Directory.CreateDirectory("Assets/Materials");
        string p = $"Assets/Materials/{name}.mat";
        var ex = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (ex != null) AssetDatabase.DeleteAsset(p);
        var m = new Material(Shader.Find("Standard"));
        m.color = col;
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Glossiness", smooth);
        if (emit) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emCol); }
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    static void EnsureTag(string tag)
    {
        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tm.ApplyModifiedProperties();
    }
}
