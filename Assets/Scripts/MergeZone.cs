using System.Collections;
using UnityEngine;

/// <summary>
/// Two blobs within <see cref="mergeProximityDistance"/> auto-unite (anywhere). After <b>X</b> split,
/// they must move apart before another unite (hysteresis). Win if unite started on the pad, or if a
/// <b>merged</b> blob enters the pad. <see cref="BlobController.RefreshMovementBaseline"/> keeps
/// merged movement/jump aligned with scale.
/// </summary>
public class MergeZone : MonoBehaviour
{
    public static MergeZone Instance { get; private set; }

    [Header("Settings")]
    public float mergeDuration = 1.4f;
    [Tooltip("Max horizontal distance between blob centers to auto-unite.")]
    public float mergeProximityDistance = 1.35f;
    [Tooltip("Horizontal distance from this transform’s center for the final zone. 0 = localScale.x × 0.5.")]
    public float finalZoneRadius = 0f;
    [Tooltip("After split, blob centers must exceed mergeProximity × this before another unite.")]
    public float separationHysteresis = 1.45f;
    [Tooltip("Extra meters added to the yellow pad’s detected radius (renderer bounds) for win checks.")]
    public float winZonePadding = 0.35f;

    private BlobController blobOne;
    private BlobController blobTwo;

    private bool merging;
    private bool blobsMerged;
    private bool autoMergeArmed = true;

    private Vector3 savedPos1, savedPos2, savedScale1, savedScale2;

    private Renderer              rend;
    private MaterialPropertyBlock mpb;
    private static readonly Color kBaseEmit = new Color(1f, 1f, 0f) * 0.6f;

    void Awake()
    {
        Instance = this;
        rend = GetComponent<Renderer>();
        mpb  = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

    float FinalZoneRadiusWorld()
    {
        return finalZoneRadius > 0.01f ? finalZoneRadius : transform.localScale.x * 0.5f;
    }

    /// <summary>XZ win area from the yellow mesh’s world bounds (matches what you see), not only transform math.</summary>
    void GetWinZoneXZ(out Vector2 centerXZ, out float radiusXZ)
    {
        if (rend != null)
        {
            Bounds b = rend.bounds;
            centerXZ = new Vector2(b.center.x, b.center.z);
            radiusXZ = Mathf.Max(b.extents.x, b.extents.z) + winZonePadding;
        }
        else
        {
            Vector3 p = transform.position;
            centerXZ = new Vector2(p.x, p.z);
            radiusXZ = FinalZoneRadiusWorld() + winZonePadding;
        }
    }

    bool BlobCenterInWinZoneXZ(Vector3 worldPos)
    {
        GetWinZoneXZ(out Vector2 c, out float r);
        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        return Vector2.Distance(p, c) <= r;
    }

    void Update()
    {
        if (rend != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            mpb.SetColor("_EmissionColor", kBaseEmit * pulse);
            rend.SetPropertyBlock(mpb);
        }

        // Win: merged blob’s center enters the yellow pad (uses renderer bounds so it matches the mesh).
        if (blobOne != null && blobsMerged && !merging && blobOne.gameObject.activeSelf)
        {
            if (BlobCenterInWinZoneXZ(blobOne.transform.position))
                GameManager.Instance?.OnLevelComplete();
        }

        if (merging || blobOne == null || blobTwo == null) return;
        if (!blobOne.gameObject.activeSelf || !blobTwo.gameObject.activeSelf) return;

        float dist = FlatDist(blobOne.transform.position, blobTwo.transform.position);
        if (!autoMergeArmed)
        {
            if (dist > mergeProximityDistance * separationHysteresis)
                autoMergeArmed = true;
            return;
        }

        if (dist > mergeProximityDistance) return;

        bool completeLevel = BothBlobsInFinalZone();
        StartCoroutine(DoMerge(completeLevel));
    }

    bool BothBlobsInFinalZone()
    {
        return BlobCenterInWinZoneXZ(blobOne.transform.position)
            && BlobCenterInWinZoneXZ(blobTwo.transform.position);
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    IEnumerator DoMerge(bool triggerLevelComplete)
    {
        merging = true;

        savedPos1   = blobOne.transform.position;
        savedPos2   = blobTwo.transform.position;
        savedScale1 = blobOne.transform.localScale;
        savedScale2 = blobTwo.transform.localScale;

        blobOne.SetControlled(false);
        blobTwo.SetControlled(false);

        var rb1 = blobOne.GetComponent<Rigidbody>();
        var rb2 = blobTwo.GetComponent<Rigidbody>();
        if (rb1 != null) { rb1.linearVelocity = Vector3.zero; rb1.isKinematic = true; }
        if (rb2 != null) { rb2.linearVelocity = Vector3.zero; rb2.isKinematic = true; }

        Vector3 startA     = savedPos1;
        Vector3 startB     = savedPos2;
        Vector3 mergePoint = (startA + startB) * 0.5f;

        Vector3 scaleA   = savedScale1;
        Vector3 scaleB   = savedScale2;
        Vector3 bigScale = scaleA * 1.5f;

        float elapsed = 0f;
        while (elapsed < mergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / mergeDuration);

            blobOne.transform.position = Vector3.Lerp(startA, mergePoint, t);
            blobTwo.transform.position = Vector3.Lerp(startB, mergePoint, t);
            blobTwo.transform.localScale = Vector3.Lerp(scaleB, Vector3.zero, t);
            blobOne.transform.localScale = Vector3.Lerp(scaleA, bigScale, t);

            yield return null;
        }

        // Restore blob 2 scale before hiding so inactive object does not keep scale 0 (fixes X split).
        blobTwo.transform.localScale = savedScale2;
        blobTwo.gameObject.SetActive(false);

        if (rb1 != null) rb1.isKinematic = false;
        if (rb2 != null) rb2.isKinematic = false;

        blobOne.SetControlled(true);
        blobOne.RefreshMovementBaseline();

        merging     = false;
        blobsMerged = true;

        if (triggerLevelComplete)
            GameManager.Instance?.OnLevelComplete();
    }

    /// <summary><b>X</b> — split blobs; auto-unite stays off until they move apart.</summary>
    public void TrySplit()
    {
        if (!blobsMerged || merging || blobOne == null || blobTwo == null) return;

        GameManager.Instance?.CancelPendingReloadAndWin();

        blobTwo.gameObject.SetActive(true);
        blobOne.transform.position   = savedPos1;
        blobTwo.transform.position   = savedPos2;
        blobOne.transform.localScale = savedScale1;
        blobTwo.transform.localScale = savedScale2;

        var rb1 = blobOne.GetComponent<Rigidbody>();
        var rb2 = blobTwo.GetComponent<Rigidbody>();
        if (rb1 != null)
        {
            rb1.linearVelocity = Vector3.zero;
            rb1.angularVelocity = Vector3.zero;
        }
        if (rb2 != null)
        {
            rb2.linearVelocity = Vector3.zero;
            rb2.angularVelocity = Vector3.zero;
        }

        blobOne.RefreshMovementBaseline();
        blobTwo.RefreshMovementBaseline();

        blobsMerged   = false;
        autoMergeArmed = false;
        BlobManager.Instance?.ReapplyControlMode();
    }

    public bool IsMerged => blobsMerged;
}
