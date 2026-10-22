using UnityEngine;

// The Circulation level, after the Renai Circulation video: circles drawn on a white floor (two of them bowls).
// Carve a full lap round one, staying on its ring (or, in a bowl, up on its wall), and it lights up and gives you
// a burst of speed; cut across it and it doesn't count. Light them all. Also draws the video's doubled pastel
// silhouettes over the frame.
[DefaultExecutionOrder(100)]
[ExecuteAlways]   // (so the review shots get the pastel look too)
public class CirculationLevel : MonoBehaviour
{
    [System.Serializable]
    public class Circle
    {
        public Vector3 centre;
        public float radius, band;   // a lap counts while you're between radius - band and radius + band from the centre
        public GameObject litDisc;
        [System.NonSerialized] public float turned;   // degrees round, this lap
        [System.NonSerialized] public float lastAngle;
        [System.NonSerialized] public bool inBand, lit;
    }

    public SlideController player;
    public Circle[] circles;
    public Material post;
    [Tooltip("Lighting a circle speeds you up by this much (up to `boostCap`).")]
    public float boost = 1.3f, boostCap = 30f;

    public int Lit { get; private set; }
    Font font;

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        font = Resources.Load<Font>("Bangers");
        if (Application.isPlaying) foreach (var c in circles) if (c.litDisc != null) c.litDisc.SetActive(false);
    }

    void FixedUpdate() { if (Application.isPlaying) Tick(Time.fixedDeltaTime); }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (post != null) Graphics.Blit(src, dst, post); else Graphics.Blit(src, dst);
    }

    // follows you round the circles (public so the tests can run it)
    public void Tick(float dt)
    {
        Vector3 p = player.transform.position;
        foreach (var c in circles)
        {
            if (c.lit) continue;
            Vector3 d = p - c.centre; d.y = 0f;
            float r = d.magnitude;
            bool inside = r > c.radius - c.band && r < c.radius + c.band && p.y < c.centre.y + 6f;
            float a = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
            if (inside && c.inBand) c.turned += Mathf.DeltaAngle(c.lastAngle, a);
            else c.turned = 0f;
            c.inBand = inside;
            c.lastAngle = a;
            if (Mathf.Abs(c.turned) >= 360f) Light(c);
        }
    }

    void Light(Circle c)
    {
        c.lit = true;
        Lit++;
        if (c.litDisc != null) c.litDisc.SetActive(true);
        var rb = player.GetComponent<Rigidbody>();
        Vector3 v = rb.linearVelocity, flat = new Vector3(v.x, 0f, v.z);
        float s = Mathf.Min(Mathf.Max(flat.magnitude * boost, flat.magnitude + 4f), boostCap);
        if (flat.sqrMagnitude > 0.01f) rb.linearVelocity = flat.normalized * s + Vector3.up * v.y;
        SlideHUD.Flash(Lit == circles.Length ? "ALL THE CIRCLES <3" : "CIRCLE " + Lit + " / " + circles.Length);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        float k = Screen.height / 1080f;
        var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * k), alignment = TextAnchor.UpperLeft };
        if (font != null) st.font = font;
        string text = "CIRCLES " + Lit + " / " + circles.Length;
        var r = new Rect(30 * k, 24 * k, 700 * k, 60 * k);
        st.normal.textColor = new Color(0.74f, 0.84f, 1f);
        GUI.Label(new Rect(r.x + 4 * k, r.y + 3 * k, r.width, r.height), text, st);
        st.normal.textColor = new Color(0.93f, 0.45f, 0.6f);
        GUI.Label(r, text, st);
    }
}
