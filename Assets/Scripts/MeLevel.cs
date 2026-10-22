using UnityEngine;

// The "Me" level (SoundCloud on one side, Discord on the other, you in the middle): both of them want your
// attention. Each has a meter that slowly drains; ride through that side's hearts to fill it. Keep both up
// and they both love you (the HUD counts how long); let one run dry and it feels ignored. On SoundCloud's
// side a playhead runs along the waveform track: ride along inside it and it pushes you along with it.
[DefaultExecutionOrder(100)]
public class MeLevel : MonoBehaviour
{
    public SlideController player;
    [Tooltip("The hearts: each one's side (0 SoundCloud, 1 Discord).")]
    public Transform[] hearts;
    public int[] heartSide;
    [Tooltip("How much a heart fills its side's meter, and how long till it's back.")]
    public float heartFill = 0.34f, heartBack = 10f;
    [Tooltip("A full meter drains in this many seconds.")]
    public float drainTime = 40f;
    [Tooltip("The waveform track: from `trackStart` along +z for `trackLength`, `trackWidth` wide.")]
    public Vector3 trackStart;
    public float trackLength = 140f, trackWidth = 10f;
    [Tooltip("The playhead: how fast it runs along the track, how long it is, and how fast it pushes you.")]
    public float playheadSpeed = 16f, playheadSize = 4f, playheadPush = 22f;
    public Transform playhead;

    public float SoundCloud { get; private set; } = 0.6f;
    public float Discord { get; private set; } = 0.6f;
    public float Loved { get; private set; }   // seconds both have been over half full, this time
    public float BestLoved { get; private set; }
    [System.NonSerialized] public float playheadAt;   // metres along the track

    float[] away;
    bool scIgnored, dcIgnored, bothFull;
    Font font;

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        font = Resources.Load<Font>("Bangers");
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    void Update()
    {
        // the hearts turn and bob
        if (hearts == null) return;
        for (int i = 0; i < hearts.Length; i++)
            if (hearts[i] != null) hearts[i].localRotation = Quaternion.Euler(0f, Time.time * 90f + i * 40f, 0f);
    }

    // drains the meters, picks up hearts you ride through, runs the playhead (public so the tests can run it)
    public void Tick(float dt)
    {
        if (away == null || away.Length != hearts.Length) away = new float[hearts.Length];
        Vector3 p = player.transform.position;
        SoundCloud = Mathf.Max(0f, SoundCloud - dt / drainTime);
        Discord = Mathf.Max(0f, Discord - dt / drainTime);
        for (int i = 0; i < hearts.Length; i++)
        {
            if (away[i] > 0f)
            {
                away[i] -= dt;
                if (away[i] <= 0f) hearts[i].gameObject.SetActive(true);
                continue;
            }
            if ((hearts[i].position - (p + Vector3.up * 0.6f)).sqrMagnitude > 1.8f * 1.8f) continue;
            hearts[i].gameObject.SetActive(false);
            away[i] = heartBack;
            if (heartSide[i] == 0) SoundCloud = Mathf.Min(1f, SoundCloud + heartFill);
            else Discord = Mathf.Min(1f, Discord + heartFill);
            SlideHUD.Flash(heartSide[i] == 0 ? "SOUNDCLOUD <3" : "DISCORD <3");
        }
        // how they feel about it
        if (SoundCloud <= 0f && !scIgnored) SlideHUD.Flash("SOUNDCLOUD FEELS IGNORED");
        if (Discord <= 0f && !dcIgnored) SlideHUD.Flash("DISCORD FEELS IGNORED");
        scIgnored = SoundCloud <= 0f; dcIgnored = Discord <= 0f;
        bool full = SoundCloud > 0.8f && Discord > 0.8f;
        if (full && !bothFull) SlideHUD.Flash("THEY BOTH LOVE YOU");
        bothFull = full;
        if (SoundCloud > 0.5f && Discord > 0.5f) { Loved += dt; BestLoved = Mathf.Max(BestLoved, Loved); }
        else Loved = 0f;

        // the playhead, and its push
        playheadAt = Mathf.Repeat(playheadAt + playheadSpeed * dt, trackLength);
        if (playhead != null) playhead.position = trackStart + Vector3.forward * playheadAt;
        float along = p.z - trackStart.z;
        if (Mathf.Abs(p.x - trackStart.x) < trackWidth * 0.5f && Mathf.Abs(along - playheadAt) < playheadSize * 0.5f && p.y < trackStart.y + 6f)
        {
            var rb = player.GetComponent<Rigidbody>();
            Vector3 v = rb.linearVelocity;
            if (v.z < playheadPush) rb.linearVelocity = new Vector3(v.x, v.y, Mathf.MoveTowards(Mathf.Max(v.z, playheadSpeed), playheadPush, 40f * dt));   // (it catches you up to its own speed at once)
        }
    }

    // ---- the HUD: a meter each side, and how long they've both loved you ------------------------------

    Texture2D white;

    void Bar(Rect r, float f, Color c)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var old = GUI.color;
        GUI.color = new Color(0.1f, 0.05f, 0.12f, 0.85f); GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white);
        GUI.color = new Color(1f, 1f, 1f, 0.25f); GUI.DrawTexture(r, white);
        GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height), white);
        GUI.color = old;
    }

    void Text(Rect r, string s, int size, TextAnchor a, Color c)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a };
        if (font != null) st.font = font;
        st.normal.textColor = Color.black;
        int o = Mathf.Max(2, size / 12);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0) GUI.Label(new Rect(r.x + x * o, r.y + y * o, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f, W = Screen.width;
        Color sc = new Color(1f, 0.33f, 0f), dc = new Color(0.35f, 0.4f, 0.95f), pink = new Color(1f, 0.6f, 0.78f);
        Text(new Rect(30 * k, 170 * k, 420 * k, 50 * k), "SOUNDCLOUD", Mathf.RoundToInt(40 * k), TextAnchor.MiddleLeft, Color.white);
        Bar(new Rect(30 * k, 225 * k, 380 * k, 26 * k), SoundCloud, sc);
        Text(new Rect(W - 450 * k, 170 * k, 420 * k, 50 * k), "DISCORD", Mathf.RoundToInt(40 * k), TextAnchor.MiddleRight, Color.white);
        Bar(new Rect(W - 410 * k, 225 * k, 380 * k, 26 * k), Discord, dc);
        string me = "ME" + (Loved > 0f ? "   LOVED " + Mathf.FloorToInt(Loved) + "s" : "") + (BestLoved > 1f ? "   BEST " + Mathf.FloorToInt(BestLoved) + "s" : "");
        Text(new Rect(0, 170 * k, W, 60 * k), me, Mathf.RoundToInt(48 * k), TextAnchor.MiddleCenter, Loved > 0f ? pink : Color.white);
    }
}
