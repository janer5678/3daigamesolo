using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Test rides, run by the builder from the command line: steps the real SlideController and the
// physics engine by hand in the built scene and reports what happened.
public static class SlideTests
{
    static SlideController c;
    static int failures;
    const float Dt = 0.02f;

    [MenuItem("Skate/Run test rides")]
    public static void RunAll()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
        var mode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Physics.SyncTransforms();
        failures = 0;
        try
        {
            Coast();
            PushDownTheRun();
            StopsOnTheFlat();
            JumpAndAirControl();
            BoardControl();
            RampLock();
            RailLock();
            JumpFeel();
            SolidEverywhere();
        }
        finally
        {
            Physics.simulationMode = mode;
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

    // walking is quick to get going, the board takes a moment longer and goes faster,
    // and stepping off drops you back to walking pace
    static void Coast()
    {
        Place(PlazaSpot, Vector3.zero);
        for (int i = 0; i < 25; i++) Step(new SlideInput { forward = 1f });
        float walk = c.Speed;
        float t = 0f;
        while (c.Speed < 14f && t < 5f) { Step(new SlideInput { push = true, kick = true }); t += Dt; }
        float board = c.Speed;
        for (int i = 0; i < 75; i++) Step(new SlideInput { forward = 1f });
        float off = c.Speed;
        Report("walk, board, step off", walk > c.walkSpeed - 0.5f && walk < c.walkSpeed + 0.5f && board >= 14f && t < 1.5f && off < c.walkSpeed + 0.5f,
               "walk after 0.5s: " + (walk * 3.6f).ToString("F0") + "km/h; board to " + (board * 3.6f).ToString("F0") + "km/h in " + t.ToString("F2")
               + "s; 1.5s after stepping off: " + (off * 3.6f).ToString("F0") + "km/h");

        // and standing still on the run's slope you stay put
        c.Respawn();
        Physics.SyncTransforms();
        Vector3 from = c.transform.position;
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            Step(new SlideInput());
            if (i % 100 == 99) log.Append(" t=" + ((i + 1) * Dt).ToString("F0") + ":" + c.Speed.ToString("F2") + (c.Grounded ? "g" : "AIR"));
        }
        float moved = Vector3.Distance(from, c.transform.position);
        Report("doesn't slide off on its own", moved < 1f, "moved " + moved.ToString("F2") + "m in 20s with nothing pressed |" + log);
    }

    // hold Shift and keep looking down the run; should get all the way to the plaza
    static void PushDownTheRun()
    {
        c.Respawn();
        Physics.SyncTransforms();
        float t = 0f, top = 0f;
        while (t < 120f && c.transform.position.x < 650f && c.transform.position.y > -50f)
        {
            float x = c.transform.position.x;
            Vector3 to = SkateWorldBuilder.OnPath(x + 15f, 0f) - c.transform.position;
            c.yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            Step(new SlideInput { push = true, kick = true });
            t += Dt;
            top = Mathf.Max(top, c.Speed);
        }
        Vector3 p = c.transform.position;
        Report("push down the run to the plaza", p.x >= 650f, "reached x=" + p.x.ToString("F0") + " in " + t.ToString("F1") + "s, top speed " + (top * 3.6f).ToString("F0") + "km/h");
    }

    // let go at speed and you stop within a couple of seconds
    static void StopsOnTheFlat()
    {
        Place(PlazaSpot, new Vector3(20f, 0f, 0f));
        float x0 = c.transform.position.x, t = 0f;
        while (c.Speed > 0.3f && t < 10f) { Step(new SlideInput()); t += Dt; }
        Report("stops when you let go", t < 2.5f, "from 72km/h: stopped in " + t.ToString("F2") + "s over " + (c.transform.position.x - x0).ToString("F1") + "m");
    }

    // on the board at full push you can carve a sharp turn without losing speed, and S brakes
    static void BoardControl()
    {
        Place(PlazaSpot, new Vector3(15f, 0f, 0f));   // yaw 90: facing +x
        float t = 0f;
        while (Vector3.Angle(Vector3.right, new Vector3(c.Velocity.x, 0f, c.Velocity.z)) < 85f && t < 3f)
        { Step(new SlideInput { push = true, strafe = -1f }); t += Dt; }   // A: turn left
        float kept = c.Speed;

        Place(PlazaSpot, new Vector3(15f, 0f, 0f));
        float b = 0f;
        while (c.Speed > 1f && b < 5f) { Step(new SlideInput { push = true, forward = -1f }); b += Dt; }
        Report("board steering and braking", t < 0.5f && kept > 13f && b < 1f,
               "90 degree turn at 54km/h in " + t.ToString("F2") + "s, still " + (kept * 3.6f).ToString("F0") + "km/h; S stops you in " + b.ToString("F2") + "s");
    }

