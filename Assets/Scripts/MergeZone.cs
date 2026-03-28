using System.Collections;
using UnityEngine;

/// <summary>
/// Glowing end-zone. Checks every frame whether both blobs are within the
/// zone radius. When they are, the merge animation plays:
/// Blob 2 shrinks into Blob 1, Blob 1 swells, level complete fires.
/// </summary>
public class MergeZone : MonoBehaviour
{
    [Header("Settings")]
    public float mergeDuration = 1.4f;

    private BlobController blobOne;
    private BlobController blobTwo;

    private bool merging = false;

    // Pulsing emission
    private Renderer              rend;
    private MaterialPropertyBlock mpb;
    private static readonly Color kBaseEmit = new Color(1f, 1f, 0f) * 0.6f;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb  = new MaterialPropertyBlock();
    }

    void Start()
    {
        var bm = BlobManager.Instance != null
            ? BlobManager.Instance
            : Object.FindAnyObjectByType<BlobManager>();

        if (bm != null)
        {
            blobOne = bm.blobOne;
            blobTwo = bm.blobTwo;
        }
    }

    void Update()
    {
        // ─ Pulse emission ──────────────────────────────────────────
        if (rend != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            mpb.SetColor("_EmissionColor", kBaseEmit * pulse);
            rend.SetPropertyBlock(mpb);
        }

        // ─ Proximity merge check ─────────────────────────────────
        if (merging || blobOne == null || blobTwo == null) return;

        // Zone radius in world space (localScale.x is diameter for a cylinder)
        float zoneRadius = transform.localScale.x * 0.5f;

        bool oneInZone = FlatDist(blobOne.transform.position, transform.position) < zoneRadius;
        bool twoInZone = FlatDist(blobTwo.transform.position, transform.position) < zoneRadius;

        if (oneInZone && twoInZone)
            StartCoroutine(DoMerge());
    }

    // Returns XZ-plane distance (ignore Y so jumping blobs still register)
    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ── Merge animation ────────────────────────────────────────────────────

    IEnumerator DoMerge()
    {
        merging = true;

        // Stop all player input
        blobOne.SetControlled(false);
        blobTwo.SetControlled(false);

        // Make physics kinematic so we can drive transforms directly
        var rb1 = blobOne.GetComponent<Rigidbody>();
        var rb2 = blobTwo.GetComponent<Rigidbody>();
        if (rb1 != null) { rb1.linearVelocity = Vector3.zero; rb1.isKinematic = true; }
        if (rb2 != null) { rb2.linearVelocity = Vector3.zero; rb2.isKinematic = true; }

        Vector3 startA     = blobOne.transform.position;
        Vector3 startB     = blobTwo.transform.position;
        Vector3 mergePoint = (startA + startB) * 0.5f;

        Vector3 scaleA    = blobOne.transform.localScale;
        Vector3 scaleB    = blobTwo.transform.localScale;
        Vector3 bigScale  = scaleA * 1.5f;

        float elapsed = 0f;
        while (elapsed < mergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / mergeDuration);

            // Both converge on the midpoint
            blobOne.transform.position = Vector3.Lerp(startA, mergePoint, t);
            blobTwo.transform.position = Vector3.Lerp(startB, mergePoint, t);

            // Blob 2 shrinks to nothing; Blob 1 grows
            blobTwo.transform.localScale = Vector3.Lerp(scaleB, Vector3.zero, t);
            blobOne.transform.localScale = Vector3.Lerp(scaleA, bigScale,     t);

            yield return null;
        }

        blobTwo.gameObject.SetActive(false);
        GameManager.Instance?.OnLevelComplete();
    }
}
