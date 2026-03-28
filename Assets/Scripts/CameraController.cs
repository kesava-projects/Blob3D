using UnityEngine;

/// <summary>
/// Indoor-aware camera system with:
///   • TPP following individual blobs (uses avatar facing, not sphere forward)
///   • Combined dual-blob framing (Both mode)
///   • SphereCast wall avoidance — camera pulls in when obstructed
///   • Room-bounds clamping — never escapes the environment
///   • Smooth transitions between modes
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Targets")]
    public Transform blobOne;
    public Transform blobTwo;

    [Header("Individual Blob TPP")]
    public float tppHeightBase       = 3.5f;
    public float tppDistance          = 6f;
    public float tppLookHeightOffset = 0.8f;
    public float tppSmoothSpeed      = 7f;

    [Header("Combined View")]
    public float combinedHeightBase  = 4f;
    public float combinedZoomPerUnit = 0.5f;
    public float combinedMaxHeight   = 5f;
    public float combinedZOffset     = -8f;
    public float combinedSmoothSpeed = 5f;

    [Header("Velocity Prediction")]
    public float velocityLookaheadScale = 0.2f;
    public float maxLookaheadDistance   = 2.5f;

    [Header("Wall Avoidance")]
    [Tooltip("SphereCast radius for detecting walls between camera and target.")]
    public float wallProbeRadius = 0.25f;
    [Tooltip("Minimum distance the camera keeps from any surface.")]
    public float wallPadding     = 0.3f;
    [Tooltip("How quickly the camera pulls in when hitting a wall (higher = snappier).")]
    public float wallPullInSpeed = 12f;

    [Header("Room Bounds Clamping")]
    public bool clampToRoom = false;
    public Vector3 roomMin  = new Vector3(-28f, 0.5f, -38f);
    public Vector3 roomMax  = new Vector3(8f, 5.5f, -2f);

    // ── Private state ───────────────────────────────────────────────────
    private BlobManager blobManager;
    private Vector3 cameraVelocity = Vector3.zero;
    private float   currentPullIn  = 0f;          // 0 = full distance, 1 = fully pulled in
    private float   smoothYaw;                     // tracked facing for TPP
    private bool    yawInitialised = false;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        blobManager = BlobManager.Instance;
    }

    void LateUpdate()
    {
        if (blobOne == null || blobTwo == null) return;

        Vector3 targetPos;
        Vector3 lookAtPos;

        // Active blob (for merged/single blob state, only blobOne is active)
        Transform activePrimary = GetActivePrimary();

        if (blobManager != null && blobManager.Mode == ControlMode.OnlyFirst)
            ComputeTPP(blobOne, out targetPos, out lookAtPos);
        else if (blobManager != null && blobManager.Mode == ControlMode.OnlySecond)
            ComputeTPP(blobTwo, out targetPos, out lookAtPos);
        else if (activePrimary != null && !blobTwo.gameObject.activeSelf)
            ComputeTPP(activePrimary, out targetPos, out lookAtPos); // merged single blob
        else
            ComputeCombinedView(out targetPos, out lookAtPos);

        // ── Wall avoidance: SphereCast from look target to desired cam pos ──
        targetPos = WallAvoid(lookAtPos, targetPos);

        // ── Smooth follow ───────────────────────────────────────────────
        float smoothness = GetCurrentSmoothSpeed();
        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position, targetPos,
            ref cameraVelocity, 1f / smoothness);

        // ── Room bounds clamp ───────────────────────────────────────────
        if (clampToRoom)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, roomMin.x, roomMax.x);
            smoothed.y = Mathf.Clamp(smoothed.y, roomMin.y, roomMax.y);
            smoothed.z = Mathf.Clamp(smoothed.z, roomMin.z, roomMax.z);
        }

        // ── Secondary wall push-out (if final pos is inside geometry) ────
        smoothed = PushOutOfWalls(smoothed);

        transform.position = smoothed;

        // ── Look-at with gentle lookahead ───────────────────────────────
        Vector3 lookahead = cameraVelocity * velocityLookaheadScale;
        if (lookahead.sqrMagnitude > maxLookaheadDistance * maxLookaheadDistance)
            lookahead = lookahead.normalized * maxLookaheadDistance;

        transform.LookAt(lookAtPos + lookahead);
    }

    // ── TPP (uses avatar facing, not sphere forward) ────────────────────

    void ComputeTPP(Transform primary, out Vector3 camPos, out Vector3 lookPos)
    {
        Vector3 blobPos = primary.position;
        Vector3 facing  = GetBlobFacing(primary);

        // Camera: behind and above
        camPos  = blobPos - facing * tppDistance + Vector3.up * tppHeightBase;
        lookPos = blobPos + Vector3.up * tppLookHeightOffset;
    }

    /// <summary>
    /// Gets the blob’s actual facing direction from its avatar (Animator child),
    /// falling back to velocity-based tracking if no avatar exists.
    /// The sphere itself never rotates (FreezeRotation), so transform.forward
    /// is useless.
    /// </summary>
    Vector3 GetBlobFacing(Transform blob)
    {
        // 1. Try avatar’s forward
        var anim = blob.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            Vector3 f = anim.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f)
            {
                float targetYaw = Quaternion.LookRotation(f.normalized).eulerAngles.y;
                if (!yawInitialised) { smoothYaw = targetYaw; yawInitialised = true; }
                smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, Time.deltaTime * 5f);
                return Quaternion.Euler(0f, smoothYaw, 0f) * Vector3.forward;
            }
        }

        // 2. Try velocity
        var rb = blob.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 hvel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (hvel.sqrMagnitude > 0.5f)
            {
                float targetYaw = Quaternion.LookRotation(hvel.normalized).eulerAngles.y;
                if (!yawInitialised) { smoothYaw = targetYaw; yawInitialised = true; }
                smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, Time.deltaTime * 3f);
                return Quaternion.Euler(0f, smoothYaw, 0f) * Vector3.forward;
            }
        }

        // 3. Fallback: use last known yaw (or default)
        if (!yawInitialised) { smoothYaw = 0f; yawInitialised = true; }
        return Quaternion.Euler(0f, smoothYaw, 0f) * Vector3.forward;
    }

    // ── Combined view ───────────────────────────────────────────────────

    void ComputeCombinedView(out Vector3 camPos, out Vector3 lookPos)
    {
        Vector3 a = blobOne.position;
        Vector3 b = blobTwo.position;
        // Use full 3D centroid so camera follows when blobs are on different levels
        Vector3 centre = (a + b) * 0.5f;

        float separation = Vector3.Distance(
            new Vector3(a.x, 0f, a.z),
            new Vector3(b.x, 0f, b.z));
        float height = Mathf.Min(
            combinedHeightBase + separation * combinedZoomPerUnit,
            combinedMaxHeight);

        camPos  = centre + new Vector3(0f, height, combinedZOffset);
        lookPos = centre;
    }

    // ── Wall avoidance ──────────────────────────────────────────────────

    /// <summary>
    /// SphereCast from <paramref name="lookOrigin"/> toward <paramref name="desiredCamPos"/>.
    /// If geometry is in the way, pull the camera in front of the hit point.
    /// </summary>
    Vector3 WallAvoid(Vector3 lookOrigin, Vector3 desiredCamPos)
    {
        Vector3 dir  = desiredCamPos - lookOrigin;
        float   dist = dir.magnitude;
        if (dist < 0.01f) return desiredCamPos;

        Vector3 dirN = dir / dist;
        RaycastHit hit;

        if (Physics.SphereCast(lookOrigin, wallProbeRadius, dirN, out hit,
                dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // Place camera just in front of the hit, keeping wallPadding clearance
            float safeDist = Mathf.Max(hit.distance - wallPadding, 0.3f);
            float targetPull = 1f - (safeDist / dist);
            currentPullIn = Mathf.Lerp(currentPullIn, targetPull, Time.deltaTime * wallPullInSpeed);
        }
        else
        {
            // No obstruction → smoothly release pull-in
            currentPullIn = Mathf.Lerp(currentPullIn, 0f, Time.deltaTime * wallPullInSpeed * 0.5f);
        }

        // Apply pull-in
        float effectiveDist = dist * (1f - currentPullIn);
        effectiveDist = Mathf.Max(effectiveDist, 0.5f); // never closer than 0.5m
        return lookOrigin + dirN * effectiveDist;
    }

    /// <summary>
    /// If the camera ended up inside geometry (e.g. thin walls), push it out
    /// using an overlap sphere test.
    /// </summary>
    Vector3 PushOutOfWalls(Vector3 pos)
    {
        float radius = wallProbeRadius + 0.05f;
        var cols = Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Ignore);
        if (cols.Length == 0) return pos;

        // Push away from the closest surface
        foreach (var col in cols)
        {
            Vector3 closest = col.ClosestPoint(pos);
            Vector3 pushDir = pos - closest;
            float penetration = radius - pushDir.magnitude;
            if (penetration > 0f && pushDir.sqrMagnitude > 0.0001f)
                pos += pushDir.normalized * (penetration + 0.05f);
        }
        return pos;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    Transform GetActivePrimary()
    {
        if (blobOne != null && blobOne.gameObject.activeSelf) return blobOne;
        if (blobTwo != null && blobTwo.gameObject.activeSelf) return blobTwo;
        return null;
    }

    float GetCurrentSmoothSpeed()
    {
        if (blobManager != null)
            return blobManager.Mode == ControlMode.Both
                ? combinedSmoothSpeed : tppSmoothSpeed;
        return combinedSmoothSpeed;
    }
}
