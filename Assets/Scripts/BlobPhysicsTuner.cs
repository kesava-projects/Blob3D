using UnityEngine;

/// <summary>
/// Applies rigidbody tuning to both blobs (after <see cref="BlobController.Awake"/>).
/// </summary>
public class BlobPhysicsTuner : MonoBehaviour
{
    [Header("Rigidbody")]
    public float linearDamping = 6f;
    public float angularDamping = 6f;

    void Start() => ApplyToBlobs();

    public void ApplyToBlobs()
    {
        var bm = BlobManager.Instance != null
            ? BlobManager.Instance
            : Object.FindAnyObjectByType<BlobManager>();

        if (bm == null) return;

        Tune(bm.blobOne);
        Tune(bm.blobTwo);
    }

    void Tune(BlobController blob)
    {
        if (blob == null) return;
        var rb = blob.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
    }
}
