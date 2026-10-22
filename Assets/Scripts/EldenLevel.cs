using UnityEngine;

// Elden Ruins, after the Elden Ring gameplay preview, as a parkour course: across a meadow of ruins, along a broken
// aqueduct over a misty gorge, up a castle wall's scaffolding, along the wall-walk and up the outside of the keep.
// Sites of Grace are the checkpoints (touch one: LOST GRACE DISCOVERED, and that's where you come back); fall into the
// gorge, or from too high, and YOU DIED, back at the last grace. Messages left on the ground by other players
// (orange scrawls) teach the moves as you get to them. Each area's name comes up as you enter it, and the top of the
// keep is the goal.
[DefaultExecutionOrder(100)]
public class EldenLevel : MonoBehaviour
{
    [System.Serializable]
    public class Grace
    {
        public string name;
        public Transform spot;
        public float radius = 3f;
        [System.NonSerialized] public bool found;
    }

    [System.Serializable]
    public class Message
    {
        public Vector3 at;
        public string text;
        public float radius = 3.5f;
    }

    [System.Serializable]
    public class Area
    {
        public string name;
        public Bounds bounds;
        [System.NonSerialized] public bool shown;
    }

    public SlideController player;
    public Grace[] graces;
    public Message[] messages;
    public Area[] areas;
    public Bounds goal;
    [Tooltip("Below this you've fallen into the gorge.")]
    public float deathY = -14f;
    [Tooltip("Land after falling further than this and you die.")]
    public float deadlyFall = 15f;

    public int Deaths { get; private set; }
    public bool Dying { get { return dying > 0f; } }
    public bool Done { get; private set; }
    public float RunTime { get; private set; }
    public int GracesFound { get { int n = 0; foreach (var g in graces) if (g.found) n++; return n; } }

    float dying, fade;
    float highest;          // the highest you've been since you were last on the ground
    bool wasGrounded = true;
    string banner, bannerSub;
    Color bannerCol;
    float bannerTimer, bannerLength;
    Message reading;
    Font serif;
    Texture2D white;

    static readonly Color Gold = new Color(0.93f, 0.8f, 0.48f), Red = new Color(0.72f, 0.08f, 0.06f);

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        serif = Resources.Load<Font>("Cinzel");
        if (graces != null && graces.Length > 0) { graces[0].found = true; player.checkpoint = graces[0].spot; }
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    void Update()
    {
        bannerTimer -= Time.deltaTime;
        fade = Mathf.MoveTowards(fade, dying > 0f && dying < 0.8f ? 1f : 0f, Time.deltaTime * 2.5f);
    }

    void Show(string text, string sub, Color col, float length)
    {
        banner = text; bannerSub = sub; bannerCol = col; bannerTimer = bannerLength = length;
    }

    // one physics step (public so the tests can run it)
    public void Tick(float dt)
    {
        if (player == null) return;
        if (dying > 0f)
        {
            dying -= dt;
            if (dying <= 0f) { player.Respawn(); highest = player.transform.position.y; wasGrounded = true; }
            return;
        }
        if (!Done) RunTime += dt;
        Vector3 p = player.transform.position;

        // falling: into the gorge, or landing from too high
        if (!player.Grounded && !player.Climbing) highest = Mathf.Max(highest, p.y);
        bool land = player.Grounded && !wasGrounded;
        if (p.y < deathY || (land && highest - p.y > deadlyFall)) { Die(); return; }
        if (player.Grounded || player.Climbing || player.WallRunning) highest = p.y;
        wasGrounded = player.Grounded;

        foreach (var g in graces)
        {
            if (Vector3.Distance(p, g.spot.position) > g.radius) continue;
            if (player.checkpoint != g.spot) player.checkpoint = g.spot;
            if (!g.found) { g.found = true; Show("LOST GRACE DISCOVERED", g.name, Gold, 3.5f); }
        }

        reading = null;
        float best = float.MaxValue;
        if (messages != null)
            foreach (var m in messages)
            {
                float d = Vector3.Distance(p, m.at);
                if (d < m.radius && d < best) { best = d; reading = m; }
            }

        if (areas != null)
            foreach (var a in areas)
                if (!a.shown && a.bounds.Contains(p)) { a.shown = true; if (bannerTimer <= 0f || banner == null) Show(a.name, null, new Color(0.9f, 0.88f, 0.82f), 4f); }

        if (!Done && goal.Contains(p))
        {
            Done = true;
            int m = Mathf.FloorToInt(RunTime / 60f);
            Show("SUMMIT REACHED", m + ":" + (RunTime - m * 60f).ToString("00.0") + "    deaths " + Deaths, Gold, 8f);
        }
    }

