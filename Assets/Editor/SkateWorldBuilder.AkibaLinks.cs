using System.Collections.Generic;
using UnityEngine;

// Akiba's ways around. Every surface you can stand on (the street, the walkways, every roof, every
// floating island, the stops) is a node; every way from one to another (an escalator, a lift, a launch
// pad, a vent, a trampoline, a rail, a jump, a flying bus, a flying car) is a link, with a test ride that
// says how to do it. The builder makes sure every node can be reached from the street, and gives every
// node ways on (up, and across) besides just dropping off; the test runs (SlideTests.RunAkiba) ride every
// link and fail if any of them doesn't get you there.
public static partial class SkateWorldBuilder
{
    public const int NGround = 0, NWalk = 1, NRoof = 2, NIsland = 3, NStop = 4;

    public class AkNode
    {
        public string name;
        public int kind;
        public float top;
        public readonly List<Rect> areas = new List<Rect>();   // where you can land on it (x, z)
        public readonly List<Rect> pads = new List<Rect>();    // where things can be put on it (not on moving walkways)
        public int ups, exits;                                 // links out of it going up, and in all

        public Vector3 Centre { get { var r = areas[0]; return new Vector3(r.center.x, top, r.center.y); } }

        public bool Contains(Vector2 p, float inset = 0f)
        {
            foreach (var r in areas)
                if (p.x > r.xMin + inset && p.x < r.xMax - inset && p.y > r.yMin + inset && p.y < r.yMax - inset) return true;
            return false;
        }
    }

    public class AkLink
    {
        public string kind;
        public int from, to;
        public Vector3 start;                  // where the test ride starts (feet)
        public float yaw;
        public bool forward = true, sprint;
        public float jumpAt = -1f;             // jump once you've come this far from the start (along the ground)
        public bool letGo;                     // let go of everything once you're in the air (a launch pad's arc does the rest)
        public float steerAbove = float.NaN;   // once you're up this high, ride towards steerTo
        public Vector3 steerTo;
        public Mover vehicle;                  // a lift or a flying bus: ride it till it's stopped at alightAt, then ride to steerTo
        public Vector3 alightAt;
        public float vehicleFrom = -1f;        // ...starting from this far along its route
        public HoverCar car;                   // fly it to steerTo and jump off over it
        public float timeout = 14f;
    }

    public static readonly List<AkNode> akNodes = new List<AkNode>();
    public static readonly List<AkLink> akLinks = new List<AkLink>();
    static readonly List<Vector4> akUsed = new List<Vector4>();   // spots taken by things (pads, vents, ramps, cars): x, y, z, radius
    static readonly List<Vector4> akLand = new List<Vector4>();   // where links land you: kept clear of things
    static readonly List<(Vector3, Vector3)> akRunUps = new List<(Vector3, Vector3)>();   // the run-ups links need: kept clear too
    static SkateRail[] akRails;
    static string akWhy = "";
    static bool No(string why) { akWhy = why; return false; }

    const float AkG = 9.81f * 3.6f, AkDt = 0.02f;   // the player's gravity (SlideController.gravityScale) and the physics step

    static int AkAddNode(string name, int kind, float top, params Rect[] areas)
    {
        var n = new AkNode { name = name, kind = kind, top = top };
        n.areas.AddRange(areas);
        n.pads.AddRange(areas);
        akNodes.Add(n);
        return akNodes.Count - 1;
    }

    static AkLink AkAddLink(string kind, int from, int to)
    {
        var l = new AkLink { kind = kind, from = from, to = to };
        akLinks.Add(l);
        akNodes[from].exits++;
        if (akNodes[to].top > akNodes[from].top + 1f) akNodes[from].ups++;
        return l;
    }

