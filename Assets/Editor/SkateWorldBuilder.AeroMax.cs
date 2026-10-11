using System.Collections.Generic;
using UnityEngine;

// Aero, turned up: the fast board (it beams up hills and ramps), a crazy amount of stuff to bounce off
// (pinball orbs, giant app icons, jelly mushrooms, trampolines, hundreds of bubbles), giant swoosh ramps
// that throw you into the sky with rings waiting up there, and digital maximalism everywhere (a glowing
// grid on everything, floating glass windows, chrome ribbons, spinning globes, rainbows, sparkles, more
// fish).
public static partial class SkateWorldBuilder
{
    public static readonly List<(Vector3 foot, Vector3 dir, float height)> aeroSwooshes = new List<(Vector3, Vector3, float)>();
    public static readonly List<Vector3> aeroBumpers = new List<Vector3>();

    static readonly Color32[] AeroVivid = {
        new Color32(40, 150, 255, 255), new Color32(80, 230, 90, 255), new Color32(255, 200, 40, 255), new Color32(255, 80, 150, 255),
        new Color32(150, 90, 255, 255), new Color32(40, 220, 220, 255), new Color32(255, 120, 40, 255), new Color32(240, 245, 255, 255) };

    // the board in this level: fast, a big push forward, Shift for flat out, beams up hills, jumps high
    static void AeroMovement(SlideController p)
    {
        p.boardSpeed = 18f; p.boardSprintSpeed = 36f; p.boardAccel = 55f;
        p.boardCoastDecel = 1.5f; p.boardRollDecel = 0.6f; p.maxSpeed = 70f;
        p.rampPop = 7f; p.uphillAssist = 0.85f; p.airAccel = 90f;
        p.jumpSpeed = 15f; p.jumpHoldMax = 0.45f; p.jumpHoldLift = 0.55f;
        p.fov = 75f; p.fovAtSpeed = 100f;
    }

    // somewhere on the hills that's not the middle of the main run, the start, or the city's buildings
    static bool AeroClear(float x, float z)
    {
        if (Mathf.Abs(x) < 5f) return false;                                  // the line straight down the valley
        if (Mathf.Abs(x) < 9f && z > 0f && z < 150f) return false;            // the halfpipe's bottom
        if (new Vector2(x, z + 130f).magnitude < 16f) return false;           // the start
        if (z > AeroCityZ0 - 5f && Mathf.Abs(x) < 90f) return false;          // the city has its own
        return Mathf.Abs(x) < AeroX - 8f && z > AeroZ0 + 8f && z < AeroZ1 - 8f;
    }

    // ---- things to bounce off ---------------------------------------------------------------------------

