using UnityEngine;

// Drives a vehicle (car, bus, train) along a path: round and round a closed loop, or back and forth
// along a line. It's a kinematic body, so it shoves you out of the way, and you can ride on its roof:
// the player picks up its Velocity while standing on it.
[RequireComponent(typeof(Rigidbody))]
public class Mover : MonoBehaviour
{
    public Vector3[] path;
    public bool loop = true;
    public float speed = 10f;
    [Tooltip("Where along the path it starts, in metres.")]
    public float startAt;
    [Tooltip("Back and forth only: how long it waits at each end.")]
    public float wait = 2f;
    [Tooltip("Back and forth only: slow down over this many metres coming into each end.")]
    public float easeIn = 25f;
    [Tooltip("Face the way it's going (cars); off for a train that just runs back and forth.")]
    public bool turn = true;
    [Tooltip("Back and forth only: places along the way (metres from the start) it stops at for ` wait ` seconds, like a station.")]
    public float[] stops = new float[0];

    public Vector3 Velocity { get; private set; }

    Rigidbody rb;
    float[] lengths;
    float total, at, dirSign = 1f, waitTimer;
    Vector3 lastTarget;
    float lastStop = -1f;

    void Awake() { Init(); }

    public void Init()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        int n = loop ? path.Length : path.Length - 1;
        lengths = new float[n];
        total = 0f;
        for (int i = 0; i < n; i++) { lengths[i] = Vector3.Distance(path[i], path[(i + 1) % path.Length]); total += lengths[i]; }
        at = Mathf.Repeat(startAt, total);
        dirSign = 1f; waitTimer = 0f; lastStop = -1f;
        lastTarget = Place(at, true);
    }

    void FixedUpdate() { Step(Time.fixedDeltaTime); }

    // one physics step along the path (public so the editor tests can drive it)
    public void Step(float dt)
    {
        if (lengths == null) Init();
        if (loop) at = Mathf.Repeat(at + speed * dt, total);
        else if (waitTimer > 0f) waitTimer -= dt;
        else
        {
            // the next place to stop: a station ahead of us, or the end of the line
            float target = dirSign > 0f ? total : 0f;
            foreach (var s0 in stops)
                if (Mathf.Abs(s0 - lastStop) > 0.5f && (s0 - at) * dirSign > 0f && Mathf.Abs(s0 - at) < Mathf.Abs(target - at)) target = s0;
            float toGo = Mathf.Abs(target - at);
            float s = speed * Mathf.Clamp(toGo / easeIn, 0.15f, 1f);
            at += dirSign * s * dt;
            if ((target - at) * dirSign <= 0f)
            {
                at = target;
                waitTimer = wait;
                lastStop = target;
                if (target <= 0f || target >= total) dirSign = -dirSign;
            }
        }
        Vector3 p = Place(at, false);
        // (from where it's been sent, not rb.position: a kinematic body's MovePosition only lands on the next physics step)
        Velocity = (p - lastTarget) / Mathf.Max(dt, 1e-4f);
        lastTarget = p;
    }

    Vector3 Place(float d, bool snap)
    {
        float left = d;
        int i = 0;
        while (i < lengths.Length - 1 && left > lengths[i]) { left -= lengths[i]; i++; }
        Vector3 a = path[i], b = path[(i + 1) % path.Length];
        Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(left / Mathf.Max(lengths[i], 1e-4f)));
        Vector3 dir = (b - a); dir.y = 0f;
        if (!loop && dirSign < 0f) dir = -dir;
        Quaternion q = turn && dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized) : transform.rotation;
        // turn corners smoothly rather than snapping round
        if (!snap) q = Quaternion.RotateTowards(rb.rotation, q, 120f * Time.fixedDeltaTime);
        if (snap || !Application.isPlaying) { rb.position = p; rb.rotation = q; transform.SetPositionAndRotation(p, q); }
        else { rb.MovePosition(p); rb.MoveRotation(q); }
        return p;
    }
}
