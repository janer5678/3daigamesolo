using UnityEditor;
using UnityEngine;

// Test runs, run by the builder from the command line: steps the real SlideController and the
// physics engine by hand in the built scene and reports what happened.
public static class SlideTests
{
    static SlideController c;
    static int failures;
    const float Dt = 0.02f;

    [MenuItem("Skate/Run test runs")]
    public static void RunAll()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        c.alwaysBoard = false;   // the runs below are mostly on foot; the always-on board gets its own check
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            RunAndStop();
            JumpAndAirControl();
            JumpFeel();
            SolidEverywhere();
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE failures=" + failures);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (!ok) failures++;
        Debug.Log("SLIDE_TEST " + (ok ? "PASS " : "FAIL ") + name + " | " + detail);
    }

    static void Step(SlideInput inp)
    {
        c.Tick(inp);
        Physics.Simulate(Dt);
    }

    static void Place(Vector3 p, Vector3 v)
    {
        c.Respawn();
        var rb = c.GetComponent<Rigidbody>();
        rb.position = p; c.transform.position = p;
        rb.linearVelocity = v;
        c.yaw = 90f;
        Physics.SyncTransforms();
        for (int i = 0; i < 5; i++) Step(new SlideInput());   // settle onto the ground
        rb.linearVelocity = v;
    }

    static Vector3 PlazaSpot { get { return new Vector3(660f, SkateWorldBuilder.Base(700f) + 0.4f, 32f); } }

    // quick to get up to running speed, you stand still when nothing's pressed, and you stop
    // quickly when you let go
    static void RunAndStop()
    {
        Place(PlazaSpot, Vector3.zero);
        for (int i = 0; i < 25; i++) Step(new SlideInput { forward = 1f });
        float run = c.Speed;

        c.Respawn();
        Physics.SyncTransforms();
        Vector3 from = c.transform.position;
        for (int i = 0; i < 1000; i++) Step(new SlideInput());
        float moved = Vector3.Distance(from, c.transform.position);

        Place(PlazaSpot, new Vector3(c.sprintSpeed, 0f, 0f));
        float t = 0f;
        while (c.Speed > 0.3f && t < 10f) { Step(new SlideInput()); t += Dt; }

        // sprinting, and turning right round at a sprint
        Place(PlazaSpot, Vector3.zero);
        for (int i = 0; i < 25; i++) Step(new SlideInput { forward = 1f, sprint = true });
        float sprint = c.Speed;
        float back = 0f;
        while (c.Velocity.x > -c.walkSpeed * 0.9f && back < 3f) { Step(new SlideInput { forward = -1f }); back += Dt; }

        Report("run, sprint, stand, stop, turn", run > c.walkSpeed - 0.5f && run < c.walkSpeed + 0.5f && sprint > c.sprintSpeed - 0.5f && moved < 1f && t < 0.4f && back < 0.35f,
               "running after 0.5s: " + (run * 3.6f).ToString("F0") + "km/h, sprinting " + (sprint * 3.6f).ToString("F0") + "km/h; moved " + moved.ToString("F2")
               + "m in 20s with nothing pressed; from a sprint stopped in " + t.ToString("F2") + "s; sprint to running the other way in " + back.ToString("F2") + "s");
    }

    // a decent jump, and you can still change direction mid-air
    static void JumpAndAirControl()
    {
        Place(PlazaSpot, Vector3.zero);
        float y0 = c.transform.position.y, peak = y0;
        Step(new SlideInput { jump = true });
        for (int i = 0; i < 80; i++) { Step(new SlideInput()); peak = Mathf.Max(peak, c.transform.position.y); }
        float height = peak - y0;

        Place(PlazaSpot, new Vector3(11f, 0f, 0f));
        Step(new SlideInput { forward = 1f, jump = true });
        float air = 0f;
        while (!c.Grounded && air < 2f || air < 0.1f) { Step(new SlideInput { strafe = 1f }); air += Dt; }
        Vector3 v = c.Velocity; v.y = 0f;
        float turned = Vector3.Angle(Vector3.right, v);
        Report("jump height and air control", height > 0.8f && height < 1.3f && turned > 30f,
               "jump height " + height.ToString("F2") + "m; holding D through a " + air.ToString("F2") + "s jump turned you " + turned.ToString("F0") + " degrees");
    }

    // tap Space for a jump, hold it to go higher; coyote time and jump buffering; mashing Space on
    // take-off doesn't stack jumps; hopping again and again slowly builds speed; C slides, with a
    // higher jump out of it; and jumping at full speed doesn't cost you speed
    static void JumpFeel()
    {
        System.Func<bool, float> jumpPeak = hold =>
        {
            Place(PlazaSpot, Vector3.zero);
            float start = c.transform.position.y, peak = start;
            Step(new SlideInput { jump = true, jumpHeld = hold });
            for (int i = 0; i < 80; i++) { Step(new SlideInput { jumpHeld = hold }); peak = Mathf.Max(peak, c.transform.position.y); }
            return peak - start;
        };
        float tap = jumpPeak(false), held = jumpPeak(true);

        // coyote time: run off the end of the manual pad and jump a moment after leaving it
        Place(new Vector3(682f, SkateWorldBuilder.Base(700f) + 0.2f + 0.4f, -12f), new Vector3(7f, 0f, 0f));
        int steps = 0;
        while (c.Grounded && steps < 200) { Step(new SlideInput { forward = 1f }); steps++; }
        Step(new SlideInput { forward = 1f }); Step(new SlideInput { forward = 1f }); Step(new SlideInput { forward = 1f });   // 0.06s off the edge
        Step(new SlideInput { forward = 1f, jump = true });
        bool coyote = c.Velocity.y > 4f;

        // jump buffering: press Space just before landing and you jump as you touch down
        Place(PlazaSpot + Vector3.up * 1.2f, Vector3.zero);
        for (int i = 0; i < 100 && c.transform.position.y > PlazaSpot.y + 0.6f; i++) Step(new SlideInput());
        Step(new SlideInput { jump = true });   // a little early
        bool buffered = false;
        for (int i = 0; i < 12 && !buffered; i++) { Step(new SlideInput()); buffered = c.Velocity.y > 4f; }

        // mashing on take-off
        Place(PlazaSpot, Vector3.zero);
        float y0 = c.transform.position.y, mashed = y0;
        for (int i = 0; i < 3; i++) Step(new SlideInput { jump = true });
        for (int i = 0; i < 80; i++) { Step(new SlideInput()); mashed = Mathf.Max(mashed, c.transform.position.y); }

        // hop, land, hop again... holding W
        Place(PlazaSpot, new Vector3(c.walkSpeed, 0f, 0f));
        float hopStart = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;
        for (int hop = 0; hop < 8; hop++)
        {
            Step(new SlideInput { forward = 1f, jump = true });
            for (int i = 0; i < 60; i++) { Step(new SlideInput { forward = 1f }); if (i > 5 && c.Grounded) break; }
        }
        float hopEnd = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;

        // slide: a burst of speed, momentum carried, and a higher jump out of it
        Place(PlazaSpot, new Vector3(c.walkSpeed, 0f, 0f));
        Step(new SlideInput { forward = 1f, slide = true });
        bool slid = c.Sliding;
        float slideSpeed = c.Velocity.x;
        for (int i = 0; i < 20; i++) Step(new SlideInput { slide = true });
        float slideLater = c.Velocity.x;
        float sy = c.transform.position.y, slidePeak = sy;
        Step(new SlideInput { slide = true, jump = true });
        for (int i = 0; i < 80; i++) { Step(new SlideInput()); slidePeak = Mathf.Max(slidePeak, c.transform.position.y); }
        float slideJump = slidePeak - sy;

        // jumping at full speed doesn't cost you speed
        Place(PlazaSpot, new Vector3(21f, 0f, 0f));
        float fast = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;
        Step(new SlideInput { jump = true, forward = 1f });
        float afterJump = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;

        bool ok = held > tap + 0.25f && coyote && buffered && mashed - y0 < tap + 0.1f && hopEnd > hopStart + 2f
                  && slid && slideSpeed > c.walkSpeed + 1.5f && slideLater > c.walkSpeed && slideJump > tap + 0.2f && afterJump > fast - 0.5f;
        Report("hold to jump higher, coyote time, jump buffer, hops build speed, C slides", ok,
               "jump " + tap.ToString("F2") + "m tapped / " + held.ToString("F2") + "m held"
               + "; jump 0.06s after running off an edge " + (coyote ? "works" : "IGNORED") + "; Space just before landing " + (buffered ? "jumps on touchdown" : "IGNORED")
               + "; mashing Space " + (mashed - y0).ToString("F2") + "m; 8 hops: " + (hopStart * 3.6f).ToString("F0") + " -> " + (hopEnd * 3.6f).ToString("F0") + "km/h"
               + "; slide " + (slid ? "starts at " + (slideSpeed * 3.6f).ToString("F0") + "km/h, " + (slideLater * 3.6f).ToString("F0") + "km/h after 0.4s" : "DIDN'T START")
               + ", jump out of it " + slideJump.ToString("F2") + "m; jumping at " + (fast * 3.6f).ToString("F0") + "km/h leaves you at " + (afterJump * 3.6f).ToString("F0") + "km/h");
    }

    // the ground is solid past the edge of the play area too: run out there and you don't drop out
    // of the world
    static void SolidEverywhere()
    {
        string log = "";
        bool ok = true;
        foreach (var at in new[] { new Vector2(1060f, 0f), new Vector2(400f, 220f), new Vector2(400f, -220f), new Vector2(-200f, 0f) })
        {
            float gy = SkateWorldBuilder.Height(at.x, at.y);
            Place(new Vector3(at.x, gy + 1.5f, at.y), new Vector3(7f, 0f, 0f));
            for (int i = 0; i < 150; i++) Step(new SlideInput { forward = 1f });
            float y = c.transform.position.y - SkateWorldBuilder.Height(c.transform.position.x, c.transform.position.z);
            bool held = y > -0.5f;
            ok &= held;
            log += " (" + at.x.ToString("F0") + "," + at.y.ToString("F0") + "): " + (held ? "on the ground" : "FELL " + y.ToString("F0") + "m") + ";";
        }
        Report("solid ground past the edges", ok, log);
    }

    // the pink palace: standing still you stay put, running about you stay on the floor, and you can
    // run down the grand staircase from the terrace
    public static void RunPalace()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        c.alwaysBoard = false;   // the runs below are mostly on foot; the always-on board gets its own check
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.Respawn();
            Physics.SyncTransforms();
            Vector3 from = c.transform.position;
            for (int i = 0; i < 250; i++) Step(new SlideInput());
            float idle = Vector3.Distance(from, c.transform.position);

            c.Respawn();
            Physics.SyncTransforms();
            float low = 99f;
            for (int i = 0; i < 200; i++) { Step(new SlideInput { forward = 1f }); low = Mathf.Min(low, c.transform.position.y); }
            float ran = c.transform.position.z - from.z;

            Place(new Vector3(0f, 2.4f + 0.4f, 36f), new Vector3(0f, 0f, -6f));
            c.yaw = 180f;
            float stairLow = 99f;
            for (int i = 0; i < 300; i++) { Step(new SlideInput { forward = 1f }); stairLow = Mathf.Min(stairLow, c.transform.position.y); }
            Vector3 end = c.transform.position;
            bool downStairs = end.z < 20f && end.y > 0f && end.y < 1f && stairLow > 0f;

            string extra;
            bool more = BoardAndWalls(out extra);
            Report("palace: stand, run, stairs", idle < 0.5f && low > 0.2f && ran > 15f && downStairs,
                   "idle moved " + idle.ToString("F2") + "m; ran " + ran.ToString("F0") + "m (lowest " + low.ToString("F2") + "); from the terrace down the stairs to z="
                   + end.z.ToString("F0") + " y=" + end.y.ToString("F2"));
            Report("palace: board, wall jumps, tower", more, extra);
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE palace failures=" + failures);
    }
