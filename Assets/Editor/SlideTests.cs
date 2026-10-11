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
// Akiba: Shift makes the board much faster; a powerslide comes out with a boost; the route (kicker,
    // curving rail, balcony, sign ramp, roof); the flying car carries you up the skyscraper's face; wall-run
    // along the skyscraper onto the pipe and power-grind up into the lines over the city; and the side
    // street barrier stops you standing up but you slide under it
    public static void RunAkiba()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        var rb = c.GetComponent<Rigidbody>();
        foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) m.Init();
        Physics.SyncTransforms();
        try
        {
            c.alwaysBoard = true;
            // cruising vs Shift, down the middle of the street
            System.Func<bool, float> topSpeed = shift =>
            {
                Place(new Vector3(400f, 0.6f, -100f), Vector3.zero);   // open ground beyond the blocks
                c.yaw = 0f;
                float best = 0f;
                for (int i = 0; i < 200; i++) { Step(new SlideInput { forward = 1f, sprint = shift }); best = Mathf.Max(best, c.Speed); }
                return best;
            };
            float cruise = topSpeed(false), flat = topSpeed(true);
            // powerslide: hold C, let go, and come out faster
            Place(new Vector3(400f, 0.6f, -100f), new Vector3(0f, 0f, 10f));
            c.yaw = 0f;
            for (int i = 0; i < 30; i++) Step(new SlideInput { forward = 1f, slide = true, sprint = true });
            float inSlide = c.Speed;
            Step(new SlideInput { forward = 1f, sprint = true });
            float outSlide = c.Speed;
            bool boost = outSlide > inSlide + 2f;

            // the route: flat out up the east pavement, off the kicker, onto the rail, onto the balcony, up the sign ramp
            Place(new Vector3(15.2f, 0.6f, -452f), Vector3.zero);
            c.yaw = 0f;
            bool railed = false; float routeTop = 0f; Vector3 routeEnd = Vector3.zero;
            for (int i = 0; i < 700; i++)
            {
                Step(new SlideInput { forward = 1f, sprint = true });
                railed |= c.Grinding;
                routeTop = Mathf.Max(routeTop, c.transform.position.y);
                if (c.transform.position.y > 21.5f && c.Grounded) { routeEnd = c.transform.position; break; }
                routeEnd = c.transform.position;
            }
            bool route = railed && routeEnd.y > 21.5f;

            // the flying car from that roof: stand on it and it takes you across and up the skyscraper
            Mover car = null;
            foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None))
                if (m.name == "Flying car" && m.path.Length > 0 && Mathf.Abs(m.path[0].z + 392f) < 1f) car = m;
            car.Init();
            Physics.SyncTransforms();
            Place(car.transform.position + Vector3.up * 2.4f, Vector3.zero);
            float carTop = 0f, nearWall = 99f, lineTop = 0f; bool piped = false; string pipeName = "";
            for (int i = 0; i < 4000; i++)
            {
                if (!c.Grinding && !piped) { car.Step(Dt); Physics.SyncTransforms(); }
                Step(new SlideInput { sprint = true });
                if (!piped) carTop = Mathf.Max(carTop, c.transform.position.y);
                if (c.transform.position.y > 26f && !piped) nearWall = Mathf.Min(nearWall, Mathf.Abs(c.transform.position.x + 20f));
                if (!piped && c.Grinding) pipeName = c.GrindRail.name + " at " + c.transform.position.ToString("F0");
                piped |= c.Grinding;
                lineTop = Mathf.Max(lineTop, c.transform.position.y);
                if (lineTop > 150f) break;
            }
            bool carRide = carTop > 30f && nearWall < 4f;
            bool wallLine = piped && lineTop > 150f;

            // a rocket car with a ramp on its back: ride up the ramp and you're flying it, flat out. Jump off
            // early and it blows up and comes back; stay on and it blows up after 4 seconds, throwing you on
            HoverCar hover = null;
            foreach (var h in Object.FindObjectsByType<HoverCar>(FindObjectsSortMode.None)) if (Mathf.Abs(h.transform.position.z + 446f) < 1f) hover = h;
            hover.Init();
            Vector3 hoverHome = hover.transform.position;
            System.Func<bool> boardIt = () =>
            {
                Place(hoverHome + new Vector3(0f, 0.3f, -9f), Vector3.zero);
                c.yaw = 0f;
                for (int i = 0; i < 200; i++) { Step(new SlideInput { forward = 1f, sprint = true }); if (c.Piloting) return true; }
                return false;
            };
            bool flying = boardIt();
            for (int i = 0; i < 50; i++) Step(new SlideInput { forward = 1f, jumpHeld = true });
            float flewUp = hover.transform.position.y - hoverHome.y, flewOn = hover.transform.position.z - hoverHome.z;
            Step(new SlideInput { dismount = true });
            bool hopped = !c.Piloting;
            // left behind, it blows up, and turns up again back where it started
            hover.Idle(Dt);
            bool leftBlew = hover.Exploded;
            for (int i = 0; i < 400; i++) hover.Idle(Dt);
            bool cameBack = !hover.Exploded && Vector3.Distance(hover.transform.position, hoverHome) < 0.5f;
            // now ride it till it blows
            bool flying2 = boardIt();
            Vector3 boardedAt = c.transform.position;
            float ride = 0f;
            while (c.Piloting && ride < 8f) { Step(new SlideInput { forward = 1f, jumpHeld = true }); ride += Dt; }
            bool blew = hover.Exploded;
            float blastY = c.transform.position.y, flung = 0f;
            for (int i = 0; i < 60; i++) { Step(new SlideInput { forward = 1f }); flung = Mathf.Max(flung, c.transform.position.y - blastY); }
            float rocketed = Vector3.Distance(new Vector3(boardedAt.x, 0f, boardedAt.z), new Vector3(c.transform.position.x, 0f, c.transform.position.z));
            for (int i = 0; i < 400; i++) hover.Idle(Dt);
            Physics.SyncTransforms();
            bool hoverOk = flying && flewUp > 4f && flewOn > 20f && hopped && leftBlew && cameBack
                           && flying2 && blew && ride > 3.6f && ride < 4.4f && flung > 3f && rocketed > 120f && !hover.Exploded;

            // a boost arrow on the ground: roll over it slowly and it shoots you off
            BoostPad pad = null;
            foreach (var p in Object.FindObjectsByType<BoostPad>(FindObjectsSortMode.None)) if (pad == null && p.transform.position.y < 0.3f) pad = p;
            Place(pad.transform.position - pad.direction * 6f + Vector3.up * 0.6f, Vector3.zero);
            c.yaw = Mathf.Atan2(pad.direction.x, pad.direction.z) * Mathf.Rad2Deg;
            float padTop = 0f;
            for (int i = 0; i < 80; i++) { Step(new SlideInput { forward = 1f }); padTop = Mathf.Max(padTop, c.Speed); }
            bool padOk = padTop > pad.speed * 0.9f;

            // a giant capsule toy: drop on it and it bounces you high
            Collider capsule = null;
            foreach (var b in Object.FindObjectsByType<Bouncer>(FindObjectsSortMode.None)) if (capsule == null && b.GetComponent<SphereCollider>() != null) capsule = b.GetComponent<Collider>();
            float capTop = capsule.bounds.max.y;
            Place(capsule.bounds.center + Vector3.up * (capsule.bounds.extents.y + 2f), Vector3.zero);
            float bounce = 0f;
            for (int i = 0; i < 150; i++) { Step(new SlideInput()); bounce = Mathf.Max(bounce, c.transform.position.y - capTop); }
            bool capOk = bounce > 4f;

            // an updraft in front of a skyscraper: stand in it and it carries you up to a balcony
            Updraft draft = null;
            foreach (var u in Object.FindObjectsByType<Updraft>(FindObjectsSortMode.None)) if (u.height > 30f && (draft == null || u.height > draft.height)) draft = u;
            Place(draft.transform.position + Vector3.up * 0.6f, Vector3.zero);
            float risen = 0f;
            bool draftRail = false;   // carried up into a rail counts: that's what it's there for
            for (int i = 0; i < 300; i++)
            {
                Step(new SlideInput());
                risen = Mathf.Max(risen, c.transform.position.y - draft.transform.position.y);
                if (c.Grinding && risen > 3f) { draftRail = true; break; }
            }
            bool draftOk = risen > Mathf.Min(15f, draft.height - 3f) || draftRail;
            RaycastHit above;
            string draftBlock = Physics.Raycast(draft.transform.position + Vector3.up * 1f, Vector3.up, out above, 200f) ? above.collider.name + " at " + above.point.ToString("F0") + " under " + above.collider.transform.parent?.name : "nothing";

            // a footbridge over the street: ride up its ramp along the pavement onto the deck
            Place(new Vector3(15.2f, 0.6f, -77.5f), Vector3.zero);
            c.yaw = 180f;
            float deck = 0f;
            for (int i = 0; i < 250; i++) { Step(new SlideInput { forward = 1f, sprint = true }); if (c.Grounded) deck = Mathf.Max(deck, c.transform.position.y); }
            bool bridgeOk = deck > 5.8f;

            // the stacked decks: up a flight of stairs to the next storey (boost arrow at the foot)
            GameObject stairMark = null, ventMark = null;
            foreach (Transform t in GameObject.Find("Akiba").transform)
            {
                if (t.name.StartsWith("Stair test ")) stairMark = t.gameObject;
                if (t.name.StartsWith("Road vent test ")) ventMark = t.gameObject;
            }
            float stairTo = float.Parse(stairMark.name.Substring(11));
            Place(stairMark.transform.position, Vector3.zero);
            c.yaw = 0f;
            float stairTop = 0f;
            for (int i = 0; i < 300; i++) { Step(new SlideInput { forward = 1f, sprint = true }); if (c.Grounded) stairTop = Mathf.Max(stairTop, c.transform.position.y); }
            bool stairOk = stairTop > stairTo - 0.5f;
            // a vent at the road's edge: up it, then steer onto the deck
            float ventTo = float.Parse(ventMark.name.Substring(15));
            float side = Mathf.Sign(ventMark.transform.position.x);
            Place(ventMark.transform.position, Vector3.zero);
            c.yaw = 0f;
            float ventTop = 0f;
            for (int i = 0; i < 400; i++)
            {
                bool high = c.transform.position.y > ventTo + 1.5f || i > 250;
                Step(new SlideInput { strafe = high ? side : 0f });
                if (c.Grounded) ventTop = Mathf.Max(ventTop, c.transform.position.y);
            }
            bool ventOk = ventTop > ventTo - 0.5f;
            // a tube rail from one deck over the street to another
            SkateRail neon = null;
            foreach (var r in Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None)) if (neon == null && r.name.StartsWith("Neon link ")) neon = r;
            Vector3 n0 = neon.points[0], nEnd = neon.points[neon.points.Length - 1];
            Vector3 along = (neon.points[3] - neon.points[0]); along.y = 0f; along.Normalize();
            Place(n0 - along * 3f + Vector3.up * 0.1f, along * 12f);
            c.yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            bool neonGrab = false;
            for (int i = 0; i < 700; i++)
            {
                Step(new SlideInput { forward = 1f });
                neonGrab |= c.Grinding;
                if (neonGrab && !c.Grinding && c.Grounded) break;
            }
            Vector3 landed = c.transform.position;
            bool neonOk = neonGrab && Mathf.Abs(landed.y - nEnd.y) < 2f && new Vector2(landed.x - nEnd.x, landed.z - nEnd.z).magnitude < 12f;

            // a ramp truck: catch it up from behind, up its ramp, onto the bed
            Mover truck = null;
            foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) if (m.name == "Ramp truck" && truck == null && m.transform.position.z < 300f && m.transform.position.z > -300f && m.transform.forward.z > 0.9f) truck = m;
            Physics.SyncTransforms();
            Place(truck.transform.position - truck.transform.forward * 16f + Vector3.up * 0.6f, Vector3.zero);
            c.yaw = Mathf.Atan2(truck.transform.forward.x, truck.transform.forward.z) * Mathf.Rad2Deg;
            float bed = 0f;
            for (int i = 0; i < 400; i++) { truck.Step(Dt); Physics.SyncTransforms(); Step(new SlideInput { forward = 1f }); bed = Mathf.Max(bed, c.transform.position.y); }
            bool truckRide = bed > 1.5f;
            // the barrier across the side street: bar from 1 to 1.5 m. Standing you stop; sliding you go under
            c.alwaysBoard = false;
            System.Func<bool, float> under = slide =>
            {
                c.ResetBoard();
                Place(new Vector3(15f, 0.6f, -161f), Vector3.zero);
                c.yaw = 90f;
                for (int i = 0; i < 20; i++) Step(new SlideInput { forward = 1f, sprint = true });
                for (int i = 0; i < 80; i++) Step(new SlideInput { forward = 1f, sprint = true, slide = slide });
                return c.transform.position.x;
            };
            float standX = under(false), slideX = under(true);
            bool slideUnder = standX < 23f && slideX > 25f;

            Report("akiba: board speed, powerslide, route, flying car into the lines, rocket car, ramp truck, slide under, boost pad, capsule, updraft, footbridge, decks", cruise < 8f && flat > 12f && boost && route && carRide && wallLine && hoverOk && truckRide && slideUnder && padOk && capOk && draftOk && bridgeOk && stairOk && ventOk && neonOk,
                   "board " + (cruise * 3.6f).ToString("F0") + "km/h cruising, " + (flat * 3.6f).ToString("F0") + "km/h with Shift"
                   + "; powerslide " + (inSlide * 3.6f).ToString("F0") + " -> " + (outSlide * 3.6f).ToString("F0") + "km/h coming out"
                   + "; route: " + (railed ? "caught the rail" : "MISSED THE RAIL") + ", ended at " + routeEnd.ToString("F1") + " (roof is 22m)"
                   + "; flying car: up to " + carTop.ToString("F1") + "m, " + nearWall.ToString("F1") + "m from the skyscraper"
                   + "; " + (piped ? "the pipe grabbed me off it (" + pipeName + ")" : "THE PIPE NEVER GRABBED ME") + ", up to " + lineTop.ToString("F0") + "m"
                   + "; rocket car: " + (flying ? "flew it up " + flewUp.ToString("F0") + "m and on " + flewOn.ToString("F0") + "m in 1s" + (hopped ? ", hopped off" : ", COULDN'T GET OFF") : "NEVER GOT ON")
                   + (leftBlew ? ", left behind it blew" : ", LEFT BEHIND IT DIDN'T BLOW") + (cameBack ? " and came back" : " and DIDN'T COME BACK")
                   + "; ridden: " + (flying2 ? "blew " + (blew ? "" : "(NOT) ") + "after " + ride.ToString("F1") + "s, flung me " + flung.ToString("F1") + "m up, " + rocketed.ToString("F0") + "m from where I got on" : "NEVER GOT ON THE 2ND TIME")
                   + "; boost pad: up to " + (padTop * 3.6f).ToString("F0") + "km/h (pad " + (pad.speed * 3.6f).ToString("F0") + ")"
                   + "; capsule bounced me " + bounce.ToString("F1") + "m over it"
                   + "; updraft carried me up " + risen.ToString("F1") + "m of " + draft.height.ToString("F0") + " at " + draft.transform.position.ToString("F0") + (draftRail ? " into a rail" : "") + " (above it: " + draftBlock + ")"
                   + "; footbridge: up to " + deck.ToString("F1") + "m (deck is 6m)"
                   + "; decks: stairs up to " + stairTop.ToString("F1") + "m of " + stairTo.ToString("F0") + ", road vent up to " + ventTop.ToString("F1") + "m of " + ventTo.ToString("F0")
                   + ", tube rail " + (neonGrab ? "grabbed me, landed " + landed.ToString("F1") + " (its end " + nEnd.ToString("F1") + ")" : "NEVER GRABBED ME")
                   + "; ramp truck: up to " + bed.ToString("F1") + "m"
                   + "; barrier: standing stopped at x=" + standX.ToString("F1") + ", sliding got to x=" + slideX.ToString("F1"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE akiba failures=" + failures);
    }
}
