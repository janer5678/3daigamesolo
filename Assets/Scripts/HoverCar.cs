using UnityEngine;

// A flying car with a ramp on its back. Ride up the ramp and you're stuck on it, flying it: it turns
// the way you look, W flies forward (Shift faster), Space climbs and C sinks, up to a ceiling a bit above
// where it started. Right click (or Q) jumps you off; then it hangs there, and after a while goes home.
[RequireComponent(typeof(Rigidbody))]
public class HoverCar : MonoBehaviour
{
    public float speed = 14f, boostSpeed = 20f, climbSpeed = 7f;
    [Tooltip("How far above where it started it can climb.")]
    public float climb = 40f;
    [Tooltip("Where you stand on it, relative to the car.")]
    public Vector3 seat = new Vector3(0f, 2.1f, -0.6f);

    public Vector3 Velocity { get; private set; }
    public bool Flying { get; private set; }

    Rigidbody rb;
    Vector3 home;
    Quaternion homeRot;
    float idle, heading;

    void Awake() { Init(); }

    public void Init()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        home = transform.position;
        homeRot = transform.rotation;
        heading = transform.eulerAngles.y;
    }

    public Vector3 SeatPosition { get { return transform.TransformPoint(seat); } }

    public void Board() { Flying = true; idle = 0f; }
    public void Release() { Flying = false; idle = 0f; }

    // one physics step of flying: forward/back, climb/sink, turning to face `yaw`
    public void Fly(SlideInput inp, float yaw, float dt)
    {
        if (rb == null) Init();
        heading = Mathf.MoveTowardsAngle(heading, yaw, 120f * dt);
        Quaternion q = Quaternion.Euler(0f, heading, 0f);
        Vector3 fwd = q * Vector3.forward;
        float target = inp.forward > 0.1f ? (inp.sprint ? boostSpeed : speed) : inp.forward < -0.1f ? -speed * 0.4f : 0f;
        float along = Vector3.Dot(Velocity, fwd);
        along = Mathf.MoveTowards(along, target, 12f * dt);
        float vy = inp.jumpHeld ? climbSpeed : inp.slide ? -climbSpeed : 0f;
        Vector3 v = fwd * along + Vector3.up * vy;
        Vector3 p = rb.position + v * dt;
        p.y = Mathf.Clamp(p.y, home.y, home.y + climb);
        // don't fly into buildings
        if (Physics.SphereCast(rb.position + Vector3.up * 1f, 1.2f, (p - rb.position).normalized, out RaycastHit hit, (p - rb.position).magnitude + 1.5f, ~0, QueryTriggerInteraction.Ignore)
            && hit.rigidbody != rb && !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<SlideController>() == null)
            p = rb.position;
        Velocity = (p - rb.position) / Mathf.Max(dt, 1e-4f);
        Move(p, q);
    }

    void Move(Vector3 p, Quaternion q)
    {
        if (Application.isPlaying) { rb.MovePosition(p); rb.MoveRotation(q); }
        else { rb.position = p; rb.rotation = q; transform.SetPositionAndRotation(p, q); }
    }

    void FixedUpdate()
    {
        if (Flying) return;
        // left alone: hang there bobbing, and after a while drift home
        idle += Time.fixedDeltaTime;
        Velocity = Vector3.zero;
        if (idle > 15f && (rb.position - home).sqrMagnitude > 1f) { Move(home, homeRot); heading = homeRot.eulerAngles.y; }
    }
}
