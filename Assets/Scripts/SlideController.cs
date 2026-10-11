using UnityEngine;

// What the player is pressing, sampled each frame and used on each physics step.
public struct SlideInput
{
    public float forward;   // W/S
    public float strafe;    // A/D
    public bool push;       // Shift, held
    public bool jump;       // Space, pressed
}

// First person on a frictionless ball. Gravity does most of the work: you slide down slopes on
// your own and keep your speed. Shift pushes you the way you're looking; WASD nudges you
// around, but only gently, so it all feels slippery.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float eyeHeight = 1.5f;
    public float fov = 70f;
    public float fovAtSpeed = 85f;

    [Header("Moving")]
    [Tooltip("Shift: how hard you push, and the speed past which pushing stops helping.")]
    public float pushAccel = 12f;
    public float pushTopSpeed = 12f;
    [Tooltip("WASD: a gentle nudge in any direction. Low = slippery.")]
    public float moveAccel = 3f;
    public float airControl = 0.35f;
    public float jumpSpeed = 5.5f;
    public float maxSpeed = 40f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public Vector3 Velocity { get { return rb.linearVelocity; } }
    public float Speed { get { return rb.linearVelocity.magnitude; } }

    Rigidbody rb;
    SphereCollider ball;
    Vector3 groundNormal = Vector3.up;
    SlideInput input;
    bool jumpLatch;
    Camera camera3d;

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        ball = GetComponent<SphereCollider>();
        if (cam != null) camera3d = cam.GetComponent<Camera>();
    }

    void Start()
    {
        Respawn();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Respawn()
    {
        if (rb == null) Setup();
        Transform s = startPoint != null ? startPoint : transform;
        rb.position = s.position + Vector3.up * ball.radius;
        transform.position = rb.position;
        rb.linearVelocity = Vector3.zero;
        Vector3 f = s.forward;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        pitch = 8f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, -85f, 85f);
        }
        if (Input.GetKeyDown(KeyCode.R)) Respawn();

        input.forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        input.strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        input.push = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jumpLatch |= Input.GetKeyDown(KeyCode.Space);
    }

    void FixedUpdate()
    {
        input.jump = jumpLatch;
        jumpLatch = false;
        Tick(input);
        if (transform.position.y < -300f) Respawn();
    }

    // One physics step's worth of steering. Public so the editor tests can drive it.
    public void Tick(SlideInput inp)
    {
        RaycastHit hit;
        Grounded = Physics.SphereCast(rb.position, ball.radius * 0.95f, Vector3.down, out hit, 0.12f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.normal.y > 0.35f;
        groundNormal = Grounded ? hit.normal : Vector3.up;

        Quaternion look = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = look * Vector3.forward, right = look * Vector3.right;
        if (Grounded)
        {
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal).normalized;
            right = Vector3.ProjectOnPlane(right, groundNormal).normalized;
        }

        Vector3 wish = fwd * inp.forward + right * inp.strafe;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        rb.AddForce(wish * moveAccel * (Grounded ? 1f : airControl), ForceMode.Acceleration);

        // Shift: push off the ground the way you're facing, until you're going fast enough
        if (inp.push && Grounded && Vector3.Dot(rb.linearVelocity, fwd) < pushTopSpeed)
            rb.AddForce(fwd * pushAccel, ForceMode.Acceleration);

        if (inp.jump && Grounded)
        {
            Vector3 v = rb.linearVelocity;
            v.y = Mathf.Max(v.y, 0f) + jumpSpeed;
            rb.linearVelocity = v;
        }

        if (rb.linearVelocity.magnitude > maxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        cam.SetPositionAndRotation(transform.position + Vector3.up * (eyeHeight - ball.radius), Quaternion.Euler(pitch, yaw, 0f));
        if (camera3d != null) camera3d.fieldOfView = Mathf.Lerp(fov, fovAtSpeed, Mathf.Clamp01(Speed / 25f));
    }
}
