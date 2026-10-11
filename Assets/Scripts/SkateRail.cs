using System.Collections.Generic;
using UnityEngine;

// Anything you can grind: a polyline in world space. Rails have no collider - the board snaps
// onto them by distance (see SkateController), which is far more forgiving than physics contact.
public class SkateRail : MonoBehaviour
{
    public static readonly List<SkateRail> All = new List<SkateRail>();

    [Tooltip("World-space points along the top of the rail, in order.")]
    public Vector3[] points;
    [Tooltip("A ramp's coping: snaps on even when you hit it going mostly upwards.")]
    public bool coping;
    public string displayName = "Rail";

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public int SegmentCount { get { return points == null ? 0 : points.Length - 1; } }

    // nearest point on the whole polyline; seg/t say where it is
    public Vector3 Closest(Vector3 p, out int seg, out float t)
    {
        seg = 0; t = 0f;
        float best = float.MaxValue;
        Vector3 bestPoint = points[0];
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i], b = points[i + 1];
            Vector3 ab = b - a;
            float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector3 q = a + ab * u;
            float d = (q - p).sqrMagnitude;
            if (d < best) { best = d; bestPoint = q; seg = i; t = u; }
        }
        return bestPoint;
    }

    public Vector3 Direction(int seg) { return (points[seg + 1] - points[seg]).normalized; }
    public float SegmentLength(int seg) { return Vector3.Distance(points[seg], points[seg + 1]); }

    void OnDrawGizmos()
    {
        if (points == null) return;
        Gizmos.color = coping ? Color.cyan : Color.magenta;
        for (int i = 0; i < points.Length - 1; i++) Gizmos.DrawLine(points[i], points[i + 1]);
    }
}
