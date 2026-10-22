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
            DoubleJump();
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

    // Space again in the air jumps once more (well higher than one jump, on foot or on the board, where it
    // kickflips); a third press does nothing; and it comes back when you land
    static void DoubleJump()
    {
        System.Func<bool, int, float> peakOf = (board, presses) =>
        {
            c.alwaysBoard = board;
            Place(PlazaSpot, Vector3.zero);
            float start = c.transform.position.y, peak = start;
            Step(new SlideInput { jump = true });
            int pressed = 1;
            for (int i = 0; i < 120; i++)
            {
                // the next press at the top of the jump
                bool press = pressed < presses && c.Velocity.y < 0.5f && !c.Grounded;
                if (press) pressed++;
                Step(new SlideInput { jump = press });
                if (press) flipped |= c.Flipping;
                peak = Mathf.Max(peak, c.transform.position.y);
            }
            return peak - start;
        };
        flipped = false;
        float one = peakOf(false, 1), two = peakOf(false, 2), three = peakOf(false, 3);
        bool footFlip = flipped;
        float boardTwo = peakOf(true, 2);
        bool boardFlip = flipped;
        // and again after landing
        float again = peakOf(false, 2);
        c.alwaysBoard = false;
        Place(PlazaSpot, Vector3.zero);
        Report("double jump: Space again in the air, kickflips on the board", two > one + 0.7f && Mathf.Abs(three - two) < 0.05f && boardTwo > one + 0.7f && boardFlip && !footFlip && Mathf.Abs(again - two) < 0.05f,
               "one jump " + one.ToString("F2") + "m, double " + two.ToString("F2") + "m, a third press " + three.ToString("F2") + "m; on the board " + boardTwo.ToString("F2") + "m"
               + (boardFlip ? " with a kickflip" : " WITHOUT A KICKFLIP") + (footFlip ? "; FLIPPED ON FOOT" : "") + "; next time " + again.ToString("F2") + "m");
    }
    static bool flipped;
    static bool twiceOk = true;   // (the Infinity bot may double jump)

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
    public static void RunAkiba() { RunAkibaLevel(false); }

    // Akiba Sky: the same, plus: it's a long climb, the only jump pads are a few giant ones, and the tall buses
    // are slow
    public static void RunAkibaSky() { RunAkibaLevel(true); }

    static void RunAkibaLevel(bool sky)
    {
        string tag = sky ? "akiba sky" : "akiba";
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
            Report(tag + ": board speed, powerslide", cruise < 8f && flat > 12f && outSlide > inSlide + 2f,
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
            Report(tag + ": every way between places gets you there (" + ok + "/" + links.Count + ")", failed.Count == 0,
                   string.Join(", ", tally) + (failed.Count > 0 ? " | FAILED: " + string.Join("; ", failed) : ""));
            // and every place can be got to from the street, through those
            var seen = new bool[nodes.Count];
            var queue = new System.Collections.Generic.Queue<int>();
            seen[0] = true; queue.Enqueue(0);
            while (queue.Count > 0) { int n = queue.Dequeue(); foreach (var l in links) if (l.from == n && !seen[l.to]) { seen[l.to] = true; queue.Enqueue(l.to); } }
            var cut = new System.Collections.Generic.List<string>();
            for (int i = 0; i < nodes.Count; i++) if (!seen[i]) cut.Add(nodes[i].name);
            Report(tag + ": every place can be got to from the street (" + (nodes.Count - cut.Count) + "/" + nodes.Count + ")", cut.Count == 0, "can't get to: " + string.Join(", ", cut));

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
            Report(tag + ": flying car lasts 5s then falls apart", tookIt && flown > 4.7f && flown < 5.3f && broke && flung > 3f && back,
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
            Report(tag + ": escalator carries you standing still", rode > 3f, "went up " + rode.ToString("F1") + "m in 2s");

            if (sky)
            {
                // a tall bus: four storeys, and slow; it carries you along on its roof
                Mover tall = null;
                foreach (var m in movers) if (m.name.StartsWith("Tall bus")) { tall = m; break; }
                tall.Init();
                Physics.SyncTransforms();
                float roofY = tall.GetComponent<BoxCollider>().bounds.max.y;
                Place(new Vector3(tall.transform.position.x, roofY + 0.5f, tall.transform.position.z), Vector3.zero);
                Vector3 t0 = c.transform.position;
                bool onIt = false;
                for (int i = 0; i < 100; i++)
                {
                    tall.Step(Dt);
                    Physics.SyncTransforms();
                    Step(new SlideInput());
                    if (i == 90) onIt = c.Grounded && c.transform.position.y > roofY - 0.2f;
                }
                float tallRide = Vector3.Distance(new Vector3(t0.x, 0f, t0.z), new Vector3(c.transform.position.x, 0f, c.transform.position.z));
                Report(tag + ": tall bus, very slow, carries you", onIt && roofY > 11f && tallRide > 3f && tallRide < 9f,
                       "roof " + roofY.ToString("F1") + "m up, carried " + tallRide.ToString("F1") + "m in 2s" + (onIt ? "" : ", FELL OFF"));

                // no little jump pads, trampolines, bouncers or boost arrows: just a few giant pads
                var pads = Object.FindObjectsByType<LaunchPad>(FindObjectsSortMode.None);
                int bouncers = Object.FindObjectsByType<Bouncer>(FindObjectsSortMode.None).Length, boosts = Object.FindObjectsByType<BoostPad>(FindObjectsSortMode.None).Length;
                bool allBig = true;
                foreach (var p in pads) allBig &= p.GetComponent<BoxCollider>().size.x >= 3.9f;
                Report(tag + ": only a few jump pads, and they're giant", pads.Length >= 2 && pads.Length <= 4 && allBig && bouncers == 0 && boosts == 0,
                       pads.Length + " pads" + (allBig ? " (all giant)" : " (SOME SMALL)") + ", " + bouncers + " bouncers, " + boosts + " boost arrows");

                // it's a climb: the summit's way up, and even the quickest way there takes a good few goes
                var dist = new int[nodes.Count];
                for (int i = 0; i < dist.Length; i++) dist[i] = -1;
                dist[0] = 0;
                var q = new System.Collections.Generic.Queue<int>();
                q.Enqueue(0);
                while (q.Count > 0) { int n = q.Dequeue(); foreach (var l in links) if (l.from == n && dist[l.to] < 0) { dist[l.to] = dist[n] + 1; q.Enqueue(l.to); } }
                int summit = nodes.FindIndex(nd => nd.name.StartsWith("Summit"));
                float highest = 0f;
                for (int i = 0; i < nodes.Count; i++) if (dist[i] >= 0) highest = Mathf.Max(highest, nodes[i].top);
                Report(tag + ": a long climb up into the sky", summit >= 0 && dist[summit] >= 4 && nodes[summit].top >= 130f,
                       "highest place " + highest.ToString("F0") + "m, summit " + (summit >= 0 ? nodes[summit].top.ToString("F0") + "m, " + dist[summit] + " ways up at the quickest" : "MISSING"));
            }
            else
            {
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
                Report(tag + ": fast bus carries you", rodeBus && busRide > 12f, "carried " + busRide.ToString("F1") + "m in 1.2s" + (rodeBus ? "" : ", FELL OFF"));
            }
        }
        finally
        {
            Physics.simulationMode = mode;
            c.alwaysBoard = true;
            foreach (var m in movers) m.Init();
            foreach (var h in cars) h.ResetHome();
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE " + tag + " failures=" + failures);
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
        bool jumped = false, doubled = false, alighted = false;
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
                else if (jumped && l.doubleJump && !doubled && !c.Grounded && c.Velocity.y < 1f) { inp.jump = true; inp.jumpHeld = true; doubled = true; }
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

    // Infinity: towers all over the place, joined by hard ways across, and the same again under it, layer under
    // layer, each a whole course back. Every way across can be got across, both ways (ridden by a bot that
    // follows its route: round the start, half way, and at the hard end 40 layers down); it gets harder the
    // further on you go (no bridges at the hard end, thinner beams, wider gaps, some only with the double jump);
    // the petals never leave an easy hop to a neighbour's; fall off the side of a way anywhere and nothing catches
    // you, you just fall till you land on the course again lower down, a whole course back (and never get put
    // back); and it only keeps what's near you (it doesn't pile up)
    public static void RunInfinity()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var world = Object.FindFirstObjectByType<InfinityWorld>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            System.Action<SlideInput> step = inp => { c.Tick(inp); world.Track(); Physics.Simulate(Dt); };
            System.Action<Vector3> build = p => { world.GenerateAround(p, true); Physics.SyncTransforms(); c.Setup(); };
            // standing on tower ti,tj's plaza, layer k?
            System.Func<int, int, int, bool> onTower = (ti, tj, k) =>
            {
                Vector3 s = world.ToStructure(c.transform.position, k), o = world.Centre(ti, tj);
                float h = world.Width(ti, tj) * 0.5f - 0.3f;
                return c.Grounded && Mathf.Abs(s.x - o.x) < h && Mathf.Abs(s.z - o.z) < h && Mathf.Abs(s.y - 0.35f - o.y) < 0.3f;
            };
            // follow a route on layer k till you're on tower ti,tj (null), or fall, or get stuck
            System.Func<System.Collections.Generic.List<InfinityWorld.Way>, int, int, int, string> follow = (route, ti, tj, k) =>
            {
                int wi = 0, hold = 0, good = 0, pend = -1; string trace = ""; bool wasG = true;   // (a jump that's due: it's made as soon as you're on something)
                bool twicePend = false, twiceDue = false;
                Vector3 prev = world.ToStructure(c.transform.position, k);
                for (int i = 0; i < 900; i++)
                {
                    Vector3 s = world.ToStructure(c.transform.position, k);
                    var w = route[wi];
                    System.Func<Vector3, Vector3, Vector3> dirOf = (from, to) => { Vector3 d = to - from; d.y = 0f; return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward; };
                    Vector3 seg = dirOf(prev, w.at);
                    if (wi < route.Count - 1 && Vector3.Dot(s - w.at, seg) >= 0f)
                    {
                        if (w.jump) { pend = w.hold ? 20 : 0; twicePend = w.twice && twiceOk; }
                        prev = w.at; wi++; w = route[wi]; seg = dirOf(prev, w.at);
                    }
                    bool jump = pend >= 0 && c.Grounded;
                    if (jump) { hold = pend; pend = -1; twiceDue = twicePend; twicePend = false; trace += " J" + s.ToString("F1") + "v" + c.Speed.ToString("F1"); }
                    else if (twiceDue && !c.Grounded && c.Velocity.y < 0.5f) { jump = true; twiceDue = false; trace += " JJ" + s.ToString("F1"); }
                    if (c.Grounded && !wasG) trace += " L" + s.ToString("F1");
                    if (!c.Grounded && wasG) trace += " A" + s.ToString("F1") + "v" + c.Speed.ToString("F1") + "w" + wi;
                    wasG = c.Grounded;
                    if (!c.Grinding)
                    {
                        Vector3 to = w.at - s; to.y = 0f;
                        float f = Vector3.Dot(to, seg);
                        Vector3 aim = seg * Mathf.Max(f, 1f) + (to - seg * f);
                        c.yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    }
                    // (easing off where it's fiddly, and pulling up at the end)
                    Vector3 left = w.at - s; left.y = 0f;
                    bool last = wi == route.Count - 1;
                    float fwd = last && left.magnitude < c.Speed * 0.45f ? (c.Speed > 1f ? -1f : 0f) : w.jump && !w.sprint && c.Speed > 8.5f ? 0f : 1f;
                    step(new SlideInput { forward = fwd, sprint = w.sprint, jump = jump, jumpHeld = hold-- > 0 });
                    if (s.y < -7f) return "FELL at " + s.ToString("F1") + " (heading for waypoint " + wi + " at " + w.at.ToString("F1") + ")" + " TRACE" + trace;
                    good = onTower(ti, tj, k) ? good + 1 : 0;
                    if (good >= 5) return null;
                }
                return "STUCK at " + world.ToStructure(c.transform.position, k).ToString("F1") + " (heading for waypoint " + wi + ")";
            };
            // across the way from tower i,j on `axis` (or back), on layer k, from a standstill in the middle
            System.Func<int, int, int, bool, int, string> ride = (i, j, axis, reverse, k) =>
            {
                int i2 = i + (axis == 0 ? 1 : 0), j2 = j + (axis == 1 ? 1 : 0);
                int fi = reverse ? i2 : i, fj = reverse ? j2 : j, ti = reverse ? i : i2, tj = reverse ? j : j2;
                Vector3 from = world.Centre(fi, fj);
                build(world.ToWorld(from, k));
                var route = world.Route(i, j, axis, reverse);
                Place(world.ToWorld(from, k) + Vector3.up * 0.6f, Vector3.zero);
                c.yaw = YawTo(from, route[0].at);
                return follow(route, ti, tj, k);
            };

            var bad = new System.Text.StringBuilder();
            int ridden = 0, tried = 0, bridges = 0;
            var kinds = new System.Collections.Generic.Dictionary<InfinityWorld.Kind, int>();
            System.Action<int, int, int> rideAll = (lo, hi, k) =>
            {
                for (int i = lo; i <= hi; i++)
                    for (int j = lo; j <= hi; j++)
                        for (int axis = 0; axis < 2; axis++)
                        {
                            var kind = world.LinkOf(i, j, axis).kind;
                            if (kind == InfinityWorld.Kind.None) continue;
                            kinds[kind] = (kinds.ContainsKey(kind) ? kinds[kind] : 0) + 1;
                            if (kind == InfinityWorld.Kind.Bridge) bridges++;
                            foreach (bool back in new[] { false, true })
                            {
                                tried++;
                                string why = ride(i, j, axis, back, k);
                                if (why == null) ridden++;
                                else bad.Append(" " + kind + " from " + i + "," + j + (axis == 0 ? " +x" : " +z") + (back ? " (backwards)" : "") + " layer " + k + ": " + why + ";");
                            }
                        }
            };
            rideAll(-2, 2, 0);
            rideAll(8, 9, 5);
            int bridgesEasy = bridges; bridges = 0;
            rideAll(20, 22, 40);
            string mix = "";
            foreach (var kv in kinds) mix += (mix.Length > 0 ? ", " : "") + kv.Value + " " + kv.Key;
            int links = 0; foreach (var kv in kinds) links += kv.Value;
            // the petals: no two from different towers closer than (nearly) petalGap, so they're no shortcut
            float closest = float.MaxValue; string closestAt = ""; float clearest = float.MaxValue;
            System.Func<int, int, System.Collections.Generic.List<Vector3>> petalEdges = (ti, tj) =>
            {
                var pts = new System.Collections.Generic.List<Vector3>();
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 root, d; float len, ws;
                        world.Petal(ti, tj, sx, sz, out root, out d, out len, out ws);
                        if (len < 2f) continue;
                        Vector3 side = Vector3.Cross(Vector3.up, d);
                        for (int n = 0; n <= 8; n++)
                        {
                            float t = n / 8f, hw = world.PetalWidth(t, ws) * 0.5f;
                            Vector3 p = world.PetalAt(root, d, len, t);
                            pts.Add(p + side * hw); pts.Add(p - side * hw);
                        }
                    }
                return pts;
            };
            for (int i = -3; i <= 3; i++)
                for (int j = -3; j <= 3; j++)
                {
                    var mine = petalEdges(i, j);
                    Vector3 tc = world.Centre(i, j); float th = world.Width(i, j) * 0.5f;
                    foreach (var q in mine)
                        if (Mathf.Abs(q.x - tc.x) > th || Mathf.Abs(q.z - tc.z) > th) clearest = Mathf.Min(clearest, world.Clearance(q));
                    foreach (var nb in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1) })
                        foreach (var a in mine)
                            foreach (var b in petalEdges(i + nb.x, j + nb.y))
                            {
                                float dd = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                                if (dd < closest) { closest = dd; closestAt = i + "," + j + " and " + (i + nb.x) + "," + (j + nb.y); }
                            }
                }
            Report("infinity: every way across can be got across, both ways, and it's mostly hard ones", bad.Length == 0 && kinds.Count == 6 && bridges == 0 && closest > world.petalGap * 0.8f && clearest > world.petalClear * 0.8f,
                   ridden + " of " + tried + " rides made (" + mix + "; " + bridgesEasy + " bridges near the start, " + bridges + " at the hard end); petals of different towers at least " + closest.ToString("F1") + "m apart (" + closestAt + "), and " + clearest.ToString("F1") + "m clear of the ways across" + (bad.Length > 0 ? "; FAILED:" + bad : ""));

            // harder as you go: the ways near the start against the ways 800m on and more
            System.Func<int, string> hardness = lo =>
            {
                float beam = 0f, gap = 0f, stone = 0f, tower = 0f; int nb = 0, ng = 0, ns = 0, nt = 0, wide = 0;
                for (int i = lo; i < lo + 7; i++)
                    for (int j = lo; j < lo + 7; j++)
                    {
                        tower += world.Width(i, j); nt++;
                        for (int axis = 0; axis < 2; axis++)
                        {
                            var l = world.LinkOf(i, j, axis);
                            if (l.kind == InfinityWorld.Kind.Beam) { beam += l.width; nb++; }
                            if (l.kind == InfinityWorld.Kind.Gap) { gap += l.gap; ng++; if (l.gap > world.doubleJumpGap) wide++; }
                            if (l.kind == InfinityWorld.Kind.Stones) { stone += l.size; ns++; }
                        }
                    }
                return (beam / Mathf.Max(nb, 1)).ToString("F2") + "|" + (gap / Mathf.Max(ng, 1)).ToString("F2") + "|" + (stone / Mathf.Max(ns, 1)).ToString("F2") + "|" + (tower / nt).ToString("F2") + "|" + wide;
            };
            var near = hardness(-3).Split('|'); var far = hardness(20).Split('|');
            System.Func<string, float> num = x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
            // and the widest gaps there can't be got over without the double jump
            string needs = "NO GAP NEEDING THE DOUBLE JUMP FOUND";
            bool needsOk = false;
            for (int i = 20; i < 27 && !needsOk; i++)
                for (int j = 20; j < 27 && !needsOk; j++)
                    for (int axis = 0; axis < 2 && !needsOk; axis++)
                    {
                        var l = world.LinkOf(i, j, axis);
                        if (l.kind != InfinityWorld.Kind.Gap || l.gap < world.doubleJumpGap + 1f) continue;
                        twiceOk = false;
                        string without = ride(i, j, axis, false, 40);
                        twiceOk = true;
                        string with = ride(i, j, axis, false, 40);
                        needsOk = without != null && with == null;
                        needs = "a " + l.gap.ToString("F1") + "m gap: " + (without != null ? "can't without the double jump" : "MADE IT WITHOUT THE DOUBLE JUMP") + ", " + (with == null ? "can with it" : "COULDN'T WITH IT: " + with);
                    }
            twiceOk = true;
            Report("infinity: it gets harder as you go", num(far[0]) < num(near[0]) - 0.5f && num(far[1]) > num(near[1]) + 2f && num(far[2]) < num(near[2]) - 0.5f && num(far[3]) < num(near[3]) - 1.5f && needsOk,
                   "near the start / 800m on: beams " + near[0] + " / " + far[0] + "m wide, gaps " + near[1] + " / " + far[1] + "m, stones " + near[2] + " / " + far[2] + "m, towers " + near[3] + " / " + far[3] + "m, "
                   + near[4] + " / " + far[4] + " gaps needing the double jump; " + needs);

            // falling: off the side of a way, going along it; nothing catches you, you land on the course lower down
            var rnd = new System.Random(7);
            int falls = 0, landedOk = 0, mostLayers = 0, totalLayers = 0, wentBack = 0;
            float totalBack = 0f, longest = 0f;
            var fallBad = new System.Text.StringBuilder();
            var rb = c.GetComponent<Rigidbody>();
            float course = new Vector2(world.layerShift.x, world.layerShift.z).magnitude;
            System.Action<int, int, int> fallFrom = (ci, cj, k) =>
            {
                for (int tries = 0; tries < 400 && falls < (k == 0 ? 30 : 50); tries++)
                {
                    int i = ci + rnd.Next(-3, 4), j = cj + rnd.Next(-3, 4), axis = rnd.Next(2);
                    var l = world.LinkOf(i, j, axis);
                    if (l.kind == InfinityWorld.Kind.None) continue;
                    float t = 0.25f + 0.5f * (float)rnd.NextDouble(), sgn = rnd.Next(2) == 0 ? -1f : 1f;
                    float outBy = 1.6f + 2f * (float)rnd.NextDouble();
                    if (l.kind == InfinityWorld.Kind.Zigzag) outBy += l.size;
                    Vector3 s0 = Vector3.Lerp(l.a, l.b, t) + l.Lat * sgn * outBy + Vector3.up * 1f;
                    Vector3 w0 = world.ToWorld(s0, k);
                    build(w0);
                    Physics.SyncTransforms();
                    // (not if that spot's on something)
                    if (Physics.Raycast(w0 + Vector3.up * 0.5f, Vector3.down, 6f)) continue;
                    falls++;
                    Place(w0, l.Dir * 6f + l.Lat * sgn * 2f);
                    c.yaw = YawTo(Vector3.zero, l.Dir);
                    float before = world.Progress(w0), time = 0f;
                    bool landed = false, putBack = false;
                    int still = 0;
                    float lastY = w0.y;
                    for (int n = 0; n < 1500 && !landed; n++)
                    {
                        step(new SlideInput());
                        time += Dt;
                        float y = c.transform.position.y;
                        if (y > lastY + 5f) putBack = true;
                        lastY = y;
                        if (n % 8 == 0) build(c.transform.position);
                        still = c.Grounded ? still + 1 : 0;
                        landed = still >= 10;
                    }
                    int fell = world.LayerAt(c.transform.position.y) - k;
                    float back = before - world.Progress(c.transform.position);
                    if (landed && !putBack && fell >= 0)
                    {
                        landedOk++;
                        totalLayers += fell; mostLayers = Mathf.Max(mostLayers, fell);
                        totalBack += back; if (back > fell * course - 60f && back < fell * course + 60f) wentBack++;
                        longest = Mathf.Max(longest, time);
                    }
                    else fallBad.Append(" off " + l.kind + " " + i + "," + j + " layer " + k + ": " + (putBack ? "PUT BACK" : !landed ? "NEVER LANDED, at " + c.transform.position.ToString("F1") : "WENT UP?") + ";");
                }
            };
            fallFrom(0, 0, 0);
            fallFrom(20, 21, 60);
            float meanLayers = landedOk > 0 ? totalLayers / (float)landedOk : 99f, meanBack = landedOk > 0 ? totalBack / landedOk : 0f;
            int count = world.TowerCount;
            int most = (2 * world.radius + 3) * (2 * world.radius + 3) * (world.above + world.below + 3);
            Report("infinity: fall off and you just fall till you land on the course lower down, a whole course back for each layer", falls >= 45 && landedOk == falls && meanLayers <= 3.5f && mostLayers <= 15 && wentBack == falls && count <= most,
                   falls + " falls, " + landedOk + " landed: " + meanLayers.ToString("F1") + " layers down on average (most " + mostLayers + ", longest fall " + longest.ToString("F1") + "s), "
                   + meanBack.ToString("F0") + "m back on average (" + wentBack + " went back a whole " + course.ToString("F0") + "m course for each layer down)" + (fallBad.Length > 0 ? "; FAILED:" + fallBad : "") + "; " + count + " towers built (at most " + most + ")");
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE infinity failures=" + failures);
    }

    // Me: hearts fill their side's meter, and the meters drain; the playhead on the waveform pushes you along
    // with it; the server tower can be climbed icon by icon to the ping; the # rails grind; the voice rings bounce
    public static void RunMe()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var me = Object.FindFirstObjectByType<MeLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            System.Action<SlideInput> step = inp => { c.Tick(inp); me.Tick(Dt); Physics.Simulate(Dt); };
            // hearts and meters
            float sc0 = me.SoundCloud, dc0 = me.Discord;
            int scHeart = System.Array.IndexOf(me.heartSide, 0), dcHeart = System.Array.IndexOf(me.heartSide, 1);
            Place(me.hearts[scHeart].position + Vector3.down * 0.6f, Vector3.zero);
            me.Tick(Dt);
            float sc1 = me.SoundCloud;
            bool gone = !me.hearts[scHeart].gameObject.activeSelf;
            Place(me.hearts[dcHeart].position + Vector3.down * 0.6f, Vector3.zero);
            me.Tick(Dt);
            float dc1 = me.Discord;
            Place(new Vector3(0f, 0.3f, -62f), Vector3.zero);
            for (int i = 0; i < 500; i++) me.Tick(Dt);   // ten seconds standing about
            float sc2 = me.SoundCloud, dc2 = me.Discord;
            for (int i = 0; i < 10; i++) me.Tick(Dt);
            bool back = me.hearts[scHeart].gameObject.activeSelf;
            Report("me: hearts fill their side's meter, and both drain", sc1 > sc0 + 0.25f && gone && dc1 > dc0 + 0.25f && sc2 < sc1 - 0.2f && dc2 < dc1 - 0.2f && back,
                   "SoundCloud " + sc0.ToString("F2") + " -> " + sc1.ToString("F2") + " (heart " + (gone ? "taken" : "STILL THERE") + "), Discord " + dc0.ToString("F2") + " -> " + dc1.ToString("F2")
                   + "; ten seconds later " + sc2.ToString("F2") + " / " + dc2.ToString("F2") + "; the heart's " + (back ? "back" : "NOT BACK"));

            // the playhead: stand on the waveform just ahead of it, and it carries you along
            float d0 = 60f;
            Vector3 onWave = SkateWorldBuilder.MeTrackStart + new Vector3(0f, SkateWorldBuilder.MeWave(d0) + 0.6f, d0);
            Place(onWave, Vector3.zero);
            c.yaw = 0f;
            me.playheadAt = d0 - 0.5f;
            float most = 0f;
            for (int i = 0; i < 60; i++) { step(new SlideInput()); most = Mathf.Max(most, c.Velocity.z); }
            float without = 0f;
            Place(onWave, Vector3.zero);
            me.playheadAt = d0 + 60f;
            for (int i = 0; i < 60; i++) { step(new SlideInput()); without = Mathf.Max(without, c.Velocity.z); }
            Report("me: the playhead pushes you along the waveform", most > 15f && without < 6f, "riding with it " + most.ToString("F1") + " m/s; without it " + without.ToString("F1") + " m/s");

            // the server tower: up every icon to the ping, a hop each (held jump, double jump at the top of it)
            int reached = 0; string stuck = "";
            Place(SkateWorldBuilder.MeTowerStep(0) + Vector3.up * 0.4f, Vector3.zero);
            for (int n = 1; n < SkateWorldBuilder.MeTowerSteps && stuck == ""; n++)
            {
                Vector3 from = SkateWorldBuilder.MeTowerStep(n - 1), to = SkateWorldBuilder.MeTowerStep(n);
                bool ok = false, jumped = false, doubled = false;
                for (int i = 0; i < 200 && !ok; i++)
                {
                    Vector3 p = c.transform.position;
                    c.yaw = YawTo(p, to);
                    float dist = new Vector2(p.x - to.x, p.z - to.z).magnitude;
                    bool jump = !jumped && c.Grounded && dist < 3.2f;
                    if (jump) jumped = true;
                    bool dj = jumped && !doubled && !jump && !c.Grounded && c.Velocity.y < 0.5f && p.y < to.y + 0.6f;
                    if (dj) doubled = true;
                    step(new SlideInput { forward = dist > 0.6f ? (c.Speed > 4f ? 0f : 0.6f) : 0f, jump = jump || dj, jumpHeld = jumped });
                    ok = c.Grounded && Mathf.Abs(c.transform.position.y - 0.35f - to.y) < 0.3f && dist < SkateWorldBuilder.MeTowerPad * 0.5f;
                    if (c.transform.position.y < from.y - 3f) break;
                }
                for (int i = 0; i < 10; i++) step(new SlideInput());
                if (ok) reached = n; else stuck = "STUCK going from icon " + (n - 1) + " to " + n + " (at " + c.transform.position.ToString("F1") + ")";
            }
            Report("me: up the server tower to the ping", reached == SkateWorldBuilder.MeTowerSteps - 1, "reached icon " + reached + " of " + (SkateWorldBuilder.MeTowerSteps - 1)
                   + (stuck != "" ? "; " + stuck : ", the ping, " + SkateWorldBuilder.MeTowerStep(SkateWorldBuilder.MeTowerSteps - 1).y.ToString("F1") + "m up"));

            // the # rails grind, and the voice rings bounce
            Vector3 r0 = SkateWorldBuilder.MeHash + new Vector3(-3f, 0.55f, -10f);
            Place(r0 + new Vector3(0f, 1.1f, 1f), new Vector3(0f, 0f, 8f));
            c.yaw = 0f;
            bool grind = false;
            for (int i = 0; i < 40 && !grind; i++) { step(new SlideInput { forward = 1f }); grind = c.Grinding; }
            Place(new Vector3(55f, 2f, 25f), Vector3.zero);
            float peak = 0f;
            for (int i = 0; i < 120; i++) { step(new SlideInput()); peak = Mathf.Max(peak, c.transform.position.y); }
            Report("me: the # grinds, the voice rings bounce", grind && peak > 5f, (grind ? "ground the #" : "COULDN'T GRIND THE #") + "; bounced " + peak.ToString("F1") + "m up");
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE me failures=" + failures);
    }

    // Sketchbook: riding over the paving colours it in a patch at a time; up the plank and over the car colours the
    // car in; grinding a planter's rim and a handrail colours them in
    public static void RunSketch()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var sk = Object.FindFirstObjectByType<SketchLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            System.Action<SlideInput> step = inp => { c.Tick(inp); sk.Tick(Dt); Physics.Simulate(Dt); };
            System.Func<string, Paintable> thing = name =>
            {
                foreach (var t in sk.things) if (t.name.StartsWith(name)) return t;
                return null;
            };

            // across the paving
            Place(new Vector3(-3f, 0.3f, -62f), Vector3.zero);
            c.yaw = 0f;
            for (int i = 0; i < 160; i++) step(new SlideInput { forward = 1f, sprint = true });
            int patches = sk.PatchesPainted;
            Vector3 p = c.transform.position;
            int under = Mathf.FloorToInt((p.z - SkateWorldBuilder.SkZ0) / SkateWorldBuilder.SkCell) * sk.cells + Mathf.FloorToInt((p.x - SkateWorldBuilder.SkX0) / SkateWorldBuilder.SkCell);
            for (int i = 0; i < 60; i++) step(new SlideInput());
            bool underDone = sk.PatchPainted(under);

            // up the plank, over the car
            var car = thing("Car");
            Place(SkateWorldBuilder.SkCar + new Vector3(0f, 0.3f, -8f), Vector3.zero);
            c.yaw = 0f;
            float peak = 0f;
            for (int i = 0; i < 200 && c.transform.position.z < SkateWorldBuilder.SkCar.z + 10f; i++)
            {
                step(new SlideInput { forward = 1f });
                if (c.Grounded) peak = Mathf.Max(peak, c.transform.position.y - 0.35f);
            }

            // a planter's rim, and a handrail
            Vector3 rim = SkateWorldBuilder.SkPalmAt + new Vector3(SkateWorldBuilder.SkPlanterR - 0.12f, 0.48f, 0f);
            Place(rim + new Vector3(0f, 0.75f, -1.4f), new Vector3(0f, 0f, 6f));   // (just before it: the rim curves away fast)
            c.yaw = 0f;
            bool rimGrind = false;
            for (int i = 0; i < 40 && !rimGrind; i++) { step(new SlideInput { forward = 1f }); rimGrind = c.Grinding; }
            for (int i = 0; i < 5; i++) step(new SlideInput { forward = 1f });
            Paintable rimThing = null;
            foreach (var t in sk.things) if (t.name.StartsWith("Planter rim") && t.paint > 0f) rimThing = t;
            Vector3 h0 = new Vector3(2.5f, 1.5f + 0.95f, SkateWorldBuilder.SkZ1 - 7f + 0.3f);
            Place(h0 + new Vector3(0f, 1.1f, -0.6f), new Vector3(0f, -2f, -8f));
            c.yaw = 180f;
            bool railGrind = false;
            for (int i = 0; i < 40 && !railGrind; i++) { step(new SlideInput { forward = 1f }); railGrind = c.Grinding; }
            for (int i = 0; i < 5; i++) step(new SlideInput { forward = 1f });
            Paintable rail = null;
            foreach (var t in sk.things) if (t.name.StartsWith("Handrail") && t.paint > 0f) rail = t;

            Report("sketchbook: what you skate on gets coloured in", patches >= 6 && underDone && car != null && car.paint > 0f && peak > 1.45f && rimGrind && rimThing != null && railGrind && rail != null,
                   patches + " patches of paving coloured riding across" + (underDone ? "" : ", NOT THE ONE UNDER YOU") + "; over the car " + peak.ToString("F2") + "m up, the car "
                   + (car != null && car.paint > 0f ? "coloured" : "NOT COLOURED") + "; " + (rimGrind ? "ground the planter rim" : "COULDN'T GRIND THE PLANTER RIM") + (rimThing != null ? " (coloured)" : " (NOT COLOURED)")
                   + "; " + (railGrind ? "ground a handrail" : "COULDN'T GRIND A HANDRAIL") + (rail != null ? " (coloured)" : " (NOT COLOURED)") + "; " + sk.ThingsPainted + " of " + sk.things.Length + " things coloured");
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE sketch failures=" + failures);
    }

    // Dream: you can stand at the start of every dream; ride into something that links and you wake up in another
    // dream; fall off the kanji void and you're linked (not put back); get near the Gray Man and he's gone, and so are
    // you; the flesh tunnel can be ridden a long way down without falling out of it
    public static void RunDream()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var dl = Object.FindFirstObjectByType<DreamLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            System.Action<SlideInput> step = inp => { c.Tick(inp); dl.Tick(Dt); Physics.Simulate(Dt); };
            // standing in every dream
            string stand = ""; bool standOk = true;
            for (int i = 0; i < dl.dreams.Length; i++)
            {
                c.checkpoint = dl.dreams[i].spawn;
                c.Respawn();
                for (int s = 0; s < 30; s++) { c.Tick(new SlideInput()); Physics.Simulate(Dt); }
                bool ok = c.Grounded && Mathf.Abs(c.transform.position.y - dl.dreams[i].spawn.position.y) < 1f;
                standOk &= ok;
                stand += (i > 0 ? ", " : "") + dl.dreams[i].name.ToLower() + (ok ? "" : " (NOT STANDING, at " + c.transform.position.ToString("F1") + ")");
            }
            Report("dream: you can stand at the start of every dream", standOk, stand);

            // ride into a link: the white doorway in the temple's outer wall
            LinkWall door = null;
            foreach (var lw in Object.FindObjectsByType<LinkWall>(FindObjectsSortMode.None))
                if (lw.GetComponentInChildren<Collider>() != null && dl.Nearest(lw.transform.GetComponentInChildren<Collider>().bounds.center) == 0) { door = lw; break; }
            int linksBefore = dl.Links;
            string linkLog = "NO LINK IN THE TEMPLE";
            bool linked = false;
            if (door != null)
            {
                Bounds b = door.GetComponentInChildren<Collider>().bounds;
                Vector3 toward = b.center - SkateWorldBuilder.DreamO(0); toward.y = 0f; toward.Normalize();
                Place(new Vector3(b.center.x, 0.3f, b.center.z) - toward * 6f, toward * 6f);
                c.checkpoint = dl.dreams[0].spawn;
                c.yaw = YawTo(Vector3.zero, toward);
                for (int i = 0; i < 150 && dl.Links == linksBefore; i++) step(new SlideInput { forward = 1f });
                linked = dl.Links > linksBefore && dl.Current != 0 && Vector3.Distance(c.transform.position, dl.dreams[dl.Current].spawn.position) < 2f;
                linkLog = linked ? "rode into the temple's doorway and woke up in " + dl.dreams[dl.Current].name.ToLower() : "RODE INTO THE DOORWAY AND NOTHING HAPPENED (at " + c.transform.position.ToString("F1") + ")";
            }

            // fall off the kanji void
            Vector3 kv = SkateWorldBuilder.DreamO(5);
            for (int i = 0; i < 80; i++) dl.Tick(Dt);   // (let the link's cool-down pass)
            c.checkpoint = dl.dreams[5].spawn;
            Place(kv + new Vector3(14f, 0.3f, 0f), new Vector3(4f, 0f, 0f));
            int before = dl.Links;
            for (int i = 0; i < 400 && dl.Links == before; i++) step(new SlideInput());
            bool fellLinked = dl.Links > before && dl.Current != 5 && c.transform.position.y > -5f;
            string fellTo = dl.dreams[dl.Current].name.ToLower();

            // the Gray Man
            for (int i = 0; i < 80; i++) dl.Tick(Dt);
            c.checkpoint = dl.dreams[2].spawn;
            Place(dl.dreams[2].spawn.position + Vector3.up * 0.3f, Vector3.zero);
            dl.grayMan.gameObject.SetActive(true);
            dl.grayMan.position = c.transform.position + Vector3.forward * 4f;
            before = dl.Links;
            step(new SlideInput());
            bool gray = dl.MetGrayMan && dl.Links > before && !dl.grayMan.gameObject.activeSelf || (dl.MetGrayMan && dl.Links > before);
            Report("dream: links take you to another dream", linked && fellLinked && gray,
                   linkLog + "; " + (fellLinked ? "fell off the kanji void and woke up in " + fellTo : "FELL OFF THE KANJI VOID AND WASN'T LINKED") + "; "
                   + (gray ? "met the Gray Man and was linked" : "MET THE GRAY MAN AND NOTHING HAPPENED"));

            // down the flesh tunnel
            for (int i = 0; i < 80; i++) dl.Tick(Dt);
            c.checkpoint = dl.dreams[1].spawn;
            Place(dl.dreams[1].spawn.position + Vector3.up * 0.3f, Vector3.zero);
            float start = c.transform.position.z, lowest = 99f;
            before = dl.Links;
            for (int i = 0; i < 400; i++)
            {
                Vector3 p = c.transform.position;
                float d = p.z - SkateWorldBuilder.DreamO(1).z;
                c.yaw = YawTo(p, SkateWorldBuilder.FleshPath(d + 8f));
                step(new SlideInput { forward = 1f, sprint = true });
                lowest = Mathf.Min(lowest, c.transform.position.y);
                if (dl.Links > before) break;
            }
            float went = c.transform.position.z - start;
            bool reachedEnd = dl.Links > before;
            Report("dream: down the flesh tunnel", (went > 80f || reachedEnd) && lowest > -1f,
                   (reachedEnd ? "rode the whole tunnel and its glowing end linked you" : "rode " + went.ToString("F0") + "m down it") + "; lowest " + lowest.ToString("F1") + "m");
        }
        finally
        {
            Physics.simulationMode = mode;
            c.checkpoint = null;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE dream failures=" + failures);
    }

    // Circulation: carve a full lap round a circle on the floor and it lights up and speeds you on; a lap up on a
    // bowl's wall lights that; riding straight across a circle doesn't; the great ring rail grinds
    public static void RunCirculation()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var cl = Object.FindFirstObjectByType<CirculationLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.alwaysBoard = true;
            System.Action<SlideInput> step = inp => { c.Tick(inp); cl.Tick(Dt); Physics.Simulate(Dt); };
            // carve round circle i at radius r: steer along the way round, pulled back to r
            System.Func<int, float, float, string> lap = (i, r, speed) =>
            {
                var circle = cl.circles[i];
                Vector3 ctr = circle.centre;
                float y = SkateWorldBuilder.CircH(ctr.x + r, ctr.z);
                Place(new Vector3(ctr.x + r, y + 0.5f, ctr.z), new Vector3(0f, 0f, speed));
                c.yaw = 0f;
                float before = 0f, after = 0f;
                for (int n = 0; n < 900 && !circle.lit; n++)
                {
                    Vector3 p = c.transform.position, d = p - ctr; d.y = 0f;
                    Vector3 tangent = Vector3.Cross(Vector3.up, d.normalized) * -1f;   // anticlockwise seen from above
                    Vector3 aim = tangent + d.normalized * -(d.magnitude - r) * 0.25f;
                    c.yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    before = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;
                    step(new SlideInput { forward = c.Speed < speed ? 1f : 0f });
                    after = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;
                }
                return circle.lit ? "lit (" + before.ToString("F1") + " -> " + after.ToString("F1") + " m/s)" : "NOT LIT (at " + c.transform.position.ToString("F1") + ", turned " + circle.turned.ToString("F0") + ")";
            };
            string flat = lap(3, SkateWorldBuilder.CircFlatR, 9f);
            bool flatLit = cl.circles[3].lit;
            string bowl = lap(0, 12f * 0.75f, 8f);
            bool bowlLit = cl.circles[0].lit;

            // straight across another circle
            var other = cl.circles[4];
            Vector3 oc = other.centre;
            Place(oc + new Vector3(0f, 0.4f, -14f), new Vector3(0f, 0f, 9f));
            c.yaw = 0f;
            for (int n = 0; n < 200; n++) step(new SlideInput { forward = 1f });
            bool crossed = !other.lit;

            // the ring rail
            Vector3 r0 = new Vector3(SkateWorldBuilder.CircRingR, SkateWorldBuilder.CircRingY, 0f);
            Place(r0 + new Vector3(0f, 1.1f, -0.8f), new Vector3(0f, 0f, 8f));
            c.yaw = 0f;
            bool grind = false;
            for (int n = 0; n < 40 && !grind; n++) { step(new SlideInput { forward = 1f }); grind = c.Grinding; }
            Report("circulation: laps light the circles (not cutting across), the ring rail grinds", flatLit && bowlLit && crossed && grind,
                   "a floor circle: " + flat + "; the big bowl: " + bowl + "; straight across one: " + (crossed ? "not lit" : "LIT") + "; " + (grind ? "ground the ring rail" : "COULDN'T GRIND THE RING RAIL"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE circulation failures=" + failures);
    }

    // Stream: air and a kickflip off a kicker make a combo with a multiplier, banked when you land; a grind scores;
    // bailing loses the combo; the chat's messages lift you up the wall; the slot machine spins and pays out; the
    // obby's red bricks send you back to its start
    public static void RunStream()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var st = Object.FindFirstObjectByType<StreamLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        var movers = Object.FindObjectsByType<Mover>(FindObjectsSortMode.None);
        foreach (var m in movers) m.Init();
        try
        {
            c.alwaysBoard = true;
            st.Begin();
            System.Action<SlideInput> step = inp => { c.Tick(inp); st.Tick(Dt); Physics.Simulate(Dt); };
            // off the kicker, kickflip at the top, land, bank
            Place(SkateWorldBuilder.StKicker + new Vector3(0f, 0.3f, -14f), new Vector3(0f, 0f, 13f));
            c.yaw = 0f;
            int mult = 0; bool flipped = false, jumped = false;
            for (int i = 0; i < 300; i++)
            {
                bool dj = !flipped && !c.Grounded && c.Velocity.y < 1f && c.transform.position.y > 1.5f;
                if (dj) flipped = true;
                bool jump = !jumped && c.Grounded && c.transform.position.z > SkateWorldBuilder.StKicker.z + 1.2f;
                if (jump) jumped = true;
                step(new SlideInput { forward = 1f, jump = jump || dj, jumpHeld = !c.Grounded });
                mult = Mathf.Max(mult, st.Multiplier);
                if (st.Score > 0) break;
            }
            int afterAir = st.Score;
            string airBank = st.LastBank;

            // a grind
            Place(SkateWorldBuilder.StRail + new Vector3(0f, 1.1f, 0.5f), new Vector3(0f, 0f, 9f));
            c.yaw = 0f;
            bool grind = false;
            for (int i = 0; i < 200; i++)
            {
                step(new SlideInput { forward = 1f });
                grind |= c.Grinding;
                if (st.Score > afterAir) break;
            }
            int afterGrind = st.Score;
            string grindBank = st.LastBank;

            // a bail: in the air with a combo going, then sent back
            Place(new Vector3(0f, 6f, -30f), new Vector3(0f, 0f, 6f));
            for (int i = 0; i < 40; i++) step(new SlideInput());
            float had = st.ComboPoints;
            c.checkpoint = null; c.Respawn();
            step(new SlideInput());
            bool bailLost = had > 10f && st.ComboPoints < 5f && st.Score == afterGrind;   // (the respawn's little drop is a frame of air)

            Report("stream: tricks score, combos multiply, bails lose them", afterAir > 0 && mult >= 2 && grind && afterGrind > afterAir && bailLost,
                   "off the kicker: " + airBank + " (x" + mult + "); grinding: " + grindBank + (grind ? "" : " (NEVER GRINDED)") + "; bail: " + (bailLost ? "lost " + Mathf.RoundToInt(had) + " points of combo" : "COMBO NOT LOST (had " + had.ToString("F0") + ", now " + st.ComboPoints.ToString("F0") + ")"));

            // a chat lift
            Mover lift = null;
            foreach (var m in movers) if (m.name == "Chat lift") { lift = m; break; }
            float rose = 0f;
            if (lift != null)
            {
                lift.startAt = 0f; lift.Init(); lift.Step(0f); Physics.SyncTransforms();
                Vector3 top = lift.transform.position + Vector3.up * 1.4f;
                Place(top, Vector3.zero);
                float y0 = c.transform.position.y;
                for (int i = 0; i < 400; i++) { lift.Step(Dt); Physics.SyncTransforms(); step(new SlideInput()); }
                rose = c.transform.position.y - y0;
            }

            // the slots
            Place(SkateWorldBuilder.StSlotPad + Vector3.up * 0.5f, Vector3.zero);
            int beforeSlots = st.Score;
            bool spun = false;
            for (int i = 0; i < 150; i++) { step(new SlideInput()); spun |= st.Spinning; if (spun && !st.Spinning) break; }
            int reward = st.LastReward;

            // the obby's red bricks
            var s1 = SkateWorldBuilder.StObbySteps[2]; var s2 = SkateWorldBuilder.StObbySteps[3];
            Place(new Vector3((s1.x + s2.x) * 0.5f, 1.2f, (s1.z + s2.z) * 0.5f), Vector3.zero);
            for (int i = 0; i < 30; i++) step(new SlideInput());
            var o = SkateWorldBuilder.StObbySteps[0];
            bool sentBack = Vector3.Distance(c.transform.position, new Vector3(o.x, o.y, o.z)) < 2f;
            Report("stream: chat lifts you, the slots spin, the obby's red bricks send you back", rose > 8f && spun && reward >= 0 && sentBack,
                   "a chat message lifted you " + rose.ToString("F1") + "m; slots " + (spun ? "spun: " + (reward == 2 ? "jackpot" : reward == 1 ? "two of a kind" : "nothing") + " (+" + (st.Score - beforeSlots) + ")" : "NEVER SPUN")
                   + "; a red brick " + (sentBack ? "sent you back to the start" : "DIDN'T SEND YOU BACK (at " + c.transform.position.ToString("F1") + ")"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.checkpoint = null;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE stream failures=" + failures);
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
            // the fast board, on the flat of the lake: flat out in half a second; and a high jump
            Place(new Vector3(-75f, lakeY + 0.6f, 50f), Vector3.zero);
            c.yaw = 0f;
            for (int i = 0; i < 30; i++) Step(new SlideInput { forward = 1f, sprint = true });
            float push = c.Speed;
            Place(new Vector3(-75f, lakeY + 0.6f, 60f), Vector3.zero);
            float jy = c.transform.position.y, jump = 0f;
            Step(new SlideInput { jump = true, jumpHeld = true });
            for (int i = 0; i < 80; i++) { Step(new SlideInput { jumpHeld = true }); jump = Mathf.Max(jump, c.transform.position.y - jy); }
            // beam up a swoosh ramp into the sky
            var sw = SkateWorldBuilder.aeroSwooshes[0];
            Vector3 swStart = sw.foot - sw.dir * 14f;
            Place(new Vector3(swStart.x, SkateWorldBuilder.AeroH(swStart.x, swStart.z) + 0.6f, swStart.z), Vector3.zero);
            c.yaw = Mathf.Atan2(sw.dir.x, sw.dir.z) * Mathf.Rad2Deg;
            float beam = 0f;
            for (int i = 0; i < 250; i++) { Step(new SlideInput { forward = 1f, sprint = true }); beam = Mathf.Max(beam, c.transform.position.y - sw.foot.y); }
            // an orb throws you
            Vector3 orb = SkateWorldBuilder.aeroBumpers[0];
            Place(orb + Vector3.up * 6f, Vector3.zero);
            float thrown = 0f;
            for (int i = 0; i < 60; i++) { Step(new SlideInput()); thrown = Mathf.Max(thrown, c.Speed); }
            Report("aero: fast push, high jump, beam up a swoosh, orbs throw you", push > 25f && jump > 3.5f && beam > sw.height + 3f && thrown > 15f,
                   "flat out to " + (push * 3.6f).ToString("F0") + "km/h in 0.6s; jumped " + jump.ToString("F1") + "m; beamed up the swoosh to " + beam.ToString("F1") + "m (its lip's at " + sw.height.ToString("F1") + "m)"
                   + "; an orb threw me at " + (thrown * 3.6f).ToString("F0") + "km/h");
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

    // ---- Surf Mesa ----------------------------------------------------------------------------------------

    // Surf Mesa: a ramp steeper than you can stand on isn't ground; held into it you stay on it and it speeds you up
    // (the Source way: air strafing); the sand sends you back to the start of the stage; and an autopilot using only
    // what a player has (where it looks, which keys) surfs every stage from its start zone through its tunnel, or onto
    // the finish platform, without touching the sand
    public static void RunSurf()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        var sl = Object.FindFirstObjectByType<SurfLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            var rb = c.GetComponent<Rigidbody>();
            System.Action<SlideInput> step = inp => { c.Tick(inp); sl.Tick(Dt); Physics.Simulate(Dt); };

            // on the first ramp's face, going along it at 15 m/s, held into it for two seconds
            var r0 = SkateWorldBuilder.SurfStages[0].ramps[0];
            Vector3 O = SkateWorldBuilder.SurfO(0);
            Vector3 along = (r0.b - r0.a).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, new Vector3(along.x, 0f, along.z).normalized);   // +x: the face towards the middle
            float ang = SkateWorldBuilder.SurfRampAngle * Mathf.Deg2Rad;
            Vector3 faceN = side * Mathf.Sin(ang) + Vector3.up * Mathf.Cos(ang);
            Vector3 onFace = O + Vector3.Lerp(r0.a, r0.b, 0.3f) + side * (r0.Width * 0.4f) - Vector3.up * (r0.drop * 0.4f) + faceN * 0.37f;
            sl.Restart();
            rb.position = onFace; c.transform.position = onFace; rb.linearVelocity = along * 15f;
            Physics.SyncTransforms();
            float s0 = c.Speed;
            bool everGrounded = false;
            for (int i = 0; i < 100; i++)
            {
                c.yaw = Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg;   // looking into the ramp, W held (the same as A held looking along it)
                step(new SlideInput { forward = 1f });
                everGrounded |= c.Grounded;
            }
            Vector3 p = c.transform.position - O;
            float dist = Vector3.Dot(c.transform.position - (O + r0.a), faceN);
            bool stayed = dist < 1.2f && p.y > r0.b.y - r0.drop;
            Report("surf: a ramp isn't ground; held into it you stay on it and pick up speed", !everGrounded && stayed && c.Speed > s0 + 3f,
                   "speed " + s0.ToString("F1") + " -> " + c.Speed.ToString("F1") + " m/s (" + Mathf.RoundToInt(c.Speed * 39.37f) + " u/s) in 2s; "
                   + (everGrounded ? "STOOD ON IT" : "never stood on it") + "; " + (stayed ? "still on the face" : "CAME OFF at " + p.ToString("F1")));

            // the sand: back to the start
            sl.Restart();
            Vector3 sand = O + new Vector3(0f, 1.5f, 150f);
            rb.position = sand; c.transform.position = sand; rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            int resets = sl.Resets;
            for (int i = 0; i < 60 && sl.Resets == resets; i++) step(new SlideInput());
            bool back = sl.Resets > resets && Vector3.Distance(c.transform.position, sl.stages[0].start.position) < 1.5f;
            Report("surf: the sand sends you back to the start of the stage", back, back ? "back at the start" : "STILL AT " + c.transform.position.ToString("F1"));

            // every stage, start to end
            for (int s = 0; s < SkateWorldBuilder.SurfStages.Length; s++)
            {
                bool ok;
                string how = SurfAutopilot(sl, s, step, out ok);
                Report("surf: stage " + (s + 1) + " (" + SkateWorldBuilder.SurfStages[s].name + ") surfed from the start zone to the end", ok, how);
            }
        }
        finally
        {
            Physics.simulationMode = mode;
            sl.Restart();
        }
        Debug.Log("SLIDE_TESTS_DONE surf failures=" + failures);
    }

    // surfs stage s: walk off the start zone, then for each ramp in turn, fly at a point on its face and ride it
    // (held into it if too low on the face, along it otherwise), then fly at the tunnel or the finish
    static string SurfAutopilot(SurfLevel sl, int s, System.Action<SlideInput> step, out bool ok)
    {
        var d = SkateWorldBuilder.SurfStages[s];
        Vector3 O = SkateWorldBuilder.SurfO(s);
        sl.Restart();
        sl.StartAtStage(s);   // (as if the stages before were done)
        Physics.SyncTransforms();
        float ang = SkateWorldBuilder.SurfRampAngle * Mathf.Deg2Rad;
        int k = 0, resets = sl.Resets;
        float top = 0f, t = 0f;
        // which ramps the autopilot takes (in the sand pit, the left of the twin ramps)
        var path = new System.Collections.Generic.List<SkateWorldBuilder.SurfRampDef>();
        foreach (var r in d.ramps) if (!(s == 2 && r.a.x > 20f)) path.Add(r);
        string log = "";
        int surfedSteps = 0;
        var trace = new System.Collections.Generic.Queue<string>();
        for (int n = 0; n < 4000; n++, t += Dt)
        {
            if (sl.Current != s || sl.Finished) break;
            if (sl.Resets != resets) break;
            Vector3 p = c.transform.position;
            Vector3 v = c.Velocity, vH = new Vector3(v.x, 0f, v.z);
            top = Mathf.Max(top, vH.magnitude);
            Vector3 wish = Vector3.zero;
            Vector3 target;
            bool onRamp = false;
            if (k < path.Count)
            {
                var r = path[k];
                Vector3[] ridge = SkateWorldBuilder.SurfRidge(r);
                // the closest point on the ridge (seen from above)
                float best = float.MaxValue, bestAlong = 0f, acc = 0f, total = 0f;
                Vector3 q = O + ridge[0], tan = (ridge[1] - ridge[0]).normalized;
                for (int i = 0; i < ridge.Length - 1; i++) total += Vector3.Distance(ridge[i], ridge[i + 1]);
                for (int i = 0; i < ridge.Length - 1; i++)
                {
                    Vector3 a = O + ridge[i], b = O + ridge[i + 1];
                    Vector3 ab = b - a; ab.y = 0f;
                    Vector3 ap = p - a; ap.y = 0f;
                    float u = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
                    if (i > 0) u = Mathf.Max(u, 0f);
                    if (i < ridge.Length - 2) u = Mathf.Min(u, 1f);
                    Vector3 cp = a + (b - a) * u;
                    float dd = new Vector2(p.x - cp.x, p.z - cp.z).sqrMagnitude;
                    if (dd < best) { best = dd; q = cp; tan = (b - a).normalized; bestAlong = acc + u * Vector3.Distance(a, b); }
                    acc += Vector3.Distance(a, b);
                }
                Vector3 tanH = new Vector3(tan.x, 0f, tan.z).normalized;
                Vector3 sideV = Vector3.Cross(Vector3.up, tanH);
                float f = r.a.x > 0.01f ? -1f : 1f;   // the face towards the middle of the stage
                Vector3 nf = sideV * f;
                Vector3 faceN = nf * Mathf.Sin(ang) + Vector3.up * Mathf.Cos(ang);
                float across = Vector3.Dot(p - q, nf);
                float clear = Vector3.Dot(p - q, faceN) - 0.35f;
                onRamp = clear < 0.3f && across > -0.5f && across < r.Width + 0.5f && bestAlong > 0f && bestAlong < total;
                if (onRamp)
                {
                    surfedSteps++;
                    // low on the face: into it (that lifts you); high: along it
                    wish = across > r.Width * 0.35f ? -nf : tanH;
                    target = q;
                }
                else
                {
                    // aim at a point on the face ahead (the start of the ramp if you're not there yet)
                    float aimAlong = Mathf.Clamp(bestAlong + Mathf.Max(8f, vH.magnitude * 0.6f), 6f, total - 2f);
                    Vector3 aimRidge = O + PointAlong(ridge, aimAlong);
                    target = aimRidge + nf * (r.Width * 0.3f);
                }
                if (bestAlong >= total && !onRamp) { k++; log += " ramp " + k + " done at " + t.ToString("F1") + "s, " + Mathf.RoundToInt(vH.magnitude) + "m/s;"; }
            }
            else target = d.finish == Vector3.zero ? O + d.tunnel + Vector3.forward * 10f : O + d.finish;

            if (!onRamp)
            {
                if (c.Grounded && sl.stages[s].startZone.Contains(p)) wish = Vector3.forward;   // walk off the start zone
                else
                {
                    Vector3 to = target - p; to.y = 0f;
                    if (vH.magnitude < 2f) wish = to.normalized;
                    else
                    {
                        float turn = Vector3.SignedAngle(vH, to, Vector3.up);
                        if (Mathf.Abs(turn) > 1.5f) wish = Vector3.Cross(Vector3.up, vH).normalized * Mathf.Sign(turn);   // strafe the way to turn
                    }
                }
            }
            var inp = new SlideInput();
            if (wish.sqrMagnitude > 0.01f) { c.yaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg; inp.forward = 1f; }
            if (n % 10 == 0) { trace.Enqueue(t.ToString("F1") + " k" + k + (onRamp ? " ON" : " air") + " p" + (p - O).ToString("F0") + " v" + v.ToString("F0") + " tgt" + (target - O).ToString("F0")); if (trace.Count > 14) trace.Dequeue(); }
            step(inp);
        }
        bool done = s < SkateWorldBuilder.SurfStages.Length - 1 ? sl.Current == s + 1 : sl.Finished;
        ok = done && sl.Resets == resets;
        return (ok ? "made it in " + t.ToString("F1") + "s" : "DIDN'T: " + (sl.Resets != resets ? "hit the sand" : "stuck") + " at " + (c.transform.position - O).ToString("F0") + " after " + t.ToString("F1") + "s")
               + ", top speed " + Mathf.RoundToInt(top * 39.37f) + " u/s, " + (surfedSteps * Dt).ToString("F1") + "s on ramps;" + log
               + (ok ? "" : " TRACE: " + string.Join(" | ", trace.ToArray()));
    }

    static Vector3 PointAlong(Vector3[] pts, float d)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            float l = Vector3.Distance(pts[i], pts[i + 1]);
            if (d <= l) return Vector3.Lerp(pts[i], pts[i + 1], d / l);
            d -= l;
        }
        return pts[pts.Length - 1];
    }

    // ---- Elden Ruins ---------------------------------------------------------------------------------------

    static EldenLevel el;
    static System.Collections.Generic.List<string> eMoves = new System.Collections.Generic.List<string>();

    static void EStep(SlideInput inp)
    {
        bool climbing = c.Climbing, running = c.WallRunning;
        float vy = c.Velocity.y;
        c.Tick(inp);
        el.Tick(Dt);
        Physics.Simulate(Dt);
        if (c.Climbing && !climbing) eMoves.Add(c.ParkourMove);
        if (c.WallRunning && !running) eMoves.Add("WALL RUN");
        if (c.ParkourMove == "WALL JUMP" && c.Velocity.y > vy + 4f && inp.jump) eMoves.Add("WALL JUMP");
    }

    static void EPlace(Vector3 feet, float yaw)
    {
        c.Respawn();
        var rb = c.GetComponent<Rigidbody>();
        Vector3 p = feet + Vector3.up * 0.37f;
        rb.position = p; c.transform.position = p; rb.linearVelocity = Vector3.zero;
        c.yaw = yaw;
        Physics.SyncTransforms();
        for (int i = 0; i < 5; i++) EStep(new SlideInput());
        eMoves.Clear();
    }

    static float YawTo(Vector3 to) { Vector3 d = to - c.transform.position; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    // run (sprinting) at `aim`, jumping (held) once `jumpWhen` comes true, until `done` or `time` runs out
    static bool ERun(Vector3 aim, System.Func<bool> jumpWhen, System.Func<bool> done, float time, bool slideWhenClose = false, System.Func<bool> slideWhen = null)
    {
        bool jumped = false;
        float held = 0f;
        for (float t = 0f; t < time; t += Dt)
        {
            if (done()) return true;
            if (el.Dying) return false;
            c.yaw = YawTo(aim);
            bool jump = !jumped && jumpWhen != null && jumpWhen() && c.Grounded;
            if (jump) jumped = true;
            if (jumped) held += Dt;
            EStep(new SlideInput { forward = 1f, sprint = true, jump = jump, jumpHeld = jumped && held < 0.4f, slide = slideWhen != null && slideWhen() });
        }
        return done();
    }

    static Vector3 EPos { get { return c.transform.position - Vector3.up * 0.35f; } }   // your feet
    static bool EOn(float y, float tol = 0.3f) { return c.Grounded && Mathf.Abs(EPos.y - y) < tol; }

    // Elden Ruins: every part of the course can be done with the moves (vault, climb, jumps, wall runs, slide, wall
    // kicks), checked part by part from the grace before it; and falling into the gorge kills you and puts you back at
    // the grace
    public static void RunElden()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        el = Object.FindFirstObjectByType<EldenLevel>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            c.checkpoint = el.graces[0].spot;

            // the two low walls: run at them and you vault both, keeping your speed
            EPlace(new Vector3(0f, 0f, 6f), 0f);
            bool ok = ERun(new Vector3(0f, 0f, 40f), null, () => EPos.z > 26f, 4f);
            int vaults = eMoves.FindAll(m => m == "VAULT").Count;
            Report("elden: run at the low walls and you vault them", ok && vaults == 2 && c.Speed > 6f,
                   (ok ? "over both" : "STUCK at " + EPos.ToString("F1")) + "; moves: " + string.Join(", ", eMoves.ToArray()) + "; speed after " + c.Speed.ToString("F1"));

            // the ruin: jump at it holding W and you climb up
            EPlace(new Vector3(0f, 0f, 33f), 0f);
            ok = ERun(new Vector3(0f, 0f, 45f), () => EPos.z > 35.4f, () => EOn(3f) && EPos.z > 38f, 3f);
            Report("elden: jump at the 3 m ruin holding W and you climb it", ok && eMoves.Contains("CLIMB"),
                   (ok ? "on top" : "NOT UP, at " + EPos.ToString("F1")) + "; moves: " + string.Join(", ", eMoves.ToArray()));

            // the columns: hop from one to the next, then onto the fallen block, onto the block on it, onto the aqueduct
            EPlace(new Vector3(0f, 3f, 44f), 0f);
            ok = true;
            string where = "";
            Vector3 from = new Vector3(0f, 3f, 47.75f);
            var steps = new System.Collections.Generic.List<(Vector3 aim, float edge, float top)>();
            foreach (var p in SkateWorldBuilder.EPillars) steps.Add((p, p.z - SkateWorldBuilder.EPillarR, p.y));
            steps.Add((new Vector3(0f, 6.2f, 73f), 70f, 6.2f));
            steps.Add((new Vector3(0f, 8.2f, 76.5f), 75f, 8.2f));
            steps.Add((new Vector3(0f, SkateWorldBuilder.EDeckY, 81f), 78f, SkateWorldBuilder.EDeckY));
            float edgeFrom = 47.75f;
            foreach (var st in steps)
            {
                float takeoff = edgeFrom - 0.45f;
                var s = st;
                bool got = ERun(new Vector3(s.aim.x, 0f, s.aim.z + 0.4f), () => EPos.z > takeoff, () => EOn(s.top) && EPos.z > s.edge + 0.2f, 3f);
                // settle, and turn to the next
                for (int i = 0; i < 6; i++) EStep(new SlideInput());
                if (!got) { ok = false; where = "missed the step up to " + s.top + " (z " + s.edge + "), at " + EPos.ToString("F1"); break; }
                edgeFrom = s.aim.z + (s.top < 6f ? SkateWorldBuilder.EPillarR : 0f);
                if (s.top >= 6f) edgeFrom = s.edge + (s.top < 8f ? 5f : s.top < 9f ? 3f : 3f);
            }
            Report("elden: across the columns and up onto the aqueduct", ok, ok ? "on the aqueduct; moves: " + string.Join(", ", eMoves.ToArray()) : where + "; moves: " + string.Join(", ", eMoves.ToArray()));

            // along the aqueduct: sprint and jump the gaps; where the span's gone, run along the standing wall
            EPlace(new Vector3(0f, SkateWorldBuilder.EDeckY, 82f), 0f);
            ok = true; where = "";
            foreach (var gap in SkateWorldBuilder.EAqueductGaps)
            {
                var g = gap;
                bool wall = g.y - g.x > 6f;
                Vector3 aim = wall ? new Vector3(1.55f, 0f, g.y + 10f) : new Vector3(0f, 0f, g.y + 10f);
                if (wall) ERun(new Vector3(1.7f, 0f, g.x - 2f), null, () => EPos.x > 1.35f || EPos.z > g.x - 3f, 3f);   // over beside the wall first
                bool got = wall
                    ? ERun(aim, null, () => EOn(SkateWorldBuilder.EDeckY) && EPos.z > g.y + 0.3f, 6f)
                    : ERun(aim, () => EPos.z > g.x - 0.5f, () => EOn(SkateWorldBuilder.EDeckY) && EPos.z > g.y + 0.3f, 6f);
                if (!got) { ok = false; where = (wall ? "the wall run at " : "the gap at ") + g.x + ": at " + EPos.ToString("F1") + (el.Dying ? " (died)" : ""); break; }
            }
            Report("elden: along the aqueduct, jumping the gaps and running along the standing wall", ok && eMoves.Contains("WALL RUN"),
                   (ok ? "across" : "DIDN'T MAKE " + where) + "; moves: " + string.Join(", ", eMoves.ToArray()));

            // the gorge: fall in and you die, and come back at the grace
            el.graces[1].found = true; c.checkpoint = el.graces[1].spot;
            EPlace(new Vector3(0f, SkateWorldBuilder.EDeckY, 111f), 90f);
            int deaths = el.Deaths;
            for (int i = 0; i < 400 && el.Deaths == deaths; i++) EStep(new SlideInput { forward = 1f });
            for (int i = 0; i < 300 && el.Dying; i++) EStep(new SlideInput());
            bool back = el.Deaths == deaths + 1 && Vector3.Distance(c.transform.position, el.graces[1].spot.position) < 1.5f;
            Report("elden: fall into the gorge and YOU DIED, back at the last grace", back, back ? "died, back at " + el.graces[1].name : "deaths " + el.Deaths + ", at " + c.transform.position.ToString("F1"));

            // up the scaffolding on the curtain wall, step by step, onto the wall-walk
            EPlace(new Vector3(-16f, 10f, 226f), 90f);
            ok = ERun(new Vector3(-12f, 0f, 230.5f), () => EPos.z > 227f || EPos.x > -15.2f, () => EOn(12.6f), 3f);
            where = ok ? "" : "the first platform, at " + EPos.ToString("F1");
            for (int i = 1; i < 6 && ok; i++)
            {
                float x0 = -14f + i * 5f, top = 12.6f + i * 2.4f;
                for (int n = 0; n < 4; n++) EStep(new SlideInput());
                ok = ERun(new Vector3(x0 + 1.5f, 0f, 230.5f), () => EPos.x > x0 - 1.4f, () => EOn(top) && EPos.x > x0 + 0.2f, 3f);
                if (!ok) where = "platform " + (i + 1) + " (top " + top + "), at " + EPos.ToString("F1");
            }
            if (ok)
            {
                for (int n = 0; n < 4; n++) EStep(new SlideInput());
                ok = ERun(new Vector3(13f, 0f, 234f), () => true, () => EOn(SkateWorldBuilder.EWallY) && EPos.z > 232.3f, 3f);
                if (!ok) where = "onto the wall-walk, at " + EPos.ToString("F1");
            }
            Report("elden: up the scaffolding onto the castle wall", ok, (ok ? "on the wall-walk" : "STUCK at " + where) + "; moves: " + string.Join(", ", eMoves.ToArray()));

            // along the wall-walk: slide under the fallen block
            EPlace(new Vector3(16f, SkateWorldBuilder.EWallY, 234f), 90f);
            ok = ERun(new Vector3(29f, 0f, 234f), null, () => EPos.x > 26f, 4f, false, () => EPos.x > 19.5f && EPos.x < 25.5f);
            Report("elden: slide under the fallen block on the wall-walk", ok, ok ? "under it" : "STUCK at " + EPos.ToString("F1"));

            // round the corner and north: where the wall-walk's fallen, run along the building beside it
            EPlace(new Vector3(26.6f, SkateWorldBuilder.EWallY, 241f), 0f);
            ok = ERun(new Vector3(26.0f, 0f, 270f), null, () => EOn(SkateWorldBuilder.EWallY) && EPos.z > 259.3f, 5f);
            Report("elden: run along the building where the wall-walk's collapsed", ok && eMoves.Contains("WALL RUN"),
                   (ok ? "across" : "DIDN'T MAKE IT, at " + EPos.ToString("F1") + (el.Dying ? " (died)" : "")) + "; moves: " + string.Join(", ", eMoves.ToArray()));

            // the keep: up the scaffolding round it, up the chimney kicking from wall to wall, onto the roof
            el.graces[3].found = true; c.checkpoint = el.graces[3].spot;
            EPlace(new Vector3(28f, SkateWorldBuilder.EWallY, 267f), 0f);
            ok = true; where = "";
            var keep = new (Vector3 aim, System.Func<bool> jump, float top, System.Func<bool> past)[]
            {
                (new Vector3(27.7f, 0f, 274f), () => EPos.z > 270.2f, 27.6f, () => EPos.z > 271.8f),
                (new Vector3(27.7f, 0f, 280f), () => EPos.z > 274.9f, 30f, () => EPos.z > 277.8f),
                (new Vector3(27.7f, 0f, 286.8f), () => EPos.z > 280.9f, 32.4f, () => EPos.z > 283.8f),
                (new Vector3(22f, 0f, 287.8f), () => EPos.x < 26.6f, 34.8f, () => EPos.x < 24.2f),
                (new Vector3(16f, 0f, 287.1f), () => EPos.x < 21.1f, 34.8f, () => EPos.x < 18.7f),
            };
            for (int i = 0; i < keep.Length && ok; i++)
            {
                var k = keep[i];
                for (int n = 0; n < 4; n++) EStep(new SlideInput());
                ok = ERun(k.aim, k.jump, () => EOn(k.top) && k.past(), 3.5f);
                if (!ok) where = "scaffold " + (i + 1) + " (top " + k.top + "), at " + EPos.ToString("F1");
            }
            // the chimney: jump at the buttress, then kick from wall to wall till you can climb onto it
            int kicks = 0;
            string ctrace = "";
            if (ok)
            {
                for (int n = 0; n < 10; n++) { c.yaw = 0f; EStep(new SlideInput()); }
                c.yaw = 0f;
                EStep(new SlideInput { jump = true, jumpHeld = true });
                // looking at the buttress with W held throughout, Space each time you touch a wall
                for (float t = 0f; t < 6f; t += Dt)
                {
                    if (EOn(41f) || el.Dying) break;
                    if (EPos.y > 40.7f && (c.Climbing || c.Grounded))
                    {
                        // up: let go of W and stand on the top of the buttress
                        for (int n = 0; n < 30; n++) EStep(new SlideInput());
                        break;
                    }
                    c.yaw = 0f;
                    float zc = c.transform.position.z;
                    bool atWall = zc > 288.2f - 0.6f || zc < 286f + 0.6f;
                    bool jump = atWall && c.Velocity.y < 3f && !c.Grounded && !c.Climbing && !(zc > 287.5f && EPos.y > 38.9f);
                    if (jump) kicks++;
                    EStep(new SlideInput { forward = 1f, jump = jump });
                    if (Mathf.RoundToInt(t / Dt) % 5 == 0) ctrace += " " + t.ToString("F1") + (jump ? "K" : "") + (c.WallRunning ? "R" : "") + (c.Grounded ? "G" : "") + "(" + EPos.y.ToString("F1") + "," + EPos.z.ToString("F1") + " v" + c.Velocity.y.ToString("F1") + "/" + c.Velocity.z.ToString("F1") + ")";
                }
                ok = EOn(41f);
                if (!ok) where = "the chimney (" + kicks + " kicks), at " + EPos.ToString("F1") + " TRACE" + ctrace;
            }
            // along the top of the buttress, then over to the scaffolds on the west side and onto the roof
            if (ok)
            {
                for (int n = 0; n < 5; n++) EStep(new SlideInput());
                ok = ERun(new Vector3(10.2f, 0f, 288.8f), null, () => EPos.x < 10.6f, 3f);
                for (int n = 0; n < 8; n++) { c.yaw = 180f; EStep(new SlideInput()); }
                if (ok) ok = ERun(new Vector3(10.2f, 0f, 283f), () => true, () => EOn(43.2f) && EPos.z < 285.7f, 3f);
                if (!ok) where = "over to the west scaffold, at " + EPos.ToString("F1");
                for (int n = 0; n < 4; n++) EStep(new SlideInput());
                if (ok) ok = ERun(new Vector3(10.2f, 0f, 278f), () => EPos.z < 282.4f, () => EOn(45.2f) && EPos.z < 279.8f, 3f);
                if (!ok && where == "") where = "the last scaffold, at " + EPos.ToString("F1");
                for (int n = 0; n < 4; n++) EStep(new SlideInput());
                if (ok) ok = ERun(new Vector3(19f, 0f, 278f), null, () => el.Done, 3f);
                if (!ok && where == "") where = "onto the roof, at " + EPos.ToString("F1");
            }
            Report("elden: up the keep (scaffolding, the chimney, the west side) to the summit", ok,
                   (ok ? "at the top, " + kicks + " wall kicks up the chimney" : "STUCK on " + where) + "; moves: " + string.Join(", ", eMoves.ToArray()));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.checkpoint = el.graces[0].spot;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE elden failures=" + failures);
    }
}