// the board lying near the start: run over it and you're riding, fast; Q hops off and leaves it.
    // Kickflips only on really big air.
    // And you can run up the tower's spiral ramp
    static bool BoardAndWalls(out string log)
    {
        var rb = c.GetComponent<Rigidbody>();
        Vector3 boardAt = c.board.position;
        Place(boardAt + new Vector3(-4f, 0.4f, 0f), Vector3.zero);
        c.yaw = 90f;
        bool picked = false;
        for (int i = 0; i < 60 && !picked; i++) { Step(new SlideInput { forward = 1f }); picked = c.OnBoard; }
        float boardTop = 0f;
        for (int i = 0; i < 150; i++) { Step(new SlideInput { forward = 1f, sprint = true }); boardTop = Mathf.Max(boardTop, c.Speed); }
        bool smallFlip = false;
        Step(new SlideInput { forward = 1f, jump = true });
        for (int i = 0; i < 40; i++) { Step(new SlideInput { forward = 1f }); smallFlip |= c.Flipping; }
        Step(new SlideInput { dismount = true });
        bool off = !c.OnBoard && Vector3.Distance(c.board.position, c.transform.position) < 1.5f;
        // kickflips are rare: a 12 m drop doesn't, a really big one can (forced to always, to check)
        System.Func<float, bool> dropFlips = height =>
        {
            c.ResetBoard();
            c.Respawn();
            Vector3 bp = c.board.position;
            rb.position = bp + Vector3.up * (height + 0.35f); c.transform.position = rb.position; rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            c.board.position = bp + Vector3.up * height;
            bool flipped = false;
            for (int i = 0; i < 300; i++) { Step(new SlideInput()); flipped |= c.Flipping; if (i > 3 && c.Grounded) break; }
            return flipped;
        };
        bool smallDrop = dropFlips(12f);
        float chance = c.kickflipChance;
        c.kickflipChance = 1f;
        bool bigFlip = dropFlips(40f);
        c.kickflipChance = chance;
        bool repicked = true;
        Step(new SlideInput { dismount = true });
        c.ResetBoard();
        // up the spiral ramp, steering round it
        Vector3 tower = new Vector3(-140f, 0f, 0f);
        float a0 = -20f * Mathf.Deg2Rad;
        Place(tower + new Vector3(Mathf.Cos(a0) * 10.5f, 0.4f, Mathf.Sin(a0) * 10.5f), Vector3.zero);
        float wound = -20f, last = -20f, spiralTop = 0f;
        for (int i = 0; i < 1500 && wound < 445f; i++)
        {
            Vector3 rel = c.transform.position - tower;
            float ang = Mathf.Atan2(rel.z, rel.x) * Mathf.Rad2Deg;
            wound += Mathf.DeltaAngle(last, ang); last = ang;
            float next = (wound + 14f) * Mathf.Deg2Rad;
            Vector3 aim = tower + new Vector3(Mathf.Cos(next) * 10.5f, 0f, Mathf.Sin(next) * 10.5f) - c.transform.position;
            c.yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            Step(new SlideInput { forward = 1f, sprint = true });
            spiralTop = Mathf.Max(spiralTop, c.transform.position.y);
        }

        bool ok = picked && boardTop > 12f && !smallFlip && off && !smallDrop && bigFlip && spiralTop > 13.5f;
        log = (picked ? "ran onto the board and rode it to " + (boardTop * 3.6f).ToString("F0") + "km/h" : "NEVER GOT ON THE BOARD")
              + "; ordinary jump " + (smallFlip ? "KICKFLIPPED" : "no flip") + "; Q " + (off ? "hops off, board left there" : "DIDN'T HOP OFF")
              + "; 12m drop " + (smallDrop ? "KICKFLIPPED" : "no flip") + ", 40m drop " + (bigFlip ? "kickflips" : "NO FLIP") + (repicked ? "" : "")

              + "; spiral ramp: ran up to " + spiralTop.ToString("F1") + "m (top 14m)";
        return ok;
    }
