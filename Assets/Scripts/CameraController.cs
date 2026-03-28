using UnityEngine;

/// <summary>
/// Adaptive camera system supporting:
///   - Individual blob TPP when controlling one blob (OnlyFirst/OnlySecond)
///   - Combined dual-blob framing when controlling both (Both mode)
///   - Smooth transitions between modes with velocity prediction
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Targets")]
    public Transform blobOne;
    public Transform blobTwo;

    [Header("Individual Blob TPP")]
    public float tppHeightBase      = 8f;      // Height above blob
    public float tppDistance        = 6f;      // Distance behind blob
    public float tppLookHeightOffset = 1.1f;   // Look slightly above blob center
    public float tppSmoothSpeed     = 7f;

    [Header("Combined View")]
    public float combinedHeightBase  = 12f;   // Min height above centroid
    public float combinedZoomPerUnit = 0.5f;  // Extra height per unit separation
    public float combinedMaxHeight   = 26f;
    public float combinedZOffset     = -6f;   // Behind centroid
    public float combinedSmoothSpeed = 5f;

    [Header("Velocity Prediction")]
    public float velocityLookaheadScale = 0.2f;
    public float maxLookaheadDistance = 2.5f;

    [Header("Room Bounds Clamping")]
    [Tooltip("Enable to prevent camera from leaving the room.")]
    public bool clampToRoom = false;
    public Vector3 roomMin = new Vector3(-28f, 0.5f, -38f);
    public Vector3 roomMax = new Vector3(8f, 5.5f, -2f);

    private BlobManager blobManager;
    private Vector3 cameraVelocity = Vector3.zero;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Start()
    {
        blobManager = BlobManager.Instance;
    }

    void LateUpdate()
    {
        if (blobOne == null || blobTwo == null) return;

        // Determine target position based on control mode:
        // - OnlyFirst  => Blob1 individual TPP
        // - OnlySecond => Blob2 individual TPP
        // - Both       => Combined framing
        Vector3 targetPos;
        Vector3 lookAtPos;

        if (blobManager != null && blobManager.Mode == ControlMode.OnlyFirst)
        {
            // Focus on Blob 1
            ComputeIndividualBlobView(blobOne, out targetPos, out lookAtPos);
        }
        else if (blobManager != null && blobManager.Mode == ControlMode.OnlySecond)
        {
            // Focus on Blob 2
            ComputeIndividualBlobView(blobTwo, out targetPos, out lookAtPos);
        }
        else
        {
            // Combined view for Both mode
            ComputeCombinedView(out targetPos, out lookAtPos);
        }

        // Apply SmoothDamp with velocity prediction
        float smoothness = GetCurrentSmoothSpeed();
        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref cameraVelocity,
            1f / smoothness);

        // Clamp to room bounds so camera never escapes the environment
        if (clampToRoom)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, roomMin.x, roomMax.x);
            smoothed.y = Mathf.Clamp(smoothed.y, roomMin.y, roomMax.y);
            smoothed.z = Mathf.Clamp(smoothed.z, roomMin.z, roomMax.z);
        }

        transform.position = smoothed;

        // Look ahead based on camera velocity, clamped for stability.
        Vector3 lookahead = cameraVelocity * velocityLookaheadScale;
        if (lookahead.sqrMagnitude > maxLookaheadDistance * maxLookaheadDistance)
            lookahead = lookahead.normalized * maxLookaheadDistance;

        Vector3 lookTarget = lookAtPos + lookahead;
        transform.LookAt(lookTarget);
    }

    /// <summary>
    /// Individual blob TPP: camera follows one blob, staying behind and above it.
    /// </summary>
    void ComputeIndividualBlobView(Transform primary,
        out Vector3 camPos, out Vector3 lookPos)
    {
        Vector3 blobWorldPos = primary.position;
        Vector3 blobFacing = Vector3.ProjectOnPlane(primary.forward, Vector3.up).normalized;
        if (blobFacing.sqrMagnitude < 0.0001f)
            blobFacing = Vector3.forward;

        // Camera: behind and above the target blob
        Vector3 behindOffset = -blobFacing * tppDistance;
        camPos = blobWorldPos + behindOffset + Vector3.up * tppHeightBase;

        // Look at the target blob upper body region.
        lookPos = blobWorldPos + Vector3.up * tppLookHeightOffset;
    }

    /// <summary>
    /// Combined view: frame both blobs in the camera by tracking centroid
    /// and zooming based on separation distance.
    /// </summary>
    void ComputeCombinedView(out Vector3 camPos, out Vector3 lookPos)
    {
        // Flat centroid (ignore Y)
        Vector3 a = new Vector3(blobOne.position.x, 0f, blobOne.position.z);
        Vector3 b = new Vector3(blobTwo.position.x, 0f, blobTwo.position.z);
        Vector3 centre = (a + b) * 0.5f;

        float separation = Vector3.Distance(a, b);
        float height = Mathf.Min(combinedHeightBase + separation * combinedZoomPerUnit, combinedMaxHeight);

        // Position: above and behind the centroid
        camPos = centre + new Vector3(0f, height, combinedZOffset);
        lookPos = centre;  // Look at centroid on the ground
    }

    float GetCurrentSmoothSpeed()
    {
        if (blobManager != null)
        {
            return (blobManager.Mode == ControlMode.Both) 
                ? combinedSmoothSpeed 
                : tppSmoothSpeed;
        }
        return combinedSmoothSpeed;
    }
}
