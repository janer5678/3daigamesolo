using System.Collections.Generic;
using UnityEngine;

// Surf Mesa, after the CS:S map surf_mesa: four stages, each surfed from a start zone down a run of ramps to a tunnel
// that takes you on to the next, ending on a platform in the lava room. Touch the sand (or the lava) and you're back
// at the start of the stage. The timer starts when you leave the first start zone; each stage's time is checked
// against your best (green faster, red slower) and the surf timer reports it in the chat, the way the servers do.
// Speed is shown in units a second. R: back to the start of the stage; T: restart the whole run.
[DefaultExecutionOrder(100)]
public class SurfLevel : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        public string name;
        public Transform start;
        public Bounds startZone, end;   // leave the start zone to start the clock; get into `end` to finish the stage
        [Header("Look")]
        public Color fog, fogSun, sky, ground, background;
        public float fogDensity;
    }

    public SlideController player;
    public Stage[] stages;
    public RealLook look;

    public int Current { get; private set; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }
    public float RunTime { get; private set; }
    public int Resets { get; private set; }

    float stageTime;
    bool stageRunning;
    readonly float[] splits = new float[8];
    float lastDelta;
    bool hasDelta;
    float deltaTimer;
    Font font;

    struct Line { public string[] parts; public Color[] cols; public float t; }
    readonly List<Line> chat = new List<Line>();

    const float UnitsPerMetre = 39.37f;   // Source units
    static readonly Color Tag = new Color(0.55f, 0.85f, 1f), Name = new Color(0.65f, 1f, 0.55f), Plain = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color Faster = new Color(0.35f, 1f, 0.35f), Slower = new Color(1f, 0.35f, 0.3f), Purple = new Color(0.85f, 0.55f, 1f);

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Restart();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) Restart();
        deltaTimer -= Time.deltaTime;
        for (int i = chat.Count - 1; i >= 0; i--) { var l = chat[i]; l.t -= Time.deltaTime; chat[i] = l; if (l.t <= 0f) chat.RemoveAt(i); }
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    // the whole run again, from the first start zone
    public void Restart()
    {
        Running = Finished = false;
        RunTime = 0f;
        Resets = 0;
        GoTo(0);
    }

    // straight to the start of a stage, as if the ones before were done (the tests)
    public void StartAtStage(int stage) { GoTo(stage); }

    void GoTo(int stage)
    {
        Current = stage;
        stageRunning = false;
        stageTime = 0f;
        player.checkpoint = stages[stage].start;
        player.Respawn();
        ApplyLook(stage);
    }

    public void ApplyLook(int stage)
    {
        var s = stages[stage];
        if (look == null) look = GetComponent<RealLook>();
        if (look != null)
        {
            look.fog = s.fog; look.fogSun = s.fogSun; look.sky = s.sky; look.ground = s.ground; look.fogDensity = s.fogDensity;
            look.Apply();
        }
        var cam = GetComponent<Camera>();
        if (cam != null) cam.backgroundColor = s.background;
    }

    // one physics step (public so the tests can run it)
    public void Tick(float dt)
    {
        if (Finished) return;
        var st = stages[Current];
        Vector3 p = player.transform.position;
        bool inStart = st.startZone.Contains(p);
        if (!inStart && !stageRunning)
        {
            stageRunning = true;
            stageTime = 0f;
            if (!Running && Current == 0) { Running = true; RunTime = 0f; }
        }
        if (inStart && stageRunning) { stageRunning = false; stageTime = 0f; }   // back in the start zone: the stage clock waits
        if (Running) RunTime += dt;
        if (stageRunning) stageTime += dt;

        // the sand: back to the start of the stage
        bool sand = p.y < -30f;
        foreach (var col in Physics.OverlapSphere(p, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            if (col.GetComponentInParent<SurfSand>() != null) { sand = true; break; }
        if (sand)
        {
            Resets++;
            GoTo(Current);
            return;
        }

        if (st.end.Contains(p)) StageDone();
    }

    void StageDone()
    {
        int i = Current;
        float t = stageTime;
        string key = "surfmesa_stage" + i;
        float best = PlayerPrefs.GetFloat(key, 0f);
        hasDelta = best > 0f;
        lastDelta = t - best;
        deltaTimer = 4f;
        if (best <= 0f || t < best) PlayerPrefs.SetFloat(key, t);
        splits[i] = t;
        bool last = i == stages.Length - 1;
        if (!last)
        {
            Say(new[] { "[Surf Timer]", " - ", "You", " finished ", "Stage " + (i + 1), " in ", Clock(t), hasDelta ? " (" + Delta(lastDelta) + ")" : "", "." },
                new[] { Tag, Plain, Name, Plain, Purple, Plain, Purple, hasDelta ? (lastDelta <= 0f ? Faster : Slower) : Plain, Plain });
            GoTo(i + 1);
            return;
        }
        Finished = true;
        Running = false;
        float bestRun = PlayerPrefs.GetFloat("surfmesa_run", 0f);
        bool record = bestRun <= 0f || RunTime < bestRun;
        if (record) PlayerPrefs.SetFloat("surfmesa_run", RunTime);
        PlayerPrefs.Save();
        Say(new[] { "[Surf Timer]", " - ", "You", " finished in ", Clock(RunTime), bestRun > 0f ? " (PB " + Delta(RunTime - bestRun) + ")" : "", record && bestRun > 0f ? ". Improving by " + Clock(bestRun - RunTime) : "", "." },
            new[] { Tag, Plain, Name, Plain, Purple, bestRun > 0f ? (record ? Faster : Slower) : Plain, Faster, Plain });
        if (record) Say(new[] { "[Surf Timer]", " - ", "You", " beat the map record! ", "T", " to go again." }, new[] { Tag, Plain, Name, Plain, Purple, Plain });
        else Say(new[] { "[Surf Timer]", " - ", "T", " to go again." }, new[] { Tag, Plain, Purple, Plain });
    }

    void Say(string[] parts, Color[] cols)
    {
        chat.Add(new Line { parts = parts, cols = cols, t = 12f });
        if (chat.Count > 6) chat.RemoveAt(0);
    }

    static string Clock(float t)
    {
        int m = Mathf.FloorToInt(t / 60f);
        float s = t - m * 60f;
        return m.ToString("00") + ":" + Mathf.FloorToInt(s).ToString("00") + ":" + Mathf.FloorToInt((s % 1f) * 100f).ToString("00");
    }

    static string Delta(float d) { return (d <= 0f ? "-" : "+") + Clock(Mathf.Abs(d)); }

    // ---- the HUD, in the plain small type of a Source game ------------------------------------------------

    void Shadowed(Rect r, string text, int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, font = font, richText = false };
        s.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, s);
        s.normal.textColor = c;
        GUI.Label(r, text, s);
    }

    void OnGUI()
    {
        if (player == null || stages == null) return;
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        int small = Mathf.RoundToInt(20 * k);
        // speed, bottom middle
        Vector3 v = player.Velocity; v.y = 0f;
        Shadowed(new Rect(0, H - 210 * k, W, 50 * k), Mathf.RoundToInt(v.magnitude * UnitsPerMetre).ToString(), Mathf.RoundToInt(34 * k), Plain, TextAnchor.MiddleCenter);
        // the clock and stage, bottom left
        string state = Finished ? "Finished" : !Running ? "Start Zone" : (stageRunning ? "" : "Stage Start");
        Shadowed(new Rect(30 * k, H - 190 * k, 600 * k, 30 * k), "Stage " + (Current + 1) + "/" + stages.Length + "  " + stages[Current].name, small, Plain);
        Shadowed(new Rect(30 * k, H - 162 * k, 600 * k, 40 * k), (Running || Finished ? Clock(RunTime) : "00:00:00") + (state.Length > 0 ? "   " + state : ""), Mathf.RoundToInt(28 * k), Plain);
        if (deltaTimer > 0f && hasDelta)
            Shadowed(new Rect(30 * k, H - 222 * k, 300 * k, 30 * k), Delta(lastDelta), Mathf.RoundToInt(24 * k), lastDelta <= 0f ? Faster : Slower);
        // the chat, above that
        float y = H - 260 * k - chat.Count * 26 * k;
        foreach (var l in chat)
        {
            float x = 30 * k;
            var s = new GUIStyle(GUI.skin.label) { fontSize = small, font = font };
            for (int i = 0; i < l.parts.Length; i++)
            {
                if (string.IsNullOrEmpty(l.parts[i])) continue;
                float w = s.CalcSize(new GUIContent(l.parts[i])).x;
                Color c = l.cols[i]; c.a = Mathf.Clamp01(l.t);
                Shadowed(new Rect(x, y, w + 4, 30 * k), l.parts[i], small, c);
                x += w - 2 * k;
            }
            y += 26 * k;
        }
    }
}