    static bool Linked(int a, int b)
    {
        foreach (var l in akLinks) if (l.from == a && l.to == b) return true;
        return false;
    }

    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }
    static Vector3 FlatDir(Vector3 v) { v.y = 0f; return v.normalized; }
    static float YawOf(Vector3 d) { return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    // the point on n's top nearest p (x, z), `inset` in from its edges (only where things can go, if pads)
    static Vector3 NodePoint(AkNode n, Vector2 p, float inset, bool pads = true)
    {
        Vector3 best = n.Centre; float bd = float.MaxValue;
        foreach (var r in pads ? n.pads : n.areas)
        {
            float x0 = r.xMin + inset, x1 = r.xMax - inset, z0 = r.yMin + inset, z1 = r.yMax - inset;
            if (x0 > x1) x0 = x1 = r.center.x;
            if (z0 > z1) z0 = z1 = r.center.y;
            var q = new Vector2(Mathf.Clamp(p.x, x0, x1), Mathf.Clamp(p.y, z0, z1));
            float d = (q - p).sqrMagnitude;
            if (d < bd) { bd = d; best = new Vector3(q.x, n.top, q.y); }
        }
        return best;
    }

    // the smallest gap between two nodes, seen from above
    static float Gap(AkNode a, AkNode b)
    {
        float best = float.MaxValue;
        foreach (var r in a.areas)
            foreach (var q in b.areas)
            {
                float dx = Mathf.Max(0f, Mathf.Max(r.xMin - q.xMax, q.xMin - r.xMax));
                float dz = Mathf.Max(0f, Mathf.Max(r.yMin - q.yMax, q.yMin - r.yMax));
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
        return best;
    }

    // nothing on this spot (no connector, no prop, no wall) from just above the surface up to head height
    static bool SpotFree(Vector3 p, float r)
    {
        foreach (var u in akUsed)
            if (Mathf.Abs(u.y - p.y) < 3f && new Vector2(u.x - p.x, u.z - p.z).magnitude < u.w + r) return false;
        foreach (var u in akLand)
            if (Mathf.Abs(u.y - p.y) < 3f && new Vector2(u.x - p.x, u.z - p.z).magnitude < u.w + r) return false;
        foreach (var (a, b) in akRunUps)
            if (Mathf.Abs(a.y - p.y) < 3f && SegDist(XZ(p), XZ(a), XZ(b)) < r + 0.9f) return false;
        return !Physics.CheckBox(p + Vector3.up * 1.35f, new Vector3(r, 1.15f, r), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    // a run-up from a to b with nothing on it that would throw you somewhere else (bar what's at b itself)
    static bool RunFree(Vector3 a, Vector3 b)
    {
        foreach (var u in akUsed)
        {
            if (Mathf.Abs(u.y - a.y) > 1.5f) continue;
            Vector2 c = new Vector2(u.x, u.z);
            if (Vector2.Distance(c, XZ(b)) < 0.5f) continue;
            if (SegDist(c, XZ(a), XZ(b)) < u.w + 0.6f) return false;
        }
        return RunClear(a, b);
    }

    static void RunUp(Vector3 a, Vector3 b) { akRunUps.Add((a, b)); }

    // how far you can run up to p, coming along dir, staying on a
    static float RunRoom(AkNode a, Vector3 p, Vector3 dir)
    {
        float room = 0f;
        for (float t = 0.25f; t < 10f; t += 0.25f)
        {
            if (a.Contains(XZ(p - dir * t), 0.6f)) room = t;
            else if (room > 0f || t > 1.5f) break;   // (starting right on the edge, the first bit's outside the inset)
        }
        return room;
    }

    static void Use(Vector3 p, float r) { akUsed.Add(new Vector4(p.x, p.y, p.z, r)); }

    // where you come down on b from this direction: a few metres in from the edge facing you, but no further
    // than the middle (so nothing put near b's edges, like a launch pad, is where you land); false if there's
    // already something there you'd land on
    static bool LandSpot(AkNode b, Vector3 from, out Vector3 land)
    {
        Rect r = b.areas[0];
        float best = float.MaxValue;
        foreach (var a in b.areas) { float d = Vector2.Distance(a.center, XZ(from)); if (d < best) { best = d; r = a; } }
        // on a small piece everything lands in the middle (leaving its edges for pads and rails); on a big one,
        // a few metres in from the edge facing you
        float inset = Mathf.Max(r.width, r.height) < 13f ? Mathf.Min(r.width, r.height) / 2f : Mathf.Min(3f, Mathf.Min(r.width, r.height) / 2f);
        Vector3 want = new Vector3(Mathf.Clamp(from.x, r.xMin + inset, r.xMax - inset), b.top, Mathf.Clamp(from.z, r.yMin + inset, r.yMax - inset));
        // the spot facing you if it's free, otherwise the nearest free one further in
        land = want;
        if (LandFree(want, 1.9f)) return true;
        float bestD = float.MaxValue; bool found = false;
        for (float x = r.xMin + 1.5f; x <= r.xMax - 1.5f; x += 0.75f)
            for (float z = r.yMin + 1.5f; z <= r.yMax - 1.5f; z += 0.75f)
            {
                Vector3 p = new Vector3(x, b.top, z);
                float d = Vector3.Distance(p, want);
                if (d < bestD && LandFree(p, 1.9f)) { bestD = d; land = p; found = true; }
            }
        return found || No("nowhere to land on " + b.name);
    }

    // no launch pad, vent or trampoline (anything that throws you) where you'd come down
    static bool LandFree(Vector3 p, float r)
    {
        foreach (var u in akUsed)
            if (Mathf.Abs(u.y - p.y) < 1.5f && new Vector2(u.x - p.x, u.z - p.z).magnitude < u.w + r) return false;
        if (Physics.CheckBox(p + Vector3.up * 1.1f, new Vector3(r, 0.9f, r), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;
        return !NearRail(p + Vector3.up * 0.6f, r);
    }

    static void Landing(Vector3 p) { akLand.Add(new Vector4(p.x, p.y, p.z, 2.2f)); }

    // a clear run along the ground from a to b (nothing in the way at body height)
    static bool RunClear(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; d.y = 0f;
        if (d.magnitude < 0.1f) return true;
        return !Physics.SphereCast(a + Vector3.up * 1.0f, 0.4f, d.normalized, out _, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
    }

    // ---- the arc a launch pad throws you along -----------------------------------------------------------

    // the speed to throw you from p0 to p1 (player centres), going `over` metres above the higher of the two;
    // stepped just like the game does it (the first step only has Unity's own gravity, the controller adds the
    // rest from the next step on)
    static bool Ballistic(Vector3 p0, Vector3 p1, float over, out Vector3 vel, out List<Vector3> path)
    {
        float apex = Mathf.Max(p0.y, p1.y) + over;
        float vy = Mathf.Sqrt(2f * AkG * (apex - p0.y));
        var ys = new List<float>();
        float y = p0.y, v = vy;
        for (int n = 0; n < 3000; n++)
        {
            v -= (n == 0 ? 9.81f : AkG) * AkDt;
            y += v * AkDt;
            ys.Add(y);
            if (v < 0f && y <= p1.y) break;
        }
        Vector3 flat = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z);
        float vh = flat.magnitude / (ys.Count * AkDt);
        vel = flat.normalized * vh + Vector3.up * vy;
        path = new List<Vector3> { p0 };
        for (int i = 0; i < ys.Count; i++)
            path.Add(new Vector3(p0.x, 0f, p0.z) + flat.normalized * vh * AkDt * (i + 1) + Vector3.up * ys[i]);
        return vh < 30f;
    }

    // nothing in the way along the path (bar its first and last bits), and no rail close enough to grab you
    static bool PathClear(List<Vector3> path, float skipStart, float skipEnd, float radius = 0.5f)
    {
        Vector3 a0 = path[0], a1 = path[path.Count - 1];
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 a = path[i - 1], b = path[i];
            if (Vector3.Distance(b, a0) < skipStart || Vector3.Distance(a, a1) < skipEnd) continue;
            Vector3 d = b - a;
            if (Physics.CheckSphere(b, radius, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.SphereCast(a, radius, d.normalized, out _, d.magnitude, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (NearRail(b, 1.8f)) return false;
        }
        return true;
    }

    static bool NearRail(Vector3 p, float d)
    {
        if (akRails == null) return false;
        foreach (var r in akRails)
        {
            if (r == null || r.points == null || r.points.Length < 2) continue;
            r.Closest(p, out int seg, out float u);
            Vector3 q = Vector3.Lerp(r.points[seg], r.points[seg + 1], u);
            if (Vector3.Distance(p, q) < d + r.catchRadius) return true;
        }
        return false;
    }

    static void RefreshRails() { akRails = featureRoot.GetComponentsInChildren<SkateRail>(); }

    // ---- connectors --------------------------------------------------------------------------------------

    // a launch pad on a, throwing you onto b at `land`; tries higher arcs until one's clear
    static bool AkLaunch(int a, int b, Vector3 pad, Vector3 aim)
    {
        AkNode A = akNodes[a];
        if (pad.y < -100f || !LandSpot(akNodes[b], pad, out Vector3 land)) return false;
        akWhy = "every arc from " + pad.ToString("F0") + " to " + land.ToString("F0") + " is blocked";
        Vector3 toward = FlatDir(land - pad);
        Vector3 p0 = pad - toward * 0.8f + Vector3.up * (0.1f + 0.35f), p1 = land + Vector3.up * 0.35f;
        foreach (var over in new[] { 3f, 6f, 10f, 15f, 22f })
        {
            if (!Ballistic(p0, p1, over, out var vel, out var path)) continue;
            if (!PathClear(path, 1.8f, 2.2f, 0.7f)) continue;
            Vector3 dir = FlatDir(vel);
            // you ride onto it from behind (where you start the test ride)
            Vector3 start = pad - dir * 4.5f;
            if (A.kind != NGround) start = NodePoint(A, XZ(start), 0.6f);
            else start.y = floorAt(start.x, start.z);
            if (!RunFree(start, pad) || Vector3.Distance(XZ(start), XZ(pad)) < 2.5f) { akWhy = "no clear run-up to the pad"; continue; }
            LaunchPadMesh(pad, vel, path);
            Landing(land);
            Use(pad, 1.25f);
            RunUp(start, pad);
            var l = AkAddLink("launch pad", a, b);
            l.start = start; l.yaw = YawOf(pad - start); l.letGo = true;
            return true;
        }
        return false;
    }

    static void LaunchPadMesh(Vector3 pad, Vector3 vel, List<Vector3> path)
    {
        Vector3 dir = FlatDir(vel), side = Rt(dir);
        Color32 rim = new Color32(255, 70, 190, 255), glow = new Color32(255, 160, 230, 255);
        AK("metal").Cylinder(pad, pad + Vector3.up * 0.1f, 1.25f, 10, 2f, new Color32(60, 50, 80, 255));
        var k = AK("white", "e");
        k.Cylinder(pad + Vector3.up * 0.1f, pad + Vector3.up * 0.13f, 1.1f, 10, 2f, rim);
        k.Cylinder(pad + Vector3.up * 0.13f, pad + Vector3.up * 0.15f, 0.8f, 10, 2f, new Color32(40, 20, 60, 255));
        // an arrow pointing where it throws you
        for (int i = 0; i < 2; i++)
        {
            Vector3 c = pad + Vector3.up * 0.17f + dir * (i * 0.55f - 0.2f);
            foreach (var s in new[] { -1f, 1f })
                k.Box(c + side * s * 0.24f, new Vector3(0.14f, 0.03f, 0.62f), Quaternion.LookRotation(dir + side * s * 0.9f), 1f, 1f, glow);
        }
        // a short trail of glowing dots up off it, showing which way it throws you
        for (int i = 2, n = 0; i < path.Count / 3 && n < 6; i += 2, n++)
            k.Box(path[i] + Vector3.down * 0.35f, Vector3.one * (0.2f - n * 0.025f), Quaternion.identity, 1f, 1f, glow);
        var col = AddBox(akCol, "Launch pad", pad + Vector3.up * 0.05f, new Vector3(2.1f, 0.1f, 2.1f), Quaternion.LookRotation(dir));
        var lp = col.gameObject.AddComponent<LaunchPad>();
        lp.velocity = vel; lp.target = path[path.Count - 1];
    }

    // a vent on a, beside b, that blows you up level with b's top
    static bool AkVentLink(int a, int b)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        Vector3 v = A.kind == NGround ? GroundSpotNear(B, 2.6f, 2.0f) : NodePoint(A, XZ(NodePoint(B, XZ(A.Centre), 0f, false)), 2.0f);
        if (v.x == float.MaxValue) return No("no ground spot for a vent by " + B.name);
        Vector3 bp = NodePoint(B, XZ(v), 0f, false);
        float d = Vector3.Distance(XZ(v), XZ(bp));
        if (d < 2.0f || d > 3.6f || B.Contains(XZ(v))) return No("vent not beside " + B.name);
        float height = B.top + 3.2f - A.top;
        if (height < 4f || !SpotFree(v, 1.9f) || !LandFree(v, 1.9f)) return No("no room for a vent at " + v.ToString("F0"));
        if (Physics.SphereCast(v + Vector3.up * 0.5f, 1.5f, Vector3.up, out RaycastHit vh, height + 1f, ~0, QueryTriggerInteraction.Ignore)) return No("vent column blocked by " + vh.collider.name + " at " + vh.point.ToString("F0"));
        Vector3 land = NodePoint(B, XZ(v), Mathf.Min(2.5f, Mathf.Min(B.areas[0].width, B.areas[0].height) / 2f), false);
        if (!LandFree(land, 1.2f)) return No("something where the vent drops you on " + B.name);
        Vent(v, height, 1.8f);
        Use(v, 2.0f);
        Landing(land);
        var l = AkAddLink("vent", a, b);
        l.start = v; l.yaw = YawOf(bp - v); l.forward = false;
        l.steerAbove = B.top + 1.2f; l.steerTo = land;
        return true;
    }

    // a trampoline on a, a few metres short of b, that bounces you up onto it
    static bool AkTrampolineLink(int a, int b)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float dh = B.top - A.top;
        if (dh < 2.5f || dh > 16f) return No("too high for a trampoline");
        float bounce = Mathf.Sqrt(2f * AkG * (dh + 2.4f));
        // how far short of b's edge it has to be so you're above b's top by the time you get there (riding at 7 m/s)
        float lead = 2f;
        for (; lead < 9f; lead += 0.25f)
        {
            float t = lead / 7f;
            if (bounce * t - 0.5f * AkG * t * t > dh + 1.2f) break;
        }
        Vector3 edge, dirBA, p;
        if (A.kind == NGround)
        {
            p = GroundSpotNear(B, lead + 0.6f, 1.7f);
            if (p.x == float.MaxValue) return No("no ground spot for a trampoline by " + B.name);
            edge = NodePoint(B, XZ(p), 0f, false);
            dirBA = FlatDir(p - edge);
        }
        else
        {
            edge = NodePoint(B, XZ(A.Centre), 0f, false);
            dirBA = FlatDir(new Vector3(A.Centre.x - edge.x, 0f, A.Centre.z - edge.z));
            p = edge + dirBA * (lead + 0.6f);
        }
        if (float.IsNaN(dirBA.x) || dirBA == Vector3.zero) return false;
        p.y = A.kind == NGround ? floorAt(p.x, p.z) : A.top;
        if (A.kind != NGround && !A.Contains(XZ(p), 1.6f)) return false;
        if (A.kind == NGround && !GroundOk(p)) return false;
        Vector3 start = p + dirBA * 6f;
        start.y = A.kind == NGround ? floorAt(start.x, start.z) : A.top;
        if (A.kind != NGround && !A.Contains(XZ(start), 0.5f)) return false;
        if (A.kind == NGround && !GroundOk(start)) return No("trampoline run-up off the pavement");
        if (!SpotFree(p, 1.7f) || !RunFree(start, p)) return No("no room or run-up for a trampoline at " + p.ToString("F0"));
        // the way up and over must be clear
        if (Physics.SphereCast(p + Vector3.up * 0.8f, 0.6f, Vector3.up, out RaycastHit th, dh + 3f, ~0, QueryTriggerInteraction.Ignore)) return No("trampoline blocked above by " + th.collider.name);
        if (!LandSpot(B, p, out Vector3 tland)) return false;
        Trampoline(p, -dirBA, bounce);
        Use(p, 1.8f);
        Landing(tland);
        RunUp(start, p);
        var l = AkAddLink("trampoline", a, b);
        l.start = start; l.yaw = YawOf(-dirBA);
        return true;
    }

    // a short rail from a's edge to b, to jump onto and grind across
    static bool AkRailLink(int a, int b)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        if (A.kind == NGround) return false;
        Vector3 ra = NodePoint(A, XZ(B.Centre), 1.2f), rb = NodePoint(B, XZ(ra), 2.8f, false);
        Vector3 dir = FlatDir(rb - ra);
        float len = Vector3.Distance(XZ(ra), XZ(rb));
        if (len < 5f || len > 20f) return false;
        if ((rb.y - ra.y) / len > 0.45f) return false;
        // a run-up on a, straight back from the rail's start
        float run = Mathf.Min(RunRoom(A, ra, dir), 8f);
        if (run < 4.5f) return No("no run-up for a rail on " + A.name);
        Vector3 start = ra - dir * run;
        Vector3 a3 = ra + Vector3.up * 0.9f, b3 = rb + Vector3.up * 0.9f;
        if (Physics.SphereCast(a3 + Vector3.up * 0.2f, 0.45f, (b3 - a3).normalized, out _, Vector3.Distance(a3, b3), ~0, QueryTriggerInteraction.Ignore)) return false;
        if (!RunFree(start, ra) || !SpotFree(ra, 0.8f)) return false;
        Vector3 past = rb + dir * 2.5f;
        if (!B.Contains(XZ(past), 0.6f)) past = rb;
        if (!LandFree(past, 1.8f)) return false;
        LinkRail(a3, b3, ra, rb);
        Use(ra, 1f);
        Landing(past);
        RunUp(start, ra);
        var l = AkAddLink("rail", a, b);
        l.start = start; l.yaw = YawOf(dir); l.sprint = true; l.jumpAt = run - 1.3f;
        return true;
    }

    static void LinkRail(Vector3 a3, Vector3 b3, Vector3 footA, Vector3 footB)
    {
        Rail("Link rail", new[] { a3, b3 }, false);
        RefreshRails();
        akRails[akRails.Length - 1].catchRadius = 0.9f;
        var pipe = AK("white", "e");
        pipe.Cylinder(a3 + Vector3.down * 0.06f, b3 + Vector3.down * 0.06f, 0.1f, 8, 2f, new Color32(255, 214, 40, 255));
        // brackets down to each end
        foreach (var (top, foot) in new[] { (a3, footA), (b3, footB) })
            AK("metal").Cylinder(top + Vector3.down * 0.1f, foot, 0.06f, 6, 1f, new Color32(150, 150, 170, 255));
    }

    // a run and a jump from a's edge across to b (no lower than you'd make it, no higher than you can jump)
    static bool AkJumpLink(int a, int b)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float dh = B.top - A.top;
        if (dh > 0.4f || A.kind == NGround) return false;
        Vector3 edge = NodePoint(A, XZ(B.Centre), 0f, false);
        Vector3 near = NodePoint(B, XZ(edge), 0f, false);
        float gap = Vector3.Distance(XZ(edge), XZ(near));
        float reach = 4.2f + Mathf.Sqrt(-Mathf.Min(0f, dh)) * 2.4f;
        if (gap > reach) return false;
        Vector3 land = NodePoint(B, XZ(edge), Mathf.Min(2.5f, gap + 1.5f), false);
        Vector3 dir = FlatDir(land - edge);
        if (dir == Vector3.zero) dir = FlatDir(B.Centre - A.Centre);
        float run = Mathf.Min(RunRoom(A, edge, dir), 10f);
        if (run < 5f) return No("no run-up for a jump on " + A.name);
        Vector3 start = edge - dir * run;
        if (!RunFree(start, edge)) return false;
        bool drop = gap < 0.8f;   // right next to it and lower: just ride off the edge
        // how long you're in the air (a jump: 8.4 m/s up, then gravity, heavier coming down), so how far you go
        // at a cruise (7 m/s) or flat out (14): it has to carry you over the gap and land you on b, not past it
        float up = drop ? 0f : 1.0f, tUp = drop ? 0f : 0.24f;
        float tDown = Mathf.Sqrt(2f * (up - dh) / (AkG * 1.5f));
        float t = tUp + tDown;
        float depth = 0f;
        for (float d = 0f; d < 40f; d += 0.5f) { if (B.Contains(XZ(edge + dir * (gap + d)), 0.3f)) depth = d; else if (depth > 0f) break; }
        bool sprint;
        if (7f * t > gap + 1.2f && 7f * t < gap + depth - 1f) sprint = false;
        else if (14f * t > gap + 1.2f && 14f * t < gap + depth - 1f) sprint = true;
        else return false;
        Vector3 touch = edge + dir * (sprint ? 14f : 7f) * t;
        touch.y = B.top;
        if (!LandFree(touch, 2.2f)) return false;
        if (!Ballistic(edge + Vector3.up * 0.6f, touch + Vector3.up * 0.35f, drop ? 0.2f : 1.2f, out _, out var path) || !PathClear(path, 0.8f, 1.5f, 0.4f)) return false;
        Landing(touch);
        RunUp(start, edge);
        var l = AkAddLink(drop ? "drop" : "jump", a, b);
        l.start = start; l.yaw = YawOf(dir); l.sprint = sprint; l.jumpAt = drop ? -1f : run - 0.4f;
        return true;
    }

    // a ramp up from one roof onto the next one along, a bit higher
    static bool AkRampLink(int a, int b)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float dh = B.top - A.top;
        if (dh < 0.8f || dh > 8f || A.kind != NRoof || B.kind != NRoof || Gap(A, B) > 0.5f) return false;
        Rect ra = A.areas[0], rb = B.areas[0];
        float dz = rb.center.y > ra.center.y ? 1f : -1f;
        float x0 = Mathf.Max(ra.xMin, rb.xMin), x1 = Mathf.Min(ra.xMax, rb.xMax);
        if (x1 - x0 < 6f) return false;
        float len = dh * 2.6f;
        if (ra.height < len + 8f) return false;
        float xc = x0 + (x1 - x0) * 0.3f;   // towards the street side
        if (ra.center.x < 0f) xc = x1 - (x1 - x0) * 0.3f;
        float edgeZ = dz > 0f ? ra.yMax : ra.yMin;
        Vector3 foot = new Vector3(xc, A.top, edgeZ - dz * len);
        Vector3 start = foot - new Vector3(0f, 0f, dz * 7f);
        if (!SpotFree(foot + new Vector3(0f, 0f, dz * len * 0.5f), 1.8f) || !RunFree(start, foot)) return false;
        if (!LandFree(foot + new Vector3(0f, 0f, dz * len * 0.5f), len * 0.5f)) return false;
        Wedge("roof", foot, new Vector3(0f, 0f, dz), len, dh + 0.05f, 4f);
        if (dh > 4f) BoostArrow(foot - new Vector3(0f, 0f, dz * 3.5f), new Vector3(0f, 0f, dz), 20f);
        Use(foot + new Vector3(0f, 0f, dz * len * 0.5f), len * 0.5f + 1f);
        RunUp(start, foot);
        Landing(new Vector3(xc, B.top, edgeZ + dz * 2.5f));
        var l = AkAddLink("ramp", a, b);
        l.start = start; l.yaw = dz > 0f ? 0f : 180f; l.sprint = true;
        return true;
    }

    // ---- the ground ---------------------------------------------------------------------------------------

    // somewhere on the pavement or the plaza (not the road), clear overhead, `dist` from b's nearest edge
    static Vector3 GroundSpotNear(AkNode b, float dist, float r)
    {
        Vector3 c = b.Centre;
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 16; i++)
            {
                float ang = i * Mathf.PI / 8f + ring * 0.2f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                // walk out from the middle of b to its edge, then on `dist`
                Vector2 p = XZ(c);
                int guard = 0;
                while (b.Contains(p) && guard++ < 200) p += dir * 0.5f;
                p += dir * (dist + ring * 1.5f);
                Vector3 q = new Vector3(p.x, floorAt(p.x, p.y), p.y);
                if (!GroundOk(q) || !SpotFree(q, r)) continue;
                if (Physics.SphereCast(q + Vector3.up * 0.8f, r, Vector3.up, out RaycastHit h, 80f, ~0, QueryTriggerInteraction.Ignore) && h.point.y < b.top + 4f) continue;
                return q;
            }
        return new Vector3(float.MaxValue, 0f, 0f);
    }

    // on the pavement or the plaza (where nothing drives), inside the canyon
    static bool GroundOk(Vector3 p)
    {
        float ax = Mathf.Abs(p.x);
        return ax > AkRoad + 1.2f && ax < AkFrontAt(p.z) - 1.5f && Mathf.Abs(p.z) < AkL - 6f;
    }

    // ---- making sure there's always a way --------------------------------------------------------------

    static bool TryUp(int a, int b, int variety)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float gap = Gap(A, B), dh = B.top - A.top;
        if (A.kind == NRoof && B.kind == NRoof && gap < 0.5f && AkRampLink(a, b)) return true;
        // a few kinds, in an order that changes from node to node so it's not the same thing everywhere
        System.Func<bool>[] tries =
        {
            () => gap < 3.4f && dh > 3f && AkVentLink(a, b),
            () => dh < 6.5f && gap > 3f && gap < 17f && AkRailLink(a, b),
            () => dh < 14f && A.kind != NWalk && AkTrampolineLink(a, b),
        };
        for (int i = 0; i < tries.Length; i++)
            if (tries[(i + variety) % tries.Length]()) return true;
        return (A.kind == NGround || gap > 2.5f) && AkLaunch(a, b, LaunchSpot(A, B), B.Centre);
    }

    static bool TryAcross(int a, int b, int variety)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float gap = Gap(A, B);
        System.Func<bool>[] tries =
        {
            () => AkJumpLink(a, b),
            () => gap > 3f && gap < 18f && AkRailLink(a, b),
            () => gap > 4f && Mathf.Max(A.areas[0].width, A.areas[0].height) >= 7f && AkLaunch(a, b, LaunchSpot(A, B), NodePoint(B, XZ(A.Centre), 3f, false)),
        };
        if (AkJumpLink(a, b)) return true;
        if (gap > 3f && gap < 18f && AkRailLink(a, b)) return true;
        for (int i = 0; i < tries.Length; i++)
            if (tries[(i + variety) % tries.Length]()) return true;
        return false;
    }

    // where on a to put a launch pad aimed at b: near the edge facing it, on a free spot
    static Vector3 LaunchSpot(AkNode A, AkNode B)
    {
        if (A.kind == NGround) { var g = GroundSpotNear(B, 6f, 1.6f); return g.x == float.MaxValue ? new Vector3(0f, -999f, 0f) : g; }
        Vector3 p = NodePoint(A, XZ(B.Centre), 1.3f);
        if (SpotFree(p, 1.25f)) return p;
        // try along a's edge
        for (int i = 1; i < 8; i++)
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 q = NodePoint(A, XZ(p) + new Vector2(-(B.Centre - A.Centre).z, (B.Centre - A.Centre).x).normalized * s * i * 1.5f, 1.3f);
                if (SpotFree(q, 1.25f)) return q;
            }
        akWhy = "no room for a launch pad on " + A.name;
        return new Vector3(0f, -999f, 0f);
    }

    // goes over every node making sure it has a way up (unless it's right at the top) and a couple of ways
    // on in all, then makes sure every node can be got to from the street
    static void AkAutoLinks()
    {
        Physics.SyncTransforms();
        RefreshRails();
        float maxTop = 0f;
        foreach (var n in akNodes) maxTop = Mathf.Max(maxTop, n.top);
        var order = new List<int>();
        for (int i = 1; i < akNodes.Count; i++) if (akNodes[i].kind != NStop) order.Add(i);
        order.Sort((p, q) => akNodes[p].top.CompareTo(akNodes[q].top));
        int variety = 0;
        foreach (int a in order)
        {
            AkNode A = akNodes[a];
            // up: the nearest few nodes above, nearest first
            var ups = new List<int>();
            for (int b = 1; b < akNodes.Count; b++)
                if (b != a && akNodes[b].kind != NStop && akNodes[b].top > A.top + 2f && akNodes[b].top < A.top + 36f && Gap(A, akNodes[b]) < 70f) ups.Add(b);
            ups.Sort((p, q) => UpScore(A, akNodes[p]).CompareTo(UpScore(A, akNodes[q])));
            bool stone = A.areas[0].width < 5f;
            int wantUps = A.top > maxTop - 12f || stone ? 0 : A.kind == NIsland ? 2 : 1;
            foreach (int b in ups)
            {
                if (A.ups >= wantUps) break;
                if (Linked(a, b)) continue;
                Physics.SyncTransforms();
                if (TryUp(a, b, variety++)) RefreshRails();
            }
            // across (or down): the nearest ones about level or lower
            var across = new List<int>();
            for (int b = 1; b < akNodes.Count; b++)
                if (b != a && akNodes[b].kind != NStop && akNodes[b].top <= A.top + 2f && akNodes[b].top > A.top - 34f && Gap(A, akNodes[b]) < 50f) across.Add(b);
            across.Sort((p, q) => Gap(A, akNodes[p]).CompareTo(Gap(A, akNodes[q])));
            foreach (int b in across)
            {
                if (A.exits >= 3) break;
                if (Linked(a, b)) continue;
                Physics.SyncTransforms();
                if (TryAcross(a, b, variety++)) RefreshRails();
            }
        }
        // anything you can't get to yet: a way straight up to it from the street
        for (int pass = 0; pass < 4; pass++)
        {
            var reach = Reachable();
            bool any = false;
            foreach (int b in order)
            {
                if (reach[b]) continue;
                Physics.SyncTransforms();
                if (TryUp(0, b, variety++)) { any = true; RefreshRails(); continue; }
                // or from any node that can be reached
                for (int a = 1; a < akNodes.Count && !reach[b]; a++)
                    if (reach[a] && a != b && akNodes[a].kind != NStop && akNodes[a].top < akNodes[b].top && TryUp(a, b, variety++)) { any = true; RefreshRails(); break; }
            }
            if (!any) break;
        }
        foreach (int a in order)
        {
            AkNode A = akNodes[a];
            if (A.ups > 0 || A.top > maxTop - 12f) continue;
            var why = new List<string>();
            for (int b = 1; b < akNodes.Count && why.Count < 4; b++)
            {
                if (b == a || akNodes[b].kind == NStop || akNodes[b].top <= A.top + 2f || akNodes[b].top > A.top + 36f || Gap(A, akNodes[b]) > 70f) continue;
                akWhy = "?";
                if (TryUp(a, b, variety++)) { why.Add(akNodes[b].name + ": MADE ONE"); break; }
                why.Add(akNodes[b].name + ": " + akWhy);
            }
            Debug.Log("AKIBA_NOUP " + A.name + " | " + string.Join(" | ", why));
        }
        // anything with fewer than two ways on: try everything nearby, nearest first
        foreach (int a in order)
        {
            AkNode A = akNodes[a];
            if (A.exits >= 2) continue;
            var near = new List<int>();
            for (int b = 1; b < akNodes.Count; b++)
                if (b != a && akNodes[b].kind != NStop && Gap(A, akNodes[b]) < 40f && !Linked(a, b) && akNodes[b].top < A.top + 36f) near.Add(b);
            near.Sort((p, q) => Gap(A, akNodes[p]).CompareTo(Gap(A, akNodes[q])));
            var why = new List<string>();
            foreach (int b in near)
            {
                if (A.exits >= 2) break;
                akWhy = "?";
                Physics.SyncTransforms();
                bool up = akNodes[b].top > A.top + 2f;
                if (up ? TryUp(a, b, variety++) : TryAcross(a, b, variety++)) { RefreshRails(); continue; }
                if (why.Count < 5) why.Add(akNodes[b].name + ": " + akWhy);
            }
            if (A.exits < 2) Debug.Log("AKIBA_FEW " + A.name + " | " + string.Join(" | ", why));
        }
        var still = Reachable();
        foreach (int b in order)
        {
            if (still[b]) continue;
            AkNode B = akNodes[b];
            var why = new List<string>();
            foreach (var k in new[] { 0, 1, 2, 3 })
            {
                akWhy = "?";
                bool got = k == 0 ? AkVentLink(0, b) : k == 1 ? AkTrampolineLink(0, b) : k == 2 ? AkLaunch(0, b, LaunchSpot(akNodes[0], B), B.Centre) : false;
                if (k == 3) break;
                why.Add((k == 0 ? "vent: " : k == 1 ? "trampoline: " : "launch pad: ") + (got ? "MADE ONE" : akWhy));
            }
            Debug.Log("AKIBA_STUCK " + B.name + " | " + string.Join(" | ", why));
        }
    }

    static float UpScore(AkNode a, AkNode b)
    {
        float dh = b.top - a.top;
        return Gap(a, b) + Mathf.Abs(dh - 9f) * 0.5f;
    }

    static bool[] Reachable()
    {
        // (from anywhere you can always drop back to the street, so this is everything)
        var seen = new bool[akNodes.Count];
        var queue = new Queue<int>();
        seen[0] = true; queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int n = queue.Dequeue();
            foreach (var l in akLinks)
                if (l.from == n && !seen[l.to]) { seen[l.to] = true; queue.Enqueue(l.to); }
        }
        return seen;
    }

    // logged after the build: anything you can't get to, anything without a way up or with too few ways on
    static void AkReportGraph()
    {
        var reach = Reachable();
        float maxTop = 0f;
        foreach (var n in akNodes) maxTop = Mathf.Max(maxTop, n.top);
        var unreachable = new List<string>(); var noUp = new List<string>(); var few = new List<string>();
        var kinds = new Dictionary<string, int>();
        foreach (var l in akLinks) { kinds.TryGetValue(l.kind, out int c); kinds[l.kind] = c + 1; }
        for (int i = 1; i < akNodes.Count; i++)
        {
            var n = akNodes[i];
            if (!reach[i]) unreachable.Add(n.name);
            if (n.kind == NStop) continue;
            if (n.ups == 0 && n.top < maxTop - 12f && n.areas[0].width >= 5f) noUp.Add(n.name);
            if (n.exits < 2) few.Add(n.name);
        }
        var parts = new List<string>();
        foreach (var kv in kinds) parts.Add(kv.Key + "=" + kv.Value);
        Debug.Log("AKIBA_GRAPH nodes=" + akNodes.Count + " links=" + akLinks.Count + " (" + string.Join(", ", parts) + ")"
                  + " unreachable=[" + string.Join(", ", unreachable) + "] no way up=[" + string.Join(", ", noUp) + "] under 2 ways on=[" + string.Join(", ", few) + "]");
    }
}
