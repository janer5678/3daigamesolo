using System.Collections.Generic;
using UnityEngine;

// What the player is pressing, sampled once per frame and handed to every physics step.
public struct SkateInput
{
    public float steer;     // A/D: -1 left, +1 right
    public float lean;      // W/S: +1 forward (W), -1 back (S)
    public bool jump;       // Space, held
    public bool style;      // Shift, held: grab in the air, manual on the ground
    public bool flipA;      // LMB pressed this step
    public bool flipB;      // RMB pressed this step
    public bool shoveA;     // Q pressed this step
    public bool shoveB;     // E pressed this step
}

// The skateboard. A kinematic rider stepped at a fixed 120 Hz, with its own ground probe instead
// of a Rigidbody, so it can stick to transitions and always do the forgiving thing.
//
// Easy: you always roll the way the board points, landings within ~70 degrees of straight are
// saved and straightened for you, rails snap you on when you come near, and holding Space
// longer just ollies higher.
//
// Hard: perfect landings (within 12 degrees) add speed and +1 multiplier, flips have to finish
// before you touch down, grinds and manuals have a balance you must hold, repeated tricks score
// less, and manuals are the only way to link one combo into the next.
public class SkateController : MonoBehaviour
{
    public enum Mode { Ground, Air, Grind, Bail }

    [Header("Rolling")]
    public float gravity = 18f;
    public float pushAccel = 7f;
    public float pushTopSpeed = 10.5f;
    public float brakeDecel = 9f;
    public float rollingResistance = 0.25f;
    public float drag = 0.0025f;
    public float maxSpeed = 30f;
    public float turnRateSlow = 165f;
    public float turnRateFast = 80f;
    [Tooltip("Turning is this much tighter while crouched (Space held).")]
    public float carveBoost = 1.35f;
    [Tooltip("Extra pull down slopes and transitions while holding W - pumping.")]
    public float pumpGain = 1.25f;

    [Header("Ollie")]
    public float ollieMin = 5.0f;
    public float ollieMax = 8.0f;
    public float ollieChargeTime = 0.4f;
    [Tooltip("Let go of Space this long after rolling off a lip and it still pops.")]
    public float coyoteTime = 0.15f;

    [Header("Air")]
    public float spinRate = 560f;
    public float spinAccel = 2800f;
    public float flipTime = 0.36f;

    [Header("Landing")]
    public float perfectAngle = 12f;
    public float cleanAngle = 40f;
    public float sketchyAngle = 72f;
    public float maxImpact = 17f;
    public float perfectBoost = 1.5f;

    [Header("Balance (grinds and manuals)")]
    public float grindSnapRadius = 0.75f;
    public float balanceControl = 5.5f;

    [Header("Respawn")]
    public Transform startPoint;

    public const float Step = 1f / 120f;

    // ---- state everything else reads ---------------------------------------------------------

    public Mode mode { get; private set; }
    public Vector3 Velocity { get { return vel; } }
    public float Speed { get { return vel.magnitude; } }
    public Vector3 Up { get { return up; } }
    public Vector3 Forward { get { return fwd; } }
    public Vector3 SimPosition { get { return pos; } }
    public bool VertAir { get { return mode == Mode.Air && vertAir; } }
    public bool Manual { get { return manual; } }
    public float Balance { get { return mode == Mode.Grind ? grindBalance : manualBalance; } }
    public bool Fakie { get { return rollSign < 0f; } }
    public float Crouch { get; private set; }
    public float Lean { get; private set; }
    public bool Pushing { get; private set; }
    public bool Grabbing { get { return grabEntry != null; } }
    public string GrabName { get; private set; }
    public string GrindName { get; private set; }
    public bool Boardslide { get; private set; }
    public float BailTime { get { return bailTimer; } }
    public float AirTime { get { return airTime; } }
    public Quaternion BoardSpin { get; private set; }
    public readonly SkateCombo combo = new SkateCombo();

    // a word or two in the middle of the screen: PERFECT, Sketchy, Bail...
    public string callout = "";
    public Color calloutColor = Color.white;
    public float calloutTime = -99f;
    public float SimTime { get { return simTime; } }

    // ---- internals ---------------------------------------------------------------------------

