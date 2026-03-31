using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks overall game state. Displays an OnGUI HUD showing the active
/// control mode and a WIN splash when both blobs are reunited.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Timer")]
    [Tooltip("Time available to finish the level before game over.")]
    public float levelTimeLimitSeconds = 120f;
    [Tooltip("Delay before auto-reloading after win or time up.")]
    public float endScreenDuration = 3.5f;

    [Header("Tutorial")]
    [Tooltip("Show a tutorial overlay before gameplay starts.")]
    public bool showTutorialBeforeStart = true;

    private bool complete = false;
    private bool gameOver = false;
    private bool tutorialVisible = false;
    private bool gameplayStarted = false;
    private float remainingTime = 0f;

    // Cached texture for semi-transparent boxes
    private Texture2D bgTex;
    private Texture2D panelTex;
    private Texture2D accentTex;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else                  Destroy(gameObject);

        bgTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));
        panelTex = MakeTex(new Color(0.06f, 0.09f, 0.14f, 0.93f));
        accentTex = MakeTex(new Color(0.12f, 0.95f, 0.95f, 1f));
        remainingTime = Mathf.Max(1f, levelTimeLimitSeconds);
        tutorialVisible = showTutorialBeforeStart;
        gameplayStarted = !tutorialVisible;
    }

    void Update()
    {
        if (tutorialVisible)
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                tutorialVisible = false;
                gameplayStarted = true;
            }
            return;
        }

        if (!CanAcceptPlayerInput()) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            OnTimeExpired();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void OnLevelComplete()
    {
        if (complete || gameOver || !gameplayStarted) return;
        complete = true;
        Debug.Log("[Blob3D] Blobs reunited – Level complete!");
        Invoke(nameof(Reload), endScreenDuration);
    }

    /// <summary>Used when splitting with X during the win countdown — abort reload and hide win UI.</summary>
    public void CancelPendingReloadAndWin()
    {
        if (!complete) return;
        CancelInvoke(nameof(Reload));
        complete = false;
    }

    public bool CanAcceptPlayerInput()
    {
        return gameplayStarted && !tutorialVisible && !complete && !gameOver;
    }

    // ── GUI ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        DrawHUD();
        if (tutorialVisible) DrawTutorialScreen();
        if (complete) DrawWinScreen();
        if (gameOver) DrawGameOverScreen();
    }

    void DrawHUD()
    {
        if (tutorialVisible || BlobManager.Instance == null) return;

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white, background = bgTex },
            padding   = new RectOffset(10, 10, 6, 6)
        };

        string mode = BlobManager.Instance.GetModeLabel();
        string timer = FormatTime(remainingTime);
        GUI.Box(new Rect(12f, 12f, 620f, 86f),
            $"Control: {mode}        Time Left: {timer}\n<size=12>[X] split/divide · [TAB] switch · [WASD] move · [SPACE] jump · approach to unite · reach exit door before time runs out</size>", boxStyle);
    }

    void DrawTutorialScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        float panelWidth = Mathf.Min(Screen.width * 0.9f, 980f);
        float panelHeight = Mathf.Min(Screen.height * 0.82f, 620f);
        float px = (Screen.width - panelWidth) * 0.5f;
        float py = (Screen.height - panelHeight) * 0.5f;

        var panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = panelTex },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(24, 24, 24, 24)
        };

        GUI.Box(new Rect(px, py, panelWidth, panelHeight), GUIContent.none, panelStyle);
        GUI.DrawTexture(new Rect(px + 18f, py + 16f, panelWidth - 36f, 6f), accentTex, ScaleMode.StretchToFill);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(panelWidth * 0.05f, 28f, 46f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.cyan }
        };

        var subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(panelWidth * 0.02f, 14f, 20f)),
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.86f, 0.94f, 1f, 0.96f) }
        };

        var sectionTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(panelWidth * 0.022f, 16f, 22f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.93f, 0.95f, 0.35f, 1f) }
        };

        var sectionBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(panelWidth * 0.019f, 13f, 18f)),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        var footerStyle = new GUIStyle
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(panelWidth * 0.03f, 18f, 30f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.yellow }
        };

        float contentX = px + 32f;
        float contentW = panelWidth - 64f;

        GUI.Label(
            new Rect(contentX, py + 28f, contentW, 62f),
            "HOW TO PLAY",
            titleStyle);

        GUI.Label(
            new Rect(contentX + 20f, py + 86f, contentW - 40f, 56f),
            "Guide both blob forms through the facility, open gates, and reach the exit before the countdown ends.",
            subtitleStyle);

        float sectionTop = py + 162f;
        float sectionGap = 18f;
        float sectionWidth = (contentW - sectionGap) * 0.5f;

        GUI.Label(new Rect(contentX, sectionTop, sectionWidth, 28f), "MISSION", sectionTitleStyle);
        GUI.Label(
            new Rect(contentX, sectionTop + 30f, sectionWidth, 128f),
            "- Reach the exit door before time runs out.\n- Use pressure buttons to open matching gates.\n- Keep moving: time is always counting down.",
            sectionBodyStyle);

        GUI.Label(new Rect(contentX + sectionWidth + sectionGap, sectionTop, sectionWidth, 28f), "CONTROLS", sectionTitleStyle);
        GUI.Label(
            new Rect(contentX + sectionWidth + sectionGap, sectionTop + 30f, sectionWidth, 172f),
            "[W / S] Move forward or backward\n[A / D] Rotate\n[SPACE] Jump\n[TAB] Switch active blob mode\n[X] Split when merged",
            sectionBodyStyle);

        float tipsTop = sectionTop + 206f;
        GUI.Label(new Rect(contentX, tipsTop, contentW, 28f), "QUICK TIPS", sectionTitleStyle);
        GUI.Label(
            new Rect(contentX, tipsTop + 30f, contentW, 74f),
            "- Bring blobs close together to merge.\n- Split and switch control to solve multi-gate paths.\n- If stuck, look for a button that matches a blocked door.",
            sectionBodyStyle);

        float pulse = 0.78f + 0.22f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 3.2f));
        footerStyle.normal.textColor = new Color(1f, 0.93f, 0.2f, pulse);

        GUI.Label(
            new Rect(contentX, py + panelHeight - 62f, contentW, 36f),
            "Press ENTER or SPACE to start",
            footerStyle);
    }

    void DrawWinScreen()
    {
        // Full-screen dim
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        var titleStyle = new GUIStyle
        {
            fontSize  = 52,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.yellow }
        };
        var subStyle = new GUIStyle
        {
            fontSize  = 22,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(1f, 1f, 1f, 0.8f) }
        };

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.Label(new Rect(cx - 300f, cy - 60f, 600f, 70f), "ESCAPED!",  titleStyle);
        GUI.Label(new Rect(cx - 300f, cy + 20f, 600f, 40f), "Level complete - reloading...", subStyle);
    }

    void DrawGameOverScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        var titleStyle = new GUIStyle
        {
            fontSize = 52,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.4f, 0.2f) }
        };
        var subStyle = new GUIStyle
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
        };

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.Label(new Rect(cx - 300f, cy - 60f, 600f, 70f), "TIME UP", titleStyle);
        GUI.Label(new Rect(cx - 300f, cy + 20f, 600f, 40f), "Game over - reloading...", subStyle);
    }

    void OnTimeExpired()
    {
        if (complete || gameOver) return;

        gameOver = true;
        Debug.Log("[Blob3D] Time expired - game over.");
        Invoke(nameof(Reload), endScreenDuration);
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int mins = total / 60;
        int secs = total % 60;
        return $"{mins:00}:{secs:00}";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    static Texture2D MakeTex(Color col)
    {
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { col, col, col, col });
        tex.Apply();
        return tex;
    }
}
