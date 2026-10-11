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

    // a modest hop, and you can still change direction mid-air
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
        Report("jump height and air control", height > 0.6f && height < 1.0f && turned > 30f,
               "jump height " + height.ToString("F2") + "m; holding D through a " + air.ToString("F2") + "s jump turned you " + turned.ToString("F0") + " degrees");
    }
}
