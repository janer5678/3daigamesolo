using System.Collections.Generic;
using UnityEngine;

// The Infinity level, built as you go so it never ends: an endless lattice of fractal towers, layer under
// layer, each layer offset half a cell from the one above. Every tower is a plaza with four arms flaring up
// into curved banks, a spire hanging under it, and from each of its four corners a bridge down to the tower
// diagonally below it, which has four of its own, and so on for ever, in every direction. Towers are built
// ahead of you and cleared away behind; if you fall you're put back on the last tower you stood on.
public class InfinityWorld : MonoBehaviour
{
    public Material fractal;
    public SlideController player;
    [Tooltip("Distance between towers in a layer, and the drop from one layer to the next.")]
    public float cell = 56f, drop = 11f;
    [Tooltip("How many towers out from you to build, layers above you and below you.")]
    public int radius = 3, above = 2, below = 5;
    public Transform checkpoint;

    readonly Dictionary<Vector3Int, GameObject> towers = new Dictionary<Vector3Int, GameObject>();
    int deepest;

    public int TowerCount { get { return towers.Count; } }

    // ---- where things are --------------------------------------------------------------------------------

    public Vector3 Offset(int k) { return (k & 1) != 0 ? new Vector3(cell * 0.5f, 0f, cell * 0.5f) : Vector3.zero; }

    public Vector3 Centre(int i, int j, int k) { return new Vector3(i * cell, -k * drop, j * cell) + Offset(k); }

    public int LayerAt(float y) { return Mathf.RoundToInt(-y / drop); }