    // on the board you stick to ramps: over the funbox at speed without taking off, and up the
    // end-wall quarterpipe and back down it
    static void RampLock()
    {
        float y = SkateWorldBuilder.Base(700f) + 0.4f;
        Place(new Vector3(690f, y, 0f), new Vector3(16f, 0f, 0f));
        float air = 0f, t = 0f;
        while (c.transform.position.x < 714f && t < 3f)
        {
            Step(new SlideInput { push = true, kick = true });
            t += Dt;
            if (c.transform.position.x > 700f && !c.Grounded) air += Dt;
        }
        bool overFunbox = c.transform.position.x >= 714f;

        Place(new Vector3(838f, y, 5f), new Vector3(13f, 0f, 0f));
        float y0 = c.transform.position.y, peak = y0, wall = 0f;
        t = 0f;
        while (t < 4f && !(c.Velocity.x < -4f && c.transform.position.y < y0 + 0.3f))
        {
            Step(new SlideInput { push = true, kick = true });
            t += Dt;
            peak = Mathf.Max(peak, c.transform.position.y);
            if (c.Locked && c.transform.position.y > y0 + 1f) wall += Dt;
        }
        bool back = c.Velocity.x < -4f;
        Report("board flies off ramps, locks onto walls", overFunbox && air > 0.15f && peak - y0 > 2.5f && back && wall > 0.1f,
               "over the funbox at 58km/h: " + air.ToString("F2") + "s in the air; up the end wall " + (peak - y0).ToString("F1")
               + "m (" + wall.ToString("F2") + "s stuck to it) and " + (back ? "rode back down" : "didn't come back") + " in " + t.ToString("F1") + "s");
    }