    void Die()
    {
        Deaths++;
        dying = 3.2f;
        Show("YOU DIED", null, Red, 3f);
    }

    // ---- drawing ------------------------------------------------------------------------------------------

    void Rect(Rect r, Color c)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old;
    }

    void Text(Rect r, string s, Font f, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
    {
        var st = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a };
        st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.7f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (fade > 0f) Rect(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, fade));

        // the banner: a dark band across the middle, the words in a big serif, fading in and out
        if (bannerTimer > 0f && banner != null)
        {
            float t = bannerLength - bannerTimer;
            float a = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01(bannerTimer / 0.8f);
            bool died = bannerCol == Red;
            float bandH = (died ? 190f : 150f) * k;
            float y = H * 0.5f - bandH * 0.5f - (died ? 0f : 60f * k);
            // the band fades out at its top and bottom edges
            for (int i = 0; i < 12; i++)
            {
                float f = i / 12f;
                float edge = Mathf.Sin(f * Mathf.PI);
                Rect(new Rect(0, y + f * bandH, W, bandH / 12f + 1), new Color(0f, 0f, 0f, 0.62f * a * edge));
            }
            Color c = bannerCol; c.a = a;
            float scale = died ? 1f + t * 0.03f : 1f;   // YOU DIED slowly swells
            Text(new Rect(0, y, W, bandH), banner, serif, Mathf.RoundToInt((died ? 110f : 66f) * k * scale), c);
            if (!string.IsNullOrEmpty(bannerSub))
                Text(new Rect(0, y + bandH * 0.62f, W, bandH * 0.4f), bannerSub, serif, Mathf.RoundToInt(28f * k), new Color(0.9f, 0.88f, 0.82f, a));
            if (!died)
            {
                // thin gold rules either side
                Rect(new Rect(W * 0.2f, y + bandH * 0.18f, W * 0.6f, 1.5f * k), new Color(Gold.r, Gold.g, Gold.b, 0.6f * a));
                Rect(new Rect(W * 0.2f, y + bandH * 0.82f, W * 0.6f, 1.5f * k), new Color(Gold.r, Gold.g, Gold.b, 0.6f * a));
            }
        }

        // a message on the ground you're standing by
        if (reading != null && dying <= 0f)
        {
            var r = new Rect(W * 0.5f - 330f * k, H - 220f * k, 660f * k, 96f * k);
            Rect(r, new Color(0f, 0f, 0f, 0.55f));
            Rect(new Rect(r.x, r.y, r.width, 1.5f * k), new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            Text(new Rect(r.x, r.y + 6f * k, r.width, 26f * k), "MESSAGE", serif, Mathf.RoundToInt(18f * k), new Color(0.75f, 0.72f, 0.65f));
            Text(new Rect(r.x + 10f * k, r.y + 34f * k, r.width - 20f * k, 56f * k), reading.text, serif, Mathf.RoundToInt(26f * k), new Color(0.95f, 0.93f, 0.86f));
        }

        if (graces != null && player != null)
            Text(new Rect(30f * k, 24f * k, 600f * k, 40f * k), "GRACE  " + GracesFound + " / " + graces.Length, serif, Mathf.RoundToInt(22f * k), new Color(0.9f, 0.85f, 0.7f, 0.85f), TextAnchor.UpperLeft);
    }
}
