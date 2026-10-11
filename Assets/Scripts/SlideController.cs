using UnityEngine;

// What the player is pressing, sampled each frame and used on each physics step.
public struct SlideInput
{
    public float forward;   // W/S
    public float strafe;    // A/D
    public bool push;       // Shift, held: on the board
    public bool jump;       // Space, pressed
}

// First person. On foot by default: slow, snappy walking. Hold Shift and you're on the board
// (it appears under you): faster, it builds speed more gradually, and downhill speed is kept
// and steered rather than braked. Let go of Shift and you step off and slow to walking pace.
// Gravity is heavier than Unity's default so jumps and drops don't feel floaty; a little less so
// on the board, which glides and hangs in the air a bit more.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;
    [Tooltip("The board under your feet; turns to face where you're going and tilts with the ground.")]
    public Transform board;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float eyeHeight = 1.5f;
    public float fov = 70f;
    public float fovAtSpeed = 85f;

    [Header("On foot")]
    public float walkSpeed = 5.5f;
    [Tooltip("How quickly you get up to walking speed. High = snappy.")]
    public float walkAccel = 40f;
    [Tooltip("How quickly you stop, or drop back to walking pace after stepping off the board.")]
    public float stopDecel = 14f;

    [Header("On the board (hold Shift)")]
    public float boardSpeed = 15f;
    [Tooltip("How quickly pushing gets you up to board speed.")]
    public float boardAccel = 12f;
    [Tooltip("With nothing else pressed, how quickly the board slows you.")]
    public float boardRollDecel = 2f;
    [Tooltip("Degrees per second you can turn on the board at pushing speed.")]
    public float boardTurnRate = 300f;
    [Tooltip("Degrees per second downhill speed turns when you're going faster than you can push.")]
    public float fastTurnRate = 220f;
    [Tooltip("How hard pressing against your direction (S) brakes on the board.")]
    public float boardBrake = 22f;
    [Tooltip("Gravity multiplier while on the board; lower than on foot so riding feels a bit floatier.")]
    public float boardGravityScale = 1.6f;
    [Tooltip("Overall speed cap, mostly reached going downhill on the board.")]
    public float maxSpeed = 22f;

    [Header("Air")]
    [Tooltip("Gravity multiplier. 1 is Unity's default; higher is less floaty.")]
    public float gravityScale = 2.2f;
    public float jumpSpeed = 5.5f;
    [Tooltip("How quickly you can change direction in the air. Nothing pressed keeps your momentum.")]
    public float airAccel = 24f;
    [Tooltip("Degrees per second you can turn fast momentum in the air.")]
    public float airTurnRate = 160f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public bool OnBoard { get; private set; }
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

        OnBoard = inp.push;

        // on the board, Shift on its own pushes you forward
        float f = inp.forward;
        if (OnBoard && f == 0f && inp.strafe == 0f) f = 1f;
        Vector3 wish = fwd * f + right * inp.strafe;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float dt = Time.fixedDeltaTime;
        float top = OnBoard ? boardSpeed : walkSpeed;

        Vector3 v = rb.linearVelocity;
        v += Physics.gravity * ((OnBoard ? boardGravityScale : gravityScale) - 1f) * dt;
        if (Grounded)
        {
            // work in the plane of the ground; the part into/out of it is left to physics
            Vector3 vN = Vector3.Project(v, groundNormal);
            Vector3 vT = v - vN;
            if (!OnBoard)
            {
                // on foot: straight to walking pace, and any extra speed bleeds off quickly
                if (wish.sqrMagnitude < 0.01f) vT = Vector3.MoveTowards(vT, Vector3.zero, stopDecel * dt);
                else if (vT.magnitude > top) vT = Vector3.MoveTowards(vT, wish * top, stopDecel * dt);
                else vT = Vector3.MoveTowards(vT, wish * top, walkAccel * dt);
            }
            else if (wish.sqrMagnitude < 0.01f)
                vT = Vector3.MoveTowards(vT, Vector3.zero, boardRollDecel * dt);
            else
            {
                float s = vT.magnitude;
                float along = s > 0.5f ? Vector3.Dot(vT / s, wish.normalized) : 1f;
                if (along < -0.5f)
                    vT = Vector3.MoveTowards(vT, Vector3.zero, boardBrake * dt);   // pressing against your direction (S): brake
                else if (s < 1f)
                    vT = Vector3.MoveTowards(vT, wish * top, boardAccel * dt);
                else
                {
                    // steer: swing your velocity round towards where you're pressing, keeping the speed,
                    // then push it up towards board speed (speed over that, from downhill, is kept)
                    float rate = s > top ? fastTurnRate : boardTurnRate;
                    Vector3 dir = Vector3.RotateTowards(vT / s, wish.normalized, rate * Mathf.Deg2Rad * dt, 0f);
                    if (s < top * wish.magnitude) s = Mathf.MoveTowards(s, top * wish.magnitude, boardAccel * dt);
                    vT = dir * s;
                }
            }
            v = vT + vN;
        }
        else if (wish.sqrMagnitude > 0.01f)
        {
            // in the air: steer almost like on the ground, but let go and you keep your momentum
            Vector3 vH = new Vector3(v.x, 0f, v.z);
            Vector3 wishH = new Vector3(wish.x, 0f, wish.z).normalized * wish.magnitude;
            if (vH.magnitude > top && Vector3.Dot(vH, wishH) > 0f)
                vH = Vector3.RotateTowards(vH, wishH * vH.magnitude, airTurnRate * Mathf.Deg2Rad * dt, 0f);
            else
                vH = Vector3.MoveTowards(vH, wishH * top, airAccel * dt);
            v.x = vH.x; v.z = vH.z;
        }
        if (inp.jump && Grounded) v.y = Mathf.Max(v.y, 0f) + jumpSpeed;
        rb.linearVelocity = v;

        if (rb.linearVelocity.magnitude > maxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        cam.SetPositionAndRotation(transform.position + Vector3.up * (eyeHeight - ball.radius), Quaternion.Euler(pitch, yaw, 0f));
        if (camera3d != null) camera3d.fieldOfView = Mathf.Lerp(fov, fovAtSpeed, Mathf.Clamp01(Speed / 20f));

        if (board != null) board.gameObject.SetActive(OnBoard);
        if (board != null && OnBoard)
        {
            Vector3 h = rb.linearVelocity; h.y = 0f;
            Vector3 heading = h.magnitude > 1f ? h : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 up = Grounded ? groundNormal : Vector3.up;
            Quaternion want = Quaternion.LookRotation(Vector3.ProjectOnPlane(heading, up), up);
            board.SetPositionAndRotation(transform.position - up * ball.radius,
                                         Quaternion.Slerp(board.rotation, want, 1f - Mathf.Exp(-14f * Time.deltaTime)));
        }
    }
}
