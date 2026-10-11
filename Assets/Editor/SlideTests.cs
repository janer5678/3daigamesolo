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

    static void Coast()
    {
        c.Respawn();
        Physics.SyncTransforms();
        Vector3 from = c.transform.position;
        for (int i = 0; i < 250; i++) Step(new SlideInput());
        float moved = Vector3.Distance(from, c.transform.position);
        Report("slides downhill on its own", moved > 5f, "moved " + moved.ToString("F1") + "m in 5s, speed " + (c.Speed * 3.6f).ToString("F0") + "km/h");
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

    // on the flat plaza with nothing pressed you keep sliding a long way - but not forever
    static void StopsOnTheFlat()
    {
        c.Respawn();
        var rb = c.GetComponent<Rigidbody>();
        rb.position = new Vector3(660f, SkateWorldBuilder.Base(700f) + 0.5f, 32f);
        c.transform.position = rb.position;
        rb.linearVelocity = new Vector3(10f, 0f, 0f);
        Physics.SyncTransforms();
        float x0 = rb.position.x;
        for (int i = 0; i < 300; i++) Step(new SlideInput());
        Report("glides on the flat", c.Speed > 3f, "after 6s: slid " + (rb.position.x - x0).ToString("F1") + "m, speed " + (c.Speed * 3.6f).ToString("F0") + "km/h");
    }
}
