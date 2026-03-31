using UnityEngine;

/// <summary>
/// Drives a single blob body: physics-based movement, speed-capping,
/// squish/stretch animation, and active/inactive colour feedback.
/// </summary>
public class BlobController : MonoBehaviour
{

    [Header("Movement")]
    public float moveSpeed = 11f;
    public float maxSpeed  = 11f;
    [Tooltip("Yaw degrees per second while holding A / D (rotate in place).")]
    public float turnSpeed = 140f;
    [Tooltip("How fast the avatar mesh catches up to facing (higher = snappier).")]
    public float avatarTurnSmoothing = 25f;
    [Tooltip("If true, initialize facing so the avatar points toward the gate at scene start.")]
    public bool faceGateOnStart = true;

    [Header("Jump / Damping")]
    public float jumpForce       = 6f;
    public float groundCheckDist = 0.52f;  // slightly > sphere radius (0.425)
    [Tooltip("Extra distance for landing detection to avoid missing ground right after jump.")]
    public float groundCheckPadding = 0.08f;
    [Tooltip("How quickly jump squash returns to normal scale after landing.")]
    public float jumpScaleRecoverSpeed = 16f;
    [Tooltip("Rigidbody linear damping while grounded (snappy stops).")]
    public float groundDamping = 5f;
    [Tooltip("Rigidbody linear damping while airborne (smooth arcs).")]
    public float airDamping    = 0.3f;

