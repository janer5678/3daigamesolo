using UnityEngine;

// What the player is pressing, sampled each frame and used on each physics step.
public struct SlideInput
{
    public float forward;   // W/S
    public float strafe;    // A/D
    public bool push;       // Shift, held: on the board
    public bool jump;       // Space, pressed
}

// On foot by default: slow, snappy walking. Hold Shift and you're on the board (it appears under
// you): faster, it grips and steers tightly, and downhill speed is kept. Let go of Shift and you
// step off and slow to walking pace. On the board you lock onto ramps (you follow the surface
// instead of flying off it) and onto rails (you grind along them until you jump or let go).
// Gravity is heavier than Unity's default so jumps and drops don't feel floaty; a little less so
// on the board. V switches between first and third person; K kickflips the board (just for show).
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SlideController : MonoBehaviour
{
    public Transform cam;
    public Transform startPoint;
    [Tooltip("The board under your feet; turns to face where you're going and tilts with the ground.")]
    public Transform board;
    [Tooltip("The skater, seen in third person. Children: Body, Leg L, Leg R (pivots at the hips).")]
    public Transform rider;

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

    [Header("On foot")]
    public float walkSpeed = 5.5f;
    [Tooltip("How quickly you get up to walking speed. High = snappy.")]
    public float walkAccel = 40f;
    [Tooltip("How quickly you stop, or drop back to walking pace after stepping off the board.")]
    public float stopDecel = 14f;

    [Header("On the board (hold Shift)")]
    public float boardSpeed = 15f;
    [Tooltip("How quickly pushing gets you up to board speed.")]
    public float boardAccel = 16f;
    [Tooltip("With nothing else pressed, how quickly the board slows you.")]
    public float boardRollDecel = 5f;
    [Tooltip("Degrees per second you can turn on the board at pushing speed.")]
    public float boardTurnRate = 600f;
    [Tooltip("Degrees per second downhill speed turns when you're going faster than you can push.")]
    public float fastTurnRate = 450f;
    [Tooltip("How hard pressing against your direction (S) brakes on the board.")]
    public float boardBrake = 22f;
    [Tooltip("Gravity multiplier while on the board. A touch lighter than on foot; the board's jump is stronger to match.")]
    public float boardGravityScale = 2.1f;
    public float boardJumpSpeed = 7f;
    [Tooltip("Overall speed cap, mostly reached going downhill on the board.")]
    public float maxSpeed = 22f;

    [Header("Ramp lock (on the board)")]
    [Tooltip("How far the board will reach to stay stuck to a curving surface each step. Small, so kicker lips still launch you.")]
    public float lockReach = 0.15f;
    [Tooltip("Biggest change of slope (degrees) in one step the board follows rather than leaving the surface.")]
    public float lockMaxBend = 50f;
    [Tooltip("On walls steeper than about 70 degrees you fall off below this speed.")]
    public float lockMinWallSpeed = 2.5f;

    [Header("Rail lock (on the board)")]
    [Tooltip("How close (sideways) to a rail you need to be for the board to lock onto it.")]
    public float railCatch = 0.8f;
    [Tooltip("How far above riding height on a rail you can be and still snap down onto it.")]
    public float railCatchAbove = 0.9f;
    [Tooltip("Slowest you grind at; you always get carried along the rail at least this fast.")]
    public float grindMinSpeed = 4f;
    [Tooltip("How quickly grinding slows you on a flat rail.")]
    public float grindFriction = 0.6f;

    [Header("Air")]
    [Tooltip("Gravity multiplier. 1 is Unity's default; higher is less floaty.")]
    public float gravityScale = 2.2f;
    public float jumpSpeed = 6.2f;
    [Tooltip("How quickly you can change direction in the air. Nothing pressed keeps your momentum.")]
    public float airAccel = 24f;
    [Tooltip("Degrees per second you can turn fast momentum in the air.")]
    public float airTurnRate = 240f;

    [Header("Kickflip (K)")]
    public float flipTime = 0.45f;
    public float flipHop = 0.4f;

    public float yaw, pitch;
    public bool Grounded { get; private set; }
    public bool OnBoard { get; private set; }
    public bool Locked { get { return locked; } }
    public bool Grinding { get { return grindRail != null; } }
    public Vector3 Velocity { get { return rb.linearVelocity; } }
    public float Speed { get { return rb.linearVelocity.magnitude; } }

    Rigidbody rb;
    SphereCollider ball;
    Vector3 groundNormal = Vector3.up;
    SlideInput input;
    bool jumpLatch;
    bool locked;           // riding stuck to a surface on the board
    float noLockTimer;     // just jumped: don't get pulled back down
    Camera camera3d;

    SkateRail[] rails;
    SkateRail grindRail;   // the rail you're locked onto, if any
    int grindSeg;
    float grindU, grindSpeed;
    float noRailTimer;     // just got off a rail: don't lock straight back on

    Quaternion boardRot = Quaternion.identity;
    float flip = -1f;      // kickflip progress 0..1, or <0 when not flipping
    float stride;          // walking leg swing
    Transform body, legL, legR;

    void Awake() { Setup(); }

    public void Setup()
    {
        rb = GetComponent<Rigidbody>();
        ball = GetComponent<SphereCollider>();
        if (cam != null) camera3d = cam.GetComponent<Camera>();
        rails = Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None);
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
        Transform s = startPoint != null ? startPoint : transform;
        rb.position = s.position + Vector3.up * ball.radius;
        transform.position = rb.position;
        rb.linearVelocity = Vector3.zero;
        grindRail = null;
        noRailTimer = noLockTimer = 0f;
        flip = -1f;
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
        if (Input.GetKeyDown(KeyCode.K)) Kickflip();

        input.forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        input.strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        input.push = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jumpLatch |= Input.GetKeyDown(KeyCode.Space);
    }

    // the board spins once round its length and pops up under you; purely for show
    public void Kickflip()
    {
        if (OnBoard && flip < 0f) flip = 0f;
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
        float dt = Time.fixedDeltaTime;
        OnBoard = inp.push;
        noLockTimer -= dt;
        noRailTimer -= dt;

        if (grindRail != null)
        {
            if (Grind(inp, dt)) return;
        }

        RaycastHit hit;

        // ramp lock: on the board, keep following the surface you're riding (up quarterpipe walls,
        // over the top of a funbox) instead of flying off it, and carry your speed round the curve
        bool wasLocked = locked;
        locked = false;
        if (OnBoard && wasLocked && noLockTimer <= 0f)
        {
            Vector3 n0 = groundNormal;
            Vector3 v0 = rb.linearVelocity;
            float along = Vector3.ProjectOnPlane(v0, n0).magnitude;
            if (Physics.SphereCast(rb.position, ball.radius * 0.95f, -n0, out hit, lockReach, ~0, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(n0, hit.normal) < lockMaxBend
                && (hit.normal.y > 0.35f || along > lockMinWallSpeed)
                && !(n0.y < 0.5f && v0.y > 0f && hit.normal.y > n0.y + 0.05f))   // going up a wall over its lip: launch, don't wrap onto the deck
            {
                locked = true;
                groundNormal = hit.normal;
                rb.position -= n0 * Mathf.Max(0f, hit.distance - 0.01f);
                Vector3 t = Vector3.ProjectOnPlane(v0, hit.normal);
                if (t.sqrMagnitude > 1e-4f) rb.linearVelocity = t.normalized * along;
            }
            else if (n0.y < 0.5f && v0.y > 0f)
            {
                // launching up off vert: go straight up and drift slightly back over the ramp, so you
                // come down into it rather than landing on the deck behind
                rb.linearVelocity = Vector3.ProjectOnPlane(v0, n0) + n0 * 0.4f;
            }
        }
        if (locked) Grounded = true;
        else
        {
            Grounded = Physics.SphereCast(rb.position, ball.radius * 0.95f, Vector3.down, out hit, 0.12f, ~0, QueryTriggerInteraction.Ignore)
                       && hit.normal.y > 0.35f;
            groundNormal = Grounded ? hit.normal : Vector3.up;
            locked = Grounded && OnBoard && noLockTimer <= 0f;
        }

        // riding a steep wall you just go with it; steering and pushing only work on ground you could stand on
        bool steep = OnBoard && groundNormal.y < 0.7f;

        Quaternion look = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = look * Vector3.forward, right = look * Vector3.right;
        if (Grounded && !steep)
        {
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal).normalized;
            right = Vector3.ProjectOnPlane(right, groundNormal).normalized;
        }

        // on the board, Shift on its own pushes you forward (on the ground; in the air it just keeps your momentum)
        float f = inp.forward;
        if (OnBoard && Grounded && f == 0f && inp.strafe == 0f) f = 1f;
        Vector3 wish = fwd * f + right * inp.strafe;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float top = OnBoard ? boardSpeed : walkSpeed;

        Vector3 v = rb.linearVelocity;
        v += Physics.gravity * ((OnBoard ? boardGravityScale : gravityScale) - 1f) * dt;
        if (Grounded && steep) { }   // locked to a wall: gravity and the ramp do the work
        else if (Grounded)
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
        if (inp.jump && Grounded)
        {
            float js = OnBoard ? boardJumpSpeed : jumpSpeed;
            if (OnBoard && steep) v += groundNormal * js;   // off a wall: away from it
            else v.y = Mathf.Max(v.y, 0f) + js;
            locked = false;
            noLockTimer = 0.25f;
        }
        rb.linearVelocity = v;

        if (rb.linearVelocity.magnitude > maxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        if (OnBoard && noRailTimer <= 0f) TryLockOntoRail();
    }

    // ---- rails --------------------------------------------------------------------------------

    float RideHeight(SkateRail r) { return r.radius + ball.radius; }

    // on the board, anywhere near a rail at about its height: snap onto it and grind. You can come
    // at it from any angle: straight along it, across it, or dropping onto it from above. Your
    // speed carries over, and you go whichever way along the rail is closer to where you're heading.
    void TryLockOntoRail()
    {
        if (rails == null) return;
        Vector3 p = rb.position, v = rb.linearVelocity;
        // shooting up off a quarterpipe, or dropping straight back down into it: leave the coping alone
        if (v.y > 4f || new Vector3(v.x, 0f, v.z).magnitude < 1.5f) return;
        bool onWall = Grounded && groundNormal.y < 0.7f;
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
            if (side.magnitude > railCatch || above < -0.3f || above > railCatchAbove) continue;
            if (onWall && Mathf.Abs(Vector3.Dot(v, dir)) < 1.5f) continue;       // riding straight up a quarterpipe past its coping
            float d = side.sqrMagnitude + above * above;
            if (d < bestD) { bestD = d; best = r; bestSeg = seg; bestU = u; }
        }
        if (best == null) return;

        Vector3 rd = (best.points[bestSeg + 1] - best.points[bestSeg]).normalized;
        Vector3 flatV = new Vector3(v.x, 0f, v.z);
        Vector3 heading = flatV.magnitude > 1f ? flatV : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float sign = Vector3.Dot(heading, rd) >= 0f ? 1f : -1f;
        // keep most of your speed even coming in at an angle
        float speed = Mathf.Max(Mathf.Abs(Vector3.Dot(v, rd)), flatV.magnitude * 0.8f, grindMinSpeed);
        grindRail = best; grindSeg = bestSeg; grindU = bestU;
        grindSpeed = sign * speed;
        locked = false;
    }
    // one step along the rail; false when you've come off it
    bool Grind(SlideInput inp, float dt)
    {
        Vector3[] pts = grindRail.points;
        Vector3 a = pts[grindSeg], b = pts[grindSeg + 1];
        Vector3 dir = (b - a).normalized;
        if (!inp.push || inp.jump)
        {
            // let go of Shift to step off, or jump off with Space, keeping your speed along the rail
            Vector3 v = dir * grindSpeed;
            if (inp.jump) { v.y = Mathf.Max(v.y, 0f) + boardJumpSpeed; noLockTimer = 0.25f; }
            LeaveRail(v);
            return !inp.push ? false : true;
        }

        // gravity pulls you down sloped rails; a little friction on the flat
        grindSpeed -= Physics.gravity.magnitude * boardGravityScale * dir.y * dt;
        float s = Mathf.Abs(grindSpeed);
        s = Mathf.Max(grindMinSpeed, s - grindFriction * dt);
        grindSpeed = Mathf.Sign(grindSpeed) * s;

        float len = Vector3.Distance(a, b);
        grindU += grindSpeed * dt / Mathf.Max(len, 0.01f);
        while (grindU > 1f || grindU < 0f)
        {
            bool fwd = grindU > 1f;
            int next = grindSeg + (fwd ? 1 : -1);
            if (next < 0 || next >= pts.Length - 1)
            {
                // off the end of the rail: fly on in the same direction
                LeaveRail(dir * grindSpeed);
                return true;
            }
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
        locked = false;
        return true;
    }

    void LeaveRail(Vector3 v)
    {
        grindRail = null;
        noRailTimer = 0.35f;
        rb.linearVelocity = v;
    }

    // ---- camera, board and rider --------------------------------------------------------------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (cam != null)
        {
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 eye = transform.position + Vector3.up * (eyeHeight - ball.radius);
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
            if (camera3d != null) camera3d.fieldOfView = Mathf.Lerp(fov, fovAtSpeed, Mathf.Clamp01(Speed / 20f));
        }

        // kickflip: the board spins once round its length while it pops up
        float pop = 0f, spin = 0f;
        if (flip >= 0f)
        {
            flip += dt / flipTime;
            if (flip >= 1f || !OnBoard) flip = -1f;
            else
            {
                pop = Mathf.Sin(flip * Mathf.PI) * flipHop;
                spin = Mathf.SmoothStep(0f, 360f, flip);
            }
        }

        Vector3 up = Grinding ? Vector3.up : (Grounded ? groundNormal : Vector3.up);
        Vector3 feet = transform.position - up * ball.radius;
        if (Grinding) feet = transform.position - Vector3.up * ball.radius;

        if (board != null)
        {
            board.gameObject.SetActive(OnBoard);
            if (OnBoard)
            {
                Vector3 heading = Vector3.ProjectOnPlane(rb.linearVelocity, up);
                if (heading.magnitude < 1f) heading = Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, up);
                if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(boardRot * Vector3.forward, up);   // facing straight at a wall
                Quaternion want = Quaternion.LookRotation(heading, up);
                boardRot = Quaternion.Slerp(boardRot, want, 1f - Mathf.Exp(-14f * dt));
                // spin round the middle of the deck rather than the wheels
                Quaternion r = boardRot * Quaternion.AngleAxis(spin, Vector3.forward);
                Vector3 mid = Vector3.up * 0.13f;
                board.SetPositionAndRotation(feet + up * pop + boardRot * mid - r * mid, r);
            }
        }

        if (rider != null)
        {
            rider.gameObject.SetActive(thirdPerson);
            if (thirdPerson) PoseRider(feet, up, pop, dt);
        }
    }

    // stand side-on on the board with knees bent; on foot, face where you're walking and swing your legs
    void PoseRider(Vector3 feet, Vector3 up, float pop, float dt)
    {
        float stance, crouch, swing = 0f;
        if (OnBoard)
        {
            Vector3 deckTop = up * 0.17f;
            rider.SetPositionAndRotation(feet + deckTop + up * pop * 0.85f, boardRot * Quaternion.Euler(0f, 90f, 0f));
            stance = 0.26f; crouch = pop > 0f ? 0.22f : 0.1f;
            stride = 0f;
        }
        else
        {
            Vector3 h = rb.linearVelocity; h.y = 0f;
            Vector3 face = h.magnitude > 0.5f ? h : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Quaternion want = Quaternion.LookRotation(face, Vector3.up);
            rider.SetPositionAndRotation(feet, Quaternion.Slerp(rider.rotation, want, 1f - Mathf.Exp(-12f * dt)));
            stance = 0.11f; crouch = Grounded ? 0f : 0.05f;
            if (Grounded) stride += h.magnitude * dt * 2.2f;
            swing = Grounded ? Mathf.Sin(stride) * Mathf.Clamp01(h.magnitude / walkSpeed) * 32f : 0f;
        }

        // legs pivot at the hips; spread them for the stance, bend them for the crouch
        float hip = 0.9f - crouch;
        float spread = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp((stance - 0.11f) / 0.9f, -1f, 1f));
        if (body != null) body.localPosition = new Vector3(0f, hip, 0f);
        if (legL != null) { legL.localPosition = new Vector3(-0.11f, hip, 0f); legL.localRotation = Quaternion.Euler(swing, 0f, -spread); legL.localScale = new Vector3(1f, hip / 0.9f, 1f); }
        if (legR != null) { legR.localPosition = new Vector3(0.11f, hip, 0f); legR.localRotation = Quaternion.Euler(-swing, 0f, spread); legR.localScale = new Vector3(1f, hip / 0.9f, 1f); }
    }
}
