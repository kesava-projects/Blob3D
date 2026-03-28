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
    [Tooltip("If true, the game starts with a single merged blob. Press X to split.")]
    public bool startMerged = false;
    public float mergeDuration = 1.4f;
    [Tooltip("Max horizontal distance between blob centers to auto-unite.")]
    public float mergeProximityDistance = 1.35f;
    [Tooltip("Extra multiplier on combined blob radii when auto-calculating merge distance.")]
    public float mergeRadiusPaddingMultiplier = 1.05f;
    [Tooltip("After split, blob centers must exceed mergeProximity × this before another unite.")]
    public float separationHysteresis = 1.45f;

    private BlobController blobOne;
    private BlobController blobTwo;

    private bool merging;
    private bool blobsMerged;
    private bool autoMergeArmed = true;

    private Vector3 savedPos1, savedPos2, savedScale1, savedScale2;

    void Awake()
    {
        Instance = this;
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

        if (startMerged && blobOne != null && blobTwo != null)
            InitMergedState();
    }

    /// <summary>Begin the level with a single merged blob. Player presses X to split.</summary>
    void InitMergedState()
    {
        savedScale1 = blobOne.transform.localScale;
        savedScale2 = blobTwo.transform.localScale;

        // Save positions with an offset so TrySplit produces a clean side-by-side split
        Vector3 pos = blobOne.transform.position;
        savedPos1 = pos + Vector3.left  * 1.2f;
        savedPos2 = pos + Vector3.right * 1.2f;

        // Hide blob 2, scale blob 1 up
        blobTwo.gameObject.SetActive(false);
        blobOne.transform.localScale = savedScale1 * 1.5f;
        blobOne.RefreshMovementBaseline();

        blobsMerged    = true;
        autoMergeArmed = false;
    }


    void Update()
    {
        if (merging || blobOne == null || blobTwo == null) return;
        if (!blobOne.gameObject.activeSelf || !blobTwo.gameObject.activeSelf) return;

        float dist = FlatDist(blobOne.transform.position, blobTwo.transform.position);
        float requiredMergeDistance = CurrentMergeDistance();
        if (!autoMergeArmed)
        {
            if (dist > requiredMergeDistance * separationHysteresis)
                autoMergeArmed = true;
            return;
        }

        if (dist > requiredMergeDistance) return;

        StartCoroutine(DoMerge());
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    float BlobWorldRadiusXZ(BlobController blob)
    {
        if (blob == null) return 0f;

        var sc = blob.GetComponent<SphereCollider>();
        if (sc != null)
        {
            float m = Mathf.Max(
                Mathf.Abs(blob.transform.lossyScale.x),
                Mathf.Abs(blob.transform.lossyScale.y),
                Mathf.Abs(blob.transform.lossyScale.z));
            return sc.radius * m;
        }

        var c = blob.GetComponent<Collider>();
        if (c != null)
            return Mathf.Max(c.bounds.extents.x, c.bounds.extents.z);

        return 0f;
    }

    float CurrentMergeDistance()
    {
        float r1 = BlobWorldRadiusXZ(blobOne);
        float r2 = BlobWorldRadiusXZ(blobTwo);
        float bySize = (r1 + r2) * Mathf.Max(1f, mergeRadiusPaddingMultiplier);
        return Mathf.Max(mergeProximityDistance, bySize);
    }

    IEnumerator DoMerge()
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
    }

    /// <summary><b>X</b> — split blobs; auto-unite stays off until they move apart.</summary>
    public void TrySplit()
    {
        if (!blobsMerged || merging || blobOne == null || blobTwo == null) return;

        GameManager.Instance?.CancelPendingReloadAndWin();

        // Calculate the offset between the original blob positions before merge
        Vector3 originalOffset = savedPos2 - savedPos1;

        // Current merged blob position
        Vector3 currentMergedPos = blobOne.transform.position;

        // Split both blobs based on current position and original offset
        Vector3 splitPos1 = currentMergedPos - originalOffset * 0.5f;
        Vector3 splitPos2 = currentMergedPos + originalOffset * 0.5f;

        // Set rigidbody states and positions BEFORE activating blob2
        var rb1 = blobOne.GetComponent<Rigidbody>();
        var rb2 = blobTwo.GetComponent<Rigidbody>();
        
        // Set blob2 kinematic temporarily while repositioning
        if (rb2 != null)
        {
            rb2.linearVelocity = Vector3.zero;
            rb2.angularVelocity = Vector3.zero;
            rb2.isKinematic = true;
        }
        
        // Set positions and scales BEFORE activation
        blobOne.transform.position   = splitPos1;
        blobTwo.transform.position   = splitPos2;
        blobOne.transform.localScale = savedScale1;
        blobTwo.transform.localScale = savedScale2;

        // Now activate blob2 (after positions are set)
        blobTwo.gameObject.SetActive(true);

        // Restore rigidbody to normal state
        if (rb1 != null)
        {
            rb1.linearVelocity = Vector3.zero;
            rb1.angularVelocity = Vector3.zero;
        }
        if (rb2 != null)
        {
            rb2.isKinematic = false;
        }

        blobOne.RefreshMovementBaseline();
        blobTwo.RefreshMovementBaseline();

        blobsMerged   = false;
        autoMergeArmed = false;
        BlobManager.Instance?.ReapplyControlMode();
    }

    public bool IsMerged => blobsMerged;
}
