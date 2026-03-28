using UnityEngine;

/// <summary>
/// Keeps both blobs in frame by tracking their centroid and dynamically
/// adjusting the camera height based on the distance between them.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Targets")]
    public Transform blobOne;
    public Transform blobTwo;

    [Header("Settings")]
    public float smoothSpeed  = 5f;
    public float heightBase   = 12f;   // Min height above ground
    public float zoomPerUnit  = 0.5f;  // Extra height added per unit of blob separation
    public float maxHeight    = 26f;
    public float tiltAngle    = 58f;   // X-rotation (degrees from horizontal)
    public float zOffset      = -6f;   // Camera behind the centroid (world Z)

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (blobOne == null || blobTwo == null) return;

        // Flat centroid (ignore Y)
        Vector3 a = new Vector3(blobOne.position.x, 0f, blobOne.position.z);
        Vector3 b = new Vector3(blobTwo.position.x, 0f, blobTwo.position.z);
        Vector3 centre = (a + b) * 0.5f;

        float separation = Vector3.Distance(a, b);
        float height     = Mathf.Min(heightBase + separation * zoomPerUnit, maxHeight);

        // Position: above and behind the centroid
        Vector3 targetPos = centre + new Vector3(0f, height, zOffset);

        transform.position = Vector3.Lerp(
            transform.position, targetPos, Time.deltaTime * smoothSpeed);

        // Always look at the centroid on the ground plane
        transform.LookAt(centre);
    }
}