    // a repeatable random number 0..1 for a tower
    public static float Hash(int i, int j, int k, int salt)
    {
        unchecked
        {
            uint h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(k * 83492791) ^ (uint)(salt * 2654435761u);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    public float Width(int i, int j, int k) { return 15f + 5f * Hash(i, j, k, 1); }

    // the tower nearest p in its layer
    public Vector3Int Nearest(Vector3 p)
    {
        int k = LayerAt(p.y);
        Vector3 o = Offset(k);
        return new Vector3Int(Mathf.RoundToInt((p.x - o.x) / cell), Mathf.RoundToInt((p.z - o.z) / cell), k);
    }

    // ---- building and clearing -----------------------------------------------------------------------------

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        GenerateAround(player.transform.position, true);
    }

    void Update()
    {
        if (player == null) return;
        GenerateAround(player.transform.position, false);
        Track();
    }

    // builds what's missing round p (nearest first; a few a frame unless `all`) and clears what's far away
    public void GenerateAround(Vector3 p, bool all)
    {
        int k0 = LayerAt(p.y);
        var missing = new List<Vector3Int>();
        for (int k = k0 - above; k <= k0 + below; k++)
        {
            Vector3 o = Offset(k);
            int ci = Mathf.RoundToInt((p.x - o.x) / cell), cj = Mathf.RoundToInt((p.z - o.z) / cell);
            for (int i = ci - radius; i <= ci + radius; i++)
                for (int j = cj - radius; j <= cj + radius; j++)
                {
                    var key = new Vector3Int(i, j, k);
                    if (!towers.ContainsKey(key)) missing.Add(key);
                }
        }
        missing.Sort((a, b) => (Centre(a.x, a.y, a.z) - p).sqrMagnitude.CompareTo((Centre(b.x, b.y, b.z) - p).sqrMagnitude));
        int budget = all ? missing.Count : 6;
        for (int n = 0; n < missing.Count && n < budget; n++) towers[missing[n]] = Build(missing[n].x, missing[n].y, missing[n].z);
        // clear the far ones
        var gone = new List<Vector3Int>();
        foreach (var kv in towers)
        {
            var key = kv.Key;
            Vector3 o = Offset(key.z);
            int ci = Mathf.RoundToInt((p.x - o.x) / cell), cj = Mathf.RoundToInt((p.z - o.z) / cell);
            if (key.z < k0 - above - 1 || key.z > k0 + below + 1 || Mathf.Abs(key.x - ci) > radius + 1 || Mathf.Abs(key.y - cj) > radius + 1) gone.Add(key);
        }
        foreach (var key in gone)
        {
            var go = towers[key];
            towers.Remove(key);
            if (go != null)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) DestroyNow(mf.sharedMesh);
                DestroyNow(go);
            }
        }
        if ((budget > 0 && missing.Count > 0) || gone.Count > 0) player.Setup();   // (it finds the rails that came and went)
    }

    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    // the last tower you stood on is where you go back to if you fall; and how deep you've got
    void Track()
    {
        if (!player.Grounded) return;
        var key = Nearest(player.transform.position);
        Vector3 c = Centre(key.x, key.y, key.z);
        float w = Width(key.x, key.y, key.z);
        Vector3 p = player.transform.position;
        if (Mathf.Abs(p.x - c.x) < w * 0.5f && Mathf.Abs(p.z - c.z) < w * 0.5f && Mathf.Abs(p.y - 0.35f - c.y) < 1f)
        {
            if (checkpoint == null) checkpoint = new GameObject("Last tower").transform;
            checkpoint.SetPositionAndRotation(c + Vector3.up * 0.2f, Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
            player.checkpoint = checkpoint;
            player.fallLimit = c.y - drop * 6f;
            if (key.z > deepest && key.z % 5 == 0) SlideHUD.Flash("DEPTH " + key.z);
            deepest = Mathf.Max(deepest, key.z);
        }
    }

    // ---- one tower ------------------------------------------------------------------------------------------

    GameObject Build(int i, int j, int k)
    {
        var kit = new SkateMeshKit();
        Vector3 c = Centre(i, j, k);
        float w = Width(i, j, k), h = w * 0.5f;
        byte hue = (byte)(Hash(i, j, k, 2) * 255f);
        System.Func<float, Color32> shade = b => { byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 30, 255); return new Color32(v, v, v, hue); };
        var go = new GameObject("Tower " + i + "," + j + "," + k, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        var rails = new List<Vector3[]>();

        // the plaza
        kit.Box(c + Vector3.down * 0.5f, new Vector3(w, 1f, w), Quaternion.identity, shade(1f));
        // the four arms, flaring up into banks with a coping to grind
        float armLen = w * 0.45f, armW = w * 0.4f, r = Mathf.Max(4.5f, w * 0.32f);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            Vector3 d = q * Vector3.forward, side = q * Vector3.right;
            kit.Box(c + d * (h + armLen * 0.5f) + Vector3.down * 0.5f, new Vector3(armW, 1f, armLen), q, shade(0.95f));
            Flare(kit, c + d * (h + armLen), d, side, armW, r, shade(0.95f), shade(0.65f), rails);
        }
        // the spire hanging underneath, swaying (decoration)
        kit.wobble = 1f;
        int rings = 6;
        for (int n = 0; n < rings; n++)
        {
            float t0 = n / (float)rings, t1 = (n + 1f) / rings;
            float y0 = c.y - 1f - t0 * drop * 0.85f, y1 = c.y - 1f - t1 * drop * 0.85f;
            float r0 = h * (1f - t0) * 0.9f, r1 = h * (1f - t1) * 0.9f;
            float twist = n * 0.35f;
            for (int s = 0; s < 4; s++)
            {
                float a0 = s * Mathf.PI * 0.5f + twist, a1 = a0 + Mathf.PI * 0.5f;
                Vector3 p00 = c + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), p01 = c + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                Vector3 p10 = c + new Vector3(Mathf.Cos(a0 + 0.35f) * r1, 0f, Mathf.Sin(a0 + 0.35f) * r1), p11 = c + new Vector3(Mathf.Cos(a1 + 0.35f) * r1, 0f, Mathf.Sin(a1 + 0.35f) * r1);
                p00.y = p01.y = y0; p10.y = p11.y = y1;
                kit.Quad(p00, p01, p11, p10, new Vector3(Mathf.Cos(a0 + 0.78f), -0.4f, Mathf.Sin(a0 + 0.78f)), shade(0.8f - n * 0.08f));
            }
        }
        // little copies of the tower round the spire: the fractal going on down
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f + 45f, 0f);
            Vector3 m = c + q * Vector3.forward * h * 0.75f + Vector3.down * (drop * 0.35f);
            kit.Box(m, new Vector3(w * 0.18f, 0.4f, w * 0.18f), q, shade(0.9f));
            kit.Box(m + Vector3.down * 1.4f, new Vector3(w * 0.05f, 2.8f, w * 0.05f), q, shade(0.7f));
        }
        kit.wobble = 0f;
        // a bridge from each corner down to the tower diagonally below (on the right-hand side of the corner,
        // so it passes the one coming down into this corner from above)
        foreach (var diag in new[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) })
        {
            Vector3 dd = new Vector3(diag.x, 0f, diag.y);
            int ci = diag.x > 0 ? ((k & 1) != 0 ? i + 1 : i) : ((k & 1) != 0 ? i : i - 1);
            int cj = diag.y > 0 ? ((k & 1) != 0 ? j + 1 : j) : ((k & 1) != 0 ? j : j - 1);
            Vector3 cc = Centre(ci, cj, k + 1);
            float ch = Width(ci, cj, k + 1) * 0.5f;
            Vector3 flat = dd.normalized, right = Vector3.Cross(Vector3.up, flat) * 1.9f;
            Vector3 a = c + dd * (h - 2.4f) + right, b = cc - dd * (ch - 2.4f) + right;
            Bridge(kit, a, b, 3.2f, shade(0.9f), shade(0.5f), rails);
        }
        var mesh = kit.Build(go.name);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = fractal;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        foreach (var pts in rails)
        {
            var rg = new GameObject("Rail");
            rg.transform.SetParent(go.transform, false);
            rg.AddComponent<SkateRail>().points = pts;
        }
        // now and then a kaleidoscope star turning in the gap beside it
        if (Hash(i, j, k, 3) < 0.35f)
        {
            var star = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer), typeof(Spin));
            star.transform.SetParent(go.transform, false);
            star.transform.position = c + new Vector3(cell * 0.5f, -drop * 0.5f + 4f, 0f);
            var sk = new SkateMeshKit();
            for (int s = 0; s < 8; s++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, s * 45f);
                sk.Tri(Vector3.zero, q * new Vector3(-0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), Vector3.back, new Color32(255, 255, 255, hue));
                sk.Tri(Vector3.zero, q * new Vector3(0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), Vector3.back, new Color32(200, 200, 200, hue));
                sk.Tri(Vector3.zero, q * new Vector3(-0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), Vector3.forward, new Color32(255, 255, 255, hue));
                sk.Tri(Vector3.zero, q * new Vector3(0.8f, 2f, 0f), q * new Vector3(0f, 5f, 0f), Vector3.forward, new Color32(200, 200, 200, hue));
            }
            star.GetComponent<MeshFilter>().sharedMesh = sk.Build("Star");
            star.GetComponent<MeshRenderer>().sharedMaterial = fractal;
            star.GetComponent<Spin>().degreesPerSecond = 25f + 40f * Hash(i, j, k, 4);
        }
        return go;
    }

    // a curved bank at the end of an arm: up from flat to vertical, a short deck at the top, coping on it
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

    // a bridge sloping down from one plaza to the next, a glowing rail along each edge
    static void Bridge(SkateMeshKit kit, Vector3 a, Vector3 b, float width, Color32 top, Color32 under, List<Vector3[]> rails)
    {
        Vector3 along = b - a, flat = new Vector3(along.x, 0f, along.z);
        Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * (width * 0.5f);
        Vector3 down = Vector3.Cross(along.normalized, side.normalized);
        if (down.y > 0f) down = -down;
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
