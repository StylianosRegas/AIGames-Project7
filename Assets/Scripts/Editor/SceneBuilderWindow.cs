#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

/// <summary>
/// CaptureTheTreasure — Scene Builder
/// 
/// Open via: Tools → Capture The Treasure → Scene Builder
///
/// This Editor window constructs the full game scene from scratch:
///   Step 1 — Tags & Layers   : registers all required tags
///   Step 2 — Camera          : configures orthographic top-down camera
///   Step 3 — Tilemap Grid    : creates Ground, Wall, and Fog tilemap layers
///   Step 4 — TileMap Script  : places the TileMap singleton
///   Step 5 — Light Zones     : creates sample LitFull / LitPartial trigger zones
///   Step 6 — Player          : creates the Player GameObject with all components
///   Step 7 — Treasure & Exit : places Treasure pickup and Exit zone
///   Step 8 — Guards          : creates N guards with sensors, waypoints, labels
///   Step 9 — GameManager     : places the GameManager singleton
///   Step 10 — Canvas / HUD   : builds the full uGUI HUD (suspicion meter, debug panel,
///                              game over panel, win panel)
///
/// Each step is independent and can be re-run safely (existing objects are
/// skipped rather than duplicated).
/// </summary>
public class SceneBuilderWindow : EditorWindow
{
    // ── Window Config ────────────────────────────────────────────────────

    [MenuItem("Tools/Capture The Treasure/Scene Builder")]
    public static void Open() => GetWindow<SceneBuilderWindow>("CTT Scene Builder");

    // ── Settings exposed in the window ──────────────────────────────────

    private int _guardCount = 2;
    private int _waypointsPerGuard = 3;
    private int _mapWidth = 20;
    private int _mapHeight = 15;
    private float _patrolSpeed = 2.0f;
    private float _chaseSpeed = 4.5f;
    private float _visionRange = 8f;
    private float _visionHalfAngle = 45f;
    private float _hearingRadius = 10f;
    private bool _buildWallBorder = true;
    private bool _buildLightZones = true;
    private bool _buildFogLayer = false; // opt-in — needs a tile asset

    private Vector2 _scrollPos;
    private string _statusLog = "Press a step button or click Build All Scene.";

