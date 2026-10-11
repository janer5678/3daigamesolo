using System.Text;
using UnityEditor;
using UnityEngine;

// Test rides, run by the builder from the command line. A little bot drives the real
// SkateController in the built scene with scripted input and reports what happened, so the
// level and the handling can be checked without anyone pressing Play.
public static class SkateTests
{
    static SkateController c;
    static int failures;

    [MenuItem("Skate/Run test rides")]
    public static void RunAll()
    {
        c = Object.FindFirstObjectByType<SkateController>();
        SkateRail.All.Clear();
        SkateRail.All.AddRange(Object.FindObjectsByType<SkateRail>(FindObjectsSortMode.None));
        Physics.SyncTransforms();
        failures = 0;

        CoastFromStart();
        RideTheRun();
        OllieKickflip();
        Spin360();
        KickerAir();
        Grind();
        QuarterPipe();
        Halfpipe();
        ManualHold();
        Debug.Log("SKATE_TESTS_DONE failures=" + failures);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (!ok) failures++;
        Debug.Log("SKATE_TEST " + (ok ? "PASS " : "FAIL ") + name + " | " + detail);
    }

    static float PlazaY { get { return SkateWorldBuilder.Base(700f); } }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

    static float SteerTo(Vector3 target)
    {
        if (c.mode != SkateController.Mode.Ground) return 0f;
        Vector3 p = c.SimPosition;
        Vector3 heading = Flat(c.Velocity.sqrMagnitude > 1f ? c.Velocity : c.Forward);
        float ang = Vector3.SignedAngle(heading, Flat(target - p), Vector3.up);
        return Mathf.Clamp(ang / 12f, -1f, 1f);
    }

    static string lastLost = "";
    static int Bails()
    {
        return 0;
    }

    // all the way down from the start, pushing, steering down the middle, over whatever's there
    static void RideTheRun()
    {
        c.Respawn(true);
        int bails = 0, airs = 0;
        float maxAir = 0f, maxSpeed = 0f, t = 0f;
        var reasons = new StringBuilder();
        SkateController.Mode was = c.mode;
        float lastLostTime = -1f;
        while (t < 120f && c.SimPosition.x < 660f)
        {
            float x = c.SimPosition.x;
            var input = new SkateInput { lean = 1f, steer = SteerTo(SkateWorldBuilder.OnPath(x + 14f, 0f)) };
            c.Simulate(input);
            t += SkateController.Step;
            if (c.mode == SkateController.Mode.Air && was != SkateController.Mode.Air) airs++;
            if (c.mode == SkateController.Mode.Bail && was != SkateController.Mode.Bail)
            {
                bails++;
                reasons.Append(c.callout).Append(" @x=").Append(x.ToString("F0")).Append("; ");
            }
            if (c.combo.lastResultLost && c.combo.lastResultTime != lastLostTime) lastLostTime = c.combo.lastResultTime;
            if (c.mode == SkateController.Mode.Air) maxAir = Mathf.Max(maxAir, c.AirTime);
            maxSpeed = Mathf.Max(maxSpeed, c.Speed);
            was = c.mode;
        }
        Report("ride the run", bails == 0 && c.SimPosition.x >= 650f,
            "reached x=" + c.SimPosition.x.ToString("F0") + " in " + t.ToString("F1") + "s, airs=" + airs + " longest air=" + maxAir.ToString("F2")
            + "s, top speed=" + (maxSpeed * 3.6f).ToString("F0") + "km/h, bails=" + bails + " " + reasons + " score=" + c.combo.totalScore);
    }

    // nothing pressed at all: the slope should carry you away from the start
    static void CoastFromStart()
    {
        c.Respawn(true);
        Vector3 from = c.SimPosition;
        Run(5f, () => new SkateInput());
        float moved = Vector3.Distance(from, c.SimPosition);
        Report("coast from a standstill", moved > 5f && c.Speed > 2f, "moved " + moved.ToString("F1") + "m, speed " + (c.Speed * 3.6f).ToString("F0") + "km/h " + Summary());
    }