    // drop onto the plaza's flat rail on the board: it locks on and carries you along it to the end;
    // Space jumps you off whenever you like, and so does letting go of Shift
    static void RailLock()
    {
        float y = SkateWorldBuilder.Base(700f) + 0.4f;
        var rb = c.GetComponent<Rigidbody>();
        System.Action<float> drop = x =>
        {
            c.Respawn();
            Vector3 p = new Vector3(x, y + 0.5f + 0.05f + 0.35f + 0.25f, 18.15f);
            rb.position = p; c.transform.position = p;
            rb.linearVelocity = new Vector3(8f, 0f, 0f);
            c.yaw = 90f;
            Physics.SyncTransforms();
        };

        drop(686f);
        float t = 0f, caughtAt = -1f, lastX = 0f;
        while (t < 4f)
        {
            Step(new SlideInput { push = true, kick = true });
            t += Dt;
            if (c.Grinding) { if (caughtAt < 0f) caughtAt = t; lastX = c.transform.position.x; }
            else if (caughtAt >= 0f) break;
        }
        bool rodeToEnd = caughtAt >= 0f && lastX > 699f;

        drop(686f);
        for (int i = 0; i < 25 && !c.Grinding; i++) Step(new SlideInput { push = true, kick = true });
        for (int i = 0; i < 10; i++) Step(new SlideInput { push = true, kick = true });
        Step(new SlideInput { push = true, kick = true, jump = true });
        bool jumpedOff = !c.Grinding && c.Velocity.y > 3f;

        drop(686f);
        for (int i = 0; i < 25 && !c.Grinding; i++) Step(new SlideInput { push = true, kick = true });
        bool wasOn = c.Grinding;
        Step(new SlideInput());
        bool steppedOff = wasOn && !c.Grinding;

        // coming at it from an angle, or straight across, it still snaps on and carries you along it
        string angles = "";
        bool allAngles = true;
        var names = new HashSet<string>();
        foreach (float a in new[] { 5f, 20f, 45f, 75f, -20f, -75f })
        {
            drop(690f);
            Vector3 p = new Vector3(690f, y + 0.5f + 0.4f + 0.3f, 18f + Mathf.Sign(a) * 0.4f);   // start on the side you're coming from
            rb.position = p; c.transform.position = p;
            rb.linearVelocity = Quaternion.Euler(0f, 90f + a, 0f) * Vector3.forward * 8f;   // heading across towards the rail
            Physics.SyncTransforms();
            bool caught = false;
            for (int i = 0; i < 40 && !caught; i++) { Step(new SlideInput { push = true, kick = true }); caught = c.Grinding; }
            bool slide = c.Boardslide;
            string gname = c.GrindName;
            if (gname != null) names.Add(gname);
            for (int i = 0; i < 10 && c.Grinding; i++) Step(new SlideInput { push = true, kick = true });
            bool along = caught && slide == (Mathf.Abs(a) > 35f) && Mathf.Abs(c.transform.position.z - 18f) < 0.05f && Mathf.Abs(c.Velocity.x) > 3f;
            allAngles &= along;
            angles += " " + a.ToString("F0") + " degrees: " + (along ? "snapped on: " + gname + ", grinding at " + (Mathf.Abs(c.Velocity.x) * 3.6f).ToString("F0") + "km/h" : "missed") + ";";
        }

        // A/D while grinding switches grind
        drop(686f);
        for (int i = 0; i < 25 && !c.Grinding; i++) Step(new SlideInput { push = true });
        string g0 = c.GrindName;
        Step(new SlideInput { push = true, strafe = 1f });
        string g1 = c.GrindName;
        Step(new SlideInput { push = true, strafe = 1f });   // still held: no change
        bool switched = g0 != null && g1 != null && g0 != g1 && c.GrindName == g1;
        angles += " D while grinding: " + g0 + " -> " + g1 + ";";

        Report("board locks onto rails", rodeToEnd && jumpedOff && steppedOff && allAngles && names.Count >= 5 && switched,
               (caughtAt >= 0f ? "locked on after " + caughtAt.ToString("F2") + "s and ground to x=" + lastX.ToString("F1") + " (rail ends at 700)" : "never locked on")
               + "; Space " + (jumpedOff ? "jumps off" : "didn't jump off") + "; letting go of Shift " + (steppedOff ? "steps off" : "didn't step off") + ";" + angles);
    }

