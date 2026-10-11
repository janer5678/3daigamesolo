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

// You and your skateboard. On foot: tight control, Shift sprints, Space jumps (hold it to go higher) with
// coyote time and jump buffering, C slides (and you fit under low things). The board's always with you
// (right click steps off, carrying it, and back on): cruise, or hold Shift to go faster; kickers launch
// you; jump onto rails to grind them (Shift: power grind; the curvy ones grab you from a couple of metres
// and rush you along); C is a powerslide that comes out with a boost. Ride up the ramp on the back of a
// flying car and you fly it. Really big air, and only that, sometimes kickflips the board.
// V switches first and third person.[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;
    [Tooltip("Fall below this height (off the edge of a floating level) and you're put back at the last checkpoint.")]
    public float fallLimit = -300f;
    [Tooltip("The runner, seen in third person. Children: Body, Leg L, Leg R (pivots at the hips).")]
    public Transform rider;
    [Tooltip("Your skateboard.")]
    public Transform board;
    [Tooltip("You always have the board: you start on it and stay on it however you land, and right click steps off (carrying it) and back on.")]
    public bool alwaysBoard = true;
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
    public float walkSpeed = 4.5f;
    [Tooltip("Holding Shift.")]
    public float sprintSpeed = 7.5f;
    [Tooltip("How quickly you get to the speed you're asking for, or turn. High = instant.")]
    public float runAccel = 90f;
    [Tooltip("How quickly you stop when you let go.")]
    public float stopDecel = 70f;
    [Tooltip("Speed over running pace from hopping bleeds off this slowly while you keep going the same way.")]
    public float overspeedDecel = 8f;
    [Tooltip("Overall speed cap along the ground.")]
    public float maxSpeed = 32f;
    [Tooltip("How hard a rocket car's explosion throws you upward.")]
    public float explodeFling = 16f;

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
    public float hopSpeedCap = 12f;

    [Tooltip("Pressing into a wall while falling: you slide down it no faster than this.")]
    public float wallSlideSpeed = 3f;

    [Tooltip("The body above the ball: switched off while you slide, so you fit under low things.")]
    public Collider torso;

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
    [Tooltip("On the board without Shift: a cruise; faster than this and you ease back down to it.")]
    public float boardSpeed = 7f;
    [Tooltip("Holding Shift on the board: flat out.")]
    public float boardSprintSpeed = 14f;
    public float boardAccel = 16f;
    [Tooltip("Let go of Shift and you ease back to cruising speed this quickly.")]
    public float boardCoastDecel = 5f;
    [Tooltip("Flying off the top of a ramp on the board: an extra pop upward, so kickers send you.")]
    public float rampPop = 3f;
    [Tooltip("C on the board: a powerslide. You turn harder, keep your speed, and come out of it with this much extra.")]
    public float powerslideBoost = 4f;
    public float boardTurnRate = 360f;
    [Tooltip("With nothing pressed the board rolls on, slowing this gently.")]
    public float boardRollDecel = 2f;
    public float boardBrake = 30f;
    [Tooltip("How close you have to run past the board to jump on it.")]
    public float pickupRadius = 0.9f;
    [Tooltip("Only really big air kickflips the board: this high above where you took off, or falling this fast.")]
    public float bigAirHeight = 15f;
    public float bigAirFallSpeed = 40f;
    [Tooltip("...and even then, only this often.")]
    [Range(0f, 1f)] public float kickflipChance = 0.35f;
    public float flipTime = 0.45f;
    public float flipHop = 0.4f;
    [Header("Rails (on the board)")]
    [Tooltip("How close (sideways) to a rail you need to be for the board to lock onto it.")]
    public float railCatch = 0.5f;
    [Tooltip("How far above riding height on a rail you can be and still snap down onto it.")]
    public float railCatchAbove = 0.6f;
    [Tooltip("Slowest you grind at; you always get carried along the rail at least this fast, even uphill.")]
    public float grindMinSpeed = 5f;
    public float grindFriction = 0.6f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public bool OnBoard { get; private set; }
    public bool Sliding { get { return sliding; } }
    public bool OnWall { get { return touchingWall; } }
    public bool Piloting { get { return piloting != null; } }
    public bool Flipping { get { return flip >= 0f; } }
    public bool Grinding { get { return grindRail != null; } }
    public string TrickName { get { return grindRail != null ? GrindStyles[grindStyle].name : null; } }
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
    float slideTime;
    HoverCar piloting;     // the flying car you're stuck on and flying, if any
    float noPilotTimer;
    float rampTimer;
    Mover lastRide;
    Vector3 lastPlatV;
    Vector3 lastGroundNormal = Vector3.up;
    Vector3 wallNormal;
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
    bool carrying;         // stepped off (right click): carrying the board, right click again to drop back on

    SkateRail[] rails;
    Updraft[] updrafts;
    float shake;           // camera shake left, after an explosion
    float bounceBlock;     // just bounced: don't bounce again off the same thing straight away
    SkateRail grindRail;   // the rail you're grinding, if any
    int grindSeg, grindStyle = 4;
    float grindU, grindSpeed, noRailTimer, lastStrafe;
    Vector3 visualOffset;  // eases the camera, board and runner over when the body snaps onto a rail

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        ball = GetComponent<SphereCollider>();
        if (cam != null) camera3d = cam.GetComponent<Camera>();
        if (rider != null) { body = rider.Find("Body"); legL = rider.Find("Leg L"); legR = rider.Find("Leg R"); }
        if (board != null && boardHome == Vector3.zero) { boardHome = board.position; boardHomeRot = board.rotation; }
        rails = Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None);
        updrafts = Object.FindObjectsByType<Updraft>(FindObjectsSortMode.None);
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
        if (alwaysBoard && board != null) { OnBoard = true; carrying = false; boardRot = Quaternion.LookRotation(s.forward); }
        jumpBuffer = 0f;
        sinceGrounded = 1f;
        slideJumpTimer = 0f;
        if (piloting != null) { piloting.Release(); piloting = null; }
        noPilotTimer = 0f;
        flip = -1f;
        grindRail = null;
        noRailTimer = 0f;
        visualOffset = Vector3.zero;
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
        dismountLatch |= Input.GetKeyDown(KeyCode.Q) || (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(1));   // Q or right click: hop off the board
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
        noPilotTimer -= dt;
        noRailTimer -= dt;

        if (grindRail != null)
        {
            if (Grind(inp, dt)) return;
        }

        if (piloting != null)
        {
            if (inp.dismount || (inp.jump && !piloting.Flying))
            {
                // jump off: up and away with the car's speed
                Vector3 off = piloting.Velocity + Vector3.up * jumpSpeed;
                piloting.Release();
                piloting = null;
                noPilotTimer = 1f;
                rb.linearVelocity = off;
                StartJump();
                return;
            }
            piloting.Fly(inp, yaw, dt);
            if (piloting.Exploded)
            {
                // boom: thrown up and on, the way it was going
                Vector3 flung = piloting.Velocity * 0.7f + Vector3.up * explodeFling;
                piloting = null;
                noPilotTimer = 1f;
                rb.linearVelocity = flung;
                shake = 0.6f;
                SlideHUD.Flash("BOOM");
                StartJump();
                holdingJump = false;
                return;
            }
            Vector3 seat = piloting.SeatPosition + Vector3.up * ball.radius;
            rb.position = seat; transform.position = seat;
            rb.linearVelocity = piloting.Velocity - Physics.gravity * dt;
            Grounded = true;
            return;
        }

        RaycastHit hit;
        Grounded = Physics.SphereCast(rb.position + Vector3.up * CastBack, ball.radius * 0.95f, Vector3.down, out hit, 0.12f + CastBack, ~0, QueryTriggerInteraction.Ignore)
                   && hit.normal.y > 0.35f;
        groundNormal = Grounded ? hit.normal : Vector3.up;
        Collider groundCol = Grounded ? hit.collider : null;   // what you're standing on (for pads and bouncers)
        // a hard landing can sink you into the ground for a step: put you straight back on top, so the
        // camera doesn't dip and the next jump isn't lost
        float sunk = Grounded ? CastBack - hit.distance - ball.radius * 0.05f : 0f;
        if (sunk > 0.005f)
        {
            rb.position += Vector3.up * sunk;
            Vector3 lv = rb.linearVelocity;
            if (lv.y < 0f) rb.linearVelocity = new Vector3(lv.x, 0f, lv.z);
        }
        // ride up the ramp on the back of a flying car and you're stuck on it, flying it
        HoverCar hover = Grounded && hit.collider != null ? hit.collider.GetComponentInParent<HoverCar>() : null;
        if (hover != null && piloting == null && noPilotTimer <= 0f) { piloting = hover; hover.Board(); }

        // flying off the top of a ramp on the board: keep all your speed, pointed the way the ramp was going,
        // plus an extra pop, so kickers send you. (At speed you're only on a kicker for a step or two, so
        // look for ramp just under the board rather than waiting to be properly on it.)
        RaycastHit rh;
        rampTimer -= dt;
        if (OnBoard && Physics.SphereCast(rb.position + Vector3.up * CastBack, ball.radius * 0.9f, Vector3.down, out rh, 0.5f + CastBack, ~0, QueryTriggerInteraction.Ignore)
            && rh.normal.y < 0.97f && rh.normal.y > 0.4f && Vector3.Dot(new Vector3(rh.normal.x, 0f, rh.normal.z), rb.linearVelocity) < 0f)   // a ramp you're going up
        {
            // the ramp on the back of a flying car: that's you on it, flying it (rather than launched off it)
            var hc = rh.collider.GetComponentInParent<HoverCar>();
            if (hc != null && piloting == null && noPilotTimer <= 0f) { piloting = hc; hc.Board(); return; }
            lastGroundNormal = rh.normal; rampTimer = 0.12f;
        }
        if (OnBoard && !Grounded && rampTimer > 0f && grindRail == null)
        {
            Vector3 lv = rb.linearVelocity;
            Vector3 along = Vector3.ProjectOnPlane(lv, lastGroundNormal);
            if (along.y > 0.5f && lv.y > -1f)
            {
                rb.linearVelocity = along.normalized * lv.magnitude + Vector3.up * rampPop;
                rampTimer = 0f;
            }
        }

        // standing on something moving (a bus, a car, the train): you move with it
        Mover ride = Grounded && hit.collider != null && hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.GetComponent<Mover>() : null;
        Vector3 platV = ride != null ? ride.Velocity : Vector3.zero;
        float platUp = platV.y;
        platV.y = 0f;

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
        sliding = Grounded && inp.slide && flatSpeed > (wasSliding ? 1f : 2.5f);
        // under something low: you can't stand up yet, so you keep sliding
        if (wasSliding && !sliding && Physics.SphereCast(rb.position, ball.radius * 0.8f, Vector3.up, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) sliding = true;
        if (sliding) { slideJumpTimer = slideJumpWindow; slideTime += dt; }
        else
        {
            slideJumpTimer -= dt;
            // coming out of a powerslide on the board: a burst of speed
            if (wasSliding && OnBoard && slideTime > 0.35f)
            {
                Vector3 fv = new Vector3(v.x, 0f, v.z);
                if (fv.magnitude > 1f) { float s = Mathf.Min(fv.magnitude + powerslideBoost, boardSprintSpeed + powerslideBoost); v.x = fv.normalized.x * s; v.z = fv.normalized.z * s; }
            }
            slideTime = 0f;
        }
        if (torso != null) torso.enabled = !sliding;

        touchingWall = !Grounded && FindWall(out wallNormal);
        if (Grounded)
        {
            // work in the plane of the ground; the part into/out of it is left to physics
            Vector3 vN = Vector3.Project(v, groundNormal);
            // relative to whatever you're standing on. Landing on something moving, you take on its speed (unless
            // you were already going with it); after that you keep your speed relative to it as it turns and speeds up
            Vector3 basis = Vector3.zero;
            if (ride != null)
            {
                Vector3 vf = new Vector3(v.x, 0f, v.z);
                basis = ride == lastRide ? lastPlatV : ((vf - platV).sqrMagnitude < vf.sqrMagnitude ? platV : Vector3.zero);
            }
            Vector3 vT = v - vN - basis;
            if (sliding && OnBoard)
            {
                // powerslide: carve hard, barely lose any speed
                if (wish.sqrMagnitude > 0.01f && vT.magnitude > 0.5f)
                    vT = Vector3.RotateTowards(vT, wish.normalized * vT.magnitude, boardTurnRate * 1.6f * Mathf.Deg2Rad * dt, 0f);
                vT = Vector3.MoveTowards(vT, Vector3.zero, 1.5f * dt);
            }
            else if (sliding)
            {
                if (!wasSliding && vT.magnitude > 0.1f)
                    vT += vT.normalized * Mathf.Min(slideKick, Mathf.Max(0f, maxSpeed - vT.magnitude));
                if (wish.sqrMagnitude > 0.01f && vT.magnitude > 0.5f)
                    vT = Vector3.RotateTowards(vT, wish.normalized * vT.magnitude, slideTurnRate * Mathf.Deg2Rad * dt, 0f);
                vT = Vector3.MoveTowards(vT, Vector3.zero, slideDecel * dt);
            }
            else if (OnBoard) vT = BoardGround(vT, wish, top, inp, dt);
            else vT = RunGround(vT, wish, top, dt);
            v = vT + vN + platV;
            if (ride != null && platUp > 0f) v.y = Mathf.Max(v.y, platUp);   // a flying car going up lifts you with it
        }
        else
        {
            // in the air: nearly as much control as on the ground. Speed over running pace (from hops, the
            // board, a wall jump) is kept and steered rather than braked; nothing pressed slows you gently
            Vector3 vH = new Vector3(v.x, 0f, v.z);
            Vector3 wishH = new Vector3(wish.x, 0f, wish.z).normalized * wish.magnitude;
            float accel = airAccel;
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

        // boost pads, bounce pads and updrafts
        bounceBlock -= dt;
        if (groundCol != null)
        {
            var pad = groundCol.GetComponent<BoostPad>();
            if (pad != null)
            {
                // shot off along the arrow, at least the pad's speed
                Vector3 dir = new Vector3(pad.direction.x, 0f, pad.direction.z).normalized;
                float s = Mathf.Max(pad.speed, Vector3.Dot(new Vector3(v.x, 0f, v.z), dir));
                v = new Vector3(dir.x * s, v.y, dir.z * s);
            }
            var bouncer = groundCol.GetComponent<Bouncer>();
            if (bouncer != null && bounceBlock <= 0f)
            {
                v.y = bouncer.speed;
                bounceBlock = 0.3f;
                sinceGrounded = 1f;
                StartJump();
                holdingJump = false;
            }
        }
        if (updrafts != null)
            foreach (var u in updrafts)
            {
                if (u == null || !u.Contains(rb.position)) continue;
                // carried up the column; easing off near the top so you can step off onto whatever's there
                float target = rb.position.y > u.Top - 3f ? u.lift * 0.4f : u.lift;
                v.y = Mathf.MoveTowards(v.y, target, 80f * dt);
                sinceGrounded = 1f;
                break;
            }
        rb.linearVelocity = v;

        // the speed cap is on how fast you're going along the ground, so jumping never eats your speed
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }

        lastStrafe = inp.strafe;
        if (OnBoard && noRailTimer <= 0f) TryLockOntoRail();

        lastRide = ride;
        lastPlatV = platV;

        // really big air on the board, and only that: the board kickflips
        if (Grounded) { takeoffY = rb.position.y; flippedThisAir = false; }
        else if (OnBoard && !flippedThisAir && (rb.position.y - takeoffY > bigAirHeight || v.y < -bigAirFallSpeed))
        {
            flippedThisAir = true;
            if (flip < 0f && Random.value < kickflipChance) flip = 0f;
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
        if (wish.sqrMagnitude < 0.01f)
        {
            // rolling with nothing pressed: slow gently, and a bit more if you're over cruising speed without Shift
            float roll = !inp.sprint && vT.magnitude > top ? boardCoastDecel : boardRollDecel;
            return Vector3.MoveTowards(vT, Vector3.zero, roll * dt);
        }
        float s = vT.magnitude;
        float along = s > 0.5f ? Vector3.Dot(vT / s, wish.normalized) : 1f;
        if (along < -0.5f) return Vector3.MoveTowards(vT, Vector3.zero, boardBrake * dt);
        if (s < 1f) return Vector3.MoveTowards(vT, wish * top, boardAccel * dt);
        Vector3 dir = Vector3.RotateTowards(vT / s, wish.normalized, boardTurnRate * Mathf.Deg2Rad * dt, 0f);
        if (s < top * wish.magnitude) s = Mathf.MoveTowards(s, top * wish.magnitude, boardAccel * dt);
        else if (!inp.sprint && s > top) s = Mathf.MoveTowards(s, top, boardCoastDecel * dt);   // let go of Shift: ease back to cruising
        if (s > maxSpeed) s = maxSpeed;
        return dir * s;
    }

    // run over the board and you're on it; Q hops off and leaves it where you are
    void UpdateBoard(SlideInput inp, float dt)
    {
        if (board == null) return;
        pickupBlock -= dt;
        Vector3 feet = rb.position - Vector3.up * ball.radius;
        if (alwaysBoard)
        {
            // step off (carrying it) and back on with right click
            if (inp.dismount && OnBoard) { OnBoard = false; carrying = true; }
            else if (inp.dismount && carrying) Remount();
            return;
        }
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

    // ---- rails --------------------------------------------------------------------------------

    float RideHeight(SkateRail r) { return r.radius + ball.radius; }

    // on the board, in the air, near a rail at about its height: snap onto it and grind. Any angle works;
    // your speed carries over, and you go whichever way along the rail is closer to where you're heading
    void TryLockOntoRail()
    {
        if (rails == null) return;
        Vector3 p = rb.position, v = rb.linearVelocity;
        if (v.y > 6f) return;
        float flatSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        SkateRail best = null; int bestSeg = 0; float bestU = 0f, bestD = float.MaxValue;
        foreach (var r in rails)
        {
            if (r == null || r.points == null || r.points.Length < 2) continue;
            int seg; float u;
            Vector3 q = r.Closest(p, out seg, out u);
            Vector3 dir = (r.points[seg + 1] - r.points[seg]).normalized;
            Vector3 off = p - q;
            if (Mathf.Abs(Vector3.Dot(off, dir)) > 0.05f) continue;              // past the end of the rail
            Vector3 side = off - dir * Vector3.Dot(off, dir);
            float above = side.y - RideHeight(r);
            side.y = 0f;
            float reach = Mathf.Max(railCatch, r.catchRadius);
            if (Grounded && r.catchRadius <= 0f) continue;                       // ordinary rails: only when you jump onto them
            if (flatSpeed < 1.5f && r.forceSpeed <= 0f) continue;
            if (side.magnitude > reach || above < -Mathf.Max(0.3f, r.catchRadius * 0.6f) || above > Mathf.Max(railCatchAbove, r.catchRadius)) continue;
            float d = side.sqrMagnitude + above * above;
            if (d < bestD) { bestD = d; best = r; bestSeg = seg; bestU = u; }
        }
        if (best == null) return;

        Vector3 rd = (best.points[bestSeg + 1] - best.points[bestSeg]).normalized;
        Vector3 flatV = new Vector3(v.x, 0f, v.z);
        Vector3 heading = flatV.magnitude > 1f ? flatV : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float sign = Vector3.Dot(heading, rd) >= 0f ? 1f : -1f;
        float speed = Mathf.Max(Mathf.Abs(Vector3.Dot(v, rd)), flatV.magnitude * 0.8f, grindMinSpeed);
        grindRail = best; grindSeg = bestSeg; grindU = bestU;
        Vector3 onRail = Vector3.Lerp(best.points[bestSeg], best.points[bestSeg + 1], bestU) + Vector3.up * RideHeight(best);
        visualOffset += p - onRail;   // the body snaps on, what you see slides over
        // the grind depends on the angle you come in at, and which side
        Vector3 railFlat = new Vector3(rd.x, 0f, rd.z).normalized * sign;
        float a = Vector3.SignedAngle(railFlat, new Vector3(heading.x, 0f, heading.z), Vector3.up);
        float abs = Mathf.Abs(a);
        string pick = abs < 12f ? (pitch > 25f ? "Nosegrind" : pitch < -10f ? "5-0" : "50-50")
                    : abs < 35f ? (a > 0f ? "Crooked" : "Feeble")
                    : abs < 62f ? (a > 0f ? "Noseslide" : "Tailslide")
                    : (a > 0f ? "Boardslide" : "Lipslide");
        grindStyle = System.Array.FindIndex(GrindStyles, s => s.name == pick);
        grindSpeed = sign * speed;
    }

    public struct GrindStyleDef { public string name; public float yaw, pitch, roll; }
    // in order round the ring A/D steps through while you're grinding
    public static readonly GrindStyleDef[] GrindStyles =
    {
        new GrindStyleDef { name = "Lipslide",   yaw = -90f, pitch =  0f, roll = -6f },
        new GrindStyleDef { name = "Tailslide",  yaw = -68f, pitch = -12f, roll = 0f },
        new GrindStyleDef { name = "Feeble",     yaw = -18f, pitch = -7f, roll = 8f },
        new GrindStyleDef { name = "5-0",        yaw =   0f, pitch = -11f, roll = 0f },
        new GrindStyleDef { name = "50-50",      yaw =   0f, pitch =  0f, roll = 0f },
        new GrindStyleDef { name = "Nosegrind",  yaw =   0f, pitch = 11f, roll = 0f },
        new GrindStyleDef { name = "Crooked",    yaw =  18f, pitch =  8f, roll = -8f },
        new GrindStyleDef { name = "Noseslide",  yaw =  68f, pitch = 12f, roll = 0f },
        new GrindStyleDef { name = "Boardslide", yaw =  90f, pitch =  0f, roll = 6f },
    };

    // one step along the rail; false when you've come off it and the rest of the step should run
    bool Grind(SlideInput inp, float dt)
    {
        int stepStyle = inp.strafe > 0.5f && lastStrafe <= 0.5f ? 1 : inp.strafe < -0.5f && lastStrafe >= -0.5f ? -1 : 0;
        lastStrafe = inp.strafe;
        if (stepStyle != 0) grindStyle = Mathf.Clamp(grindStyle + stepStyle, 0, GrindStyles.Length - 1);

        Vector3[] pts = grindRail.points;
        Vector3 dir = (pts[grindSeg + 1] - pts[grindSeg]).normalized;
        if (inp.jump || inp.dismount || !OnBoard)
        {
            // jump off (Space), or hop off the board altogether (right click / Q), keeping your speed along the rail
            Vector3 v = dir * grindSpeed;
            if (inp.jump) { v.y = Mathf.Max(v.y, 0f) + jumpSpeed; StartJump(); }
            LeaveRail(v);
            return inp.jump;
        }

        // gravity pulls you down sloped rails (and up them you're carried at least grindMinSpeed); a little friction
        grindSpeed -= Physics.gravity.magnitude * gravityScale * 0.5f * dir.y * dt;
        float s = Mathf.Max(grindMinSpeed, Mathf.Abs(grindSpeed) - grindFriction * dt);
        if (inp.sprint) s = Mathf.MoveTowards(s, Mathf.Max(s, boardSprintSpeed), 20f * dt);   // Shift: power grind, flat out even uphill
        if (grindRail.forceSpeed > 0f) s = Mathf.MoveTowards(s, Mathf.Max(s, grindRail.forceSpeed), 40f * dt);   // the curvy rails rush you along
        grindSpeed = Mathf.Sign(grindSpeed) * Mathf.Min(s, maxSpeed);

        float len = Vector3.Distance(pts[grindSeg], pts[grindSeg + 1]);
        grindU += grindSpeed * dt / Mathf.Max(len, 0.01f);
        while (grindU > 1f || grindU < 0f)
        {
            bool fwd = grindU > 1f;
            int next = grindSeg + (fwd ? 1 : -1);
            if (next < 0 || next >= pts.Length - 1) { LeaveRail(dir * grindSpeed); return true; }   // off the end: fly on
            float over = (fwd ? grindU - 1f : -grindU) * len;
            grindSeg = next;
            len = Vector3.Distance(pts[grindSeg], pts[grindSeg + 1]);
            grindU = fwd ? over / len : 1f - over / len;
            dir = (pts[grindSeg + 1] - pts[grindSeg]).normalized;
        }

        Vector3 on = Vector3.Lerp(pts[grindSeg], pts[grindSeg + 1], grindU) + Vector3.up * RideHeight(grindRail);
        rb.position = on;
        transform.position = on;
        rb.linearVelocity = dir * grindSpeed - Physics.gravity * dt;   // cancel the physics step's gravity
        Grounded = false;
        return true;
    }

    void LeaveRail(Vector3 v)
    {
        grindRail = null;
        noRailTimer = 0.35f;
        rb.linearVelocity = v;
    }
    void Remount()
    {
        OnBoard = true;
        carrying = false;
        boardRot = Quaternion.Euler(0f, yaw, 0f);
        sliding = false;
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
        visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, 1f - Mathf.Exp(-16f * dt));
        if (cam != null)
        {
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            eyeDrop = Mathf.Lerp(eyeDrop, sliding ? 0.65f : 0f, 1f - Mathf.Exp(-12f * dt));
            Vector3 eye = transform.position + visualOffset + Vector3.up * (eyeHeight - ball.radius - eyeDrop + (OnBoard ? 0.2f : 0f));
            shake = Mathf.Max(0f, shake - dt);
            if (shake > 0f) eye += Random.insideUnitSphere * shake * 0.5f;
            if (thirdPerson)
            {
                // orbit behind you, pulled in if something's in the way
                Vector3 pivot = transform.position + Vector3.up * (camPivotHeight - ball.radius);
                Vector3 back = view * Vector3.back;
                float dist = camDistance;
                foreach (var h in Physics.SphereCastAll(pivot, 0.25f, back, camDistance, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider != ball && h.collider != torso && h.distance > 0f) dist = Mathf.Min(dist, h.distance);
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
        Vector3 feet = transform.position + visualOffset - up * ball.radius;

        // the board, while you're riding it: under your feet, facing where you're going, tilted with
        // the ground, and spinning once round its length if you've kickflipped
        float pop = 0f, spin = 0f;
        if (flip >= 0f)
        {
            flip += dt / flipTime;
            if (flip >= 1f || !OnBoard) flip = -1f;
            else { pop = Mathf.Sin(flip * Mathf.PI) * flipHop; spin = Mathf.SmoothStep(0f, 360f, flip); }
        }
        if (board != null) board.gameObject.SetActive(!carrying);   // carried: tucked under your arm, out of sight
        if (board != null && OnBoard)
        {
            Vector3 heading = Vector3.ProjectOnPlane(rb.linearVelocity, up);
            if (heading.magnitude < 1f) heading = Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, up);
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(boardRot * Vector3.forward, up);
            Quaternion want = Quaternion.LookRotation(heading, up);
            if (sliding && grindRail == null) want *= Quaternion.Euler(0f, 75f, 0f);   // powerslide: the board swings sideways
            float turnIn = 14f;
            if (grindRail != null)
            {
                // on a rail, sitting on it the way the grind says
                Vector3[] pts = grindRail.points;
                Vector3 along = (pts[grindSeg + 1] - pts[grindSeg]).normalized * Mathf.Sign(grindSpeed);
                var gs = GrindStyles[grindStyle];
                want = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(gs.pitch, gs.yaw, gs.roll);
                turnIn = 30f;
            }
            boardRot = Quaternion.Slerp(boardRot, want, 1f - Mathf.Exp(-turnIn * dt));
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
