using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks overall game state. Displays an OnGUI HUD showing the active
/// control mode and a WIN splash when both blobs are reunited.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private bool complete = false;

    // Cached texture for semi-transparent boxes
    private Texture2D bgTex;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else                  Destroy(gameObject);

        bgTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void OnLevelComplete()
    {
        if (complete) return;
        complete = true;
        Debug.Log("[Blob3D] Blobs reunited – Level complete!");
        Invoke(nameof(Reload), 3.5f);
    }

    // ── GUI ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        DrawHUD();
        if (complete) DrawWinScreen();
    }

    void DrawHUD()
    {
        if (BlobManager.Instance == null) return;

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white, background = bgTex },
            padding   = new RectOffset(10, 10, 6, 6)
        };

        string mode = BlobManager.Instance.GetModeLabel();
        GUI.Box(new Rect(12f, 12f, 380f, 48f),
            $"Control: {mode}\n<size=12>[TAB] cycle · [WASD] move · [SPACE] jump · [X] separate</size>", boxStyle);
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

        GUI.Label(new Rect(cx - 300f, cy - 60f, 600f, 70f), "BLOBS REUNITED!",  titleStyle);
        GUI.Label(new Rect(cx - 300f, cy + 20f, 600f, 40f), "Reloading level…", subStyle);
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
