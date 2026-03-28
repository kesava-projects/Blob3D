using UnityEngine;

/// <summary>
/// Invisible trigger placed at the exit door.
/// When any blob (split or merged) enters, the level is complete.
/// </summary>
public class ExitZone : MonoBehaviour
{
    private bool triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Blob")) return;

        triggered = true;
        Debug.Log("[Blob3D] Blob reached the exit!");
        GameManager.Instance?.OnLevelComplete();
    }
}
