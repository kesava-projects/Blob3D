using UnityEngine;

/// <summary>
/// Physical gate panel. Slides upward to open (blobs can walk under it),
/// slides back down to close. Colour gradually shifts from red → green as
/// it opens. Requires a kinematic Rigidbody for smooth physics-safe movement.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Gate : MonoBehaviour
{
    [Header("Settings")]
    public float openHeight = 3.5f;   // Units to rise when opened
    public float speed      = 5f;     // Units per second

    [Header("Colors")]
    public Color closedColor = new Color(1f,  0.15f, 0.15f);
    public Color openColor   = new Color(0.1f, 1f,  0.1f);

    private Vector3  closedPos;
    private Vector3  openPos;
    private bool     isOpen = false;
    private Rigidbody rb;
    private Renderer  rend;
    private MaterialPropertyBlock mpb;

    public bool IsOpen => isOpen;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        rb            = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        rend      = GetComponent<Renderer>();
        mpb       = new MaterialPropertyBlock();
        closedPos = transform.position;
        openPos   = closedPos + Vector3.up * openHeight;

        RefreshColor(0f);
    }

    void FixedUpdate()
    {
        Vector3 target = isOpen ? openPos : closedPos;
        rb.MovePosition(Vector3.MoveTowards(
            transform.position, target, speed * Time.fixedDeltaTime));

        // Colour feedback: 0 = fully closed, 1 = fully open
        float t = (transform.position.y - closedPos.y) / openHeight;
        RefreshColor(Mathf.Clamp01(t));
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Open()  => isOpen = true;
    public void Close() => isOpen = false;

    // ── Helpers ────────────────────────────────────────────────────────────

    void RefreshColor(float t)
    {
        if (rend == null) return;
        Color c = Color.Lerp(closedColor, openColor, t);
        mpb.SetColor("_Color",         c);
        mpb.SetColor("_EmissionColor", c * 0.35f);
        rend.SetPropertyBlock(mpb);
    }
}
