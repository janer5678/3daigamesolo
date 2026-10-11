using UnityEngine;

// What the player is pressing, sampled each frame and used on each physics step.
public struct SlideInput
{
    public float forward;   // W/S
    public float strafe;    // A/D
    public bool jump;       // Space, pressed
    public bool jumpHeld;   // Space, held: a held jump goes higher
    public bool slide;      // C, held
}

// The parkour runner. Quick, snappy running; Space jumps (hold it to go higher), with coyote time and
// jump buffering, and every hop adds a little speed, so hopping again and again builds it up. C slides:
// a burst of speed, momentum carried, and a higher jump out of it. Gravity is heavy, and heavier on the
// way down, so jumps are quick and don't hang. V switches first and third person.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;
    [Tooltip("Fall below this height (off the edge of a floating level) and you're put back at the last checkpoint.")]
    public float fallLimit = -300f;
    [Tooltip("The runner, seen in third person. Children: Body, Leg L, Leg R (pivots at the hips).")]
    public Transform rider;
    [Tooltip("The last checkpoint you reached; you're put back here when you fall or press R.")]
    public Transform checkpoint;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float eyeHeight = 1.5f;
    public float fov = 70f;
    public float fovAtSpeed = 85f;

    [Header("Third person (V)")]
    public bool thirdPerson;
    public float camDistance = 4.2f;
    [Tooltip("Height above your feet the third-person camera orbits round.")]
    public float camPivotHeight = 1.6f;

    [Header("Running")]
    public float walkSpeed = 7.5f;
    [Tooltip("How quickly you get up to running speed. High = snappy.")]
    public float walkAccel = 40f;
    [Tooltip("How quickly you stop when you let go.")]
    public float stopDecel = 14f;
    [Tooltip("How quickly speed over running pace (from hops or slides) bleeds off on the ground: low, so you carry it.")]
    public float overspeedDecel = 7f;
    [Tooltip("Overall speed cap along the ground.")]
    public float maxSpeed = 25f;

    [Header("Jumping")]
    public float jumpSpeed = 8.4f;
    [Tooltip("Hold Space and you rise for up to this long with lighter gravity, so a held jump goes higher than a tap.")]
    public float jumpHoldMax = 0.32f;
    [Tooltip("How much of gravity holding Space takes away while you're rising.")]
    public float jumpHoldLift = 0.4f;
    [Tooltip("Coyote time: you can still jump this long after running off an edge.")]
    public float coyoteTime = 0.12f;
    [Tooltip("Jump buffering: press Space this long before you land and you jump as soon as you touch down.")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Speed each hop adds, so hopping again and again slowly speeds you up...")]
    public float hopBoost = 0.8f;
    [Tooltip("...up to this.")]
    public float hopSpeedCap = 22f;

    [Header("Slide (C)")]
    public float slideKick = 2.5f;
    public float slideDecel = 3f;
    public float slideTurnRate = 70f;
    [Tooltip("Jump during a slide, or this soon after, and the jump's this much stronger.")]
    public float slideJumpWindow = 0.35f;
    public float slideJumpMult = 1.3f;

    [Header("Air")]
    [Tooltip("Gravity multiplier. 1 is Unity's default; higher is less floaty.")]
    public float gravityScale = 3.6f;
    [Tooltip("Extra gravity on the way down, so jumps snap back to the ground instead of hanging.")]
    public float fallBoost = 1.5f;
    [Tooltip("How quickly you can change direction in the air. Nothing pressed keeps your momentum.")]
    public float airAccel = 24f;
    [Tooltip("Degrees per second you can turn fast momentum in the air.")]
    public float airTurnRate = 240f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public bool Sliding { get { return sliding; } }
    public bool HoldingJump { get { return holdingJump; } }
    public Vector3 Velocity { get { return rb.linearVelocity; } }
    public float Speed { get { return rb.linearVelocity.magnitude; } }

    // ground checks start a little above the ball: after a hard landing it can sit slightly in the ground
    // for a step, and a cast that starts inside something doesn't see it
    const float CastBack = 0.45f;
    Rigidbody rb;
    SphereCollider ball;
    Vector3 groundNormal = Vector3.up;
    SlideInput input;
    bool jumpLatch;
    float jumpCooldown;
    bool holdingJump;
    float jumpHold;
    float sinceGrounded;   // for coyote time
    float jumpBuffer;
    bool sliding;
    float slideJumpTimer;
    float eyeDrop;         // first person: the camera dips while you slide
    float fovNow;
    Camera camera3d;
    float stride;          // running leg swing
    Transform body, legL, legR;

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        ball = GetComponent<SphereCollider>();
        if (cam != null) camera3d = cam.GetComponent<Camera>();
        if (rider != null) { body = rider.Find("Body"); legL = rider.Find("Leg L"); legR = rider.Find("Leg R"); }
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
        Transform s = checkpoint != null ? checkpoint : startPoint != null ? startPoint : transform;
        rb.position = s.position + Vector3.up * ball.radius;
        transform.position = rb.position;
        rb.linearVelocity = Vector3.zero;
        jumpCooldown = 0f;
        holdingJump = sliding = false;
        jumpBuffer = 0f;
        sinceGrounded = 1f;
        slideJumpTimer = 0f;
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
        if (Input.GetKeyDown(KeyCode.V)) thirdPerson = !thirdPerson;

        input.forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        input.strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        jumpLatch |= Input.GetKeyDown(KeyCode.Space);
        input.jumpHeld = Input.GetKey(KeyCode.Space);
        input.slide = Input.GetKey(KeyCode.C);
    }

    void FixedUpdate()
    {
        input.jump = jumpLatch;
        jumpLatch = false;
        Tick(input);
        if (transform.position.y < fallLimit) Respawn();
    }

    // One physics step's worth of moving. Public so the editor tests can drive it.
    public void Tick(SlideInput inp)
    {
        float dt = Time.fixedDeltaTime;
        jumpCooldown -= dt;

        RaycastHit hit;
        Grounded = Physics.SphereCast(rb.position + Vector3.up * CastBack, ball.radius * 0.95f, Vector3.down, out hit, 0.12f + CastBack, ~0, QueryTriggerInteraction.Ignore)
                   && hit.normal.y > 0.35f;
        groundNormal = Grounded ? hit.normal : Vector3.up;
        // a hard landing can sink you into the ground for a step: put you straight back on top, so the
        // camera doesn't dip and the next jump isn't lost
        float sunk = Grounded ? CastBack - hit.distance - ball.radius * 0.05f : 0f;
        if (sunk > 0.005f)
        {
            rb.position += Vector3.up * sunk;
            Vector3 lv = rb.linearVelocity;
            if (lv.y < 0f) rb.linearVelocity = new Vector3(lv.x, 0f, lv.z);
        }

        Quaternion look = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = look * Vector3.forward, right = look * Vector3.right;
        if (Grounded)
        {
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal).normalized;
            right = Vector3.ProjectOnPlane(right, groundNormal).normalized;
        }
        Vector3 wish = fwd * inp.forward + right * inp.strafe;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float top = walkSpeed;

        Vector3 v = rb.linearVelocity;
        float g = gravityScale;
        if (!Grounded && v.y < 0f) g *= fallBoost;
        v += Physics.gravity * (g - 1f) * dt;

        // C: slide. A burst of speed to start it, momentum kept, only gentle steering, slowing gradually;
        // a jump during the slide or just after it goes higher
        bool wasSliding = sliding;
        float flatSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        sliding = Grounded && inp.slide && flatSpeed > (wasSliding ? 1f : 2.5f);
        if (sliding) slideJumpTimer = slideJumpWindow; else slideJumpTimer -= dt;

        if (Grounded)
        {
            // work in the plane of the ground; the part into/out of it is left to physics
            Vector3 vN = Vector3.Project(v, groundNormal);
            Vector3 vT = v - vN;
            if (sliding)
            {
                if (!wasSliding && vT.magnitude > 0.1f)
                    vT += vT.normalized * Mathf.Min(slideKick, Mathf.Max(0f, maxSpeed - vT.magnitude));
                if (wish.sqrMagnitude > 0.01f && vT.magnitude > 0.5f)
                    vT = Vector3.RotateTowards(vT, wish.normalized * vT.magnitude, slideTurnRate * Mathf.Deg2Rad * dt, 0f);
                vT = Vector3.MoveTowards(vT, Vector3.zero, slideDecel * dt);
            }
            else if (wish.sqrMagnitude < 0.01f) vT = Vector3.MoveTowards(vT, Vector3.zero, stopDecel * dt);
            else if (vT.magnitude > top) vT = Vector3.MoveTowards(vT, wish * top, overspeedDecel * dt);   // extra speed bleeds off slowly
            else vT = Vector3.MoveTowards(vT, wish * top, walkAccel * dt);
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

        // jump buffering: a press shortly before you land still counts; coyote time: shortly after you run
        // off an edge you can still jump
        if (inp.jump) jumpBuffer = jumpBufferTime; else jumpBuffer -= dt;
        if (Grounded) sinceGrounded = 0f; else sinceGrounded += dt;
        bool canJump = (Grounded || sinceGrounded < coyoteTime) && jumpCooldown <= 0f;
        if (jumpBuffer > 0f && canJump)
        {
            jumpBuffer = 0f;
            sinceGrounded = 1f;   // no second coyote jump
            float js = jumpSpeed;
            if (sliding || slideJumpTimer > 0f) js *= slideJumpMult;   // out of a slide: higher
            v.y = Mathf.Max(v.y, 0f) + js;
            // every hop adds a little speed, so jumping again and again slowly builds it up
            Vector3 fl = new Vector3(v.x, 0f, v.z);
            if (fl.magnitude > 2f && fl.magnitude < hopSpeedCap)
            {
                fl += fl.normalized * Mathf.Min(hopBoost, hopSpeedCap - fl.magnitude);
                v.x = fl.x; v.z = fl.z;
            }
            jumpCooldown = 0.15f;   // you're still touching the ground for a moment after take-off; don't count that as another jump
            sliding = false;
            slideJumpTimer = 0f;
            holdingJump = true;
            jumpHold = 0f;
        }
        else if (holdingJump)
        {
            // keep holding Space and you go higher (gravity's lighter while you rise)
            if (inp.jumpHeld)
            {
                jumpHold += dt;
                if (jumpHold < jumpHoldMax && v.y > 0f)
                    v -= Physics.gravity * gravityScale * jumpHoldLift * dt;
                if (jumpHold >= jumpHoldMax) holdingJump = false;
            }
            else holdingJump = false;
        }
        rb.linearVelocity = v;

        // the speed cap is on how fast you're going along the ground, so jumping never eats your speed
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }
    }

    // ---- camera and runner --------------------------------------------------------------------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (cam != null)
        {
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            eyeDrop = Mathf.Lerp(eyeDrop, sliding ? 0.65f : 0f, 1f - Mathf.Exp(-12f * dt));
            Vector3 eye = transform.position + Vector3.up * (eyeHeight - ball.radius - eyeDrop);
            if (thirdPerson)
            {
                // orbit behind you, pulled in if something's in the way
                Vector3 pivot = transform.position + Vector3.up * (camPivotHeight - ball.radius);
                Vector3 back = view * Vector3.back;
                float dist = camDistance;
                foreach (var h in Physics.SphereCastAll(pivot, 0.25f, back, camDistance, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider != ball && h.distance > 0f) dist = Mathf.Min(dist, h.distance);
                eye = pivot + back * dist;
            }
            cam.SetPositionAndRotation(eye, view);
            if (camera3d != null)
            {
                // widen with speed along the ground only, smoothly, so jumps and landings don't pump the view
                Vector3 h = rb.linearVelocity; h.y = 0f;
                float target = Mathf.Lerp(fov, fovAtSpeed, Mathf.Clamp01(h.magnitude / 20f));
                fovNow = fovNow <= 0f ? target : Mathf.Lerp(fovNow, target, 1f - Mathf.Exp(-4f * dt));
                camera3d.fieldOfView = fovNow;
            }
        }

        if (rider != null)
        {
            rider.gameObject.SetActive(thirdPerson);
            if (thirdPerson) PoseRider(transform.position - Vector3.up * ball.radius, dt);
        }
    }

    // face where you're running and swing your legs; crouch low in a slide
    void PoseRider(Vector3 feet, float dt)
    {
        Vector3 h = rb.linearVelocity; h.y = 0f;
        Vector3 face = h.magnitude > 0.5f ? h : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Quaternion want = Quaternion.LookRotation(face, Vector3.up);
        rider.SetPositionAndRotation(feet, Quaternion.Slerp(rider.rotation, want, 1f - Mathf.Exp(-12f * dt)));
        float crouch = sliding ? 0.35f : Grounded ? 0f : 0.05f;
        if (Grounded && !sliding) stride += h.magnitude * dt * 2.2f;
        float swing = Grounded && !sliding ? Mathf.Sin(stride) * Mathf.Clamp01(h.magnitude / walkSpeed) * 32f : sliding ? 40f : 0f;

        // legs pivot at the hips; bend them for the crouch
        float hip = 0.9f - crouch;
        if (body != null) body.localPosition = new Vector3(0f, hip, 0f);
        if (legL != null) { legL.localPosition = new Vector3(-0.11f, hip, 0f); legL.localRotation = Quaternion.Euler(sliding ? -swing : swing, 0f, 0f); legL.localScale = new Vector3(1f, hip / 0.9f, 1f); }
        if (legR != null) { legR.localPosition = new Vector3(0.11f, hip, 0f); legR.localRotation = Quaternion.Euler(-swing, 0f, 0f); legR.localScale = new Vector3(1f, hip / 0.9f, 1f); }
    }
}
