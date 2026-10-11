using UnityEngine;

// A grindable rail: the line along its top, in world space. The board locks onto it (see
// SlideController). The player finds them all when it starts.
public class SkateRail : MonoBehaviour
{
    public Vector3[] points;
    [Tooltip("Radius of the bar; the board sits on top of it.")]
    public float radius = 0.05f;

    // nearest point on the rail to p: which segment, and how far along it (0..1)
    public Vector3 Closest(Vector3 p, out int seg, out float u)
    {
        seg = 0; u = 0f;
        Vector3 best = points[0];
        float bestD = float.MaxValue;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i], b = points[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            Vector3 q = a + ab * t;
            float d = (p - q).sqrMagnitude;
            if (d < bestD) { bestD = d; best = q; seg = i; u = t; }
        }
        return best;
    }
}
