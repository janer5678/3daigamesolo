using System.Collections.Generic;
using UnityEngine;

// The Infinity level, built as you go so it never ends: fractal towers all over the place, at all heights, for
// ever in every direction, joined by hard ways across: narrow beams, zigzags, stepping stones, rails over the
// void, gaps to jump (proper bridges only near the start). It gets harder the further along the way on you get:
// thinner beams, smaller stones, wider gaps (the widest need the double jump), smaller towers. Each tower
// flares out at its corners into wide petals. Under it is the same thing again one layer down, but set a whole
// course back: what's under the hard end is the easy start. Fall off and nothing catches you: you just fall
// till you land on something, which is that same course again lower down, back at (or before) the start; fall
// further and you land further back still. The layers interleave, so there's nearly always something below.
//
// Everything is worked out in "structure" coordinates, the same for every layer; layer k is that plus
// k * layerShift.
[DefaultExecutionOrder(100)]   // (after the player's physics step, so the fall speed limit has the last word)
public class InfinityWorld : MonoBehaviour
{
    public Material fractal;
    public SlideController player;
    [Tooltip("Distance between towers.")]
    public float cell = 30f;
    [Tooltip("From one layer to the copy of it below: down, and a whole course back along the way on.")]
    public Vector3 layerShift = new Vector3(428.1f, -16f, 612.3f);
    [Tooltip("How far along the way on it takes to get as hard as it gets.")]
    public float hardAfter = 800f;
    [Tooltip("How many towers out from you to build, layers above you and below you.")]
    public int radius = 3, above = 2, below = 4;
    [Tooltip("Falling: no faster than this.")]
    public float maxFall = 45f;
    [Tooltip("Gaps wider than this need the double jump (the bot uses it on them).")]
    public float doubleJumpGap = 8f;
    [Tooltip("The petals at a tower's corners: longest; widths at the root, the widest part and the tip; the least gap left to a neighbour's, and to any way across.")]
    public float petalLength = 12f, petalRoot = 2.6f, petalWide = 7f, petalTip = 3.2f, petalGap = 6f, petalClear = 0.5f;
    public Transform checkpoint;

    public enum Kind { None, Bridge, Beam, Zigzag, Stones, Rail, Gap }

    // the way from one tower to its neighbour (+x for axis 0, +z for axis 1), in structure coordinates: from
    // the edge of one plaza (a) to the edge of the other (b)
    public struct Link
    {
        public Kind kind;
        public Vector3 a, b;
        public float width, size, gap, side;
        public int count;
        public Vector3 Dir { get { Vector3 d = b - a; d.y = 0f; return d.normalized; } }
        public Vector3 Lat { get { return Vector3.Cross(Vector3.up, Dir); } }
        public float Length { get { Vector3 d = b - a; d.y = 0f; return d.magnitude; } }
    }

    // where the bot (and the tests) go to get across: a point (structure coordinates), and what to do there
    public struct Way { public Vector3 at; public bool jump, hold, sprint, twice; }   // (twice: double jump at the top)

    readonly Dictionary<Vector3Int, GameObject> built = new Dictionary<Vector3Int, GameObject>();
    int lastK;
    float best;

    public int TowerCount { get { return built.Count; } }
    public int Layer { get { return lastK; } }   // the layer you last stood on
    // the way on: each layer down is a step back along it
    public Vector3 Forward { get { return new Vector3(layerShift.x, 0f, layerShift.z).normalized; } }
    // how far along the way on you are
    public float Progress(Vector3 world) { return Vector3.Dot(ToStructure(world, LayerAt(world.y)), Forward); }
    // how hard it is at s (structure coordinates): 0 at the start and anywhere behind it, up to 1 hardAfter on
    public float Difficulty(Vector3 s) { return Mathf.Clamp01(Vector3.Dot(s, Forward) / hardAfter); }
    float DifficultyAt(float i, float j) { return Difficulty(new Vector3(i * cell, 0f, j * cell)); }

    // ---- where things are --------------------------------------------------------------------------------

    public Vector3 ToWorld(Vector3 s, int k) { return s + layerShift * k; }
    public Vector3 ToStructure(Vector3 w, int k) { return w - layerShift * k; }
    public int LayerAt(float y) { return Mathf.RoundToInt(y / layerShift.y); }