    static void Run(float seconds, System.Func<SkateInput> input)
    {
        int steps = Mathf.RoundToInt(seconds / SkateController.Step);
        for (int i = 0; i < steps; i++) c.Simulate(input());
    }

    static string Summary()
    {
        return "mode=" + c.mode + " combo=[" + c.combo.Describe(6) + "] last=[" + c.combo.lastResult + (c.combo.lastResultLost ? " LOST" : " +" + c.combo.lastResultValue) + "] landing=" + c.lastLanding;
    }

    static void OllieKickflip()
    {
        c.Respawn(true);
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(656f, PlazaY, 32f), Vector3.right, 8f);
        Run(0.45f, () => new SkateInput { jump = true });
        c.Simulate(new SkateInput());                       // let go: ollie
        float peak = c.SimPosition.y;
        c.Simulate(new SkateInput { flipA = true });          // kickflip
        float air = 0f;
        while (c.mode == SkateController.Mode.Air && air < 3f) { c.Simulate(new SkateInput()); air += SkateController.Step; peak = Mathf.Max(peak, c.SimPosition.y); }
        Run(0.6f, () => new SkateInput());
        bool ok = !c.combo.lastResultLost && c.combo.lastResult.Contains("Kickflip");
        Report("ollie + kickflip on flat", ok, "air=" + air.ToString("F2") + "s height=" + (peak - PlazaY).ToString("F2") + "m " + Summary());
    }

    static void Spin360()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(656f, PlazaY, 32f), Vector3.right, 8f);
        Run(0.45f, () => new SkateInput { jump = true });
        c.Simulate(new SkateInput());
        Vector3 f0 = Flat(c.Forward);
        float turned = 0f;
        Vector3 prev = f0;
        while (c.mode == SkateController.Mode.Air)
        {
            c.Simulate(new SkateInput { steer = turned < 330f ? 1f : 0f });
            Vector3 f = Flat(c.Forward);
            turned += Mathf.Abs(Vector3.SignedAngle(prev, f, Vector3.up));
            prev = f;
        }
        Run(0.6f, () => new SkateInput());
        Report("ollie 360", !c.combo.lastResultLost && c.combo.lastResult.Contains("360"), "turned=" + turned.ToString("F0") + " " + Summary());
    }

    static void KickerAir()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        Vector3 from = SkateWorldBuilder.OnPath(200f, 0f);
        c.Teleport(from, SkateWorldBuilder.PathDir(200f), 13f);
        float maxAir = 0f, t = 0f;
        while (t < 6f)
        {
            float x = c.SimPosition.x;
            var inp = new SkateInput { steer = SteerTo(SkateWorldBuilder.OnPath(x + 10f, 0f)), jump = x > 222f && x < 234f && c.mode == SkateController.Mode.Ground };
            if (c.mode == SkateController.Mode.Air) { maxAir = Mathf.Max(maxAir, c.AirTime); inp.flipB = c.AirTime > 0.3f && c.AirTime < 0.32f; }
            c.Simulate(inp);
            t += SkateController.Step;
        }
        Report("big kicker + heelflip", maxAir > 0.8f && !c.combo.lastResultLost && c.mode != SkateController.Mode.Bail, "longest air=" + maxAir.ToString("F2") + "s " + Summary());
    }

    static void Grind()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(678f, PlazaY, 18f), Vector3.right, 8f);
        Run(0.2f, () => new SkateInput { jump = true });
        c.Simulate(new SkateInput());
        bool grinded = false; float grindTime = 0f; string name = "";
        float t = 0f;
        while (t < 4f)
        {
            var inp = new SkateInput();
            if (c.mode == SkateController.Mode.Grind)
            {
                grinded = true; grindTime += SkateController.Step; name = c.GrindName;
                inp.steer = -Mathf.Clamp(c.Balance * 4f, -1f, 1f);
            }
            c.Simulate(inp);
            t += SkateController.Step;
        }
        Report("ollie onto flat rail, balance it", grinded && !c.combo.lastResultLost, "grind=" + name + " for " + grindTime.ToString("F2") + "s " + Summary());

        // the same with nobody balancing: should eventually fall off a long one, not instantly
        var longRail = SkateRail.All.Find(r => r.displayName == "Long rail");
        if (longRail != null)
        {
            c.combo.Clear();
            Vector3 a = longRail.points[0], b = longRail.points[1];
            c.Launch(a - (b - a).normalized * 1.5f + Vector3.up * 0.4f, (b - a).normalized * 10f);
            float fallAfter = -1f, gt = 0f;
            for (int i = 0; i < 1200; i++)
            {
                c.Simulate(new SkateInput());
                if (c.mode == SkateController.Mode.Grind) gt += SkateController.Step;
                if (c.mode == SkateController.Mode.Bail) { fallAfter = gt; break; }
                if (gt > 0f && c.mode != SkateController.Mode.Grind) break;
            }
            Report("unbalanced grind lasts a moment", gt > 0.6f, "grinded " + gt.ToString("F2") + "s, fell=" + (fallAfter >= 0f) + " " + Summary());
        }
    }

    static void QuarterPipe()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(826f, PlazaY, 5f), Vector3.right, 14f);
        float t = 0f, peak = 0f; bool vert = false;
        while (t < 7f)
        {
            c.Simulate(new SkateInput());
            if (c.VertAir) vert = true;
            peak = Mathf.Max(peak, c.SimPosition.y - PlazaY);
            t += SkateController.Step;
        }
        bool back = c.Velocity.x < -3f;
        Report("end wall quarterpipe", vert && back && c.mode != SkateController.Mode.Bail && !c.combo.lastResultLost,
            "peak=" + peak.ToString("F2") + "m vertAir=" + vert + " vel=" + c.Velocity.ToString("F1") + " fakie=" + c.Fakie + " " + Summary());
    }

    static void Halfpipe()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(790f, PlazaY, 27f), Vector3.forward, 9f);
        int airs = 0, bails = 0; float t = 0f;
        var was = c.mode;
        float lastBank = 0f;
        while (t < 10f)
        {
            // pump: W on the way down each wall
            var inp = new SkateInput();
            if (c.mode == SkateController.Mode.Ground && Vector3.Dot(c.Velocity, Vector3.down) > 0f) inp.lean = 1f;
            if (c.mode == SkateController.Mode.Air && c.AirTime > 0.1f && c.AirTime < 0.4f) inp.style = true;
            c.Simulate(inp);
            if (c.mode == SkateController.Mode.Air && was != SkateController.Mode.Air) airs++;
            if (c.mode == SkateController.Mode.Bail && was != SkateController.Mode.Bail) bails++;
            was = c.mode;
            t += SkateController.Step;
            lastBank = c.combo.totalScore;
        }
        Report("halfpipe back and forth with grabs", airs >= 2 && bails == 0, "airs=" + airs + " bails=" + bails + " score=" + lastBank + " " + Summary());
    }

    static void ManualHold()
    {
        c.combo.Clear(); c.combo.lastResult = ""; c.combo.lastResultLost = false;
        c.Teleport(new Vector3(656f, PlazaY, 32f), Vector3.right, 6f);
        float held = 0f;
        for (int i = 0; i < 600; i++)
        {
            var inp = new SkateInput { style = true, lean = -Mathf.Clamp(c.Balance * 3f, -1f, 1f) };
            c.Simulate(inp);
            if (!c.Manual) break;
            held += SkateController.Step;
        }
        Report("manual held with balancing", held > 4f, "held " + held.ToString("F2") + "s " + Summary());

        c.combo.Clear();
        c.Teleport(new Vector3(656f, PlazaY, 32f), Vector3.right, 6f);
        float alone = 0f;
        for (int i = 0; i < 600; i++)
        {
            c.Simulate(new SkateInput { style = true });
            if (!c.Manual && i > 2) break;
            alone += SkateController.Step;
        }
        Report("manual with no balancing falls", alone < 4.9f && alone > 0.5f, "lasted " + alone.ToString("F2") + "s");
    }
}
