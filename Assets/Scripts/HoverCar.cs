using UnityEngine;

// A flying car you can take: land on its roof and it's yours, off the way you're looking. Steer with the
// mouse, W faster, S slower, Space climbs, C sinks. It only holds together for a few seconds, then it
// breaks apart and throws you up and onward (hit something and it goes straight away). Right click (or Q)
// jumps off early. It turns up again where it was parked a few seconds later.
[RequireComponent(typeof(Rigidbody))]
public class HoverCar : MonoBehaviour
{
    public float speed = 22f, fastSpeed = 30f, slowSpeed = 12f, climbSpeed = 12f;
    [Tooltip("How far above where it was parked it can climb.")]
    public float climb = 50f;
    [Tooltip("Seconds from getting on until it breaks.")]
    public float fuse = 5f;
    [Tooltip("Seconds after breaking until it's back where it was parked.")]
    public float respawn = 6f;
    [Tooltip("Where you stand on it, relative to the car.")]
    public Vector3 seat = new Vector3(0f, 1.7f, 0f);
    public ParticleSystem fire, smoke;

    public Vector3 Velocity { get; private set; }
    public bool Flying { get; private set; }
    public bool Exploded { get; private set; }
    public float FuseLeft { get { return fuseLeft; } }
    public string CrashedInto { get; private set; }

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

    // you've landed on it: it turns to face where you're looking and goes
    public void Board(float yaw)
    {
        if (rb == null) Init();
        Flying = true;
        fuseLeft = fuse;
        heading = yaw;
        Velocity = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * 0.5f;
    }

    public void Release() { Flying = false; }

    // park it somewhere new (the tests use it)
    public void Park(Vector3 p, Quaternion q)
    {
        if (rb == null) Init();
        home = p; homeRot = q;
        ResetHome();
    }

    // straight back where it was parked, whole again (the tests use it)
    public void ResetHome()
    {
        if (rb == null) Init();
        Flying = false; Exploded = false; Velocity = Vector3.zero; downTimer = 0f; CrashedInto = null;
        rb.position = home; rb.rotation = homeRot; transform.SetPositionAndRotation(home, homeRot);
        heading = homeRot.eulerAngles.y;
        Show(true);
    }

    // one physics step of flying: on the way you're looking, climbing or sinking
    public void Fly(SlideInput inp, float yaw, float dt)
    {
        if (rb == null) Init();
        if (Exploded) return;
        fuseLeft -= dt;
        heading = Mathf.MoveTowardsAngle(heading, yaw, 160f * dt);
        Quaternion q = Quaternion.Euler(0f, heading, 0f);
        Vector3 fwd = q * Vector3.forward;
        float want = inp.forward > 0.5f ? fastSpeed : inp.forward < -0.5f ? slowSpeed : speed;
        float along = Mathf.MoveTowards(Vector3.Dot(Velocity, fwd), want, 30f * dt);
        float vy = inp.jumpHeld ? climbSpeed : inp.slide ? -climbSpeed : 0f;
        Vector3 v = fwd * along + Vector3.up * vy;
        Vector3 p = rb.position + v * dt;
        p.y = Mathf.Clamp(p.y, 0.3f, home.y + climb);
        bool crash = Physics.SphereCast(rb.position + Vector3.up * 1f, 1.1f, v.normalized, out RaycastHit hit, v.magnitude * dt + 1.2f, ~0, QueryTriggerInteraction.Ignore)
                     && hit.rigidbody != rb && !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<SlideController>() == null;
        Velocity = (p - rb.position) / Mathf.Max(dt, 1e-4f);
        Move(p, q);
        if (crash) CrashedInto = hit.collider.name + " at " + hit.point.ToString("F1");
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
        if (rb == null) Init();
        if (Exploded)
        {
            downTimer -= dt;
            if (downTimer <= 0f)
            {
                // back where it was parked
                rb.position = home; rb.rotation = homeRot; transform.SetPositionAndRotation(home, homeRot);
                heading = homeRot.eulerAngles.y;
                Exploded = false;
                Show(true);
            }
        }
        else if ((rb.position - home).sqrMagnitude > 1f)
        {
            // left somewhere after you jumped off it early: it falls apart, and comes back
            Explode();
        }
    }

    void FixedUpdate() { Idle(Time.fixedDeltaTime); }
}
