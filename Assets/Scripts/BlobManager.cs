using UnityEngine;

/// <summary>
/// Control modes:
///   Both       – Same input drives both blobs.
///   OnlyFirst  – Only Blob 1 takes input.
///   OnlySecond – Only Blob 2 takes input.
/// Per blob: W/S = forward/back along facing; A/D = turn in place. TAB switches mode.
/// </summary>
public enum ControlMode { Both, OnlyFirst, OnlySecond }

public class BlobManager : MonoBehaviour
{
    public static BlobManager Instance { get; private set; }

    [Header("Blobs")]
    public BlobController blobOne;
    public BlobController blobTwo;

    [Header("Input")]
    public KeyCode switchKey = KeyCode.Tab;

    public ControlMode Mode { get; private set; } = ControlMode.Both;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else                  Destroy(gameObject);
    }

    void Start() => ApplyMode();

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            Mode = (ControlMode)(((int)Mode + 1) % 3);
            ApplyMode();
        }

        if (Input.GetKeyDown(KeyCode.X))
            MergeZone.Instance?.TrySplit();

        // Jump (RobotFreeAnim Space/roll is disabled on blob avatars)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (blobOne != null && blobOne.IsControlled) blobOne.Jump();
            if (blobTwo != null && blobTwo.IsControlled) blobTwo.Jump();
        }

        // Normalised so diagonal movement isn't faster
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).normalized;

        if (blobOne != null && blobOne.IsControlled) blobOne.Move(input);
        if (blobTwo != null && blobTwo.IsControlled) blobTwo.Move(input);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    void ApplyMode()
    {
        if (blobOne != null) blobOne.SetControlled(Mode != ControlMode.OnlySecond);
        if (blobTwo != null) blobTwo.SetControlled(Mode != ControlMode.OnlyFirst);
    }

    public void ReapplyControlMode() => ApplyMode();

    public string GetModeLabel() => Mode switch
    {
        ControlMode.Both       => "[ BOTH ]",
        ControlMode.OnlyFirst  => "[ BLOB 1 ]",
        ControlMode.OnlySecond => "[ BLOB 2 ]",
        _                      => "[ ? ]"
    };
}