    Vector3 pos, vel, up = Vector3.up, fwd = Vector3.forward;
    Vector3 prevPos, prevUp, prevFwd;
    float accumulator, simTime;
    float rollSign = 1f;

    bool manual;
    float manualBalance, manualBalanceVel, manualTime;
    SkateCombo.Entry manualEntry;
    float bankTimer = -1f;

    float jumpHeldTime;
    bool jumpWasHeld, styleWasHeld;
    float leftGroundTime = -99f;
    bool leftByOllie;

    bool vertAir;
    Vector3 vertNormal;
    float airTime, airSpin, spinVel;
    bool trickedThisAir;
    string lastFlipName; SkateCombo.Entry lastFlipEntry; int flipChain;
    float flipT = -1f, flipKick, flipShove; string flipName; float flipPoints;
    bool flipQueued; float qKick, qShove; string qName; float qPoints;
    SkateCombo.Entry grabEntry;
    float grabTime;

    SkateRail rail; int railSeg; float railS; float railDirSign; float railSpeed;
    float grindBalance, grindBalanceVel, grindTime; SkateCombo.Entry grindEntry;
    float boardOnRailSign;
    SkateRail lastRail; float lastRailTime = -99f;

    float bailTimer;
    readonly List<Vector3> safePos = new List<Vector3>();
    readonly List<Vector3> safeFwd = new List<Vector3>();
    float safeTimer;

    float currentLean;
    bool latchFlipA, latchFlipB, latchShoveA, latchShoveB;

    const int WaterLayer = 4;

    // ---- setup --------------------------------------------------------------------------------

    void Start()
    {
        Respawn(true);
    }

    public void Respawn(bool toStart)
    {
        Vector3 p, f;
        if (!toStart && safePos.Count > 3) { p = safePos[safePos.Count - 4]; f = safeFwd[safeFwd.Count - 4]; }
        else if (startPoint != null) { p = startPoint.position; f = startPoint.forward; }
        else { p = transform.position; f = transform.forward; }
        Teleport(p, f, 0f);
        if (toStart) { safePos.Clear(); safeFwd.Clear(); }
    }