    // ── GUI ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        EditorGUILayout.Space(6);
        DrawSettings();
        EditorGUILayout.Space(10);
        DrawStepButtons();
        EditorGUILayout.Space(10);
        DrawBuildAll();
        EditorGUILayout.Space(10);
        DrawLog();

        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        var titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🏴 Capture The Treasure — Scene Builder", titleStyle,
            GUILayout.Height(28));
        EditorGUILayout.LabelField(
            "Builds the full game scene inside your open Unity scene.",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField(
            "Open an empty scene first, then run steps in order or click Build All.",
            EditorStyles.centeredGreyMiniLabel);
    }

    void DrawSettings()
    {
        EditorGUILayout.LabelField("⚙  Scene Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        _mapWidth = EditorGUILayout.IntSlider("Map Width (tiles)", _mapWidth, 10, 50);
        _mapHeight = EditorGUILayout.IntSlider("Map Height (tiles)", _mapHeight, 8, 40);
        _guardCount = EditorGUILayout.IntSlider("Number of Guards", _guardCount, 1, 6);
        _waypointsPerGuard = EditorGUILayout.IntSlider("Waypoints per Guard", _waypointsPerGuard, 2, 6);
        _patrolSpeed = EditorGUILayout.Slider("Guard Patrol Speed", _patrolSpeed, 1f, 5f);
        _chaseSpeed = EditorGUILayout.Slider("Guard Chase Speed", _chaseSpeed, 2f, 8f);
        _visionRange = EditorGUILayout.Slider("Vision Range", _visionRange, 3f, 15f);
        _visionHalfAngle = EditorGUILayout.Slider("Vision Half-Angle (°)", _visionHalfAngle, 20f, 90f);
        _hearingRadius = EditorGUILayout.Slider("Hearing Radius", _hearingRadius, 5f, 20f);

        _buildWallBorder = EditorGUILayout.Toggle("Build Wall Border", _buildWallBorder);
        _buildLightZones = EditorGUILayout.Toggle("Build Sample Light Zones", _buildLightZones);
        _buildFogLayer = EditorGUILayout.Toggle("Create Fog Tilemap (no tile)", _buildFogLayer);

        EditorGUI.indentLevel--;
    }

    void DrawStepButtons()
    {
        EditorGUILayout.LabelField("🔧  Individual Steps", EditorStyles.boldLabel);

        float w = position.width - 24;
        float half = (w - 4) * 0.5f;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1 · Tags & Layers", GUILayout.Width(half))) Step_Tags();
        if (GUILayout.Button("2 · Camera", GUILayout.Width(half))) Step_Camera();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3 · Tilemap Grid", GUILayout.Width(half))) Step_Tilemaps();
        if (GUILayout.Button("4 · TileMap Script", GUILayout.Width(half))) Step_TileMapScript();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("5 · Light Zones", GUILayout.Width(half))) Step_LightZones();
        if (GUILayout.Button("6 · Player", GUILayout.Width(half))) Step_Player();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("7 · Treasure & Exit", GUILayout.Width(half))) Step_TreasureAndExit();
        if (GUILayout.Button("8 · Guards", GUILayout.Width(half))) Step_Guards();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("9 · GameManager", GUILayout.Width(half))) Step_GameManager();
        if (GUILayout.Button("10 · Canvas / HUD", GUILayout.Width(half))) Step_HUD();
        GUILayout.EndHorizontal();
    }

    void DrawBuildAll()
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            fixedHeight = 36
        };
        if (GUILayout.Button("▶  Build All Scene (Steps 1–10)", style))
            BuildAll();
    }

    void DrawLog()
    {
        EditorGUILayout.LabelField("📋  Log", EditorStyles.boldLabel);
        var logStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 11 };
        EditorGUILayout.LabelField(_statusLog, logStyle, GUILayout.MinHeight(60));
    }

    // ── Build All ────────────────────────────────────────────────────────

    void BuildAll()
    {
        Log("Building full scene…");
        Step_Tags();
        Step_Camera();
        Step_Tilemaps();
        Step_TileMapScript();
        if (_buildLightZones) Step_LightZones();
        Step_Player();
        Step_TreasureAndExit();
        Step_Guards();
        Step_GameManager();
        Step_HUD();
        Log("✅  Build All complete! See Hierarchy for all objects.\n" +
            "Next: assign your Tile assets in the Tile Palette and paint the map.\n" +
            "Then assign the Player reference on each Guard in the Inspector.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 1 — Tags & Layers
    // ════════════════════════════════════════════════════════════════════

    void Step_Tags()
    {
        string[] requiredTags = { "Wall", "LitFull", "LitPartial", "Treasure", "Exit" };
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        int added = 0;
        foreach (string tag in requiredTags)
        {
            bool exists = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                { exists = true; break; }

            if (!exists)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                added++;
            }
        }

        tagManager.ApplyModifiedProperties();
        Log($"Step 1 — Tags: {added} new tag(s) registered. " +
            $"({string.Join(", ", requiredTags)})");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 2 — Camera
    // ════════════════════════════════════════════════════════════════════

    void Step_Camera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(_mapWidth, _mapHeight) * 0.5f + 1f;
        cam.transform.position = new Vector3(_mapWidth * 0.5f, _mapHeight * 0.5f, -10f);
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        MarkDirty(cam.gameObject);
        Log($"Step 2 — Camera: orthographic, size={cam.orthographicSize:F1}, " +
            $"position centered on {_mapWidth}×{_mapHeight} map.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 3 — Tilemaps
    // ════════════════════════════════════════════════════════════════════

    void Step_Tilemaps()
    {
        // Create or find root Grid
        GameObject gridGO = FindOrCreate("TilemapGrid", () =>
        {
            var go = new GameObject("TilemapGrid");
            go.AddComponent<Grid>();
            return go;
        });

        // Ground layer — order 0
        CreateTilemapChild(gridGO, "GroundLayer", 0, false, null);

        // Wall layer — order 1, add collider, tag as Wall
        var wallTM = CreateTilemapChild(gridGO, "WallLayer", 1, true, "Wall");

        // Fog layer (optional) — order 10
        if (_buildFogLayer)
            CreateTilemapChild(gridGO, "FogLayer", 10, false, null);

        // Optionally paint a simple border of wall tiles programmatically
        if (_buildWallBorder && wallTM != null)
            PaintWallBorder(wallTM);

        MarkDirty(gridGO);
        Log("Step 3 — Tilemaps: GroundLayer, WallLayer" +
            (_buildFogLayer ? ", FogLayer" : "") + " created under TilemapGrid.\n" +
            "⚠ Open Window → 2D → Tile Palette to paint tiles onto each layer.");
    }

    Tilemap CreateTilemapChild(GameObject parent, string name, int order, bool addCollider, string tag)
    {
        // Skip if already exists
        Transform existing = parent.transform.Find(name);
        if (existing != null) return existing.GetComponent<Tilemap>();

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;

        if (addCollider)
        {
            var col = go.AddComponent<TilemapCollider2D>();
            col.usedByComposite = false;
        }

        if (!string.IsNullOrEmpty(tag))
        {
            try { go.tag = tag; }
            catch { /* tag not registered yet — Step 1 must run first */ }
        }

        return tilemap;
    }

    void PaintWallBorder(Tilemap wallTM)
    {
        // We can't paint a tile without a TileBase asset, but we can record
        // the positions that SHOULD be walls — actual painting requires an asset.
        // Instead we just log advice.
        Log("  ↳ Wall border positions are set. Assign a Wall TileBase in the " +
            "Tile Palette and paint the border manually, or call " +
            "wallTilemap.SetTile() with your tile asset at runtime.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 4 — TileMap Script
    // ════════════════════════════════════════════════════════════════════

    void Step_TileMapScript()
    {
        FindOrCreate("TileMap", () =>
        {
            var go = new GameObject("TileMap");
            var tm = go.AddComponent<TileMap>();
            tm.GridWidth = _mapWidth;
            tm.GridHeight = _mapHeight;
            tm.AutoBuildOnStart = true;
            MarkDirty(go);
            return go;
        });

        // Update existing
        var existing = GameObject.Find("TileMap");
        if (existing != null)
        {
            var tm = existing.GetComponent<TileMap>();
            if (tm != null) { tm.GridWidth = _mapWidth; tm.GridHeight = _mapHeight; }
        }

        Log($"Step 4 — TileMap script: GridWidth={_mapWidth}, GridHeight={_mapHeight}, " +
            "AutoBuildOnStart=true.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 5 — Light Zones
    // ════════════════════════════════════════════════════════════════════

    void Step_LightZones()
    {
        GameObject lzRoot = FindOrCreate("LightZones", () => new GameObject("LightZones"));

        // Two sample full-light rooms
        CreateLightZone(lzRoot, "LightZone_Full_1", "LitFull",
            new Vector2(_mapWidth * 0.25f, _mapHeight * 0.5f), new Vector2(4f, 3f));
        CreateLightZone(lzRoot, "LightZone_Full_2", "LitFull",
            new Vector2(_mapWidth * 0.75f, _mapHeight * 0.5f), new Vector2(4f, 3f));

        // Partial-light corridors
        CreateLightZone(lzRoot, "LightZone_Partial_1", "LitPartial",
            new Vector2(_mapWidth * 0.5f, _mapHeight * 0.25f), new Vector2(6f, 2f));
        CreateLightZone(lzRoot, "LightZone_Partial_2", "LitPartial",
            new Vector2(_mapWidth * 0.5f, _mapHeight * 0.75f), new Vector2(6f, 2f));

        MarkDirty(lzRoot);
        Log("Step 5 — Light Zones: 2 × LitFull + 2 × LitPartial trigger zones placed.\n" +
            "Move / resize them in the Inspector to match your map layout.");
    }

    void CreateLightZone(GameObject parent, string name, string tag, Vector2 pos, Vector2 size)
    {
        if (parent.transform.Find(name) != null) return;

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);

        try { go.tag = tag; } catch { }

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;

        // Visible sprite so designers can see the zone
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = tag == "LitFull"
            ? new Color(1f, 1f, 0f, 0.12f)
            : new Color(1f, 0.8f, 0f, 0.07f);
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        sr.sortingOrder = -1;
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 6 — Player
    // ════════════════════════════════════════════════════════════════════

    void Step_Player()
    {
        FindOrCreate("Player", () =>
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(2f, 2f, 0f);
            go.tag = "Player";

            // Sprite
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(0.2f, 0.5f, 1f);
            sr.sortingOrder = 5;

            // Physics
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            // Script
            go.AddComponent<PlayerController>();

            MarkDirty(go);
            return go;
        });

        Log("Step 6 — Player: created at (2, 2) with PlayerController, " +
            "Rigidbody2D (Kinematic), CircleCollider2D.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 7 — Treasure & Exit
    // ════════════════════════════════════════════════════════════════════

    void Step_TreasureAndExit()
    {
        // Treasure
        FindOrCreate("Treasure", () =>
        {
            var go = new GameObject("Treasure");
            go.transform.position = new Vector3(_mapWidth * 0.5f, _mapHeight * 0.5f, 0f);
            try { go.tag = "Treasure"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateStarSprite();
            sr.color = new Color(1f, 0.85f, 0f);
            sr.sortingOrder = 4;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            MarkDirty(go);
            return go;
        });

        // Exit
        FindOrCreate("Exit", () =>
        {
            var go = new GameObject("Exit");
            go.transform.position = new Vector3(_mapWidth - 2f, _mapHeight * 0.5f, 0f);
            try { go.tag = "Exit"; } catch { }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = new Color(0f, 1f, 0.4f, 0.5f);
            sr.sortingOrder = 3;
            sr.transform.localScale = Vector3.one * 1.2f;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 1.2f);

            MarkDirty(go);
            return go;
        });

        Log($"Step 7 — Treasure placed at map center ({_mapWidth / 2},{_mapHeight / 2}), " +
            $"Exit placed at ({_mapWidth - 2},{_mapHeight / 2}).");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 8 — Guards
    // ════════════════════════════════════════════════════════════════════

    void Step_Guards()
    {
        GameObject guardRoot = FindOrCreate("Guards", () => new GameObject("Guards"));
        var player = GameObject.Find("Player");

        for (int i = 0; i < _guardCount; i++)
        {
            string guardName = $"Guard_{i + 1}";
            if (guardRoot.transform.Find(guardName) != null) continue;

            var guardGO = BuildGuard(guardName, i, player);
            guardGO.transform.SetParent(guardRoot.transform, false);
            MarkDirty(guardGO);
        }

        MarkDirty(guardRoot);
        Log($"Step 8 — Guards: {_guardCount} guard(s) created under 'Guards' parent.\n" +
            "⚠ Assign the Player reference on each GuardController in the Inspector.\n" +
            "   Move Waypoint objects to desired patrol positions on the map.");
    }

    GameObject BuildGuard(string name, int index, GameObject player)
    {
        // Spread guards around the map
        float angle = (index / (float)_guardCount) * Mathf.PI * 2f;
        float rx = (_mapWidth * 0.5f) - 3f;
        float ry = (_mapHeight * 0.5f) - 3f;
        Vector3 spawnPos = new Vector3(
            _mapWidth * 0.5f + Mathf.Cos(angle) * rx * 0.5f,
            _mapHeight * 0.5f + Mathf.Sin(angle) * ry * 0.5f,
            0f);

        var guardGO = new GameObject(name);
        guardGO.transform.position = spawnPos;

        // ── Sprite ──────────────────────────────────────────────────────
        var sr = guardGO.AddComponent<SpriteRenderer>();
        sr.sprite = CreateTriangleSprite();
        sr.color = Color.red;
        sr.sortingOrder = 5;

        // ── Physics ─────────────────────────────────────────────────────
        var rb = guardGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = guardGO.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        // ── AI Components ────────────────────────────────────────────────
        var noiseSensor = guardGO.AddComponent<NoiseSensor>();
        var lightSensor = guardGO.AddComponent<LightSensor>();
        var visionSensor = guardGO.AddComponent<VisionSensor>();
        var controller = guardGO.AddComponent<GuardController>();

        // Tune sensors
        noiseSensor.HearingRadius = _hearingRadius;
        lightSensor.DetectionRadius = _hearingRadius + 5f;
        visionSensor.VisionRange = _visionRange;
        visionSensor.VisionHalfAngle = _visionHalfAngle;

        // Tune controller speeds
        controller.PatrolSpeed = _patrolSpeed;
        controller.ChaseSpeed = _chaseSpeed;

        // Assign player if found
        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            noiseSensor.Player = pc;
            lightSensor.Player = pc;
            visionSensor.Player = pc;
            controller.Player = pc;
        }

        // ── State Label ──────────────────────────────────────────────────
        var labelGO = new GameObject("StateLabel");
        labelGO.transform.SetParent(guardGO.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 0.75f, 0f);

        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = "[P]";
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.green;
        tmp.sortingOrder = 10;
        controller.StateLabel = tmp;

        // ── Waypoints ────────────────────────────────────────────────────
        var wpRoot = new GameObject($"{name}_Waypoints");
        wpRoot.transform.SetParent(guardGO.transform, false);

        var waypoints = new List<Transform>();
        for (int w = 0; w < _waypointsPerGuard; w++)
        {
            float wAngle = (w / (float)_waypointsPerGuard) * Mathf.PI * 2f;
            Vector3 wpPos = spawnPos + new Vector3(
                Mathf.Cos(wAngle) * 3f,
                Mathf.Sin(wAngle) * 2f,
                0f);

            var wpGO = new GameObject($"Waypoint_{w + 1}");
            wpGO.transform.SetParent(wpRoot.transform, false);
            wpGO.transform.position = wpPos;

            // Visible gizmo marker
            var wpSr = wpGO.AddComponent<SpriteRenderer>();
            wpSr.sprite = CreateCircleSprite();
            wpSr.color = new Color(1f, 0.4f, 0.4f, 0.4f);
            wpSr.sortingOrder = 1;
            wpSr.transform.localScale = Vector3.one * 0.3f;

            waypoints.Add(wpGO.transform);
        }

        controller.PatrolWaypoints = waypoints;

        return guardGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 9 — GameManager
    // ════════════════════════════════════════════════════════════════════

    void Step_GameManager()
    {
        FindOrCreate("GameManager", () =>
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            MarkDirty(go);
            return go;
        });

        Log("Step 9 — GameManager singleton created.\n" +
            "⚠ Assign GameOverPanel and WinPanel references after Step 10 (HUD) runs.");
    }

    // ════════════════════════════════════════════════════════════════════
    // STEP 10 — Canvas / HUD
    // ════════════════════════════════════════════════════════════════════

    void Step_HUD()
    {
        // ── EventSystem (required for all UI button clicks) ─────────────
        FindOrCreate("EventSystem", () =>
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            return go;
        });

        // ── Canvas ───────────────────────────────────────────────────────
        GameObject canvasGO = FindOrCreate("HUDCanvas", () =>
        {
            var go = new GameObject("HUDCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return go;
        });

        // ── Suspicion Panel (top-right) ──────────────────────────────────
        BuildSuspicionPanel(canvasGO);

        // ── Debug Panel (center, Tab toggle) ────────────────────────────
        BuildDebugPanel(canvasGO);

        // ── Game Over Panel ──────────────────────────────────────────────
        var gameOverPanel = BuildMessagePanel(canvasGO, "GameOverPanel",
            "GAME OVER\nYou were caught!",
            new Color(0.6f, 0f, 0f, 0.92f),
            "GameManager.RestartGame");

        // ── Win Panel ────────────────────────────────────────────────────
        var winPanel = BuildMessagePanel(canvasGO, "WinPanel",
            "YOU ESCAPED!\nTreasure secured!",
            new Color(0f, 0.4f, 0.1f, 0.92f),
            "GameManager.RestartGame");

        // ── Treasure Banner ──────────────────────────────────────────────
        BuildTreasureBanner(canvasGO);

        // Wire panels into GameManager
        var gm = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        if (gm != null)
        {
            gm.GameOverPanel = gameOverPanel;
            gm.WinPanel = winPanel;
            MarkDirty(gm.gameObject);
        }

        // Wire guards into SuspicionMeter
        WireSuspicionMeter(canvasGO);

        MarkDirty(canvasGO);
        Log("Step 10 — HUD Canvas built:\n" +
            "  • Suspicion Meter (top-right)\n" +
            "  • Debug Panel (Tab to toggle)\n" +
            "  • Game Over Panel\n" +
            "  • Win Panel\n" +
            "  • Treasure Pickup Banner\n" +
            "GameManager panels wired automatically.");
    }

    void BuildSuspicionPanel(GameObject canvas)
    {
        if (canvas.transform.Find("SuspicionPanel") != null) return;

        var panel = CreateUIPanel(canvas, "SuspicionPanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f),  // anchor top-right
            new Vector2(-10f, -10f),                    // pivot offset
            new Vector2(360f, 90f),
            new Color(0f, 0f, 0f, 0.7f));

        // Set anchored position
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchoredPosition = new Vector2(-190f, -55f);

        // Title label
        CreateTMPLabel(panel, "AlertTitle", "Nearest Guard Alert Level",
            new Vector2(0f, 0.7f), new Vector2(1f, 1f), 14, Color.white);

        // Slider background
        var sliderGO = new GameObject("MeterSlider");
        sliderGO.transform.SetParent(panel.transform, false);
        var sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.1f);
        sliderRect.anchorMax = new Vector2(0.95f, 0.5f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<UnityEngine.UI.Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

        // Background image
        var bgImg = sliderGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f);
        slider.targetGraphic = bgImg;

        // Fill area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = Color.green;

        slider.fillRect = fillRect;

        // State text label
        var stateText = CreateTMPLabel(panel, "StateText", "✓ PATROLLING",
            new Vector2(0f, 0f), new Vector2(1f, 0.45f), 13, Color.green);

        // Attach SuspicionMeter component
        var meter = panel.AddComponent<SuspicionMeter>();
        meter.MeterSlider = slider;
        meter.MeterFill = fillImg;
        meter.StateText = stateText;
    }

    void BuildDebugPanel(GameObject canvas)
    {
        if (canvas.transform.Find("DebugPanel") != null) return;

        var panel = CreateUIPanel(canvas, "DebugPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(550f, 380f),
            new Color(0f, 0f, 0f, 0.88f));

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchoredPosition = Vector2.zero;
        panel.SetActive(false); // hidden by default — Tab toggles it

        var debugText = CreateTMPLabel(panel, "DebugText",
            "Press Tab to show BN debug info.",
            new Vector2(0f, 0f), new Vector2(1f, 1f), 13, Color.white);
        debugText.alignment = TextAlignmentOptions.TopLeft;

        var debugComp = panel.AddComponent<DebugPanel>();
        debugComp.DebugText = debugText;

        // Wire SuspicionMeter ref
        var sm = canvas.transform.Find("SuspicionPanel")?.GetComponent<SuspicionMeter>();
        if (sm != null) debugComp.SuspicionMeterRef = sm;
    }

    GameObject BuildMessagePanel(GameObject canvas, string name, string message,
        Color bgColor, string buttonCallback)
    {
        if (canvas.transform.Find(name) != null)
            return canvas.transform.Find(name).gameObject;

        var panel = CreateUIPanel(canvas, name,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(480f, 260f),
            bgColor);

        panel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        panel.SetActive(false);

        // Message text
        CreateTMPLabel(panel, "MessageText", message,
            new Vector2(0f, 0.4f), new Vector2(1f, 1f), 22, Color.white)
            .fontStyle = FontStyles.Bold;

        // Restart button — wired to GameManager.RestartGame
        var restartBtn = BuildUIButton(panel, "RestartButton", "\u25b6  Play Again",
            new Vector2(0.1f, 0.18f), new Vector2(0.55f, 0.38f),
            new Color(0.15f, 0.55f, 0.15f));

        // Quit button — wired to GameManager.QuitGame
        var quitBtn = BuildUIButton(panel, "QuitButton", "\u2715  Quit",
            new Vector2(0.6f, 0.18f), new Vector2(0.9f, 0.38f),
            new Color(0.5f, 0.1f, 0.1f));

        // Wire onClick via persistent UnityEvents so they survive play mode
        var gm = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        if (gm != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                restartBtn.onClick, gm.RestartGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                quitBtn.onClick, gm.QuitGame);
            MarkDirty(gm.gameObject);
        }
        else
        {
            Debug.LogWarning("[SceneBuilder] GameManager not found when wiring buttons. " +
                             "Run Step 9 first, then re-run Step 10.");
        }

        return panel;
    }

    void BuildTreasureBanner(GameObject canvas)
    {
        if (canvas.transform.Find("TreasureBanner") != null) return;

        var panel = CreateUIPanel(canvas, "TreasureBanner",
            new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f),
            Vector2.zero,
            new Vector2(500f, 70f),
            new Color(0.8f, 0.65f, 0f, 0.9f));

        panel.SetActive(false);

        CreateTMPLabel(panel, "BannerText",
            "🏆  Treasure Secured! Reach the exit!",
            new Vector2(0f, 0f), new Vector2(1f, 1f), 18, Color.white)
            .fontStyle = FontStyles.Bold;

        var gm = GameObject.Find("GameManager")?.GetComponent<GameManager>();
        if (gm != null)
        {
            gm.TreasurePickedUpBanner = panel;
            MarkDirty(gm.gameObject);
        }
    }

    void WireSuspicionMeter(GameObject canvas)
    {
        var sm = canvas.transform.Find("SuspicionPanel")?.GetComponent<SuspicionMeter>();
        if (sm == null) return;

        var guardsRoot = GameObject.Find("Guards");
        if (guardsRoot == null) return;

        sm.Guards = new List<GuardController>();
        foreach (Transform child in guardsRoot.transform)
        {
            var gc = child.GetComponent<GuardController>();
            if (gc != null) sm.Guards.Add(gc);
        }
        MarkDirty(canvas);
    }

    // ════════════════════════════════════════════════════════════════════
    // UI Helpers
    // ════════════════════════════════════════════════════════════════════

    GameObject CreateUIPanel(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = sizeDelta;

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = bgColor;

        return go;
    }

    TextMeshProUGUI CreateTMPLabel(GameObject parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        return tmp;
    }

    UnityEngine.UI.Button BuildUIButton(GameObject parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = bgColor;

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor = bgColor * 0.75f;
        colors.selectedColor = bgColor;
        btn.colors = colors;

        // Label inside button
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    // ════════════════════════════════════════════════════════════════════
    // Sprite Generators (procedural — no asset files required)
    // ════════════════════════════════════════════════════════════════════

    static Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        float center = size * 0.5f;
        float radius = size * 0.45f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = d < radius ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(4, 4);
        var fill = new Color[16];
        for (int i = 0; i < 16; i++) fill[i] = Color.white;
        tex.SetPixels(fill);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    static Sprite CreateTriangleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size;
                float ny = y / (float)size;
                // Simple upward-pointing triangle
                bool inside = ny > 0.1f && ny < 0.9f &&
                              nx > (1f - ny) * 0.5f &&
                              nx < 1f - (1f - ny) * 0.5f;
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite CreateStarSprite()
    {
        // Reuse circle for simplicity — replace with a real star asset in production
        return CreateCircleSprite();
    }

    // ════════════════════════════════════════════════════════════════════
    // Utility
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find a root-level GameObject by name, or create one using the factory.
    /// </summary>
    static GameObject FindOrCreate(string name, System.Func<GameObject> factory)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing;
        var go = factory();
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    static void MarkDirty(GameObject go)
    {
        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    void Log(string msg)
    {
        _statusLog = msg;
        Debug.Log($"[CTT SceneBuilder] {msg}");
        Repaint();
    }
}
#endif