using UnityEngine;

// A rocket car with a ramp on its back. Ride up the ramp and you're on it, and it's off: flat out the way
// you're looking, Space climbs and C sinks. It only lasts a few seconds before it explodes, throwing you
// up and onward (hit something and it goes up straight away). Right click (or Q) jumps off early. It
// turns up again where it started a few seconds later.
[RequireComponent(typeof(Rigidbody))]
public class HoverCar : MonoBehaviour
{
    public float speed = 38f, climbSpeed = 12f;
    [Tooltip("How far above where it started it can climb.")]
    public float climb = 60f;
    [Tooltip("Seconds from getting on until it explodes.")]
    public float fuse = 4f;
    [Tooltip("Seconds after exploding until it's back where it started.")]
    public float respawn = 6f;
    [Tooltip("Where you stand on it, relative to the car.")]
    public Vector3 seat = new Vector3(0f, 1.7f, -0.8f);
    public ParticleSystem fire, smoke;

    public Vector3 Velocity { get; private set; }
    public bool Flying { get; private set; }
    public bool Exploded { get; private set; }
    public float FuseLeft { get { return fuseLeft; } }

    Rigidbody rb;
    Vector3 home;
    Quaternion homeRot;
    float heading, fuseLeft, downTimer;
    bool ready;

    void Awake() { Init(); }

    public void Init()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        if (!ready) { home = transform.position; homeRot = transform.rotation; ready = true; }
        heading = transform.eulerAngles.y;
    }

    public Vector3 SeatPosition { get { return transform.TransformPoint(seat); } }

    public void Board()
    {
        if (rb == null) Init();
        Flying = true;
        fuseLeft = fuse;
        heading = transform.eulerAngles.y;
        Velocity = transform.forward * speed * 0.5f;
    }

    public void Release() { Flying = false; }

    // one physics step of flying: flat out, turning towards `yaw`, climbing or sinking
    public void Fly(SlideInput inp, float yaw, float dt)
    {
        if (rb == null) Init();
        if (Exploded) return;
        fuseLeft -= dt;
        heading = Mathf.MoveTowardsAngle(heading, yaw, 150f * dt);
        Quaternion q = Quaternion.Euler(0f, heading, 0f);
        Vector3 fwd = q * Vector3.forward;
        float along = Mathf.MoveTowards(Vector3.Dot(Velocity, fwd), speed, 40f * dt);
        float vy = inp.jumpHeld ? climbSpeed : inp.slide ? -climbSpeed : 0f;
        Vector3 v = fwd * along + Vector3.up * vy;
        Vector3 p = rb.position + v * dt;
        p.y = Mathf.Clamp(p.y, home.y, home.y + climb);
        bool crash = Physics.SphereCast(rb.position + Vector3.up * 1f, 1.1f, v.normalized, out RaycastHit hit, v.magnitude * dt + 1.2f, ~0, QueryTriggerInteraction.Ignore)
                     && hit.rigidbody != rb && !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<SlideController>() == null;
        Velocity = (p - rb.position) / Mathf.Max(dt, 1e-4f);
        Move(p, q);
        if (crash || fuseLeft <= 0f) Explode();
    }

    public void Explode()
    {
        if (Exploded) return;
        Exploded = true;
        Flying = false;
        downTimer = respawn;
        if (fire != null) { fire.transform.position = transform.position + Vector3.up; fire.Play(); }
        if (smoke != null) { smoke.transform.position = transform.position + Vector3.up; smoke.Play(); }
        Show(false);
    }

    void Show(bool on)
    {
        foreach (var r in GetComponents<Renderer>()) r.enabled = on;
        foreach (var c in GetComponents<Collider>()) c.enabled = on;
    }

    void Move(Vector3 p, Quaternion q)
    {
        if (Application.isPlaying) { rb.MovePosition(p); rb.MoveRotation(q); }
        else { rb.position = p; rb.rotation = q; transform.SetPositionAndRotation(p, q); }
    }

    // called every physics step when nobody's flying it (public so the tests can run it)
    public void Idle(float dt)
    {
        if (Flying) return;
        Velocity = Vector3.zero;
        if (Exploded)
        {
            downTimer -= dt;
            if (downTimer <= 0f)
            {
                // back where it started
                if (rb == null) Init();
                rb.position = home; rb.rotation = homeRot; transform.SetPositionAndRotation(home, homeRot);
                heading = homeRot.eulerAngles.y;
                Exploded = false;
                Show(true);
            }
        }
        else if ((rb.position - home).sqrMagnitude > 1f)
        {
            // left somewhere after you jumped off it early: blow it up so it comes home
            Explode();
        }
    }

    void FixedUpdate() { Idle(Time.fixedDeltaTime); }
}