    public void Teleport(Vector3 p, Vector3 f, float speed)
    {
        pos = p; fwd = f; up = Vector3.up;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point; up = hit.normal;
        }
        fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
        vel = fwd * speed;
        rollSign = 1f;
        mode = Mode.Ground;
        manual = false; manualEntry = null; grabEntry = null; grindEntry = null; rail = null;
        flipT = -1f; flipQueued = false; bailTimer = 0f; vertAir = false; bankTimer = -1f;
        jumpWasHeld = false; styleWasHeld = false; jumpHeldTime = 0f;
        BoardSpin = Quaternion.identity;
        prevPos = pos; prevUp = up; prevFwd = fwd;
        ApplyTransform(1f);
    }

    // straight into the air at p with velocity v (for the editor tests)
    public void Launch(Vector3 p, Vector3 v)
    {
        Teleport(p, v, 0f);
        pos = p; up = Vector3.up;
        fwd = Vector3.ProjectOnPlane(v, Vector3.up).normalized;
        vel = v;
        EnterAir(false, false);
        airTime = 0.1f;
        prevPos = pos; prevUp = up; prevFwd = fwd;
    }

    // ---- the frame loop -----------------------------------------------------------------------

    void Update()
    {
        var input = new SkateInput
        {
            steer = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            lean = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f),
            jump = Input.GetKey(KeyCode.Space),
            style = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
        };
        latchFlipA |= Input.GetMouseButtonDown(0);
        latchFlipB |= Input.GetMouseButtonDown(1);
        latchShoveA |= Input.GetKeyDown(KeyCode.Q);
        latchShoveB |= Input.GetKeyDown(KeyCode.E);

        if (Input.GetKeyDown(KeyCode.R)) { combo.Lose("Reset", simTime); Respawn(true); }

        accumulator = Mathf.Min(accumulator + Time.deltaTime, 0.25f);
        while (accumulator >= Step)
        {
            input.flipA = latchFlipA; input.flipB = latchFlipB;
            input.shoveA = latchShoveA; input.shoveB = latchShoveB;
            latchFlipA = latchFlipB = latchShoveA = latchShoveB = false;
            Simulate(input);
            accumulator -= Step;
        }
        ApplyTransform(accumulator / Step);
    }

    void ApplyTransform(float alpha)
    {
        Vector3 p = Vector3.Lerp(prevPos, pos, alpha);
        Vector3 u = Vector3.Slerp(prevUp, up, alpha);
        Vector3 f = Vector3.Slerp(prevFwd, fwd, alpha);
        transform.position = p;
        if (f.sqrMagnitude > 1e-6f && u.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(f, u).normalized, u.normalized);
    }

    // One fixed step. Public so the editor tests can drive it with scripted input.
    public void Simulate(SkateInput input)
    {
        float dt = Step;
        prevPos = pos; prevUp = up; prevFwd = fwd;
        simTime += dt;

        bool jumpReleased = jumpWasHeld && !input.jump;
        bool stylePressed = input.style && !styleWasHeld;
        if (input.jump) jumpHeldTime += dt;
        float charge = Mathf.Clamp01(jumpHeldTime / ollieChargeTime);
        Lean = Mathf.MoveTowards(Lean, input.steer, dt * 6f);
        currentLean = input.lean;

        switch (mode)
        {
            case Mode.Ground: GroundStep(input, dt, jumpReleased, stylePressed, charge); break;
            case Mode.Air:    AirStep(input, dt, jumpReleased, charge); break;
            case Mode.Grind:  GrindStep(input, dt, jumpReleased, charge); break;
            case Mode.Bail:   BailStep(dt); break;
        }

        if (!input.jump) jumpHeldTime = 0f;
        jumpWasHeld = input.jump;
        styleWasHeld = input.style;
        Crouch = Mathf.MoveTowards(Crouch, (input.jump && mode != Mode.Bail) || Grabbing ? 1f : 0f, dt * 7f);

        if (pos.y < -300f) { combo.Lose("Lost", simTime); Respawn(false); }
    }

    // ---- on the ground ------------------------------------------------------------------------

    void GroundStep(SkateInput input, float dt, bool jumpReleased, bool stylePressed, float charge)
    {
        float speed = vel.magnitude;

        // steer: rotate the board about the surface normal; the wheels take the velocity with it
        float t = Mathf.Clamp01(speed / 20f);
        float rate = Mathf.Lerp(turnRateSlow, turnRateFast, t) * (input.jump ? carveBoost : 1f);
        fwd = Quaternion.AngleAxis(input.steer * rate * dt, up) * fwd;

        // gravity along the surface; W going down a slope or a transition pumps it for more
        Vector3 g = Vector3.down * gravity;
        Vector3 gT = Vector3.ProjectOnPlane(g, up);
        bool pumping = input.lean > 0f && !manual && Vector3.Dot(gT, vel) > 0f && up.y < 0.995f;
        if (pumping) gT *= pumpGain;
        vel += gT * dt;

        // which way along the board we're rolling
        float along = Vector3.Dot(vel, fwd);
        if (Mathf.Abs(along) > 1e-4f) rollSign = Mathf.Sign(along);

        Pushing = false;
        if (!manual)
        {
            if (input.lean > 0f && Mathf.Abs(along) < pushTopSpeed && up.y > 0.9f)
            {
                vel += fwd * rollSign * pushAccel * dt;
                Pushing = true;
            }
            else if (input.lean < 0f)
            {
                float s = vel.magnitude;
                if (s > 0f) vel = vel / s * Mathf.Max(0f, s - brakeDecel * dt);
            }
        }

        // wheels grip: all in-plane speed goes the way the board points
        Vector3 inPlane = Vector3.ProjectOnPlane(vel, up);
        speed = inPlane.magnitude;
        speed = Mathf.Max(0f, speed - (rollingResistance + drag * speed * speed) * dt);
        speed = Mathf.Min(speed, maxSpeed);
        vel = fwd * rollSign * speed;

        // manual: Shift on the ground
        if (!manual && (stylePressed || (input.style && bankTimer > 0f)) && speed > 1.5f)
            StartManual(input);
        if (manual)
        {
            if (!input.style) EndManual(false);
            else ManualStep(input, dt);
        }

        // a finished combo banks a moment after you land, unless a manual picks it up
        if (!manual && bankTimer > 0f)
        {
            bankTimer -= dt;
            if (bankTimer <= 0f) combo.Bank(simTime);
        }

        // ollie
        if (jumpReleased)
        {
            Ollie(charge, false);
            return;
        }

        MoveOnSurface(dt);
        RememberSafeSpot(dt);
    }

    void StartManual(SkateInput input)
    {
        manual = true;
        manualTime = 0f;
        manualBalance = input.lean * 0.1f;
        manualBalanceVel = 0f;
        bankTimer = -1f;
        manualEntry = combo.Add(input.lean > 0f ? "Nose Manual" : "Manual", 50f);
    }

    void ManualStep(SkateInput input, float dt)
    {
        manualTime += dt;
        StepBalance(ref manualBalance, ref manualBalanceVel, input.lean, manualTime, 2.2f, 0.12f, 1.3f, dt);
        combo.Accrue(manualEntry, 110f * dt);
        if (Mathf.Abs(manualBalance) >= 1f)
        {
            EndManual(true);
            vel *= 0.7f;
        }
    }

    void EndManual(bool lostIt)
    {
        manual = false;
        manualEntry = null;
        if (lostIt) { combo.Lose("Lost balance", simTime); Callout("Lost balance", new Color(0.95f, 0.45f, 0.6f)); }
        else bankTimer = 0.3f;
    }

    // An inverted pendulum the player holds up. It tips further the longer you hold it, and
    // wanders on its own, so a long grind or manual is a real test.
    void StepBalance(ref float b, ref float bv, float input, float time, float tip, float growth, float wander, float dt)
    {
        float instability = tip * (1f + time * growth);
        float drift = (Mathf.PerlinNoise(simTime * 0.9f, 3.7f) - 0.5f) * 2f * wander;
        bv += (b * instability + drift + input * balanceControl) * dt;
        bv *= 1f - 1.8f * dt;
        b += bv * dt;
    }

    void Ollie(float charge, bool late)
    {
        // pop up off the surface, leaning towards straight up so a slope doesn't throw you sideways
        Vector3 dir = (up + Vector3.up).normalized;
        float pop = Mathf.Lerp(ollieMin, ollieMax, charge);
        if (late) vel += Vector3.up * pop * 0.8f;
        else vel += dir * pop;

        if (manual) { manual = false; manualEntry = null; }
        else if (!late && bankTimer > 0f) { combo.Bank(simTime); bankTimer = -1f; }
        if (mode == Mode.Ground) EnterAir(false, true);
        leftByOllie = true;
        jumpHeldTime = 0f;
    }

    void EnterAir(bool fromVert, bool byOllie)
    {
        mode = Mode.Air;
        vertAir = fromVert;
        vertNormal = up;
        airTime = 0f; airSpin = 0f; spinVel = 0f;
        trickedThisAir = false;
        lastFlipName = null; lastFlipEntry = null; flipChain = 0;
        leftGroundTime = simTime;
        leftByOllie = byOllie;
        if (fromVert)
        {
            // drop back in: drift gently towards the ramp you came up instead of straight up the wall
            Vector3 nh = new Vector3(up.x, 0f, up.z).normalized;
            Vector3 h = new Vector3(vel.x, 0f, vel.z);
            float away = Vector3.Dot(h, nh);
            vel += nh * (0.6f - away);
        }
    }

    // move along the surface and find it again under the new position
    void MoveOnSurface(float dt)
    {
        Vector3 next = pos + vel * dt;
        if (WallCheck(pos, next, up)) return;

        RaycastHit hit;
        Vector3 origin = next + up * 0.6f;
        if (Physics.Raycast(origin, -up, out hit, 0.95f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject.layer == WaterLayer) { Bail("Splash!"); return; }
            float angle = Vector3.Angle(up, hit.normal);
            bool concave = Vector3.Dot(hit.normal, vel) < Vector3.Dot(up, vel) - 0.001f;
            if ((concave && angle < 50f) || (!concave && angle < 22f))
            {
                if (hit.normal.y < -0.05f) { pos = next; EnterAir(true, false); return; }   // can't hang upside down
                pos = hit.point;
                float s = vel.magnitude;
                up = hit.normal;
                fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
                vel = Vector3.ProjectOnPlane(vel, up).normalized * s;
                return;
            }
        }
        // nothing under us: off a lip or over a crest
        pos = next;
        EnterAir(up.y < 0.4f, false);
    }

    void RememberSafeSpot(float dt)
    {
        safeTimer -= dt;
        if (safeTimer > 0f || manual || up.y < 0.9f) return;
        safeTimer = 0.4f;
        safePos.Add(pos);
        safeFwd.Add(vel.sqrMagnitude > 1f ? vel.normalized : fwd);
        if (safePos.Count > 10) { safePos.RemoveAt(0); safeFwd.RemoveAt(0); }
    }

    // trees, the sides of boxes, the back of a ramp. A glancing blow slides you along it, a
    // head-on one at speed is a slam.
    bool WallCheck(Vector3 from, Vector3 to, Vector3 upDir)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 1e-5f) return false;
        RaycastHit hit;
        Vector3 origin = from + upDir * 0.55f;
        if (!Physics.SphereCast(origin, 0.25f, delta / dist, out hit, dist + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.collider.GetComponent<SkateRamp>() != null) return false;
        if (Vector3.Dot(hit.normal, upDir) > 0.45f) return false;      // something we can ride onto
        if (hit.collider.gameObject.layer == WaterLayer) return false;

        Vector3 n = hit.normal;
        float into = -Vector3.Dot(vel, n);
        if (into > 7.5f) { Bail("Slammed"); return true; }
        if (into > 0f) vel += n * into;
        pos += n * 0.02f;
        return true;
    }

    // ---- in the air ---------------------------------------------------------------------------

    void AirStep(SkateInput input, float dt, bool jumpReleased, float charge)
    {
        airTime += dt;
        Pushing = false;

        // late pop: rolled off a lip with Space still held
        if (jumpReleased && !leftByOllie && simTime - leftGroundTime < coyoteTime)
            Ollie(charge, true);

        vel += Vector3.down * gravity * dt;

        // the board settles flat (or stays square to the wall on a vert air)
        Vector3 targetUp = vertAir ? vertNormal : Vector3.up;
        up = Vector3.Slerp(up, targetUp, 1f - Mathf.Exp(-6f * dt)).normalized;

        // spin with A/D, about the board's own up so it works on vert too
        float target = input.steer * spinRate;
        spinVel = Mathf.MoveTowards(spinVel, target, spinAccel * dt);
        float dYaw = spinVel * dt;
        airSpin += dYaw;
        fwd = Quaternion.AngleAxis(dYaw, up) * fwd;
        fwd = Vector3.ProjectOnPlane(fwd, up).normalized;

        FlipTricks(input, dt);
        Grab(input, dt);

        if (TryGrind()) return;

        Vector3 next = pos + vel * dt;
        if (WallCheck(pos, next, Vector3.up)) return;

        // landing: anything between here and the next position we'd go into
        RaycastHit hit;
        Vector3 delta = next - pos;
        if (airTime > 0.05f &&
            Physics.Raycast(pos - delta.normalized * 0.05f, delta.normalized, out hit, delta.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore) &&
            Vector3.Dot(vel, hit.normal) < 0f)
        {
            if (hit.collider.gameObject.layer == WaterLayer) { pos = hit.point; Bail("Splash!"); return; }
            if (hit.normal.y > 0.3f || (hit.collider.GetComponent<SkateRamp>() != null && hit.normal.y > -0.05f))
            {
                Land(hit, input);
                return;
            }
        }
        pos = next;
    }

    struct FlipDef { public string name; public float kick, shove, points; }

    void FlipTricks(SkateInput input, float dt)
    {
        FlipDef? press = null;
        if (input.flipA)
            press = input.lean > 0f ? new FlipDef { name = "Hardflip", kick = 1f, shove = -180f, points = 150f }
                  : input.lean < 0f ? new FlipDef { name = "Varial Kickflip", kick = 1f, shove = 180f, points = 150f }
                  : new FlipDef { name = "Kickflip", kick = 1f, shove = 0f, points = 100f };
        else if (input.flipB)
            press = input.lean > 0f ? new FlipDef { name = "Inward Heelflip", kick = -1f, shove = 180f, points = 150f }
                  : input.lean < 0f ? new FlipDef { name = "Varial Heelflip", kick = -1f, shove = -180f, points = 150f }
                  : new FlipDef { name = "Heelflip", kick = -1f, shove = 0f, points = 100f };
        else if (input.shoveA) press = new FlipDef { name = "Pop Shove-it", kick = 0f, shove = 180f, points = 75f };
        else if (input.shoveB) press = new FlipDef { name = "360 Shove-it", kick = 0f, shove = 360f, points = 150f };

        if (press.HasValue && !Grabbing)
        {
            var p = press.Value;
            if (flipT < 0f) StartFlip(p.name, p.kick, p.shove, p.points);
            else { flipQueued = true; qName = p.name; qKick = p.kick; qShove = p.shove; qPoints = p.points; }
        }

        if (flipT >= 0f)
        {
            flipT += dt / flipTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flipT));
            BoardSpin = Quaternion.AngleAxis(flipShove * e, Vector3.up) * Quaternion.AngleAxis(flipKick * 360f * e, Vector3.forward);
            if (flipT >= 1f)
            {
                flipT = -1f;
                BoardSpin = Quaternion.identity;
                FinishFlip();
                if (flipQueued) { flipQueued = false; StartFlip(qName, qKick, qShove, qPoints); }
            }
        }
    }

    void StartFlip(string name, float kick, float shove, float points)
    {
        flipT = 0f; flipName = name; flipKick = kick; flipShove = shove; flipPoints = points;
    }

    // a flip scores when it comes round; the same one twice in a row is a double, then a triple
    void FinishFlip()
    {
        trickedThisAir = true;
        bankTimer = -1f;
        if (lastFlipEntry != null && lastFlipName == flipName && combo.Last == lastFlipEntry && flipChain < 3)
        {
            flipChain++;
            string prefix = flipChain == 2 ? "Double " : "Triple ";
            combo.Upgrade(lastFlipEntry, prefix + flipName, flipPoints * (flipChain == 2 ? 1.6f : 2.4f));
        }
        else
        {
            flipChain = 1;
            lastFlipEntry = combo.Add(flipName, flipPoints);
            lastFlipName = flipName;
        }
    }

    void Grab(SkateInput input, float dt)
    {
        bool want = input.style && airTime > 0.08f && flipT < 0f;
        if (want && grabEntry == null)
        {
            GrabName = input.lean > 0f ? "Nosegrab" : input.lean < 0f ? "Tailgrab" : (input.steer != 0f ? "Melon" : "Indy");
            grabEntry = combo.Add(GrabName, 60f);
            grabTime = 0f;
            trickedThisAir = true;
            bankTimer = -1f;
        }
        else if (!want && grabEntry != null) grabEntry = null;
        if (grabEntry != null)
        {
            grabTime += dt;
            combo.Accrue(grabEntry, 160f * dt);
        }
    }

    void Land(RaycastHit hit, SkateInput input)
    {
        Vector3 n = hit.normal;
        float impact = -Vector3.Dot(vel, n);
        Vector3 vT = Vector3.ProjectOnPlane(vel, n);
        Vector3 f = Vector3.ProjectOnPlane(fwd, n).normalized;
        bool flipUnfinished = flipT >= 0f && flipT < 0.7f;
        bool wasGrabbing = grabEntry != null;
        grabEntry = null;

        // how far off straight the board is from where you're going, forwards or fakie
        float err = 0f;
        float sign = 1f;
        if (vT.magnitude > 1.5f)
        {
            float a = Vector3.Angle(f, vT);
            sign = a <= 90f ? 1f : -1f;
            err = a <= 90f ? a : 180f - a;
        }

        string why = null;
        if (flipUnfinished) why = "Bailed the flip";
        else if (err > sketchyAngle) why = "Landed sideways";
        else if (impact > maxImpact) why = "Too much impact";
        if (why != null)
        {
            pos = hit.point; up = n;
            Bail(why);
            return;
        }

        // spins, rounded to the nearest half turn you actually came round
        int halves = Mathf.FloorToInt((Mathf.Abs(airSpin) + 40f) / 180f);
        if (halves > 0)
        {
            string side = airSpin > 0f ? "FS " : "BS ";
            combo.Add(side + (halves * 180), 100f * halves * (halves + 1) / 2f);
            trickedThisAir = true;
        }
        if (flipT >= 0f) { FinishFlip(); flipT = -1f; flipQueued = false; BoardSpin = Quaternion.identity; }
        if (!trickedThisAir && leftByOllie) combo.Add("Ollie", 25f);

        // ramps keep your speed (it's a transition), flat ground takes the part that went into it
        float speed = n.y < 0.95f ? vel.magnitude * 0.98f : vT.magnitude;
        string grade;
        if (err <= perfectAngle && !wasGrabbing)
        {
            grade = "PERFECT";
            speed = Mathf.Min(speed + perfectBoost, maxSpeed);
            if (combo.Active) combo.bonusMultiplier++;
            Callout("PERFECT!", new Color(0.35f, 0.85f, 0.95f));
        }
        else if (err <= cleanAngle)
        {
            grade = "Clean";
            speed *= 0.97f;
        }
        else
        {
            grade = "Sketchy";
            speed *= 0.7f;
            Callout("Sketchy", new Color(0.95f, 0.65f, 0.4f));
        }

        mode = Mode.Ground;
        vertAir = false;
        pos = hit.point;
        up = n;
        // saved and straightened: the board now points exactly where you're going
        Vector3 dir = vT.magnitude > 0.5f ? vT.normalized : f;
        fwd = sign > 0f ? dir : -dir;
        rollSign = sign;
        vel = dir * speed;
        bankTimer = 0.3f;
        if (input.style && speed > 1.5f && combo.Active) StartManual(input);
        lastLanding = grade;
    }

    public string lastLanding = "";

    // ---- grinding -----------------------------------------------------------------------------

    bool TryGrind()
    {
        if (airTime < 0.05f) return false;
        foreach (var r in SkateRail.All)
        {
            if (r == null || r.SegmentCount < 1) continue;
            if (r == lastRail && simTime - lastRailTime < 0.35f) continue;
            int seg; float t;
            Vector3 q = r.Closest(pos, out seg, out t);
            Vector3 d = pos - q;
            float dy = d.y;
            d.y = 0f;
            if (d.magnitude > grindSnapRadius || dy < -0.5f || dy > 0.75f) continue;
            if (!r.coping && vel.y > 3f) continue;

            Vector3 dir = r.Direction(seg);
            float along = Vector3.Dot(vel, dir);
            if (Mathf.Abs(along) < 1.5f) continue;
            if (!r.coping)
            {
                Vector3 hv = new Vector3(vel.x, 0f, vel.z);
                Vector3 hd = new Vector3(dir.x, 0f, dir.z);
                if (hv.sqrMagnitude < 0.01f || hd.sqrMagnitude < 0.01f) continue;
                if (Mathf.Abs(Vector3.Dot(hv.normalized, hd.normalized)) < 0.42f) continue;
            }
            StartGrind(r, seg, t, along);
            return true;
        }
        return false;
    }

    void StartGrind(SkateRail r, int seg, float t, float along)
    {
        if (flipT >= 0f) { FinishFlip(); flipT = -1f; flipQueued = false; BoardSpin = Quaternion.identity; }
        grabEntry = null;
        mode = Mode.Grind;
        rail = r; railSeg = seg;
        railDirSign = Mathf.Sign(along);
        railS = (railDirSign > 0f ? t : 1f - t) * r.SegmentLength(seg);
        railSpeed = Mathf.Max(Mathf.Abs(along), 4f);
        grindBalance = 0f; grindBalanceVel = 0f; grindTime = 0f;
        bankTimer = -1f;

        Vector3 dir = r.Direction(seg) * railDirSign;
        Vector3 hf = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
        Vector3 hd = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        float a = Vector3.Angle(hf, hd);
        float lean = currentLean;
        Boardslide = a > 55f && a < 125f;
        boardOnRailSign = a <= 90f ? 1f : -1f;
        string kind = Boardslide ? "Boardslide" : lean > 0f ? "Nosegrind" : lean < 0f ? "5-0" : "50-50";
        GrindName = r.coping ? "Lip " + kind : kind;
        grindEntry = combo.Add(GrindName, 75f);
    }

    void GrindStep(SkateInput input, float dt, bool jumpReleased, float charge)
    {
        grindTime += dt;
        Vector3 dir = rail.Direction(railSeg) * railDirSign;

        railSpeed += Vector3.Dot(Vector3.down * gravity, dir) * dt;
        railSpeed -= 0.5f * dt;
        if (railSpeed < 0f)
        {
            // slid to a stop on an uphill rail: come back down it
            railDirSign = -railDirSign;
            railSpeed = -railSpeed;
            railS = rail.SegmentLength(railSeg) - railS;
            dir = -dir;
        }

        // along the polyline, across segment joins
        railS += railSpeed * dt;
        bool offEnd = false;
        while (railS > rail.SegmentLength(railSeg))
        {
            railS -= rail.SegmentLength(railSeg);
            int nextSeg = railSeg + (railDirSign > 0f ? 1 : -1);
            if (nextSeg < 0 || nextSeg >= rail.SegmentCount) { offEnd = true; railS = rail.SegmentLength(railSeg); break; }
            railSeg = nextSeg;
        }
        dir = rail.Direction(railSeg) * railDirSign;
        Vector3 a = railDirSign > 0f ? rail.points[railSeg] : rail.points[railSeg + 1];
        pos = a + dir * railS;
        vel = dir * railSpeed;

        up = Vector3.ProjectOnPlane(Vector3.up, dir).normalized;
        Vector3 boardDir = dir * boardOnRailSign;
        fwd = Boardslide ? Vector3.Cross(up, dir).normalized * boardOnRailSign : boardDir;

        StepBalance(ref grindBalance, ref grindBalanceVel, input.steer, grindTime, 2.6f, 0.18f, 1.5f, dt);
        combo.Accrue(grindEntry, 130f * dt);

        if (Mathf.Abs(grindBalance) >= 1f)
        {
            ExitGrind();
            vel += Vector3.Cross(dir, Vector3.up) * Mathf.Sign(grindBalance) * 2f;
            Bail("Fell off the rail");
            return;
        }
        if (jumpReleased)
        {
            ExitGrind();
            vel += Vector3.up * Mathf.Lerp(ollieMin, ollieMax, charge) * 0.9f;
            EnterAir(false, true);
            jumpHeldTime = 0f;
            trickedThisAir = true;
            return;
        }
        if (offEnd)
        {
            ExitGrind();
            vel += Vector3.up * 1.2f;
            EnterAir(false, false);
            trickedThisAir = true;
        }
    }

    void ExitGrind()
    {
        lastRail = rail; lastRailTime = simTime;
        rail = null; grindEntry = null;
        mode = Mode.Air;
        up = Vector3.up;
        fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
        if (Boardslide)
        {
            // come off a boardslide pointing down the rail again
            Vector3 h = Vector3.ProjectOnPlane(vel, Vector3.up).normalized;
            if (h.sqrMagnitude > 0.1f) fwd = h;
        }
        Boardslide = false;
    }

    // ---- falling off --------------------------------------------------------------------------

    void Bail(string why)
    {
        mode = Mode.Bail;
        bailTimer = 0f;
        manual = false; manualEntry = null; grabEntry = null; grindEntry = null; rail = null;
        flipT = -1f; flipQueued = false; BoardSpin = Quaternion.identity;
        combo.Lose(why, simTime);
        Callout(why, new Color(0.95f, 0.4f, 0.55f));
        bankTimer = -1f;
    }

    void BailStep(float dt)
    {
        bailTimer += dt;
        vel += Vector3.down * gravity * dt;
        vel *= Mathf.Exp(-1.5f * dt);
        Vector3 next = pos + vel * dt;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 0.5f, (next - pos).normalized, out hit, (next - pos).magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore)
            || Physics.Raycast(next + Vector3.up * 0.5f, Vector3.down, out hit, 0.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
            vel = Vector3.ProjectOnPlane(vel, hit.normal) * Mathf.Exp(-4f * dt);
            up = Vector3.Slerp(up, hit.normal, 0.2f);
        }
        else pos = next;
        if (bailTimer > 1.6f) Respawn(false);
    }

    public void Callout(string text, Color c)
    {
        callout = text; calloutColor = c; calloutTime = simTime;
    }
}
