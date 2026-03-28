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
    public float tppHeightBase    = 8f;      // Height above blob
    public float tppDistance      = 6f;      // Distance behind blob
    public float tppTiltAngle     = 55f;     // View angle
    public float tppSmoothSpeed   = 5f;

    [Header("Combined View")]
    public float combinedHeightBase  = 12f;   // Min height above centroid
    public float combinedZoomPerUnit = 0.5f;  // Extra height per unit separation
    public float combinedMaxHeight   = 26f;
    public float combinedZOffset     = -6f;   // Behind centroid
    public float combinedSmoothSpeed = 5f;

    [Header("Velocity Prediction")]
    public float velocityLookaheadScale = 2f;

    private BlobManager blobManager;
    private Vector3    cameraVelocity = Vector3.zero;
    private ControlMode lastMode = ControlMode.Both;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Start()
    {
        blobManager = BlobManager.Instance;
    }

    void LateUpdate()
    {
        if (blobOne == null || blobTwo == null) return;

        // Determine target position based on control mode
        Vector3 targetPos;
        Vector3 lookAtPos;

        if (blobManager != null && blobManager.Mode == ControlMode.OnlyFirst)
        {
            // Focus on Blob 1
            ComputeIndividualBlobView(blobOne, blobTwo, out targetPos, out lookAtPos);
        }
        else if (blobManager != null && blobManager.Mode == ControlMode.OnlySecond)
        {
            // Focus on Blob 2
            ComputeIndividualBlobView(blobTwo, blobOne, out targetPos, out lookAtPos);
        }
        else
        {
            // Combined view for Both mode
            ComputeCombinedView(out targetPos, out lookAtPos);
        }

        // Apply SmoothDamp with velocity prediction
        float smoothness = GetCurrentSmoothSpeed();
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref cameraVelocity,
            1f / smoothness);

        // Look ahead based on velocity
        Vector3 lookTarget = lookAtPos + cameraVelocity * velocityLookaheadScale;
        transform.LookAt(lookTarget);

        lastMode = blobManager != null ? blobManager.Mode : ControlMode.Both;
    }

    /// <summary>
    /// Individual blob TPP: camera follows one blob, staying behind and above it.
    /// Takes the other blob into account for context awareness.
    /// </summary>
    void ComputeIndividualBlobView(Transform primary, Transform secondary, 
        out Vector3 camPos, out Vector3 lookPos)
    {
        Vector3 blobWorldPos = primary.position;
        Vector3 blobFacing = primary.forward;  // Use blob's facing direction

        // Camera: behind and above the target blob
        Vector3 behindOffset = -blobFacing * tppDistance;
        camPos = blobWorldPos + behindOffset + Vector3.up * tppHeightBase;

        // Look at the blob, slightly ahead for velocity prediction
        lookPos = blobWorldPos + Vector3.up * (primary.localScale.y * 0.5f);
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