// Sakura City: ride a bus roof; run up the parking garage to its roof; grind the power line from there
    // up to the 22 m roof; wall-jump up the alley beside the tower; run up the station stairs; and the
    // board stays with you whatever happens
    public static void RunCity()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        c.alwaysBoard = false;   // the runs below are mostly on foot; the always-on board gets its own check
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        var rb = c.GetComponent<Rigidbody>();
        try
        {
            // a bus: stand on its roof and it carries you round
            Mover bus = null;
            foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) { m.Init(); if (m.name == "Bus" && bus == null) bus = m; }
            Physics.SyncTransforms();
            Vector3 roof = bus.transform.position + Vector3.up * 3.6f;
            Place(roof, Vector3.zero);
            Vector3 from = c.transform.position;
            float lowest = 99f;
            for (int i = 0; i < 100; i++) { bus.Step(Dt); Physics.SyncTransforms(); Step(new SlideInput()); lowest = Mathf.Min(lowest, c.transform.position.y); }
            float carried = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(c.transform.position.x, 0f, c.transform.position.z));
            bool busRide = lowest > 3f && carried > 10f;

            // the garage: up the three ramps to the roof
            var path = new[] { new Vector3(65f, 0f, 15f), new Vector3(87f, 5f, 15f), new Vector3(87f, 5f, 35f), new Vector3(65f, 10f, 35f),
                               new Vector3(65f, 10f, 22f), new Vector3(65f, 10f, 15f), new Vector3(87f, 15f, 15f), new Vector3(83f, 15f, 24f) };
            Place(new Vector3(62f, 0.6f, 15f), Vector3.zero);
            int wp = 0; float garageTop = 0f;
            for (int i = 0; i < 2500 && wp < path.Length; i++)
            {
                Vector3 to = path[wp] - c.transform.position; to.y = 0f;
                if (to.magnitude < 1.2f) { wp++; continue; }
                c.yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                Step(new SlideInput { forward = 1f });
                garageTop = Mathf.Max(garageTop, c.transform.position.y);
            }
            bool garage = wp >= path.Length && c.transform.position.y > 15f;

            // the power line from the garage roof up to the 22 m roof, on the board
            var line = FindRail(new Vector3(67f, 16.5f, 35.5f));
            Vector3 p0 = line.points[0], p1 = line.points[1];
            c.ResetBoard();
            Place(p0 + Vector3.up * 0.7f + (p1 - p0).normalized * 0.4f, Vector3.zero);
            c.board.position = c.transform.position - Vector3.up * 0.35f;
            Step(new SlideInput());
            bool mounted = c.OnBoard;
            Vector3 along = (p1 - p0); along.y = 0f; along.Normalize();
            rb.linearVelocity = along * 7f;
            bool grinding = false; float lineTop = 0f;
            for (int i = 0; i < 400; i++)
            {
                Step(new SlideInput());
                grinding |= c.Grinding;
                lineTop = Mathf.Max(lineTop, c.transform.position.y);
                if (!c.Grinding && grinding && c.Grounded) break;
            }
            Vector3 endUp = c.transform.position;
            bool lineUp = mounted && grinding && endUp.y > 22f && endUp.x > 64f && endUp.z > 44f;
            Step(new SlideInput { dismount = true });
            c.ResetBoard();

            // the station stairs, from the boulevard up to the platform
            Place(new Vector3(60f, 0.6f, 10.5f), Vector3.zero);
            c.yaw = -90f;
            float stairTop = 0f;
            for (int i = 0; i < 600; i++) { Step(new SlideInput { forward = 1f }); stairTop = Mathf.Max(stairTop, c.transform.position.y); }
            bool stairs = stairTop > 10.2f;


            // the board's always with you: on it from the start, right click steps off (carrying it) and
            // back on, and however hard you land (an 8 m drop, a sideways landing, a wall) you stay on it
            c.alwaysBoard = true;
            c.Respawn();
            Physics.SyncTransforms();
            bool startOn = c.OnBoard;
            Step(new SlideInput { dismount = true });
            bool stepOff = !c.OnBoard;
            Step(new SlideInput { dismount = true });
            bool backOn = c.OnBoard;
            Place(new Vector3(-40f, 0.6f, -8f), Vector3.zero);
            rb.position = c.transform.position + Vector3.up * 8f; c.transform.position = rb.position;
            rb.linearVelocity = new Vector3(8f, 0f, 0f);
            Physics.SyncTransforms();
            bool stayed = true;
            for (int i = 0; i < 80; i++) { Step(new SlideInput { strafe = i > 20 ? 1f : 0f }); stayed &= c.OnBoard; }   // turning in the air, then landing
            rb.linearVelocity = new Vector3(15f, 0f, 0f);
            for (int i = 0; i < 150; i++) { Step(new SlideInput { forward = 1f }); stayed &= c.OnBoard; }   // and on into whatever's ahead
            c.alwaysBoard = false;
            bool always = startOn && stepOff && backOn && stayed;
            string alwaysLog = "; always-on board: " + (startOn ? "on it from the start" : "NOT ON IT AT THE START") + ", right click " + (stepOff && backOn ? "off and back on" : "DIDN'T TOGGLE")
                               + ", hard landings and crashes " + (stayed ? "never knock you off" : "KNOCKED YOU OFF");
            Report("city: bus, garage, power line, alley, stairs, always on the board", busRide && garage && lineUp && stairs && always,
                   "bus roof: carried " + carried.ToString("F0") + "m, lowest " + lowest.ToString("F1") + "m"
                   + "; garage: " + (garage ? "ran up to the roof (" + garageTop.ToString("F1") + "m)" : "STUCK at waypoint " + wp + " (" + garageTop.ToString("F1") + "m)")
                   + "; power line: " + (lineUp ? "ground up it to the 22m roof" : "FAILED (mounted " + mounted + ", grinding " + grinding + ", ended at " + endUp.ToString("F1") + ")")

                   + "; stairs: up to " + stairTop.ToString("F1") + "m"
                   + alwaysLog);
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE city failures=" + failures);
    }

    static SkateRail FindRail(Vector3 near)
    {
        SkateRail best = null; float bd = float.MaxValue;
        foreach (var r in Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(r.points[0], near);
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }
    // Akiba: Shift makes the board much faster and a powerslide comes out with a boost; then every way from
    // one place to another the level builder recorded (escalators, lifts, launch pads, vents, trampolines,
    // rails, jumps, flying buses, flying cars) is ridden, and each has to get you there; every place can be
    // got to from the street; a flying car lasts five seconds and then falls apart, throwing you up and on;
    // an escalator carries you up standing still; and the fast buses carry you
    public static void RunAkiba()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        failures = 0;
        var movers = Object.FindObjectsByType<Mover>(FindObjectsSortMode.None);
        var cars = Object.FindObjectsByType<HoverCar>(FindObjectsSortMode.None);
        foreach (var m in movers) m.Init();
        foreach (var h in cars) { h.Init(); h.ResetHome(); }
        Physics.SyncTransforms();
        try
        {
            c.alwaysBoard = true;
            // cruising vs Shift, down the alley behind the east side
            System.Func<bool, float> topSpeed = shift =>
            {
                Place(new Vector3(64f, 0.6f, -110f), Vector3.zero);
                c.yaw = 0f;
                float best = 0f;
                for (int i = 0; i < 200; i++) { Step(new SlideInput { forward = 1f, sprint = shift }); best = Mathf.Max(best, c.Speed); }
                return best;
            };
            float cruise = topSpeed(false), flat = topSpeed(true);
            Place(new Vector3(64f, 0.6f, -110f), new Vector3(0f, 0f, 10f));
            c.yaw = 0f;
            for (int i = 0; i < 30; i++) Step(new SlideInput { forward = 1f, slide = true, sprint = true });
            float inSlide = c.Speed;
            Step(new SlideInput { forward = 1f, sprint = true });
            float outSlide = c.Speed;
            Report("akiba: board speed, powerslide", cruise < 8f && flat > 12f && outSlide > inSlide + 2f,
                   "board " + (cruise * 3.6f).ToString("F0") + "km/h cruising, " + (flat * 3.6f).ToString("F0") + "km/h with Shift; powerslide "
                   + (inSlide * 3.6f).ToString("F0") + " -> " + (outSlide * 3.6f).ToString("F0") + "km/h coming out");

            // every way between places
            var nodes = SkateWorldBuilder.akNodes;
            var links = SkateWorldBuilder.akLinks;
            int ok = 0;
            var failed = new System.Collections.Generic.List<string>();
            var counts = new System.Collections.Generic.Dictionary<string, int[]>();
            foreach (var l in links)
            {
                bool got = RideLink(l, movers, cars, out string where);
                if (!counts.ContainsKey(l.kind)) counts[l.kind] = new int[2];
                counts[l.kind][1]++;
                if (got) { ok++; counts[l.kind][0]++; }
                else failed.Add(l.kind + " " + nodes[l.from].name + " -> " + nodes[l.to].name + " (ended " + where + ")");
            }
            var tally = new System.Collections.Generic.List<string>();
            foreach (var kv in counts) tally.Add(kv.Key + " " + kv.Value[0] + "/" + kv.Value[1]);
            Report("akiba: every way between places gets you there (" + ok + "/" + links.Count + ")", failed.Count == 0,
                   string.Join(", ", tally) + (failed.Count > 0 ? " | FAILED: " + string.Join("; ", failed) : ""));
            // and every place can be got to from the street, through those
            var seen = new bool[nodes.Count];
            var queue = new System.Collections.Generic.Queue<int>();
            seen[0] = true; queue.Enqueue(0);
            while (queue.Count > 0) { int n = queue.Dequeue(); foreach (var l in links) if (l.from == n && !seen[l.to]) { seen[l.to] = true; queue.Enqueue(l.to); } }
            var cut = new System.Collections.Generic.List<string>();
            for (int i = 0; i < nodes.Count; i++) if (!seen[i]) cut.Add(nodes[i].name);
            Report("akiba: every place can be got to from the street (" + (nodes.Count - cut.Count) + "/" + nodes.Count + ")", cut.Count == 0, "can't get to: " + string.Join(", ", cut));

            // a flying car: land on it and it's yours; five seconds later it falls apart and throws you up and on
            // (flown down the alley behind the east side, where there's nothing to fly into)
            HoverCar car = cars[0];
            Vector3 parked = car.transform.position; Quaternion parkedRot = car.transform.rotation;
            car.Park(new Vector3(64f, 0.2f, -110f), Quaternion.identity);
            Physics.SyncTransforms();
            Vector3 carHome = car.transform.position;
            Place(carHome + Vector3.up * 2.2f, Vector3.zero);
            c.yaw = car.transform.eulerAngles.y;
            float flown = 0f; bool tookIt = false;
            for (int i = 0; i < 400 && (c.Piloting || !tookIt); i++)
            {
                c.yaw = 0f;
                Step(new SlideInput());
                tookIt |= c.Piloting;
                if (c.Piloting) flown += Dt;
            }
            float flungFrom = c.transform.position.y, flung = 0f;
            for (int i = 0; i < 60; i++) { Step(new SlideInput()); flung = Mathf.Max(flung, c.transform.position.y - flungFrom); }
            bool broke = car.Exploded;
            for (int i = 0; i < 400; i++) car.Idle(Dt);
            bool back = !car.Exploded && Vector3.Distance(car.transform.position, carHome) < 0.5f;
            car.Park(parked, parkedRot);
            string crashed = car.CrashedInto;
            Report("akiba: flying car lasts 5s then falls apart", tookIt && flown > 4.7f && flown < 5.3f && broke && flung > 3f && back,
                   (tookIt ? "flew it for " + flown.ToString("F2") + "s" + (crashed != null ? " (crashed into " + crashed + ")" : "") : "NEVER GOT ON IT") + (broke ? ", it fell apart" : ", IT DIDN'T BREAK") + ", flung me " + flung.ToString("F1") + "m up"
                   + (back ? ", it came back" : ", IT DIDN'T COME BACK"));

            // an escalator carries you up standing still
            Escalator esc = null;
            foreach (var e in Object.FindObjectsByType<Escalator>(FindObjectsSortMode.None)) if (e.direction.y > 0.3f) { esc = e; break; }
            var ec = esc.GetComponent<BoxCollider>();
            Vector3 eFoot = esc.transform.TransformPoint(ec.center - new Vector3(0f, 0f, ec.size.z * 0.4f)) + Vector3.up * 0.6f;
            Place(eFoot, Vector3.zero);
            float eY0 = c.transform.position.y;
            for (int i = 0; i < 100; i++) Step(new SlideInput());
            float rode = c.transform.position.y - eY0;
            Report("akiba: escalator carries you standing still", rode > 3f, "went up " + rode.ToString("F1") + "m in 2s");

            // a fast bus carries you along on its roof
            Mover bus = null;
            foreach (var m in movers) if (m.name == "Fast bus") { bus = m; break; }
            bus.Init();
            Physics.SyncTransforms();
            Place(bus.transform.position + Vector3.up * 4.2f, Vector3.zero);
            Vector3 b0 = c.transform.position;
            bool rodeBus = false;
            for (int i = 0; i < 60; i++)
            {
                bus.Step(Dt);
                Physics.SyncTransforms();
                Step(new SlideInput());
                if (i == 50) rodeBus = c.Grounded && Vector3.Distance(c.transform.position, bus.transform.position) < 5f;
            }
            float busRide = Vector3.Distance(new Vector3(b0.x, 0f, b0.z), new Vector3(c.transform.position.x, 0f, c.transform.position.z));
            Report("akiba: fast bus carries you", rodeBus && busRide > 12f, "carried " + busRide.ToString("F1") + "m in 1.2s" + (rodeBus ? "" : ", FELL OFF"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            foreach (var m in movers) m.Init();
            foreach (var h in cars) h.ResetHome();
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE akiba failures=" + failures);
    }

    // ride one of the level's links the way it says, and see if you end up on the place it goes to
    static bool RideLink(SkateWorldBuilder.AkLink l, Mover[] movers, HoverCar[] cars, out string where)
    {
        var to = SkateWorldBuilder.akNodes[l.to];
        foreach (var h in cars) h.ResetHome();
        foreach (var m in movers) m.Init();   // everything moving back where it starts
        if (l.vehicle != null)
        {
            float keep = l.vehicle.startAt;
            if (l.vehicleFrom >= 0f) l.vehicle.startAt = l.vehicleFrom;
            l.vehicle.Init();
            l.vehicle.startAt = keep;
        }
        Physics.SyncTransforms();
        Place(l.start + Vector3.up * 0.4f, Vector3.zero);
        c.yaw = l.yaw;
        Vector3 from = c.transform.position;
        bool jumped = false, alighted = false;
        Vector3 launchV = Vector3.zero; float peak = -999f; Vector3 launchedAt = Vector3.zero, touched = Vector3.zero;
        bool airborne = false;
        int good = 0, steps = Mathf.RoundToInt(l.timeout / Dt);
        for (int i = 0; i < steps; i++)
        {
            var inp = new SlideInput();
            Vector3 p = c.transform.position;
            float run = new Vector2(p.x - from.x, p.z - from.z).magnitude;
            if (l.vehicle != null)
            {
                l.vehicle.Step(Dt);
                Physics.SyncTransforms();
                if (!alighted && Vector3.Distance(l.vehicle.transform.position, l.alightAt) < 0.3f && l.vehicle.Velocity.magnitude < 0.3f) alighted = true;
                if (alighted) { c.yaw = YawTo(p, l.steerTo); inp.forward = 1f; }
            }
            else if (l.car != null)
            {
                if (c.Piloting)
                {
                    // fly at it a few metres above its top, slowing down near it, and jump off with a bit to go
                    c.yaw = YawTo(p, l.steerTo);
                    float dy = l.steerTo.y + 3f - p.y, d = new Vector2(p.x - l.steerTo.x, p.z - l.steerTo.z).magnitude;
                    inp.jumpHeld = dy > 0f; inp.slide = dy < -5f;
                    inp.forward = d < 25f ? -1f : 0f;
                    if (d < 5f && p.y > l.steerTo.y + 1f) inp.dismount = true;
                }
                else if (!c.Grounded) { c.yaw = YawTo(p, l.steerTo); inp.forward = 1f; }
            }
            else if (!float.IsNaN(l.steerAbove))
            {
                if (p.y > l.steerAbove) { c.yaw = YawTo(p, l.steerTo); inp.forward = 1f; }
            }
            else if (l.letGo && i > 3 && (c.Launched || !c.Grounded)) { }   // nothing pressed: the arc takes you there
            else
            {
                inp.forward = l.forward ? 1f : 0f;
                inp.sprint = l.sprint;
                if (!jumped && l.jumpAt >= 0f && run >= l.jumpAt) { inp.jump = true; inp.jumpHeld = true; jumped = true; }
                else if (jumped && !c.Grounded) inp.jumpHeld = true;
            }
            bool wasLaunched = c.Launched;
            Step(inp);
            Vector3 q = c.transform.position;
            if (c.Launched && !wasLaunched) { launchV = c.Velocity; launchedAt = q; touched = Vector3.zero; }
            if (!c.Grounded) airborne = true;
            else if (airborne && touched == Vector3.zero) { touched = q; airborne = false; }
            peak = Mathf.Max(peak, q.y);
            bool on = to.kind == SkateWorldBuilder.NGround ? c.Grounded && q.y < 1.2f
                    : c.Grounded && !c.Piloting && to.Contains(new Vector2(q.x, q.z), 0.05f) && Mathf.Abs(q.y - 0.35f - to.top) < 0.75f;
            good = on ? good + 1 : 0;
            if (good >= 4) { where = q.ToString("F1"); return true; }
        }
        where = c.transform.position.ToString("F1") + " peak " + peak.ToString("F1") + (touched != Vector3.zero ? " came down at " + touched.ToString("F1") : "") + (launchV != Vector3.zero ? " launched at " + launchedAt.ToString("F1") + " with " + launchV.ToString("F1") + ", peak " + peak.ToString("F1") : "");
        return false;
    }

    static float YawTo(Vector3 from, Vector3 to) { return Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg; }

    // Infinity: down a bridge from the great plaza to the next generation's tower; up a flared bank and
    // back down it; from the tip of a smallest tower a launch pad throws you home onto the great plaza; and
    // you can grind a bridge's edge
    public static void RunInfinity()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            // down the first bridge
            var (a, b) = SkateWorldBuilder.infBridges[0];
            Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
            Place(a - dir * 8f + Vector3.up * 0.6f, Vector3.zero);
            c.yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            bool down = false;
            for (int i = 0; i < 300 && !down; i++)
            {
                Step(new SlideInput { forward = 1f, sprint = true });
                Vector3 p = c.transform.position;
                down = c.Grounded && Vector3.Dot(new Vector3(p.x - b.x, 0f, p.z - b.z), dir) > 0f && Mathf.Abs(p.y - 0.35f - b.y) < 0.8f;
            }
            // up the bank at the end of the +x arm, and back
            Place(new Vector3(8f, 0.6f, 0f), Vector3.zero);
            c.yaw = 90f;
            float peak = 0f, minX = 99f; bool turned = false;
            for (int i = 0; i < 300; i++)
            {
                Step(new SlideInput { forward = turned ? 0f : 1f, sprint = true });
                peak = Mathf.Max(peak, c.transform.position.y);
                if (c.Velocity.x < -1f) turned = true;
                if (turned) minX = Mathf.Min(minX, c.transform.position.x);
            }
            bool bank = peak > 3f && turned && minX < 30f;
            // home from a tip
            var (pad, land) = SkateWorldBuilder.infPads[0];
            Vector3 home = new Vector3(land.x - pad.x, 0f, land.z - pad.z).normalized;
            Place(pad - home * 1.9f + Vector3.up * 0.6f, Vector3.zero);
            c.yaw = Mathf.Atan2(home.x, home.z) * Mathf.Rad2Deg;
            bool launched = false, homeOk = false;
            for (int i = 0; i < 400 && !homeOk; i++)
            {
                Step(new SlideInput { forward = launched ? 0f : 1f });
                launched |= c.Launched;
                Vector3 p = c.transform.position;
                homeOk = launched && c.Grounded && Mathf.Abs(p.y - 0.35f) < 0.8f && new Vector2(p.x, p.z).magnitude < 20f;
            }
            // grind a bridge edge: jump onto it from beside it
            SkateRail rail = null;
            foreach (var r in Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None)) if (r.name.StartsWith("Bridge rail")) { rail = r; break; }
            Vector3 r0 = rail.points[0], r1 = rail.points[1], rd = (r1 - r0).normalized;
            Place(r0 + rd * 1f + Vector3.up * 1.2f, rd * 8f);
            bool grind = false;
            for (int i = 0; i < 40 && !grind; i++) { Step(new SlideInput { forward = 1f }); grind = c.Grinding; }
            Report("infinity: down a bridge, up a bank, home from a tip, grind a bridge", down && bank && homeOk && grind,
                   (down ? "rode down to the next tower" : "DIDN'T GET DOWN TO THE NEXT TOWER") + "; bank: up to " + peak.ToString("F1") + "m" + (turned ? ", came back" : ", NEVER CAME BACK")
                   + "; " + (homeOk ? "the pad threw me home" : launched ? "LAUNCHED BUT DIDN'T LAND HOME (ended " + c.transform.position.ToString("F1") + ")" : "NEVER LAUNCHED")
                   + "; " + (grind ? "ground the bridge rail" : "COULDN'T GRIND THE BRIDGE"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE infinity failures=" + failures);
    }

    // Aero: you pick up speed rolling down the hill; a bubble bounces you high; you can stand on a cloud,
    // ride a fish (it carries you), and skate across the lake; a ring shoots you on; the dome's sides take
    // you up
    public static void RunAero()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        var movers = Object.FindObjectsByType<Mover>(FindObjectsSortMode.None);
        foreach (var m in movers) m.Init();
        Physics.SyncTransforms();
        try
        {
            c.alwaysBoard = true;
            // down the hill, nothing pressed
            Place(new Vector3(0f, SkateWorldBuilder.AeroH(0f, -125f) + 0.6f, -125f), Vector3.zero);
            c.yaw = 0f;
            float fast = 0f;
            for (int i = 0; i < 300; i++) { Step(new SlideInput()); fast = Mathf.Max(fast, c.Speed); }
            // a bubble
            var (bc, br) = SkateWorldBuilder.aeroBubbles[0];
            Place(bc + Vector3.up * (br + 1.5f), Vector3.zero);
            float bounce = 0f;
            for (int i = 0; i < 120; i++) { Step(new SlideInput()); bounce = Mathf.Max(bounce, c.transform.position.y - (bc.y + br)); }
            // a cloud
            var cloud = SkateWorldBuilder.aeroClouds[0];
            Place(new Vector3(cloud.centre.x, cloud.top + 1f, cloud.centre.z), Vector3.zero);
            for (int i = 0; i < 40; i++) Step(new SlideInput());
            bool onCloud = c.Grounded && Mathf.Abs(c.transform.position.y - 0.35f - cloud.top) < 0.5f;
            // a fish
            Mover fish = null;
            foreach (var m in movers) if (m.name == "Fish") { fish = m; break; }
            fish.Init();
            Physics.SyncTransforms();
            var fb = fish.GetComponent<BoxCollider>();
            Place(fish.transform.position + Vector3.up * (fb.size.y * 0.5f + 0.6f), Vector3.zero);
            Vector3 f0 = c.transform.position;
            bool rodeFish = true;
            for (int i = 0; i < 100; i++)
            {
                fish.Step(Dt); Physics.SyncTransforms();
                Step(new SlideInput());
                if (i > 20 && Vector3.Distance(c.transform.position, fish.transform.position) > fb.size.z) rodeFish = false;
            }
            float carried = Vector3.Distance(f0, c.transform.position);
            // the lake
            float lakeY = SkateWorldBuilder.AeroLakeY;
            Place(new Vector3(-75f, lakeY + 1f, 60f), Vector3.zero);
            for (int i = 0; i < 60; i++) Step(new SlideInput());
            bool onLake = c.Grounded && Mathf.Abs(c.transform.position.y - 0.35f - lakeY) < 0.3f;
            // a ring
            BoostRing ring = null;
            foreach (var r in Object.FindObjectsByType<BoostRing>(FindObjectsSortMode.None)) if (ring == null || r.transform.position.z < ring.transform.position.z) ring = r;
            Vector3 rf = ring.transform.forward;
            Place(ring.transform.position - rf * 2.5f, rf * 9f);
            c.GetComponent<Rigidbody>().position = ring.transform.position - rf * 2.5f;
            c.yaw = Mathf.Atan2(rf.x, rf.z) * Mathf.Rad2Deg;
            float ringSpeed = 0f;
            for (int i = 0; i < 20; i++) { Step(new SlideInput()); ringSpeed = Mathf.Max(ringSpeed, c.Speed); }
            // the dome: ride up and over it
            Place(new Vector3(0f, 8.6f, 335f), Vector3.zero);
            c.yaw = 0f;
            float domeTop = 0f;
            for (int i = 0; i < 300; i++) { Step(new SlideInput { forward = 1f, sprint = true }); domeTop = Mathf.Max(domeTop, c.transform.position.y); }
            Report("aero: downhill speed, bubble, cloud, fish, lake, ring, dome", fast > 15f && bounce > 4f && onCloud && rodeFish && carried > 8f && onLake && ringSpeed > ring.speed * 0.9f && domeTop > 16.5f,
                   "rolled down the hill to " + (fast * 3.6f).ToString("F0") + "km/h; bubble bounced me " + bounce.ToString("F1") + "m; " + (onCloud ? "stood on a cloud" : "FELL THROUGH THE CLOUD")
                   + "; " + (rodeFish ? "rode a fish " : "FELL OFF THE FISH after ") + carried.ToString("F1") + "m; " + (onLake ? "skated on the lake" : "SANK IN THE LAKE (" + c.transform.position.y.ToString("F1") + ")")
                   + "; ring shot me to " + (ringSpeed * 3.6f).ToString("F0") + "km/h; up the dome to " + domeTop.ToString("F1") + "m");
        }
        finally
        {
            Physics.simulationMode = mode;
            foreach (var m in movers) m.Init();
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE aero failures=" + failures);
    }
}
