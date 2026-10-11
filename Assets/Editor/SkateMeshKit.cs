using System.Collections.Generic;
using UnityEngine;

// Builds vertex-coloured meshes out of boxes, cylinders, cones and blobs for the toon shader.
public class SkateMeshKit
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<Color32> c = new List<Color32>();
    public readonly List<int> t = new List<int>();

    public Matrix4x4 frame = Matrix4x4.identity;

    int Add(Vector3 p, Vector3 normal, Color32 col)
    {
        v.Add(frame.MultiplyPoint3x4(p));
        n.Add(frame.MultiplyVector(normal).normalized);
        c.Add(col);
        return v.Count - 1;
    }

    // a flat quad a-b-c-d (in order round the edge) facing `facing`
    public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 facing, Color32 col)
    {
        Vector3 nrm = Vector3.Cross(b - a, cc - a);
        if (nrm.sqrMagnitude < 1e-10f) nrm = Vector3.Cross(cc - a, d - a);
        if (Vector3.Dot(nrm, facing) < 0f) { var tmp = b; b = d; d = tmp; }
        int i0 = Add(a, facing, col), i1 = Add(b, facing, col), i2 = Add(cc, facing, col), i3 = Add(d, facing, col);
        t.Add(i0); t.Add(i1); t.Add(i2);
        t.Add(i0); t.Add(i2); t.Add(i3);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 cc, Vector3 facing, Color32 col)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, cc - a), facing) < 0f) { var tmp = b; b = cc; cc = tmp; }
        int i0 = Add(a, facing, col), i1 = Add(b, facing, col), i2 = Add(cc, facing, col);
        t.Add(i0); t.Add(i1); t.Add(i2);
    }

    // a quad with its own normal at each corner (for smooth curves); winding from the average
    public void SmoothQuad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Color32 col)
    {
        Vector3 facing = na + nb + nc + nd;
        Vector3 nrm = Vector3.Cross(b - a, cc - a);
        if (nrm.sqrMagnitude < 1e-10f) nrm = Vector3.Cross(cc - a, d - a);
        if (Vector3.Dot(nrm, facing) < 0f)
        {
            var tmp = b; b = d; d = tmp;
            var tn = nb; nb = nd; nd = tn;
        }
        int i0 = Add(a, na, col), i1 = Add(b, nb, col), i2 = Add(cc, nc, col), i3 = Add(d, nd, col);
        t.Add(i0); t.Add(i1); t.Add(i2);
        t.Add(i0); t.Add(i2); t.Add(i3);
    }

    public void Box(Vector3 centre, Vector3 size, Quaternion rot, Color32 col)
    {
        Vector3 e = size * 0.5f;
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var f in axes)
        {
            Vector3 u = Mathf.Abs(f.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 r = Vector3.Cross(u, f);
            Vector3 mid = Vector3.Scale(f, e);
            Vector3 ru = Vector3.Scale(r, e), uu = Vector3.Scale(u, e);
            Quad(centre + rot * (mid - ru - uu), centre + rot * (mid - ru + uu), centre + rot * (mid + ru + uu), centre + rot * (mid + ru - uu), rot * f, col);
        }
    }

    // a round bar from a to b, smooth sided, capped
    public void Cylinder(Vector3 a, Vector3 b, float r, int sides, Color32 col, bool caps = true)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 side2 = Vector3.Cross(axis, side);
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0);
            Vector3 d1 = side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1);
            SmoothQuad(a + d0 * r, b + d0 * r, b + d1 * r, a + d1 * r, d0, d0, d1, d1, col);
            if (caps)
            {
                Tri(a, a + d0 * r, a + d1 * r, -axis, col);
                Tri(b, b + d0 * r, b + d1 * r, axis, col);
            }
        }
    }

    // flat-faceted cone/frustum round the y axis, open at the bottom (trees)
    public void Cone(Vector3 baseAt, float r0, float r1, float height, int sides, Color32 col, float turn)
    {
        for (int s = 0; s < sides; s++)
        {
            float a0 = turn + s * Mathf.PI * 2f / sides, a1 = turn + (s + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 b0 = baseAt + d0 * r0, b1 = baseAt + d1 * r0;
            Vector3 u0 = baseAt + Vector3.up * height + d0 * r1, u1 = baseAt + Vector3.up * height + d1 * r1;
            Vector3 normal = Vector3.Cross(u0 - b0, b1 - b0).normalized;
            if (Vector3.Dot(normal, d0 + d1) < 0) normal = -normal;
            if (r1 > 0.001f) Quad(b0, u0, u1, b1, normal, col);
            else Tri(b0, u0, b1, normal, col);
        }
    }

    public void Ellipsoid(Vector3 centre, Vector3 radii, Quaternion rot, int seg, int rings, Color32 col)
    {
        for (int r = 0; r < rings; r++)
        {
            float p0 = Mathf.PI * r / rings - Mathf.PI / 2f, p1 = Mathf.PI * (r + 1) / rings - Mathf.PI / 2f;
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
                Vector3 u00 = Unit(p0, a0), u01 = Unit(p0, a1), u10 = Unit(p1, a0), u11 = Unit(p1, a1);
                SmoothQuad(centre + rot * Vector3.Scale(u00, radii), centre + rot * Vector3.Scale(u10, radii),
                           centre + rot * Vector3.Scale(u11, radii), centre + rot * Vector3.Scale(u01, radii),
                           rot * Nrm(u00, radii), rot * Nrm(u10, radii), rot * Nrm(u11, radii), rot * Nrm(u01, radii), col);
            }
        }
    }

    static Vector3 Unit(float pitch, float yaw)
    {
        return new Vector3(Mathf.Cos(pitch) * Mathf.Cos(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Sin(yaw));
    }

    static Vector3 Nrm(Vector3 u, Vector3 radii)
    {
        return new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z).normalized;
    }

    public bool Empty { get { return v.Count == 0; } }

    public Mesh Build(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