    static void AeroBounceStuff()
    {
        aeroBumpers.Clear();
        var orbs = new SkateMeshKit();
        var icons = new SkateMeshKit();
        var shrooms = new SkateMeshKit();
        var rnd = new System.Random(31);
        System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
        int placed = 0;
        // pinball orbs sitting in the grass: hit one from any side and it throws you
        for (int n = 0; n < 400 && placed < 110; n++)
        {
            float x = R(-185f, 185f), z = R(-240f, 290f);
            if (!AeroClear(x, z)) continue;
            float r = R(1.4f, 3.6f);
            Vector3 c = AeroAt(x, z) + Vector3.up * r * 0.7f;
            Color32 col = AeroVivid[rnd.Next(AeroVivid.Length)];
            orbs.Ellipsoid(c, Vector3.one * r, Quaternion.identity, 16, 10, col);
            orbs.Ellipsoid(c + new Vector3(-r * 0.35f, r * 0.45f, -r * 0.3f), new Vector3(r * 0.32f, r * 0.18f, r * 0.32f), Quaternion.identity, 8, 5, new Color32(255, 255, 255, 255));
            var sc = new GameObject("Orb").AddComponent<SphereCollider>();
            sc.transform.SetParent(aeroRoot, false); sc.transform.position = c; sc.radius = r;
            var bm = sc.gameObject.AddComponent<Bumper>(); bm.strength = 22f; bm.lift = 11f;
            aeroBumpers.Add(c);
            placed++;
        }
        // giant glossy app icons: rounded-looking blocks with a glyph on top; they throw you too
        placed = 0;
        for (int n = 0; n < 400 && placed < 80; n++)
        {
            float x = R(-185f, 185f), z = R(-240f, 290f);
            if (!AeroClear(x, z)) continue;
            float s = R(2.5f, 5f);
            Vector3 c = AeroAt(x, z) + Vector3.up * (s * 0.5f - 0.3f);
            Quaternion q = Quaternion.Euler(0f, R(0f, 90f), 0f);
            Color32 col = AeroVivid[rnd.Next(AeroVivid.Length)];
            Color32 dark = new Color32((byte)(col.r * 0.6f), (byte)(col.g * 0.6f), (byte)(col.b * 0.7f), 255);
            icons.Box(c + Vector3.down * s * 0.2f, new Vector3(s, s * 0.6f, s), q, dark);
            icons.Box(c + Vector3.up * s * 0.2f, new Vector3(s * 0.98f, s * 0.42f, s * 0.98f), q, col);
            icons.Box(c + Vector3.up * (s * 0.42f), new Vector3(s * 0.5f, 0.06f, s * 0.5f), q, new Color32(255, 255, 255, 255));
            icons.Ellipsoid(c + Vector3.up * (s * 0.45f), new Vector3(s * 0.15f, 0.05f, s * 0.15f), q, 10, 4, col);
            var bc = new GameObject("App icon").AddComponent<BoxCollider>();
            bc.transform.SetParent(aeroRoot, false); bc.transform.SetPositionAndRotation(c, q); bc.size = Vector3.one * s;
            var bm = bc.gameObject.AddComponent<Bumper>(); bm.strength = 18f; bm.lift = 16f;
            aeroBumpers.Add(c);
            placed++;
        }
        // jelly mushrooms: land on a cap and it bounces you high
        placed = 0;
        for (int n = 0; n < 300 && placed < 45; n++)
        {
            float x = R(-185f, 185f), z = R(-240f, 290f);
            if (!AeroClear(x, z)) continue;
            float h = R(2f, 5f), r = R(1.8f, 3.2f);
            Vector3 g = AeroAt(x, z), cap = g + Vector3.up * h;
            shrooms.Cylinder(g + Vector3.down * 0.3f, cap, r * 0.3f, 10, new Color32(250, 250, 255, 255));
            Color32 col = rnd.NextDouble() < 0.5 ? new Color32(255, 70, 110, 255) : new Color32(120, 110, 255, 255);
            shrooms.Ellipsoid(cap, new Vector3(r, r * 0.55f, r), Quaternion.identity, 16, 8, col);
            for (int d = 0; d < 5; d++)
            {
                float a = d * 1.26f + R(0f, 0.5f);
                shrooms.Ellipsoid(cap + new Vector3(Mathf.Cos(a) * r * 0.55f, r * 0.42f, Mathf.Sin(a) * r * 0.55f), new Vector3(r * 0.16f, r * 0.08f, r * 0.16f), Quaternion.identity, 8, 4, new Color32(255, 255, 255, 255));
            }
            var sc = new GameObject("Mushroom").AddComponent<SphereCollider>();
            sc.transform.SetParent(aeroRoot, false); sc.transform.position = cap; sc.radius = r * 0.55f;
            sc.gameObject.AddComponent<Bouncer>().speed = 27f;
            var stem = new GameObject("Stem").AddComponent<CapsuleCollider>();
            stem.transform.SetParent(aeroRoot, false); stem.transform.position = (g + cap) / 2f; stem.radius = r * 0.3f; stem.height = h;
            placed++;
        }
        // trampolines set in the grass
        placed = 0;
        for (int n = 0; n < 300 && placed < 40; n++)
        {
            float x = R(-185f, 185f), z = R(-240f, 290f);
            if (!AeroClear(x, z)) continue;
            Vector3 g = AeroAt(x, z);
            if (Vector3.Angle(GroundNormal(x, z), Vector3.up) > 12f) continue;
            shrooms.Cylinder(g + Vector3.down * 0.3f, g + Vector3.up * 0.12f, 1.6f, 16, new Color32(230, 240, 255, 255));
            shrooms.Cylinder(g + Vector3.up * 0.12f, g + Vector3.up * 0.14f, 1.35f, 16, AeroVivid[rnd.Next(AeroVivid.Length)]);
            var col = new GameObject("Trampoline").AddComponent<BoxCollider>();
            col.transform.SetParent(aeroRoot, false); col.transform.position = g + Vector3.up * 0.04f; col.size = new Vector3(2.8f, 0.2f, 2.8f);
            col.gameObject.AddComponent<Bouncer>().speed = 30f;
            placed++;
        }
        foreach (var (k, name) in new[] { (orbs, "Orbs"), (icons, "App icons"), (shrooms, "Mushrooms and trampolines") })
        {
            var mesh = k.Build(name);
            SaveFeatureMesh(mesh);
            MeshObject(name, aeroRoot, mesh, aeroGlossy, outlineMat);
        }
    }

