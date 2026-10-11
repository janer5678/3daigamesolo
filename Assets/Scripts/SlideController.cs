using UnityEngine;

// What the player is pressing, sampled each frame and used on each physics step.
public struct SlideInput
{
    public float forward;   // W/S
    public float strafe;    // A/D
    public bool sprint;     // Shift, held
    public bool jump;       // Space, pressed
    public bool jumpHeld;   // Space, held: a held jump goes higher
    public bool slide;      // C, held
    public bool dismount;   // Q, pressed: hop off the board
}

// The parkour runner. Tight control: you get up to speed, stop and turn almost at once, on the ground
// and in the air. Shift sprints. Space jumps (hold it to go higher), with coyote time and jump
// buffering; hopping again and again in the same direction slowly builds speed. In the air against a
// wall, Space wall jumps off it (bouncing between walls climbs a shaft, but you can't climb one wall on
// its own), and pressing into a wall while falling slides you down it slowly. C slides, with a higher
// jump out of it.
// There's a skateboard lying about: run over it and you're riding it, fast; Q hops off and leaves it
// on the ground. Really big air on the board, and only that, kickflips it.
// V switches first and third person.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;
    [Tooltip("Fall below this height (off the edge of a floating level) and you're put back at the last checkpoint.")]
    public float fallLimit = -300f;
    [Tooltip("The runner, seen in third person. Children: Body, Leg L, Leg R (pivots at the hips).")]
    public Transform rider;
    [Tooltip("The skateboard lying in the level. Run over it to ride it.")]
    public Transform board;
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
    public float walkSpeed = 6.5f;
    [Tooltip("Holding Shift.")]
    public float sprintSpeed = 11f;
    [Tooltip("How quickly you get to the speed you're asking for, or turn. High = instant.")]
    public float runAccel = 90f;
    [Tooltip("How quickly you stop when you let go.")]
    public float stopDecel = 70f;
    [Tooltip("Speed over running pace from hopping bleeds off this slowly while you keep going the same way.")]
    public float overspeedDecel = 8f;
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
    public float hopSpeedCap = 20f;

    [Header("Wall jump")]
    public float wallJumpUp = 9.5f;
    public float wallJumpOut = 7f;
    [Tooltip("After a wall jump, steering is weaker for this long so you actually leave the wall.")]
    public float wallJumpSteerDelay = 0.18f;
    [Tooltip("Pressing into a wall while falling: you slide down it no faster than this.")]
    public float wallSlideSpeed = 3f;

    [Header("Slide (C)")]
    public float slideKick = 2.5f;
    public float slideDecel = 5f;
    public float slideTurnRate = 90f;
    [Tooltip("Jump during a slide, or this soon after, and the jump's this much stronger.")]
    public float slideJumpMult = 1.3f;
    public float slideJumpWindow = 0.35f;

    [Header("Air")]
    [Tooltip("Gravity multiplier. 1 is Unity's default; higher is less floaty.")]
    public float gravityScale = 3.6f;
    [Tooltip("Extra gravity on the way down, so jumps snap back to the ground instead of hanging.")]
    public float fallBoost = 1.5f;
    [Tooltip("How quickly you can change direction in the air.")]
    public float airAccel = 60f;
    [Tooltip("With nothing pressed in the air you slow down this quickly (0 keeps your momentum).")]
    public float airStop = 6f;

    [Header("Skateboard")]
    public float boardSpeed = 16f;
    [Tooltip("Holding Shift on the board.")]
    public float boardSprintSpeed = 22f;
    public float boardAccel = 18f;
    public float boardTurnRate = 360f;
    [Tooltip("With nothing pressed the board rolls on, slowing this gently.")]
    public float boardRollDecel = 2f;
    public float boardBrake = 30f;
    [Tooltip("How close you have to run past the board to jump on it.")]
    public float pickupRadius = 0.9f;
    [Tooltip("Only really big air kickflips the board: this high above where you took off, or falling this fast.")]
    public float bigAirHeight = 6f;
    public float bigAirFallSpeed = 26f;
    public float flipTime = 0.45f;
    public float flipHop = 0.4f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public bool OnBoard { get; private set; }
    public bool Sliding { get { return sliding; } }
    public bool OnWall { get { return touchingWall; } }
    public bool Flipping { get { return flip >= 0f; } }
    public Vector3 Velocity { get { return rb.linearVelocity; } }
    public float Speed { get { return rb.linearVelocity.magnitude; } }

    // ground checks start a little above the ball: after a hard landing it can sit slightly in the ground
    // for a step, and a cast that starts inside something doesn't see it
    const float CastBack = 0.45f;
    Rigidbody rb;
    SphereCollider ball;
    Vector3 groundNormal = Vector3.up;
    SlideInput input;
    bool jumpLatch, dismountLatch;
    float jumpCooldown;
    bool holdingJump;
    float jumpHold;
    float sinceGrounded;   // for coyote time
    float jumpBuffer;
    bool sliding;
    float slideJumpTimer;
    bool touchingWall;
    Vector3 wallNormal, lastWallJump;
    float steerDelay;
    float eyeDrop;         // first person: the camera dips while you slide
    float fovNow;
    Camera camera3d;
    float stride;          // running leg swing
    Transform body, legL, legR;

    Vector3 boardHome;     // where the board started, for when it's lost
    Quaternion boardHomeRot;
    float pickupBlock;     // after hopping off: no picking it straight back up
    Quaternion boardRot = Quaternion.identity;
    float takeoffY;
    bool flippedThisAir;
    float flip = -1f;      // kickflip progress 0..1, or <0 when not flipping

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        ball = GetComponent<SphereCollider>();
        if (cam != null) camera3d = cam.GetComponent<Camera>();
        if (rider != null) { body = rider.Find("Body"); legL = rider.Find("Leg L"); legR = rider.Find("Leg R"); }
        if (board != null && boardHome == Vector3.zero) { boardHome = board.position; boardHomeRot = board.rotation; }
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
        holdingJump = sliding = touchingWall = false;
        jumpBuffer = 0f;
        sinceGrounded = 1f;
        slideJumpTimer = steerDelay = 0f;
        lastWallJump = Vector3.zero;
        flip = -1f;
        Vector3 f = s.forward;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        pitch = 8f;
    }

    // puts the board back where it started (and you on foot)
    public void ResetBoard()
    {
        OnBoard = false;
        if (board == null) return;
        board.SetPositionAndRotation(boardHome, boardHomeRot);
        pickupBlock = 0f;
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
        input.sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jumpLatch |= Input.GetKeyDown(KeyCode.Space);
        dismountLatch |= Input.GetKeyDown(KeyCode.Q);
        input.jumpHeld = Input.GetKey(KeyCode.Space);
        input.slide = Input.GetKey(KeyCode.C);
    }

    void FixedUpdate()
    {
        input.jump = jumpLatch;
        input.dismount = dismountLatch;
        jumpLatch = dismountLatch = false;
        Tick(input);
        if (transform.position.y < fallLimit) Respawn();
        if (board != null && !OnBoard && board.position.y < fallLimit) ResetBoard();
    }

    // One physics step's worth of moving. Public so the editor tests can drive it.
    public void Tick(SlideInput inp)
    {
        float dt = Time.fixedDeltaTime;
        jumpCooldown -= dt;
        steerDelay -= dt;

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
        if (Grounded) lastWallJump = Vector3.zero;

        UpdateBoard(inp, dt);

        Quaternion look = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = look * Vector3.forward, right = look * Vector3.right;
        if (Grounded)
        {
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal).normalized;
            right = Vector3.ProjectOnPlane(right, groundNormal).normalized;
        }
        Vector3 wish = fwd * inp.forward + right * inp.strafe;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float top = OnBoard ? (inp.sprint ? boardSprintSpeed : boardSpeed) : (inp.sprint ? sprintSpeed : walkSpeed);

        Vector3 v = rb.linearVelocity;
        float g = gravityScale;
        if (!Grounded && v.y < 0f) g *= fallBoost;
        v += Physics.gravity * (g - 1f) * dt;

        // C: slide (on foot). A burst of speed to start it, momentum kept, gentle steering, slowing
        // gradually; a jump during the slide or just after it goes higher
        bool wasSliding = sliding;
        float flatSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        sliding = Grounded && !OnBoard && inp.slide && flatSpeed > (wasSliding ? 1f : 2.5f);
        if (sliding) slideJumpTimer = slideJumpWindow; else slideJumpTimer -= dt;

        touchingWall = !Grounded && FindWall(out wallNormal);

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
            else if (OnBoard) vT = BoardGround(vT, wish, top, inp, dt);
            else vT = RunGround(vT, wish, top, dt);
            v = vT + vN;
        }
        else
        {
            // in the air: nearly as much control as on the ground. Speed over running pace (from hops, the
            // board, a wall jump) is kept and steered rather than braked; nothing pressed slows you gently
            Vector3 vH = new Vector3(v.x, 0f, v.z);
            Vector3 wishH = new Vector3(wish.x, 0f, wish.z).normalized * wish.magnitude;
            float accel = steerDelay > 0f ? airAccel * 0.15f : airAccel;
            if (wishH.sqrMagnitude < 0.01f) vH = Vector3.MoveTowards(vH, Vector3.zero, airStop * dt);
            else if (vH.magnitude > top)
            {
                vH = Vector3.RotateTowards(vH, wishH.normalized * vH.magnitude, accel * 0.2f * dt, 0f);
                vH = Vector3.MoveTowards(vH, wishH * vH.magnitude, accel * dt * 0.3f);
            }
            else vH = Vector3.MoveTowards(vH, wishH * top, accel * dt);
            v.x = vH.x; v.z = vH.z;

            // pressing into a wall on the way down: slide down it slowly
            if (touchingWall && v.y < -wallSlideSpeed && Vector3.Dot(wishH, -wallNormal) > 0.3f) v.y = -wallSlideSpeed;
        }

        // jump buffering: a press shortly before you land still counts; coyote time: shortly after you run
        // off an edge you can still jump. In the air against a wall it's a wall jump instead
        if (inp.jump) jumpBuffer = jumpBufferTime; else jumpBuffer -= dt;
        if (Grounded) sinceGrounded = 0f; else sinceGrounded += dt;
        bool canJump = (Grounded || sinceGrounded < coyoteTime) && jumpCooldown <= 0f;
        bool canWallJump = !canJump && touchingWall && jumpCooldown <= 0f
                           && (lastWallJump == Vector3.zero || Vector3.Dot(wallNormal, lastWallJump) < 0.5f);   // not the same wall twice running
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
            StartJump();
        }
        else if (jumpBuffer > 0f && canWallJump)
        {
            // wall jump: off the wall and up, keeping some of your speed along it
            jumpBuffer = 0f;
            Vector3 along = Vector3.ProjectOnPlane(new Vector3(v.x, 0f, v.z), wallNormal) * 0.6f;
            v = along + wallNormal * wallJumpOut + Vector3.up * wallJumpUp;
            lastWallJump = wallNormal;
            steerDelay = wallJumpSteerDelay;
            touchingWall = false;
            StartJump();
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

        // really big air on the board, and only that: the board kickflips
        if (Grounded) { takeoffY = rb.position.y; flippedThisAir = false; }
        else if (OnBoard && !flippedThisAir && (rb.position.y - takeoffY > bigAirHeight || v.y < -bigAirFallSpeed))
        {
            flippedThisAir = true;
            if (flip < 0f) flip = 0f;
        }
    }

    void StartJump()
    {
        jumpCooldown = 0.15f;   // you're still touching the ground for a moment after take-off; don't count that as another jump
        sliding = false;
        slideJumpTimer = 0f;
        holdingJump = true;
        jumpHold = 0f;
    }

    // on foot: straight to the speed you're asking for, in the direction you're asking. Speed over that
    // (from hopping) bleeds off slowly while you keep going the same way, fast if you turn or let go
    Vector3 RunGround(Vector3 vT, Vector3 wish, float top, float dt)
    {
        if (wish.sqrMagnitude < 0.01f) return Vector3.MoveTowards(vT, Vector3.zero, stopDecel * dt);
        float s = vT.magnitude;
        if (s > top && Vector3.Dot(vT / s, wish.normalized) > 0.9f)
        {
            Vector3 dir = Vector3.RotateTowards(vT / s, wish.normalized, 10f * dt, 0f);
            return dir * Mathf.MoveTowards(s, top, overspeedDecel * dt);
        }
        return Vector3.MoveTowards(vT, wish * top, runAccel * dt);
    }

    // on the board: W (and Shift) push you up to speed, you steer by turning your velocity, nothing
    // pressed rolls on, S brakes
    Vector3 BoardGround(Vector3 vT, Vector3 wish, float top, SlideInput inp, float dt)
    {
        if (wish.sqrMagnitude < 0.01f) return Vector3.MoveTowards(vT, Vector3.zero, boardRollDecel * dt);
        float s = vT.magnitude;
        float along = s > 0.5f ? Vector3.Dot(vT / s, wish.normalized) : 1f;
        if (along < -0.5f) return Vector3.MoveTowards(vT, Vector3.zero, boardBrake * dt);
        if (s < 1f) return Vector3.MoveTowards(vT, wish * top, boardAccel * dt);
        Vector3 dir = Vector3.RotateTowards(vT / s, wish.normalized, boardTurnRate * Mathf.Deg2Rad * dt, 0f);
        if (s < top * wish.magnitude) s = Mathf.MoveTowards(s, top * wish.magnitude, boardAccel * dt);
        else if (s > maxSpeed) s = maxSpeed;
        return dir * s;
    }

    // run over the board and you're on it; Q hops off and leaves it where you are
    void UpdateBoard(SlideInput inp, float dt)
    {
        if (board == null) return;
        pickupBlock -= dt;
        Vector3 feet = rb.position - Vector3.up * ball.radius;
        if (OnBoard && inp.dismount)
        {
            OnBoard = false;
            pickupBlock = 0.6f;
            // leave it on the ground under you (or back home if there's nothing under you)
            RaycastHit g;
            if (Physics.Raycast(rb.position, Vector3.down, out g, 50f, ~0, QueryTriggerInteraction.Ignore))
                board.SetPositionAndRotation(g.point, Quaternion.LookRotation(Vector3.ProjectOnPlane(boardRot * Vector3.forward, g.normal), g.normal));
            else ResetBoard();
        }
        else if (!OnBoard && pickupBlock <= 0f && Vector3.Distance(feet, board.position) < pickupRadius && Mathf.Abs(feet.y - board.position.y) < 0.6f)
        {
            OnBoard = true;
            boardRot = board.rotation;
            sliding = false;
        }
        if (!OnBoard && pickupBlock > 0f && Vector3.Distance(feet, board.position) < pickupRadius) pickupBlock = 0.3f;   // move away first
    }

    // a wall touching you, if any (checked all the way round)
    bool FindWall(out Vector3 normal)
    {
        normal = Vector3.zero;
        float best = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            RaycastHit h;
            if (!Physics.SphereCast(rb.position, ball.radius * 0.8f, d, out h, 0.25f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(h.normal.y) > 0.3f || h.distance >= best) continue;
            best = h.distance;
            normal = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
        }
        return best < float.MaxValue;
    }

    // ---- camera, board and runner ---------------------------------------------------------------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (cam != null)
        {
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            eyeDrop = Mathf.Lerp(eyeDrop, sliding ? 0.65f : 0f, 1f - Mathf.Exp(-12f * dt));
            Vector3 eye = transform.position + Vector3.up * (eyeHeight - ball.radius - eyeDrop + (OnBoard ? 0.2f : 0f));
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

        Vector3 up = Grounded ? groundNormal : Vector3.up;
        Vector3 feet = transform.position - up * ball.radius;

        // the board, while you're riding it: under your feet, facing where you're going, tilted with
        // the ground, and spinning once round its length if you've kickflipped
        float pop = 0f, spin = 0f;
        if (flip >= 0f)
        {
            flip += dt / flipTime;
            if (flip >= 1f || !OnBoard) flip = -1f;
            else { pop = Mathf.Sin(flip * Mathf.PI) * flipHop; spin = Mathf.SmoothStep(0f, 360f, flip); }
        }
        if (board != null && OnBoard)
        {
            Vector3 heading = Vector3.ProjectOnPlane(rb.linearVelocity, up);
            if (heading.magnitude < 1f) heading = Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, up);
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(boardRot * Vector3.forward, up);
            boardRot = Quaternion.Slerp(boardRot, Quaternion.LookRotation(heading, up), 1f - Mathf.Exp(-14f * dt));
            Quaternion r = boardRot * Quaternion.AngleAxis(spin, Vector3.forward);
            Vector3 mid = Vector3.up * 0.16f;   // spin round the middle of the deck rather than the wheels
            board.SetPositionAndRotation(feet + up * pop + boardRot * mid - r * mid, r);
        }

        if (rider != null)
        {
            rider.gameObject.SetActive(thirdPerson);
            if (thirdPerson) PoseRider(feet, up, pop, dt);
        }
    }

    // on the board: side-on, knees bent; on foot: face where you're running and swing your legs; crouch
    // low in a slide
    void PoseRider(Vector3 feet, Vector3 up, float pop, float dt)
    {
        float stance, crouch, swing = 0f;
        if (OnBoard)
        {
            rider.SetPositionAndRotation(feet + up * 0.2f + up * pop * 0.85f, boardRot * Quaternion.Euler(0f, 90f, 0f));
            stance = 0.3f; crouch = pop > 0f ? 0.22f : 0.1f;
            stride = 0f;
        }
        else
        {
            Vector3 h = rb.linearVelocity; h.y = 0f;
            Vector3 face = h.magnitude > 0.5f ? h : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Quaternion want = Quaternion.LookRotation(face, Vector3.up);
            rider.SetPositionAndRotation(feet, Quaternion.Slerp(rider.rotation, want, 1f - Mathf.Exp(-12f * dt)));
            stance = 0.11f; crouch = sliding ? 0.35f : Grounded ? 0f : 0.05f;
            if (Grounded && !sliding) stride += h.magnitude * dt * 2.2f;
            swing = Grounded && !sliding ? Mathf.Sin(stride) * Mathf.Clamp01(h.magnitude / walkSpeed) * 32f : sliding ? 40f : 0f;
        }

        // legs pivot at the hips; spread them for the stance, bend them for the crouch
        float hip = 0.9f - crouch;
        float spread = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp((stance - 0.11f) / 0.9f, -1f, 1f));
        if (body != null) body.localPosition = new Vector3(0f, hip, 0f);
        if (legL != null) { legL.localPosition = new Vector3(-0.11f, hip, 0f); legL.localRotation = Quaternion.Euler(sliding ? -swing : swing, 0f, -spread); legL.localScale = new Vector3(1f, hip / 0.9f, 1f); }
        if (legR != null) { legR.localPosition = new Vector3(0.11f, hip, 0f); legR.localRotation = Quaternion.Euler(-swing, 0f, spread); legR.localScale = new Vector3(1f, hip / 0.9f, 1f); }
    }
}
