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

        Place(PlazaSpot, new Vector3(20f, 0f, 0f));
        float t = 0f;
        while (c.Speed > 0.3f && t < 10f) { Step(new SlideInput()); t += Dt; }

        Report("run, stand, stop", run > c.walkSpeed - 0.5f && run < c.walkSpeed + 0.5f && moved < 1f && t < 2.5f,
               "running after 0.5s: " + (run * 3.6f).ToString("F0") + "km/h; moved " + moved.ToString("F2") + "m in 20s with nothing pressed; from 72km/h stopped in " + t.ToString("F2") + "s");
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
            for (int i = 0; i < 150; i++) { Step(new SlideInput { forward = 1f }); stairLow = Mathf.Min(stairLow, c.transform.position.y); }
            Vector3 end = c.transform.position;
            bool downStairs = end.z < 20f && end.y > 0f && end.y < 1f && stairLow > 0f;

            Report("palace: stand, run, stairs", idle < 0.5f && low > 0.2f && ran > 20f && downStairs,
                   "idle moved " + idle.ToString("F2") + "m; ran " + ran.ToString("F0") + "m (lowest " + low.ToString("F2") + "); from the terrace down the stairs to z="
                   + end.z.ToString("F0") + " y=" + end.y.ToString("F2"));
        }
        finally
        {
            Physics.simulationMode = mode;
            c.Respawn();
        }
        Debug.Log("SLIDE_TESTS_DONE palace failures=" + failures);
    }
}