    // tap Space for a jump, hold it to go higher; on the board a held jump kickflips, which pushes you
    // forward. Mashing Space on take-off doesn't stack jumps, hopping again and again slowly builds
    // speed, and C slides, with a higher jump out of it
    static void JumpFeel()
    {
        float y0;
        System.Func<bool, bool, float> jumpPeak = (board, hold) =>
        {
            Place(PlazaSpot, Vector3.zero);
            float start = c.transform.position.y, peak = start;
            Step(new SlideInput { push = board, jump = true, jumpHeld = hold });
            for (int i = 0; i < 80; i++) { Step(new SlideInput { push = board, jumpHeld = hold }); peak = Mathf.Max(peak, c.transform.position.y); }
            return peak - start;
        };
        float footTap = jumpPeak(false, false), footHold = jumpPeak(false, true);
        float boardTap = jumpPeak(true, false), boardHold = jumpPeak(true, true);

        // an ordinary jump doesn't flip the board; big air (dropping from high up) does, by itself
        Place(PlazaSpot, new Vector3(8f, 0f, 0f));
        Step(new SlideInput { push = true, jump = true });
        bool tapFlip = false;
        for (int i = 0; i < 30; i++) { Step(new SlideInput { push = true }); tapFlip |= c.Flipping; }
        Place(PlazaSpot + Vector3.up * 6f, new Vector3(8f, 0f, 0f));
        bool bigFlip = false;
        for (int i = 0; i < 60 && !bigFlip; i++) { Step(new SlideInput { push = true }); bigFlip = c.Flipping; }

        // coyote time: ride off the end of the manual pad and jump a moment after leaving it
        Place(new Vector3(682f, SkateWorldBuilder.Base(700f) + 0.2f + 0.4f, -12f), new Vector3(8f, 0f, 0f));
        int steps = 0;
        while (c.Grounded && steps < 200) { Step(new SlideInput { push = true }); steps++; }
        Step(new SlideInput { push = true }); Step(new SlideInput { push = true }); Step(new SlideInput { push = true });   // 0.06s off the edge
        Step(new SlideInput { push = true, jump = true });
        bool coyote = c.Velocity.y > 4f;

        // jump buffering: press Space just before landing and you jump as you touch down
        Place(PlazaSpot + Vector3.up * 1.2f, Vector3.zero);
        while (!c.Grounded && c.transform.position.y > PlazaSpot.y + 0.35f && steps < 400) { Step(new SlideInput()); steps++; if (c.transform.position.y < PlazaSpot.y + 0.6f) break; }
        Step(new SlideInput { jump = true });   // a little early
        bool buffered = false;
        for (int i = 0; i < 12 && !buffered; i++) { Step(new SlideInput()); buffered = c.Velocity.y > 4f; }

        // mashing on take-off
        Place(PlazaSpot, Vector3.zero);
        y0 = c.transform.position.y;
        float mashed = y0;
        for (int i = 0; i < 3; i++) Step(new SlideInput { push = true, jump = true });
        for (int i = 0; i < 80; i++) { Step(new SlideInput { push = true }); mashed = Mathf.Max(mashed, y0 + 0f + (c.transform.position.y - y0)); }

        // hop, land, hop again... on foot, holding W
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
        Step(new SlideInput { push = true, jump = true, forward = 1f });
        float afterJump = new Vector3(c.Velocity.x, 0f, c.Velocity.z).magnitude;

        bool ok = footHold > footTap + 0.25f && boardHold > boardTap + 0.25f && !tapFlip && bigFlip && coyote && buffered
                  && mashed - y0 < boardTap + 0.1f && hopEnd > hopStart + 2f && slid && slideSpeed > c.walkSpeed + 1.5f
                  && slideLater > c.walkSpeed && slideJump > footTap + 0.2f && afterJump > fast - 0.5f;
        Report("hold to jump higher, big-air kickflip, coyote time, jump buffer, hops build speed, C slides", ok,
               "jump on foot " + footTap.ToString("F2") + "m tapped / " + footHold.ToString("F2") + "m held; on the board " + boardTap.ToString("F2") + " / " + boardHold.ToString("F2")
               + "m; ordinary jump " + (tapFlip ? "KICKFLIPPED" : "no flip") + ", big drop " + (bigFlip ? "kickflips" : "NO FLIP")
               + "; jump 0.06s after riding off an edge " + (coyote ? "works" : "IGNORED") + "; Space just before landing " + (buffered ? "jumps on touchdown" : "IGNORED")
               + "; mashing Space " + (mashed - y0).ToString("F2") + "m; 8 hops on foot: " + (hopStart * 3.6f).ToString("F0") + " -> " + (hopEnd * 3.6f).ToString("F0") + "km/h"
               + "; slide " + (slid ? "starts at " + (slideSpeed * 3.6f).ToString("F0") + "km/h, " + (slideLater * 3.6f).ToString("F0") + "km/h after 0.4s" : "DIDN'T START")
               + ", jump out of it " + slideJump.ToString("F2") + "m; jumping at " + (fast * 3.6f).ToString("F0") + "km/h leaves you at " + (afterJump * 3.6f).ToString("F0") + "km/h");
    }
    // the ground is solid past the edge of the skateable area too: ride out there and you don't drop
    // out of the world
    static void SolidEverywhere()
    {
        string log = "";
        bool ok = true;
        foreach (var at in new[] { new Vector2(1060f, 0f), new Vector2(400f, 220f), new Vector2(400f, -220f), new Vector2(-200f, 0f) })
        {
            float gy = SkateWorldBuilder.Height(at.x, at.y);
            Place(new Vector3(at.x, gy + 1.5f, at.y), new Vector3(8f, 0f, 0f));
            for (int i = 0; i < 150; i++) Step(new SlideInput { push = true, kick = true });
            float y = c.transform.position.y - SkateWorldBuilder.Height(c.transform.position.x, c.transform.position.z);
            bool held = y > -0.5f;
            ok &= held;
            log += " (" + at.x.ToString("F0") + "," + at.y.ToString("F0") + "): " + (held ? "on the ground" : "FELL " + y.ToString("F0") + "m") + ";";
        }
        Report("solid ground past the edges", ok, log);
    }
    // a modest hop, and you can still change direction mid-air
    static void JumpAndAirControl()
    {
        Place(PlazaSpot, Vector3.zero);
        float y0 = c.transform.position.y, peak = y0;
        Step(new SlideInput { jump = true });
        for (int i = 0; i < 80; i++) { Step(new SlideInput()); peak = Mathf.Max(peak, c.transform.position.y); }
        float height = peak - y0;

        // on the board you jump higher
        Place(PlazaSpot, Vector3.zero);
        y0 = c.transform.position.y; float boardPeak = y0, hang = 0f;
        Step(new SlideInput { push = true, kick = true, jump = true });
        while (!c.Grounded && hang < 3f || hang < 0.1f) { Step(new SlideInput { push = true, kick = true }); hang += Dt; boardPeak = Mathf.Max(boardPeak, c.transform.position.y); }
        float boardHeight = boardPeak - y0;
        Report("board jumps higher than on foot", boardHeight > height + 0.15f && boardHeight < 1.6f,
               "on foot " + height.ToString("F2") + "m; on the board " + boardHeight.ToString("F2") + "m with " + hang.ToString("F2") + "s in the air");

        Place(PlazaSpot, new Vector3(11f, 0f, 0f));
        Step(new SlideInput { forward = 1f, jump = true });
        float air = 0f;
        while (!c.Grounded && air < 2f || air < 0.1f) { Step(new SlideInput { strafe = 1f }); air += Dt; }
        Vector3 v = c.Velocity; v.y = 0f;
        float turned = Vector3.Angle(Vector3.right, v);
        Report("jump height and air control", height > 0.8f && height < 1.1f && turned > 30f,
               "jump height " + height.ToString("F2") + "m; holding D through a " + air.ToString("F2") + "s jump turned you " + turned.ToString("F0") + " degrees");
    }
// the pink palace: standing still you stay put, riding about you stay on the floor, and you can
    // ride down the grand staircase from the terrace
    public static void RunPalace()
    {
        c = Object.FindFirstObjectByType<SlideController>();
        c.Setup();
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
            for (int i = 0; i < 200; i++) { Step(new SlideInput { push = true, kick = true }); low = Mathf.Min(low, c.transform.position.y); }
            float rode = c.transform.position.z - from.z;

            Place(new Vector3(0f, 2.4f + 0.4f, 36f), new Vector3(0f, 0f, -6f));
            c.yaw = 180f;
            float stairLow = 99f;
            for (int i = 0; i < 150; i++) { Step(new SlideInput { push = true, kick = true }); stairLow = Mathf.Min(stairLow, c.transform.position.y); }
            Vector3 end = c.transform.position;
            bool downStairs = end.z < 20f && end.y > 0f && end.y < 1f && stairLow > 0f;

// wallride: in the air beside the palace's front wall, lean into it (A) and you ride along it
            Place(new Vector3(-7f, 4.5f, 46.55f), new Vector3(12f, 0f, 0f));
            float wy0 = c.transform.position.y;
            int riding = 0;
            for (int i = 0; i < 30; i++) { Step(new SlideInput { push = true, strafe = -1f }); if (c.WallRiding) riding++; }
            float wallDrop = wy0 - c.transform.position.y;
            float wallX = c.transform.position.x;
            Place(new Vector3(-7f, 4.5f, 46.55f), new Vector3(12f, 0f, 0f));
            for (int i = 0; i < 30; i++) Step(new SlideInput { push = true });   // not leaning in: you just fall
            float freeDrop = wy0 - c.transform.position.y;
            bool wallride = riding > 25 && wallDrop < 0.6f && freeDrop > 1f && wallX > -1f;
            string wallLog = "; wallride: " + riding + "/30 steps on the wall, dropped " + wallDrop.ToString("F2") + "m (" + freeDrop.ToString("F2") + "m without leaning in), along to x=" + wallX.ToString("F1");
            Report("palace: stand, ride, stairs, wallride", idle < 0.5f && low > 0.2f && rode > 20f && downStairs && wallride,
                   "idle moved " + idle.ToString("F2") + "m; rode " + rode.ToString("F0") + "m (lowest " + low.ToString("F2") + "); from the terrace down the stairs to z="
                   + end.z.ToString("F0") + " y=" + end.y.ToString("F2") + wallLog);
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE palace failures=" + failures);
    }
}