    static Vector3 GroundNormal(float x, float z)
    {
        float e = 0.5f;
        return new Vector3(AeroH(x - e, z) - AeroH(x + e, z), 2f * e, AeroH(x, z - e) - AeroH(x, z + e)).normalized;
    }

    // ---- giant swoosh ramps that beam you into the sky ------------------------------------------------------

    static void AeroSwooshes()
    {
        aeroSwooshes.Clear();
        var spots = new[] { (-40f, -60f, 0f, 30f), (45f, -20f, 20f, 34f), (-55f, 120f, -15f, 38f), (60f, 160f, 10f, 30f), (-25f, 230f, 0f, 42f),
                            (35f, 255f, -10f, 32f), (-110f, 20f, 40f, 34f), (110f, 90f, -40f, 36f), (-90f, 200f, 25f, 30f), (100f, 230f, -20f, 40f) };
        var rings = new SkateMeshKit();
        foreach (var (x, z, yaw, radius) in spots)
        {
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float len = radius * Mathf.Sin(58f * Mathf.Deg2Rad), height = radius * (1f - Mathf.Cos(58f * Mathf.Deg2Rad));
            // sit it on the lowest ground under it
            float low = float.MaxValue;
            for (float t = 0f; t <= len; t += 2f) low = Mathf.Min(low, AeroH(x + dir.x * t, z + dir.z * t));
            Vector3 foot = new Vector3(x, low - 0.15f, z);
            var oldTop = RampTop;
            RampTop = new Color32(245, 250, 255, 255);
            Kicker("Swoosh ramp", Matrix4x4.TRS(foot, Quaternion.LookRotation(dir), Vector3.one), radius, 58f, 7f, new Color32(60, 160, 255, 255));
            RampTop = oldTop;
            aeroSwooshes.Add((foot, dir, height));
            // a ring up where it throws you, to throw you further
            Vector3 lip = foot + dir * len + Vector3.up * height;
            Vector3 fly = (dir * Mathf.Cos(58f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(58f * Mathf.Deg2Rad)).normalized;
            AeroRing(rings, lip + fly * 16f + Vector3.down * 3f, Quaternion.LookRotation((dir + Vector3.up * 0.4f).normalized), 3.2f, 40f);
        }
        var mesh = rings.Build("Swoosh rings");
        SaveFeatureMesh(mesh);
        MeshObject("Swoosh rings", aeroRoot, mesh, aeroGlossy, null);
    }

    // ---- digital maximalism -----------------------------------------------------------------------------------

    static void AeroMaximal()
    {
        var rnd = new System.Random(47);
        System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
        // floating glass windows (a title bar, three buttons, lines of content), drifting and turning
        var windows = new List<Mesh>();
        for (int v = 0; v < 3; v++)
        {
            var k = new SkateMeshKit();
            float w = 6f + v * 2f, h = 4f + v;
            Color32 bar = new[] { new Color32(40, 120, 240, 255), new Color32(60, 190, 90, 255), new Color32(30, 170, 230, 255) }[v];
            k.Box(Vector3.zero, new Vector3(w, h, 0.15f), Quaternion.identity, new Color32(225, 240, 255, 255));
            k.Box(new Vector3(0f, h * 0.5f - 0.35f, 0f), new Vector3(w + 0.1f, 0.7f, 0.25f), Quaternion.identity, bar);
            for (int b = 0; b < 3; b++)
                k.Ellipsoid(new Vector3(w * 0.5f - 0.4f - b * 0.55f, h * 0.5f - 0.35f, -0.15f), Vector3.one * 0.2f, Quaternion.identity, 8, 5,
                            new[] { new Color32(255, 80, 80, 255), new Color32(255, 200, 40, 255), new Color32(80, 220, 90, 255) }[b]);
            for (int l = 0; l < 4; l++)
                k.Box(new Vector3(-w * 0.1f, h * 0.5f - 1.3f - l * 0.7f, -0.1f), new Vector3(w * (0.7f - l * 0.1f), 0.25f, 0.05f), Quaternion.identity, new Color32(150, 200, 255, 255));
            var m = k.Build("Window " + v);
            SaveFeatureMesh(m);
            windows.Add(m);
        }
        for (int i = 0; i < 45; i++)
        {
            float x = R(-170f, 170f), z = R(-200f, 440f);
            var go = MeshObject("Window", aeroRoot, windows[i % 3], aeroGlass, outlineMat);
            go.transform.SetPositionAndRotation(AeroAt(x, z) + Vector3.up * R(14f, 55f), Quaternion.Euler(R(-10f, 10f), R(0f, 360f), R(-8f, 8f)));
            var sp = go.AddComponent<Spin>(); sp.degreesPerSecond = R(-12f, 12f); sp.bob = R(1f, 3f); sp.bobSpeed = R(0.3f, 0.7f);
        }
        // spinning glossy globes
        var gk = new SkateMeshKit();
        gk.Ellipsoid(Vector3.zero, Vector3.one * 3f, Quaternion.identity, 20, 12, new Color32(50, 140, 255, 255));
        for (int p = 0; p < 9; p++)
        {
            Vector3 d = Quaternion.Euler(R(-60f, 60f), p * 40f, 0f) * Vector3.forward;
            gk.Ellipsoid(d * 2.75f, new Vector3(1.1f, 0.7f, 1.1f), Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f), 10, 5, new Color32(90, 220, 80, 255));
        }
        gk.Cylinder(new Vector3(0f, -3.6f, 0f), new Vector3(0f, 3.6f, 0f), 0.08f, 6, new Color32(240, 240, 250, 255));
        var gm = gk.Build("Globe");
        SaveFeatureMesh(gm);
        for (int i = 0; i < 18; i++)
        {
            var go = MeshObject("Globe", aeroRoot, gm, aeroGlossy, null);
            float x = R(-170f, 170f), z = R(-200f, 440f);
            go.transform.position = AeroAt(x, z) + Vector3.up * R(10f, 45f);
            go.transform.localScale = Vector3.one * R(0.6f, 1.6f);
            var sp = go.AddComponent<Spin>(); sp.degreesPerSecond = R(20f, 50f); sp.bob = R(0.5f, 2f);
        }
        // chrome ribbons swooping through the sky
        var rib = new SkateMeshKit();
        for (int i = 0; i < 14; i++)
        {
            Vector3 c = new Vector3(R(-160f, 160f), R(40f, 90f), R(-150f, 420f));
            float yaw = R(0f, 360f), len = R(60f, 120f), amp = R(6f, 16f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Color32 edge = AeroVivid[rnd.Next(AeroVivid.Length)];
            int segs = 40;
            for (int s = 0; s < segs; s++)
            {
                float t0 = s / (float)segs, t1 = (s + 1f) / segs;
                System.Func<float, Vector3> at = t => c + q * new Vector3(Mathf.Sin(t * 6.28f) * amp, Mathf.Sin(t * 3.14f) * amp * 0.6f, (t - 0.5f) * len);
                System.Func<float, Vector3> up = t => q * new Vector3(Mathf.Cos(t * 6.28f), 1f, 0f).normalized;
                float w0 = Mathf.Sin(t0 * 3.14f) * 2.4f, w1 = Mathf.Sin(t1 * 3.14f) * 2.4f;
                Vector3 a0 = at(t0), a1 = at(t1);
                rib.Quad(a0 - up(t0) * w0, a1 - up(t1) * w1, a1 + up(t1) * w1, a0 + up(t0) * w0, Vector3.Cross(a1 - a0, up(t0)), new Color32(245, 248, 255, 255));
                rib.Quad(a0 - up(t0) * w0, a1 - up(t1) * w1, a1 + up(t1) * w1, a0 + up(t0) * w0, -Vector3.Cross(a1 - a0, up(t0)), edge);
            }
        }
        var rm = rib.Build("Ribbons");
        SaveFeatureMesh(rm);
        MeshObject("Ribbons", aeroRoot, rm, aeroGlass, null);
        // rainbows: one over the valley, one behind the city
        var rain = new SkateMeshKit();
        Color32[] bands = { new Color32(255, 70, 70, 255), new Color32(255, 160, 50, 255), new Color32(255, 235, 70, 255), new Color32(80, 220, 100, 255), new Color32(70, 160, 255, 255), new Color32(150, 100, 255, 255) };
        foreach (var (c, r, yaw) in new[] { (new Vector3(0f, -20f, 120f), 150f, 0f), (new Vector3(0f, -10f, 620f), 260f, 0f) })
            for (int b = 0; b < 6; b++)
            {
                var pts = new List<Vector3>();
                for (int s = 0; s <= 48; s++)
                {
                    float a = Mathf.PI * s / 48f;
                    pts.Add(c + Quaternion.Euler(0f, yaw, 0f) * new Vector3(Mathf.Cos(a) * (r - b * 3f), Mathf.Sin(a) * (r - b * 3f), 0f));
                }
                for (int s = 0; s < pts.Count - 1; s++) rain.Cylinder(pts[s], pts[s + 1], 1.6f, 6, bands[b], false);
            }
        var rnm = rain.Build("Rainbows");
        SaveFeatureMesh(rnm);
        MeshObject("Rainbows", aeroRoot, rnm, aeroCloud, null);
        // more fish
        var kinds = new[] { (new Color32(255, 80, 180, 255), new Color32(255, 255, 255, 255), 6f), (new Color32(60, 230, 200, 255), new Color32(30, 60, 160, 255), 7.5f),
                            (new Color32(255, 230, 60, 255), new Color32(30, 30, 40, 255), 5.5f) };
        for (int i = 0; i < 10; i++)
        {
            var (body, stripe, len) = kinds[i % kinds.Length];
            Vector3 c = new Vector3(R(-140f, 140f), 0f, R(-180f, 380f));
            c.y = AeroH(c.x, c.z) + R(8f, 30f);
            float rx = R(20f, 50f), rz = R(20f, 50f);
            var go = new GameObject("Fish", typeof(Rigidbody), typeof(Mover), typeof(BoxCollider), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(aeroRoot, false);
            go.GetComponent<Rigidbody>().isKinematic = true;
            go.GetComponent<MeshFilter>().sharedMesh = FishMesh(body, stripe, len, i % 3 == 2 ? 2 : 0);
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { aeroGlossy, outlineMat };
            var bc = go.GetComponent<BoxCollider>(); bc.size = new Vector3(len * 0.22f, len * 0.36f, len * 0.75f);
            var path = new List<Vector3>();
            for (int k = 0; k < 20; k++)
            {
                float a = k * Mathf.PI * 2f / 20f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a * 3f) * 4f, Mathf.Sin(a) * rz);
                p.y = Mathf.Max(p.y, AeroH(p.x, p.z) + 6f);
                path.Add(p);
            }
            var m = go.GetComponent<Mover>();
            m.path = path.ToArray(); m.loop = true; m.speed = R(6f, 10f); m.startAt = R(0f, 100f);
        }
    }

    // sparkles drifting round you wherever you go
    static void AeroSparkles(Transform cam)
    {
        var go = new GameObject("Sparkles", typeof(ParticleSystem));
        go.transform.SetParent(cam, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 1f, 0.9f), new Color(1f, 0.75f, 1f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        var em = ps.emission; em.rateOverTime = 150f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(70f, 30f, 70f);
        var fade = ps.colorOverLifetime; fade.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        fade.color = grad;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("AeroSparkle", "Sprites/Default");
    }
}