    // a repeatable random number 0..1
    public static float Hash(int i, int j, int k, int salt)
    {
        unchecked
        {
            uint h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(k * 83492791) ^ (uint)(salt * 2654435761u);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // the middle of the top of tower i,j (structure coordinates): jumbled about, at heights from -3 to 3
    public Vector3 Centre(int i, int j)
    {
        if (i == 0 && j == 0) return Vector3.zero;
        return new Vector3(i * cell + (Hash(i, j, 0, 11) - 0.5f) * 8f, Mathf.Round((Hash(i, j, 0, 13) * 6f - 3f) * 2f) * 0.5f, j * cell + (Hash(i, j, 0, 12) - 0.5f) * 8f);
    }

    public float Width(int i, int j) { return i == 0 && j == 0 ? 12f : 8f - 1.5f * DifficultyAt(i, j) + 4f * Hash(i, j, 0, 1); }

    // the tower whose plaza p (structure coordinates) is over, if any
    public bool TowerAt(Vector3 s, out int ti, out int tj)
    {
        int ci = Mathf.RoundToInt(s.x / cell), cj = Mathf.RoundToInt(s.z / cell);
        for (ti = ci - 1; ti <= ci + 1; ti++)
            for (tj = cj - 1; tj <= cj + 1; tj++)
            {
                Vector3 c = Centre(ti, tj);
                float h = Width(ti, tj) * 0.5f;
                if (Mathf.Abs(s.x - c.x) < h && Mathf.Abs(s.z - c.z) < h) return true;
            }
        ti = tj = 0;
        return false;
    }

    public Link LinkOf(int i, int j, int axis)
    {
        Vector3 ax = axis == 0 ? Vector3.right : Vector3.forward;
        int i2 = i + (axis == 0 ? 1 : 0), j2 = j + (axis == 1 ? 1 : 0);
        var l = new Link { a = Centre(i, j) + ax * Width(i, j) * 0.5f, b = Centre(i2, j2) - ax * Width(i2, j2) * 0.5f };
        float len = l.Length, slope = Mathf.Abs(l.b.y - l.a.y) / len;
        bool home = (i == 0 && j == 0) || (i2 == 0 && j2 == 0);
        float d = DifficultyAt((i + i2) * 0.5f, (j + j2) * 0.5f);
        if (!home && Hash(i, j, axis, 20) < 0.12f + 0.1f * d) { l.kind = Kind.None; return l; }
        float h1 = Hash(i, j, axis, 22), h2 = Hash(i, j, axis, 23);
        l.side = Hash(i, j, axis, 24) < 0.5f ? -1f : 1f;
        // stepping stones: as many as fit with jumpable gaps, each a small step up or down from the last (smaller
        // stones and wider gaps the harder it is)
        float st = 2.6f - 0.9f * d + 0.6f * h1, lo = 1.2f + 0.5f * d, hi = 2.1f + 0.8f * d;
        int count = Mathf.Max(2, Mathf.RoundToInt((len - (lo + hi) * 0.5f) / (st + (lo + hi) * 0.5f)));
        float sg = (len - count * st) / (count + 1);
        if (sg < lo) { count--; sg = (len - count * st) / (count + 1); }
        if (sg > hi) { count++; sg = (len - count * st) / (count + 1); }
        bool stones = sg >= lo && sg <= hi && Mathf.Abs(l.b.y - l.a.y) / (count + 1) <= 0.5f;
        float gap = 3.5f + 5f * d + 1.5f * h2;
        // which kinds work here, and how likely each is
        var kinds = new List<Kind>(); var weights = new List<float>();
        System.Action<Kind, float, bool> may = (k, w, ok) => { if (ok) { kinds.Add(k); weights.Add(w); } };
        may(Kind.Bridge, 1.6f * (1f - d) * (1f - d), true);
        may(Kind.Beam, 2f, slope <= 0.38f);
        may(Kind.Zigzag, 1.6f, slope <= 0.3f);
        may(Kind.Stones, 2.2f, stones);
        may(Kind.Rail, 2f, slope <= 0.35f);
        may(Kind.Gap, 1.8f, slope <= 0.15f && len >= gap + 8f);
        float total = 0f; foreach (var w in weights) total += w;
        float pick = Hash(i, j, axis, 21) * total;
        l.kind = kinds[kinds.Count - 1];
        for (int n = 0; n < kinds.Count; n++) { if (pick < weights[n]) { l.kind = kinds[n]; break; } pick -= weights[n]; }
        switch (l.kind)
        {
            case Kind.Bridge: l.width = 3.4f; break;
            case Kind.Beam: l.width = 1.7f - 0.9f * d + 0.5f * h1; break;
            case Kind.Zigzag: l.width = 1.9f - 0.9f * d + 0.4f * h1; l.size = (1.5f + 1f * h2) * (1f + 0.5f * d); break;
            case Kind.Stones: l.size = st; l.gap = sg; l.count = count; break;
            case Kind.Rail: l.size = 1.2f + 0.3f * d; break;
            case Kind.Gap: l.width = 3.2f - 0.9f * d + 0.6f * h1; l.gap = gap; break;
        }
        return l;
    }

    // the petal off corner sx,sz (each -1 or 1) of tower i,j: from its root (on the plaza) out diagonally, len
    // long and `scale` as wide as it might be; kept short enough to leave petalGap to any neighbour's petals, and
    // narrow and short enough to keep clear of every way across, so it's no shortcut and nothing to trip on
    public void Petal(int i, int j, int sx, int sz, out Vector3 root, out Vector3 dir, out float len, out float scale)
    {
        Vector3 c = Centre(i, j);
        float h = Width(i, j) * 0.5f;
        root = c + new Vector3(sx, 0f, sz) * h * 0.8f;
        dir = new Vector3(sx, 0f, sz).normalized;
        float midX = Mathf.Abs(Centre(i + sx, j).x - c.x) * 0.5f, midZ = Mathf.Abs(Centre(i, j + sz).z - c.z) * 0.5f;
        float room = Mathf.Min(midX, midZ) - petalGap * 0.5f - petalTip * 0.5f * 0.71f - h * 0.8f;
        float most = Mathf.Min(petalLength * (0.8f + 0.2f * Hash(i, j, sx * 3 + sz, 40)), room * 1.414f);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        foreach (float ls in new[] { 1f, 0.8f, 0.6f })
            foreach (float ws in new[] { 1f, 0.8f, 0.6f, 0.45f })
            {
                len = most * ls; scale = ws;
                if (len < 2f) break;
                bool clear = true;
                for (int n = 0; n <= 10 && clear; n++)
                {
                    float t = n / 10f, hw = PetalWidth(t, scale) * 0.5f;
                    Vector3 m = root + dir * (len * t);
                    foreach (var q in new[] { m + side * hw, m - side * hw, m })
                        if ((Mathf.Abs(q.x - c.x) > h || Mathf.Abs(q.z - c.z) > h) && Clearance(q) < petalClear) { clear = false; break; }
                }
                if (clear) return;
            }
        len = 0f; scale = 0f;
    }

    // how wide a petal is t of the way out (0 at the root, 1 at the tip): a leaf, widest a bit under half way
    public float PetalWidth(float t, float scale)
    {
        return (t < 0.45f ? Mathf.Lerp(petalRoot, petalWide, t / 0.45f) : Mathf.Lerp(petalWide, petalTip, (t - 0.45f) / 0.55f)) * scale;
    }

    // a point along a petal (t from 0 at the root to 1 at the tip), on top: it droops a little, like a trumpet's bell
    public Vector3 PetalAt(Vector3 root, Vector3 dir, float len, float t) { return root + dir * (len * t) + Vector3.down * (1.2f * t * t); }

    // how far p (structure coordinates, across) is from the edge of the nearest way across
    public float Clearance(Vector3 p)
    {
        int ci = Mathf.RoundToInt(p.x / cell), cj = Mathf.RoundToInt(p.z / cell);
        float best = float.MaxValue;
        for (int i = ci - 1; i <= ci + 1; i++)
            for (int j = cj - 1; j <= cj + 1; j++)
                for (int axis = 0; axis < 2; axis++)
                {
                    var l = LinkOf(i, j, axis);
                    if (l.kind == Kind.None) continue;
                    IList<Vector3> line = new[] { l.a, l.b };
                    float hw = l.width * 0.5f;
                    if (l.kind == Kind.Zigzag) line = Zigzag(l);
                    else if (l.kind == Kind.Stones) hw = 0.5f + l.size * 0.71f;
                    else if (l.kind == Kind.Rail) { line = Rail(l); hw = 0.3f; }
                    for (int n = 0; n < line.Count - 1; n++) best = Mathf.Min(best, FlatDistance(p, line[n], line[n + 1]) - hw);
                }
        return best;
    }

    static float FlatDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 pp = new Vector2(p.x, p.z), aa = new Vector2(a.x, a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
        float t = Mathf.Clamp01(Vector2.Dot(pp - aa, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (pp - aa - ab * t).magnitude;
    }

    // the stepping stones' middles (on top), from a to b
    public List<Vector3> Stones(Link l)
    {
        var r = new List<Vector3>();
        for (int n = 0; n < l.count; n++)
        {
            float along = l.gap + l.size * 0.5f + n * (l.size + l.gap);
            Vector3 p = Vector3.Lerp(l.a, l.b, along / l.Length) + l.Lat * ((n & 1) == 0 ? 0.5f : -0.5f) * l.side;
            r.Add(p);
        }
        return r;
    }

    // the zigzag's corners, from a to b
    public List<Vector3> Zigzag(Link l)
    {
        return new List<Vector3> { l.a, Vector3.Lerp(l.a, l.b, 1f / 3f) + l.Lat * l.size * l.side, Vector3.Lerp(l.a, l.b, 2f / 3f) - l.Lat * l.size * l.side, l.b };
    }

    // the rail's ends, from a to b: off to one side, running onto both plazas
    public Vector3[] Rail(Link l)
    {
        Vector3 off = l.Lat * l.size * l.side + Vector3.up * 0.8f;
        return new[] { l.a - l.Dir * 1.2f + off, l.b + l.Dir * 1.2f + off };
    }

    // ---- building and clearing -----------------------------------------------------------------------------

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        lastK = LayerAt(player.transform.position.y);
        GenerateAround(player.transform.position, true);
    }

    void Update()
    {
        if (player == null) return;
        GenerateAround(player.transform.position, false);
    }

    void FixedUpdate()
    {
        if (player == null) return;
        Track();
    }

    // builds what's missing round p (nearest first; a few a frame unless `all`) and clears what's far away
    public void GenerateAround(Vector3 p, bool all)
    {
        int k0 = LayerAt(p.y);
        var missing = new List<Vector3Int>();
        for (int k = k0 - above; k <= k0 + below; k++)
        {
            Vector3 s = ToStructure(p, k);
            int ci = Mathf.RoundToInt(s.x / cell), cj = Mathf.RoundToInt(s.z / cell);
            for (int i = ci - radius; i <= ci + radius; i++)
                for (int j = cj - radius; j <= cj + radius; j++)
                {
                    var key = new Vector3Int(i, j, k);
                    if (!built.ContainsKey(key)) missing.Add(key);
                }
        }
        System.Func<Vector3Int, float> dist = key => (ToWorld(Centre(key.x, key.y), key.z) - p).sqrMagnitude;
        missing.Sort((a, b) => dist(a).CompareTo(dist(b)));
        int budget = all ? missing.Count : 6;
        for (int n = 0; n < missing.Count && n < budget; n++) built[missing[n]] = Build(missing[n].x, missing[n].y, missing[n].z);
        var gone = new List<Vector3Int>();
        foreach (var kv in built)
        {
            var key = kv.Key;
            Vector3 s = ToStructure(p, key.z);
            int ci = Mathf.RoundToInt(s.x / cell), cj = Mathf.RoundToInt(s.z / cell);
            if (key.z < k0 - above - 1 || key.z > k0 + below + 1 || Mathf.Abs(key.x - ci) > radius + 1 || Mathf.Abs(key.y - cj) > radius + 1) gone.Add(key);
        }
        foreach (var key in gone)
        {
            var go = built[key];
            built.Remove(key);
            if (go == null) continue;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) DestroyNow(mf.sharedMesh);
            DestroyNow(go);
        }
        if ((budget > 0 && missing.Count > 0) || gone.Count > 0) player.Setup();   // (it finds the rails that came and went)
    }

    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    // ---- falling -----------------------------------------------------------------------------------------

    // Nothing catches you: this only keeps your fall to a speed you can follow (and the building can keep up
    // with), remembers the last tower you stood on (R puts you back there), and says how far down and along you
    // are. Public so the tests can run it after each physics step.
    public void Track()
    {
        Vector3 p = player.transform.position;
        var rb = player.GetComponent<Rigidbody>();
        if (rb.linearVelocity.y < -maxFall) rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxFall, rb.linearVelocity.z);
        if (player.Grounded)
        {
            int k = LayerAt(p.y);
            Vector3 s = ToStructure(p, k);
            if (k > lastK) SlideHUD.Flash(k - lastK == 1 ? "DOWN A LAYER" : "DOWN " + (k - lastK) + " LAYERS");
            lastK = k;
            int ti, tj;
            if (TowerAt(s, out ti, out tj))
            {
                if (checkpoint == null) checkpoint = new GameObject("Last tower").transform;
                checkpoint.SetPositionAndRotation(ToWorld(Centre(ti, tj) + Vector3.up * 0.2f, k), Quaternion.Euler(0f, player.yaw, 0f));
                player.checkpoint = checkpoint;
            }
            float far = Vector3.Dot(s, Forward);
            if (far > best + 100f) { best = Mathf.Floor(far / 100f) * 100f; SlideHUD.Flash(best + " M ON"); }
        }
        player.fallLimit = (lastK + 40) * layerShift.y;   // (only if something's gone badly wrong)
    }

    // ---- the way across, for the bot --------------------------------------------------------------------------

    // from the middle of tower i,j to the middle of its neighbour on `axis` (or back, if `reverse`)
    public List<Way> Route(int i, int j, int axis, bool reverse)
    {
        var l = LinkOf(i, j, axis);
        Vector3 ca = Centre(i, j), cb = Centre(i + (axis == 0 ? 1 : 0), j + (axis == 1 ? 1 : 0));
        Vector3 a = l.a, b = l.b;
        if (reverse) { var t = ca; ca = cb; cb = t; t = a; a = b; b = t; }
        Vector3 dir = b - a; dir.y = 0f; dir.Normalize();
        var r = new List<Way>();
        switch (l.kind)
        {
            case Kind.Bridge:
                r.Add(new Way { at = a - dir * 3f, sprint = true });
                r.Add(new Way { at = b + dir * 1f, sprint = true });
                break;
            case Kind.Beam:
                r.Add(new Way { at = a - dir * 3f });
                r.Add(new Way { at = b });
                break;
            case Kind.Zigzag:
            {
                var z = Zigzag(l);
                if (reverse) z.Reverse();
                r.Add(new Way { at = a - dir * 3f });
                foreach (var c in z) r.Add(new Way { at = c });
                break;
            }
            case Kind.Stones:
            {
                var s = Stones(l);
                if (reverse) s.Reverse();
                Vector3 lat0 = (s[0] - a) - dir * Vector3.Dot(s[0] - a, dir);
                lat0.y = 0f;
                r.Add(new Way { at = a - dir * 3f + lat0 * 0.5f });
                r.Add(new Way { at = a - dir * 0.35f + lat0 * 0.5f, jump = true });
                foreach (var c in s) r.Add(new Way { at = c + dir * (l.size * 0.5f - 0.35f), jump = true });
                break;
            }
            case Kind.Rail:
            {
                var e = Rail(l);
                Vector3 r0 = reverse ? e[1] : e[0], r1 = reverse ? e[0] : e[1];
                // a run up from just behind the middle of the plaza, in line with the rail
                Vector3 lat = r0 - a - dir * Vector3.Dot(r0 - a, dir); lat.y = 0f;
                r.Add(new Way { at = ca - dir * 1.5f + lat });
                r.Add(new Way { at = r0 - dir * 0.8f, jump = true });
                r.Add(new Way { at = r1 });
                break;
            }
            case Kind.Gap:
            {
                float half = (l.Length - l.gap) * 0.5f;
                r.Add(new Way { at = a - dir * 3f, sprint = true });
                r.Add(new Way { at = a + dir * (half - 0.4f), jump = true, hold = true, sprint = true, twice = l.gap > doubleJumpGap });
                break;
            }
        }
        r.Add(new Way { at = cb, sprint = l.kind == Kind.Gap || l.kind == Kind.Bridge });
        return r;
    }

    // ---- one tower, and the ways from it to its +x and +z neighbours --------------------------------------------

    GameObject Build(int i, int j, int k)
    {
        var solid = new SkateMeshKit();
        var deco = new SkateMeshKit();
        var rails = new List<Vector3[]>();
        Vector3 c = ToWorld(Centre(i, j), k);
        float w = Width(i, j), h = w * 0.5f;
        byte hue = (byte)(Hash(i, j, 0, 2) * 255f);
        System.Func<float, Color32> shade = b => { byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 30, 255); return new Color32(v, v, v, hue); };
        var go = new GameObject("Tower " + i + "," + j + " layer " + k);
        go.transform.SetParent(transform, false);

        // the plaza
        solid.Box(c + Vector3.down * 0.6f, new Vector3(w, 1.2f, w), Quaternion.identity, shade(1f));
        // petals flaring out of its corners, some curling up into a bank at the tip
        for (int s = 0; s < 4; s++)
        {
            int sx = (s & 1) == 0 ? -1 : 1, sz = (s & 2) == 0 ? -1 : 1;
            Vector3 root, d; float len, ws;
            Petal(i, j, sx, sz, out root, out d, out len, out ws);
            if (len < 2f) continue;
            Vector3 off = layerShift * k;
            PetalMesh(solid, root + off, d, len, ws, shade(0.95f), shade(0.55f));
            if (Hash(i, j, s, 30) < 0.4f)
                Flare(solid, PetalAt(root, d, len, 1f) + off, d, Vector3.Cross(Vector3.up, d), petalTip * ws, 2.6f + 1.4f * Hash(i, j, s, 31), shade(0.95f), shade(0.6f), rails);
        }
        // the spire hanging underneath, with little copies of the tower round it: the fractal going on down
        Spire(deco, c + Vector3.down * 1.2f, h * 0.85f, 8f, shade);
        deco.wobble = 1f;
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f + 45f, 0f);
            Vector3 m = c + q * Vector3.forward * h * 0.8f + Vector3.down * 4f;
            deco.Box(m, new Vector3(w * 0.18f, 0.4f, w * 0.18f), q, shade(0.9f));
            deco.Box(m + Vector3.down * 1.4f, new Vector3(w * 0.05f, 2.8f, w * 0.05f), q, shade(0.7f));
        }
        deco.wobble = 0f;

        for (int axis = 0; axis < 2; axis++)
        {
            var l = LinkOf(i, j, axis);
            if (l.kind == Kind.None) continue;
            Vector3 a = ToWorld(l.a, k), b = ToWorld(l.b, k), off = layerShift * k;
            switch (l.kind)
            {
                case Kind.Bridge:
                    Bridge(solid, a, b, l.width, shade(0.9f), shade(0.5f), rails);
                    break;
                case Kind.Beam:
                    Strip(solid, a, b, l.width, 0.5f, shade(0.9f));
                    Strip(deco, a + Vector3.down * 0.9f, b + Vector3.down * 0.9f, l.width * 0.35f, 1f, shade(0.5f));
                    break;
                case Kind.Zigzag:
                {
                    var z = Zigzag(l);
                    for (int n = 0; n < z.Count - 1; n++) Strip(solid, z[n] + off, z[n + 1] + off, l.width, 0.5f, shade(0.9f), l.width);
                    break;
                }
                case Kind.Stones:
                    foreach (var s in Stones(l))
                    {
                        Vector3 p = s + off;
                        solid.Box(p + Vector3.down * 0.4f, new Vector3(l.size, 0.8f, l.size), Quaternion.LookRotation(l.Dir), shade(0.92f));
                        Spire(deco, p + Vector3.down * 0.8f, l.size * 0.5f, 3f + 2f * Hash(Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.z), 0, 6), shade);
                    }
                    break;
                case Kind.Rail:
                {
                    var e = Rail(l);
                    Vector3 r0 = e[0] + off, r1 = e[1] + off;
                    rails.Add(new[] { r0, r1 });
                    deco.Cylinder(r0, r1, 0.06f, 6, shade(1f));
                    foreach (var p in new[] { r0, r1 }) deco.Cylinder(p, p + Vector3.down * 0.8f, 0.05f, 5, shade(0.7f));
                    break;
                }
                case Kind.Gap:
                {
                    float f = (l.Length - l.gap) * 0.5f / l.Length;
                    Strip(solid, a, Vector3.Lerp(a, b, f), l.width, 0.6f, shade(0.9f));
                    Strip(solid, Vector3.Lerp(a, b, 1f - f), b, l.width, 0.6f, shade(0.9f));
                    break;
                }
            }
        }

        // now and then a kaleidoscope star turning in the gap beside it
        if (Hash(i, j, 0, 3) < 0.3f)
        {
            var star = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer), typeof(Spin));
            star.transform.SetParent(go.transform, false);
            star.transform.position = c + new Vector3(cell * 0.5f, 6f + 6f * Hash(i, j, 0, 5), cell * 0.5f);
            var sk = new SkateMeshKit();
            for (int s = 0; s < 8; s++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, s * 45f);
                foreach (var f in new[] { Vector3.back, Vector3.forward })
                {
                    sk.Tri(Vector3.zero, q * new Vector3(-0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), f, new Color32(255, 255, 255, hue));
                    sk.Tri(Vector3.zero, q * new Vector3(0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), f, new Color32(200, 200, 200, hue));
                }
            }
            star.GetComponent<MeshFilter>().sharedMesh = sk.Build("Star");
            star.GetComponent<MeshRenderer>().sharedMaterial = fractal;
            star.GetComponent<Spin>().degreesPerSecond = 25f + 40f * Hash(i, j, 0, 4);
        }

        Part(go, "Course", solid, true);
        Part(go, "Decoration", deco, false);
        foreach (var pts in rails)
        {
            var rg = new GameObject("Rail");
            rg.transform.SetParent(go.transform, false);
            rg.AddComponent<SkateRail>().points = pts;
        }
        return go;
    }

    void Part(GameObject parent, string name, SkateMeshKit kit, bool collide)
    {
        if (kit.Empty) return;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent.transform, false);
        var mesh = kit.Build(parent.name + " " + name);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = fractal;
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // a petal: a thick tapering leaf from the root out along d, drooping at the tip
    void PetalMesh(SkateMeshKit kit, Vector3 root, Vector3 d, float len, float scale, Color32 top, Color32 under)
    {
        Vector3 side = Vector3.Cross(Vector3.up, d);
        int segs = 8;
        for (int n = 0; n < segs; n++)
        {
            float t0 = n / (float)segs, t1 = (n + 1f) / segs;
            Vector3 p0 = PetalAt(root, d, len, t0), p1 = PetalAt(root, d, len, t1);
            Vector3 s0 = side * PetalWidth(t0, scale) * 0.5f, s1 = side * PetalWidth(t1, scale) * 0.5f;
            Vector3 up = Vector3.Cross(p1 - p0, side).normalized;
            if (up.y < 0f) up = -up;
            float th0 = Mathf.Lerp(1.2f, 0.5f, t0), th1 = Mathf.Lerp(1.2f, 0.5f, t1);
            kit.Quad(p0 - s0, p1 - s1, p1 + s1, p0 + s0, up, top);
            kit.Quad(p0 - s0 + Vector3.down * th0, p1 - s1 + Vector3.down * th1, p1 + s1 + Vector3.down * th1, p0 + s0 + Vector3.down * th0, Vector3.down, under);
            foreach (var sg in new[] { -1f, 1f })
                kit.Quad(p0 + s0 * sg, p1 + s1 * sg, p1 + s1 * sg + Vector3.down * th1, p0 + s0 * sg + Vector3.down * th0, side * sg, under);
            if (n == segs - 1) kit.Quad(p1 - s1, p1 + s1, p1 + s1 + Vector3.down * th1, p1 - s1 + Vector3.down * th1, d, under);
        }
    }

    // a straight beam from p0 to p1 along its top (sloping if they're at different heights), `extend` longer
    static void Strip(SkateMeshKit kit, Vector3 p0, Vector3 p1, float width, float thick, Color32 col, float extend = 0.3f)
    {
        Quaternion q = Quaternion.LookRotation(p1 - p0);
        kit.Box((p0 + p1) * 0.5f + q * Vector3.down * (thick * 0.5f), new Vector3(width, thick, (p1 - p0).magnitude + extend), q, col);
    }

    // a spire hanging down, twisting as it narrows (decoration: it sways)
    static void Spire(SkateMeshKit kit, Vector3 top, float r, float len, System.Func<float, Color32> shade)
    {
        kit.wobble = 1f;
        int rings = 6;
        for (int n = 0; n < rings; n++)
        {
            float t0 = n / (float)rings, t1 = (n + 1f) / rings;
            float y0 = top.y - t0 * len, y1 = top.y - t1 * len;
            float r0 = r * (1f - t0), r1 = r * (1f - t1);
            float twist = n * 0.35f;
            for (int s = 0; s < 4; s++)
            {
                float a0 = s * Mathf.PI * 0.5f + twist, a1 = a0 + Mathf.PI * 0.5f;
                Vector3 p00 = top + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), p01 = top + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                Vector3 p10 = top + new Vector3(Mathf.Cos(a0 + 0.35f) * r1, 0f, Mathf.Sin(a0 + 0.35f) * r1), p11 = top + new Vector3(Mathf.Cos(a1 + 0.35f) * r1, 0f, Mathf.Sin(a1 + 0.35f) * r1);
                p00.y = p01.y = y0; p10.y = p11.y = y1;
                kit.Quad(p00, p01, p11, p10, new Vector3(Mathf.Cos(a0 + 0.78f), -0.4f, Mathf.Sin(a0 + 0.78f)), shade(0.8f - n * 0.08f));
            }
        }
        kit.wobble = 0f;
    }

    // a curved bank: up from flat to vertical, a short deck at the top, coping on it
    static void Flare(SkateMeshKit kit, Vector3 end, Vector3 d, Vector3 side, float width, float r, Color32 col, Color32 sideCol, List<Vector3[]> rails)
    {
        int segs = 10;
        float hw = width * 0.5f;
        var pts = new List<Vector3>(); var nrm = new List<Vector3>();
        for (int i = 0; i <= segs; i++)
        {
            float a = i * 0.5f * Mathf.PI / segs;
            pts.Add(end + d * (r * Mathf.Sin(a)) + Vector3.up * (r * (1f - Mathf.Cos(a))));
            nrm.Add((Vector3.up * Mathf.Cos(a) - d * Mathf.Sin(a)).normalized);
        }
        Vector3 lip = end + d * r + Vector3.up * (r + 0.3f);
        pts.Add(lip); nrm.Add(-d);
        pts.Add(lip + d * 0.8f); nrm.Add(Vector3.up);
        for (int i = 0; i < pts.Count - 1; i++)
        {
            kit.SmoothQuad(pts[i] - side * hw, pts[i + 1] - side * hw, pts[i + 1] + side * hw, pts[i] + side * hw, nrm[i], nrm[i + 1], nrm[i + 1], nrm[i], col);
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 p0 = pts[i] + side * s * hw, p1 = pts[i + 1] + side * s * hw;
                kit.Quad(p0, p1, new Vector3(p1.x, end.y - 1f, p1.z), new Vector3(p0.x, end.y - 1f, p0.z), side * s, sideCol);
            }
        }
        kit.Quad(lip + d * 0.8f - side * hw, lip + d * 0.8f + side * hw, end + d * (r + 0.8f) + side * hw + Vector3.down, end + d * (r + 0.8f) - side * hw + Vector3.down, d, sideCol);
        rails.Add(new[] { lip - side * (hw - 0.3f) + Vector3.up * 0.03f, lip + side * (hw - 0.3f) + Vector3.up * 0.03f });
    }

    // a bridge from one plaza to the next, a glowing rail along each edge
    static void Bridge(SkateMeshKit kit, Vector3 a, Vector3 b, float width, Color32 top, Color32 under, List<Vector3[]> rails)
    {
        Vector3 along = b - a, flat = new Vector3(along.x, 0f, along.z);
        Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * (width * 0.5f);
        Vector3 down = Vector3.Cross(along.normalized, side.normalized);
        if (down.y > 0f) down = -down;
        a -= flat.normalized * 0.3f; b += flat.normalized * 0.3f;
        kit.Quad(a - side, b - side, b + side, a + side, Vector3.up, top);
        kit.Quad(a - side + down * 0.7f, b - side + down * 0.7f, b + side + down * 0.7f, a + side + down * 0.7f, Vector3.down, under);
        foreach (var s in new[] { -1f, 1f })
        {
            kit.Quad(a + side * s, b + side * s, b + side * s + down * 0.7f, a + side * s + down * 0.7f, side * s, under);
            Vector3 r0 = a + side * s * 0.92f + Vector3.up * 0.55f + flat.normalized * 1.2f, r1 = b + side * s * 0.92f + Vector3.up * 0.55f - flat.normalized * 1.2f;
            kit.Cylinder(r0, r1, 0.06f, 6, top);
            rails.Add(new[] { r0, r1 });
        }
    }
}