    [Header("Visuals")]
    public Color activeColor   = Color.cyan;
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("RobotSphere Avatar")]
    public GameObject robotSpherePrefab;
    public Transform avatarRoot;
    public bool hideBlobMeshWhenAvatarPresent = true;
    public string walkAnimParam = "Walk_Anim";
    public string jumpAnimParam = "Jump_Anim";
    public bool autoFitAvatarToBlob = true;
    public float avatarScaleMultiplier = 1f;
    public float avatarGroundOffset = 0f;
    [Tooltip("Minimum horizontal speed required to keep walk animation active.")]
    public float walkVelocityThreshold = 0.18f;

    private Rigidbody rb;
    private Renderer  rend;
    private Vector3   baseScale;
    private Animator  avatarAnim;
    private Transform avatarPivot;
    private bool      controlled  = true;
    private bool      isGrounded  = false;
    private bool      hasWalkAnim = false;
    private bool      hasJumpAnim = false;

    /// <summary>Target world Yaw in degrees (A/D); avatar and fallback heading.</summary>
    private float facingYaw;

    public bool IsControlled => controlled;

    /// <summary>
    /// After unite/split changes blob sphere scale: updates squish baseline and ground ray only.
    /// Does not rescale the RobotSphere (that was shrinking avatars and breaking split visuals).
    /// </summary>
    public void RefreshMovementBaseline()
    {
        baseScale = transform.localScale;

        var sc = GetComponent<SphereCollider>();
        if (sc != null)
        {
            float m = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
            float worldRadius = sc.radius * m;
            groundCheckDist = Mathf.Max(0.35f, worldRadius * 1.12f);
        }
        else
        {
            float m = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            groundCheckDist = Mathf.Max(0.4f, m * 0.62f);
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        rb   = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        baseScale = transform.localScale;

        if (avatarRoot == null)
            avatarRoot = transform;

        avatarAnim = GetComponentInChildren<Animator>();

        if (robotSpherePrefab != null && avatarAnim == null)
        {
            var avatar = Instantiate(robotSpherePrefab, avatarRoot);
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.identity;
            avatar.transform.localScale    = Vector3.one;
            avatarAnim = avatar.GetComponentInChildren<Animator>();
        }

        if (avatarAnim != null)
        {
            avatarPivot = avatarAnim.transform.root == transform
                ? avatarAnim.transform
                : avatarAnim.transform.root;

            if (autoFitAvatarToBlob)
                FitAvatarToBlob();

            hasWalkAnim = HasAnimatorBool(walkAnimParam);
            hasJumpAnim = HasAnimatorBool(jumpAnimParam);

            if (hideBlobMeshWhenAvatarPresent && rend != null)
                rend.enabled = false;

            facingYaw = avatarPivot != null ? avatarPivot.eulerAngles.y : transform.eulerAngles.y;
        }
        else
            facingYaw = transform.eulerAngles.y;

        if (faceGateOnStart)
            SetFacingTowardsGate();

        foreach (var rfa in GetComponentsInChildren<RobotFreeAnim>(true))
            rfa.disableInput = true;

        rb.linearDamping  = groundDamping;
        rb.angularDamping = 5f;
        rb.useGravity     = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        // Freeze rotation only – Y is free so blobs can jump
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    // ── Lifecycle (physics) ────────────────────────────────────────────────

    void FixedUpdate()
    {
        // Ray starts inside the sphere collider so the blob's own collider is skipped
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.05f,
            Vector3.down,
            groundCheckDist + groundCheckPadding,
            ~0,
            QueryTriggerInteraction.Ignore);

        // Always recover jump squash; faster on ground, softer in air.
        float recoverSpeed = isGrounded ? jumpScaleRecoverSpeed : jumpScaleRecoverSpeed * 0.35f;
        transform.localScale = Vector3.Lerp(
            transform.localScale, baseScale, Time.fixedDeltaTime * recoverSpeed);

        // Smooth platforming: low drag in air preserves horizontal momentum
        rb.linearDamping = isGrounded ? groundDamping : airDamping;

        if (hasJumpAnim && isGrounded)
            avatarAnim.SetBool(jumpAnimParam, false);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Toggle whether this blob accepts player input.</summary>
    public void SetControlled(bool value)
    {
        controlled = value;
        if (!controlled)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        RefreshColor();
    }

    /// <summary>Apply a vertical impulse when grounded (called by BlobManager).</summary>
    public void Jump()
    {
        if (!controlled || !isGrounded) return;
        // Squash before launch for a punchy feel
        transform.localScale = new Vector3(baseScale.x * 1.2f, baseScale.y * 0.7f, baseScale.z * 1.2f);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); // reset Y first
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        if (hasJumpAnim)
            avatarAnim.SetBool(jumpAnimParam, true);
    }

    /// <summary>
    /// Tank-style: A/D set target yaw; W/S accelerates along the character model’s
    /// forward on the ground (after rotation is applied this frame).
    /// </summary>
    public void Move(Vector2 input)
    {
        if (!controlled) return;

        facingYaw += input.x * turnSpeed * Time.deltaTime;

        if (avatarPivot != null)
        {
            Quaternion target = Quaternion.Euler(0f, facingYaw, 0f);
            avatarPivot.rotation = Quaternion.Slerp(
                avatarPivot.rotation, target, Time.deltaTime * avatarTurnSmoothing);
        }

        Vector3 moveDir = CharacterPlanarForward();
        rb.AddForce(moveDir * input.y * moveSpeed, ForceMode.Force);

        Vector3 hv = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (hv.magnitude > maxSpeed)
        {
            hv = hv.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(hv.x, rb.linearVelocity.y, hv.z);
        }

        if (Mathf.Abs(input.y) > 0.05f)
        {
            if (avatarAnim == null)
            {
                float s = Mathf.Sin(Time.time * 12f) * 0.07f;
                transform.localScale = new Vector3(
                    baseScale.x * (1f - s),
                    baseScale.y * (1f + s * 1.5f),
                    baseScale.z * (1f - s));
            }
        }
        else if (avatarAnim == null)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, baseScale, Time.deltaTime * 10f);
        }

        if (hasWalkAnim)
        {
            float walkThresholdSq = walkVelocityThreshold * walkVelocityThreshold;
            bool hasMoveInput = Mathf.Abs(input.y) > 0.05f;
            bool hasPlanarSpeed = hv.sqrMagnitude > walkThresholdSq;
            bool shouldWalk = hasMoveInput && (isGrounded || hasPlanarSpeed);
            avatarAnim.SetBool(walkAnimParam, shouldWalk);
        }
    }

    // ── Internal ───────────────────────────────────────────────────────────

    /// <summary>Unit direction on XZ the character is facing (model forward), fallback to yaw.</summary>
    Vector3 CharacterPlanarForward()
    {
        if (avatarPivot != null)
        {
            Vector3 f = avatarPivot.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-6f)
                return f.normalized;
        }

        Vector3 fromYaw = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        fromYaw.y = 0f;
        return fromYaw.sqrMagnitude > 1e-6f ? fromYaw.normalized : Vector3.forward;
    }

    void SetFacingTowardsGate()
    {
        var gate = Object.FindAnyObjectByType<Gate>();
        if (gate == null) return;

        Vector3 toGate = gate.transform.position - transform.position;
        toGate.y = 0f;
        if (toGate.sqrMagnitude < 1e-6f) return;

        facingYaw = Quaternion.LookRotation(toGate.normalized, Vector3.up).eulerAngles.y;

        if (avatarPivot != null)
            avatarPivot.rotation = Quaternion.Euler(0f, facingYaw, 0f);
    }

    void RefreshColor()
    {
        if (rend != null && rend.enabled)
            rend.material.color = controlled ? activeColor : inactiveColor;

        if (avatarAnim != null)
            avatarAnim.speed = controlled ? 1f : 0.5f;
    }

    bool HasAnimatorBool(string paramName)
    {
        if (avatarAnim == null || string.IsNullOrEmpty(paramName)) return false;

        foreach (var param in avatarAnim.parameters)
        {
            if (param.name == paramName && param.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    void FitAvatarToBlob()
    {
        if (avatarPivot == null) return;

        var blobCollider = GetComponent<Collider>();
        if (blobCollider == null) return;

        if (!TryGetCombinedRendererBounds(avatarPivot, out Bounds avatarBounds)) return;

        float blobHeight = Mathf.Max(0.01f, blobCollider.bounds.size.y);
        float avatarHeight = Mathf.Max(0.01f, avatarBounds.size.y);

        float scaleFactor = (blobHeight / avatarHeight) * Mathf.Max(0.01f, avatarScaleMultiplier);
        avatarPivot.localScale *= scaleFactor;

        // Recalculate bounds after scaling, then align bottoms (feet) to blob bottom.
        if (!TryGetCombinedRendererBounds(avatarPivot, out avatarBounds)) return;

        float desiredMinY = blobCollider.bounds.min.y + avatarGroundOffset;
        float deltaY = desiredMinY - avatarBounds.min.y;
        avatarPivot.position += Vector3.up * deltaY;
    }

    bool TryGetCombinedRendererBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }
}
