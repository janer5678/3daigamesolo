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

    // quick to get going: W for a second, Shift for two
    static void Coast()
    {
        Place(PlazaSpot, Vector3.zero);
        for (int i = 0; i < 50; i++) Step(new SlideInput { forward = 1f });
        float run = c.Speed;
        for (int i = 0; i < 100; i++) Step(new SlideInput { push = true });
        float push = c.Speed;
        Report("gets up to speed fast", run > 9f && push > 19f, "W for 1s: " + (run * 3.6f).ToString("F0") + "km/h, then Shift for 2s: " + (push * 3.6f).ToString("F0") + "km/h");

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
}
