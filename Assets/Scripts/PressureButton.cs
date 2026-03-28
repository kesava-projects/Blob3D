using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Flat trigger pad. Fires onPressed the first time an occupant enters,
/// fires onReleased when the last occupant exits.
/// Reacts to GameObjects tagged "Blob" or "Pushable" (a cube pushed onto it).
/// </summary>
public class PressureButton : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onPressed  = new UnityEvent();
    public UnityEvent onReleased = new UnityEvent();

    [Header("Colors")]
    public Color pressedColor  = new Color(0.1f, 1f, 0.1f);
    public Color releasedColor = new Color(1f, 0.15f, 0.15f);

    private int  occupants = 0;
    private bool pressed   = false;

    private Renderer              rend;
    private MaterialPropertyBlock mpb;

    public bool IsPressed => pressed;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb  = new MaterialPropertyBlock();
        Refresh();
    }

    // ── Trigger callbacks ──────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!IsOccupant(other)) return;

        occupants++;
        if (!pressed)
        {
            pressed = true;
            onPressed.Invoke();
            Refresh();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOccupant(other)) return;

        occupants = Mathf.Max(0, occupants - 1);
        if (occupants == 0 && pressed)
        {
            pressed = false;
            onReleased.Invoke();
            Refresh();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static bool IsOccupant(Collider c) =>
        c.CompareTag("Blob") || c.CompareTag("Pushable");

    void Refresh()
    {
        if (rend == null) return;
        mpb.SetColor("_Color",         pressed ? pressedColor  : releasedColor);
        mpb.SetColor("_EmissionColor", pressed ? pressedColor * 0.7f : releasedColor * 0.4f);
        rend.SetPropertyBlock(mpb);
    }
}
