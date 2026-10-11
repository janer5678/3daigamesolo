using System.Collections.Generic;
using UnityEngine;

// Builds textured meshes for the PSX look: like SkateMeshKit, but every vertex has a uv. Boxes and
// cylinders tile their texture by world size (`tile` metres per repeat), so a facade texture made for
// one 4 x 3.2 m bay repeats bay by bay; panels map one whole texture (signs, billboards).
public class PSXKit
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<Color32> c = new List<Color32>();
    public readonly List<Vector2> uv = new List<Vector2>();
    public readonly List<int> t = new List<int>();

    static readonly Color32 White = new Color32(255, 255, 255, 255);

    int Add(Vector3 p, Vector3 normal, Color32 col, Vector2 u)
    {
        v.Add(p); n.Add(normal.normalized); c.Add(col); uv.Add(u);
        return v.Count - 1;
    }

    // a quad a-b-c-d (round the edge) facing `facing`, with its own uvs
    public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing, Color32? col = null)
    {
        Color32 k = col ?? White;
        Vector3 nrm = Vector3.Cross(b - a, cc - a);
        if (nrm.sqrMagnitude < 1e-12f) nrm = Vector3.Cross(cc - a, d - a);
        if (Vector3.Dot(nrm, facing) < 0f) { var tb = b; b = d; d = tb; var tu = ub; ub = ud; ud = tu; }
        int i0 = Add(a, facing, k, ua), i1 = Add(b, facing, k, ub), i2 = Add(cc, facing, k, uc), i3 = Add(d, facing, k, ud);
        t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i0); t.Add(i2); t.Add(i3);
    }

    // a flat panel showing one whole texture (or a part of it): centre, half-width along `right`,
    // half-height along `up`; it faces Cross(up, right)
    public void Panel(Vector3 centre, Vector3 right, Vector3 up, Color32? col = null, bool twoSided = false, Rect? uvRect = null)
    {
        Rect r = uvRect ?? new Rect(0f, 0f, 1f, 1f);
        Vector3 f = Vector3.Cross(up, right);
        Quad(centre - right - up, centre - right + up, centre + right + up, centre + right - up,
             new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), f, col);
        if (twoSided)
            Quad(centre + right - up, centre + right + up, centre - right + up, centre - right - up,
                 new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), -f, col);
    }

    // a box; each face tiles the texture every `tileU` x `tileV` metres (side faces: across x height)
    public void Box(Vector3 centre, Vector3 size, Quaternion rot, float tileU, float tileV, Color32? col = null, bool top = true, bool bottom = false)
    {
        Vector3 e = size * 0.5f;
        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
        foreach (var f in faces)
        {
            if (f == Vector3.up && !top) continue;
            if (f == Vector3.down && !bottom) continue;
            Vector3 up = Mathf.Abs(f.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, f);   // so that Cross(up, right) = f
            right = -right;
            float w = Mathf.Abs(Vector3.Dot(size, right)), h = Mathf.Abs(Vector3.Dot(size, up));
            Vector3 mid = Vector3.Scale(f, e);
            Vector3 ru = right * (w / 2f), uu = up * (h / 2f);
            float u1 = w / tileU, v1 = h / tileV;
            Quad(centre + rot * (mid - ru - uu), centre + rot * (mid - ru + uu), centre + rot * (mid + ru + uu), centre + rot * (mid + ru - uu),
                 new Vector2(0f, 0f), new Vector2(0f, v1), new Vector2(u1, v1), new Vector2(u1, 0f), rot * f, col);
        }
    }

    // a flat ground patch from (x0,z0) to (x1,z1) at height y, split into cells so the texture warp
    // stays small, tiling every `tile` metres
    public void Ground(float x0, float z0, float x1, float z1, float y, float tile, float cell = 8f, Color32? col = null)
    {
        int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / cell)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / cell));
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float xa = Mathf.Lerp(x0, x1, i / (float)nx), xb = Mathf.Lerp(x0, x1, (i + 1) / (float)nx);
                float za = Mathf.Lerp(z0, z1, j / (float)nz), zb = Mathf.Lerp(z0, z1, (j + 1) / (float)nz);
                Quad(new Vector3(xa, y, za), new Vector3(xa, y, zb), new Vector3(xb, y, zb), new Vector3(xb, y, za),
                     new Vector2(xa / tile, za / tile), new Vector2(xa / tile, zb / tile), new Vector2(xb / tile, zb / tile), new Vector2(xb / tile, za / tile), Vector3.up, col);
            }
    }

    // a round bar from a to b, the texture wrapping once round it and tiling along it
    public void Cylinder(Vector3 a, Vector3 b, float r, int sides, float tileAlong, Color32? col = null)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 side2 = Vector3.Cross(axis, side);
        float len = Vector3.Distance(a, b) / tileAlong;
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0), d1 = side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1);
            Quad(a + d0 * r, b + d0 * r, b + d1 * r, a + d1 * r, new Vector2(s / (float)sides, 0f), new Vector2(s / (float)sides, len),
                 new Vector2((s + 1f) / sides, len), new Vector2((s + 1f) / sides, 0f), d0 + d1, col);
        }
    }

    // a tube along a path (rails, pipes, cables)
    public void Tube(IList<Vector3> pts, float r, int sides, float tileAlong, Color32? col = null)
    {
        for (int i = 0; i < pts.Count - 1; i++) Cylinder(pts[i], pts[i + 1], r, sides, tileAlong, col);
    }

    // an ellipsoid (heads, balloons, capsules)
    public void Ellipsoid(Vector3 centre, Vector3 radii, int seg, int rings, Color32? col = null)
    {
        for (int r = 0; r < rings; r++)
        {
            float p0 = Mathf.PI * r / rings - Mathf.PI / 2f, p1 = Mathf.PI * (r + 1) / rings - Mathf.PI / 2f;
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
                Vector3 u00 = U(p0, a0), u01 = U(p0, a1), u10 = U(p1, a0), u11 = U(p1, a1);
                Quad(centre + Vector3.Scale(u00, radii), centre + Vector3.Scale(u10, radii), centre + Vector3.Scale(u11, radii), centre + Vector3.Scale(u01, radii),
                     new Vector2(s / (float)seg, r / (float)rings), new Vector2(s / (float)seg, (r + 1f) / rings),
                     new Vector2((s + 1f) / seg, (r + 1f) / rings), new Vector2((s + 1f) / seg, r / (float)rings), u00 + u11, col);
            }
        }
    }

    static Vector3 U(float pitch, float yaw) { return new Vector3(Mathf.Cos(pitch) * Mathf.Cos(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Sin(yaw)); }

    public bool Empty { get { return v.Count == 0; } }

    public Mesh Build(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
