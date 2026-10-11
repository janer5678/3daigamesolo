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
        while (c.Speed < 14f && t < 5f) { Step(new SlideInput { push = true }); t += Dt; }
        float board = c.Speed;
        for (int i = 0; i < 75; i++) Step(new SlideInput { forward = 1f });
        float off = c.Speed;
        Report("walk, board, step off", walk > 5f && walk < 6f && board >= 14f && t < 1.5f && off < 6f,
               "walk after 0.5s: " + (walk * 3.6f).ToString("F0") + "km/h; board to " + (board * 3.6f).ToString("F0") + "km/h in " + t.ToString("F2")
               + "s; 1.5s after letting go of Shift: " + (off * 3.6f).ToString("F0") + "km/h");

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
            Step(new SlideInput { push = true });
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
            Step(new SlideInput { push = true });
            t += Dt;
            if (c.transform.position.x > 700f && !c.Grounded) air += Dt;
        }
        bool overFunbox = c.transform.position.x >= 714f;

        Place(new Vector3(838f, y, 5f), new Vector3(13f, 0f, 0f));
        float y0 = c.transform.position.y, peak = y0, wall = 0f;
        t = 0f;
        while (t < 4f && !(c.Velocity.x < -4f && c.transform.position.y < y0 + 0.3f))
        {
            Step(new SlideInput { push = true });
            t += Dt;
            peak = Mathf.Max(peak, c.transform.position.y);
            if (c.Locked && c.transform.position.y > y0 + 1f) wall += Dt;
        }
        bool back = c.Velocity.x < -4f;
        Report("board locks onto ramps", overFunbox && air < 0.15f && peak - y0 > 2.5f && back && wall > 0.1f,
               "over the funbox at 58km/h: " + air.ToString("F2") + "s off the ground; up the end wall " + (peak - y0).ToString("F1")
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
            Step(new SlideInput { push = true });
            t += Dt;
            if (c.Grinding) { if (caughtAt < 0f) caughtAt = t; lastX = c.transform.position.x; }
            else if (caughtAt >= 0f) break;
        }
        bool rodeToEnd = caughtAt >= 0f && lastX > 699f;

        drop(686f);
        for (int i = 0; i < 25 && !c.Grinding; i++) Step(new SlideInput { push = true });
        for (int i = 0; i < 10; i++) Step(new SlideInput { push = true });
        Step(new SlideInput { push = true, jump = true });
        bool jumpedOff = !c.Grinding && c.Velocity.y > 3f;

        drop(686f);
        for (int i = 0; i < 25 && !c.Grinding; i++) Step(new SlideInput { push = true });
        bool wasOn = c.Grinding;
        Step(new SlideInput());
        bool steppedOff = wasOn && !c.Grinding;

        // coming at it from an angle, or straight across, it still snaps on and carries you along it
        string angles = "";
        bool allAngles = true;
        foreach (float a in new[] { 20f, 45f, 90f })
        {
            drop(690f);
            Vector3 p = new Vector3(690f, y + 0.5f + 0.4f + 0.5f, 18.15f + 0.6f);
            rb.position = p; c.transform.position = p;
            rb.linearVelocity = Quaternion.Euler(0f, 90f + a, 0f) * Vector3.forward * 8f;   // heading across towards the rail
            Physics.SyncTransforms();
            bool caught = false;
            for (int i = 0; i < 40 && !caught; i++) { Step(new SlideInput { push = true }); caught = c.Grinding; }
            bool slide = c.Boardslide;
            for (int i = 0; i < 10 && c.Grinding; i++) Step(new SlideInput { push = true });
            bool along = caught && slide == (a > 35f) && Mathf.Abs(c.transform.position.z - 18f) < 0.05f && Mathf.Abs(c.Velocity.x) > 3f;
            allAngles &= along;
            angles += " " + a.ToString("F0") + " degrees: " + (along ? "snapped on" + (slide ? " (boardslide)" : " (on the trucks)") + ", grinding at " + (Mathf.Abs(c.Velocity.x) * 3.6f).ToString("F0") + "km/h" : "missed") + ";";
        }

        Report("board locks onto rails", rodeToEnd && jumpedOff && steppedOff && allAngles,
               (caughtAt >= 0f ? "locked on after " + caughtAt.ToString("F2") + "s and ground to x=" + lastX.ToString("F1") + " (rail ends at 700)" : "never locked on")
               + "; Space " + (jumpedOff ? "jumps off" : "didn't jump off") + "; letting go of Shift " + (steppedOff ? "steps off" : "didn't step off") + ";" + angles);
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
        Step(new SlideInput { push = true, jump = true });
        while (!c.Grounded && hang < 3f || hang < 0.1f) { Step(new SlideInput { push = true }); hang += Dt; boardPeak = Mathf.Max(boardPeak, c.transform.position.y); }
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
}
