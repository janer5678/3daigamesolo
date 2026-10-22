using System.Collections.Generic;
using UnityEngine;

// The Stream level, after swagtwinn's stream (gambling, THUG Pro, Roblox, Minecraft, FL Studio): it plays like
// THUG Pro's "most points wins". Two minutes on the clock; air, grinds and kickflips score points into a combo,
// each trick in it adds to the multiplier, and it's banked once you've been back on the ground a moment. Bail (get
// sent back) and the combo's lost. The stream's overlay is over it all, chat included, and chat reacts. Ride over
// the slot machine's pad to spin it; in the obby, the red bricks send you back to its start.
[DefaultExecutionOrder(100)]
public class StreamLevel : MonoBehaviour
{
    public SlideController player;
    [Tooltip("Seconds on the clock.")]
    public float session = 120f;
    [Tooltip("Back on the ground this long and the combo's banked.")]
    public float bankAfter = 0.6f;
    public Transform obbyStart;
    [Tooltip("The slot machine's three reels (each turns about its local x), and how long a spin takes.")]
    public Transform[] reels;
    public float spinTime = 1.6f;
    public int seed = 3;

    public int Score { get; private set; }
    public int Best { get; private set; }
    public float ComboPoints { get; private set; }
    public int Multiplier { get { return tricks.Count; } }
    public string LastBank { get; private set; }
    public int LastReward { get; private set; } = -1;   // the last spin: 2 three of a kind, 1 two, 0 nothing
    public bool Spinning { get { return spinLeft > 0f; } }
    public float TimeLeft { get { return timeLeft; } }

    readonly List<string> tricks = new List<string>();
    readonly List<(string name, string msg, Color col)> chat = new List<(string, string, Color)>();
    float timeLeft, ground, air, nextChat, spinLeft, overTimer;
    bool wasGrinding, wasFlipping, airCounted;
    Vector3 lastPos;
    int[] result = new int[3];
    float[] reelFrom = new float[3], reelTo = new float[3];
    System.Random rng;
    Font font;
    Texture2D white;

    static readonly string[] Names = { "gaggingwithtape", "sicktwinnn", "ironmmouse", "yashen_drg", "resultingpsychoanalysts", "bro_thebunny", "natsuki", "viewers twitchstar" };
    static readonly string[] Idle = { "w", "W", "wwwwwwww", "pls rtaid", "Swag", "hi dada", "stream?", "can u react", "just leaked base", "L bu", "rakai funny", "W stream" };

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        font = Resources.Load<Font>("Bangers");
        try { Best = PlayerPrefs.GetInt("StreamBest", 0); } catch { }
        Begin();
    }

    // a fresh two minutes
    public void Begin()
    {
        if (rng == null) rng = new System.Random(seed);
        timeLeft = session; Score = 0; ComboPoints = 0f; tricks.Clear(); overTimer = 0f;
        if (player != null) lastPos = player.transform.position;
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && timeLeft <= 0f) Begin();
        // idle chat
        nextChat -= Time.deltaTime;
        if (nextChat <= 0f) { Say(Names[Random.Range(0, Names.Length)], Idle[Random.Range(0, Idle.Length)]); nextChat = Random.Range(1.2f, 3.5f); }
    }

    void Say(string name, string msg)
    {
        float h = (name.GetHashCode() & 0xffff) / 65535f;
        chat.Add((name, msg, Color.HSVToRGB(Mathf.Repeat(0.72f + h * 0.4f, 1f), 0.55f, 1f)));
        if (chat.Count > 10) chat.RemoveAt(0);
    }

    // scores your skating, runs the clock, the slot machine and the obby (public so the tests can run it)
    public void Tick(float dt)
    {
        if (rng == null) Begin();
        Vector3 p = player.transform.position;
        // bailed: sent back somewhere (a kill brick, R, a fall)
        if ((p - lastPos).sqrMagnitude > 36f && ComboPoints > 0f) { ComboPoints = 0f; tricks.Clear(); SlideHUD.Flash("BAIL"); Say("gaggingwithtape", "L"); }
        lastPos = p;

        if (timeLeft > 0f)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f)
            {
                Bank();
                timeLeft = 0f;
                if (Score > Best) { Best = Score; try { PlayerPrefs.SetInt("StreamBest", Best); } catch { } }
                SlideHUD.Flash("TIME! " + Score.ToString("N0"));
                overTimer = 6f;
            }
        }
        else if ((overTimer -= dt) <= 0f) Begin();

        bool grinding = player.Grinding, flipping = player.Flipping;
        if (timeLeft > 0f)
        {
            if (grinding)
            {
                if (!wasGrinding) Add(player.TrickName ?? "GRIND", 100);
                ComboPoints += 60f * dt * Mathf.Max(1f, player.Speed / 8f);
                ground = 0f;
            }
            else if (!player.Grounded)
            {
                air += dt;
                ComboPoints += 40f * dt;
                if (air > 0.6f && !airCounted) { airCounted = true; Add("AIR", 100); }
                ground = 0f;
            }
            else
            {
                air = 0f; airCounted = false;
                ground += dt;
                if (ground > bankAfter && ComboPoints > 0f) Bank();
            }
            if (flipping && !wasFlipping) Add("KICKFLIP", 250);
        }
        wasGrinding = grinding; wasFlipping = flipping;

        // the obby's kill bricks
        var on = player.StandingOn;
        if (on != null && on.GetComponentInParent<KillBrick>() != null && obbyStart != null)
        {
            player.checkpoint = obbyStart;
            player.Respawn();
            lastPos = player.transform.position;
            SlideHUD.Flash("OOF");
        }
        // the slot machine
        if (on != null && on.GetComponentInParent<SlotPad>() != null && !Spinning) Spin();
        if (Spinning)
        {
            spinLeft -= dt;
            float t = 1f - Mathf.Clamp01(spinLeft / spinTime);
            for (int i = 0; i < reels.Length; i++)
            {
                float ti = Mathf.Clamp01(t * (1.2f + i * 0.25f));
                float a = Mathf.Lerp(reelFrom[i], reelTo[i], 1f - (1f - ti) * (1f - ti));
                if (reels[i] != null) reels[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            if (spinLeft <= 0f) Payout();
        }
    }

    void Add(string trick, int points)
    {
        tricks.Add(trick);
        ComboPoints += points;
    }

    void Bank()
    {
        if (ComboPoints <= 0f) { tricks.Clear(); return; }
        int got = Mathf.RoundToInt(ComboPoints * Mathf.Max(1, tricks.Count));
        Score += got;
        LastBank = string.Join(" + ", tricks) + " = " + got.ToString("N0");
        if (got >= 1500) { SlideHUD.Flash("+" + got.ToString("N0")); for (int i = 0; i < Mathf.Min(5, got / 1500); i++) Say(Names[rng.Next(Names.Length)], i % 2 == 0 ? "WWWWWWW" : "W"); }
        ComboPoints = 0f;
        tricks.Clear();
    }

    void Spin()
    {
        spinLeft = spinTime;
        // three of a kind now and then, two often enough
        double roll = rng.NextDouble();
        int a = rng.Next(5), b = rng.Next(5), c = rng.Next(5);
        if (roll < 0.15) b = c = a;
        else if (roll < 0.55) { b = a; while (c == a) c = rng.Next(5); }
        else { while (b == a) b = rng.Next(5); while (c == a || c == b) c = rng.Next(5); }
        result[0] = a; result[1] = b; result[2] = c;
        for (int i = 0; i < 3; i++)
        {
            reelFrom[i] = reels != null && i < reels.Length && reels[i] != null ? reels[i].localEulerAngles.x : 0f;
            reelTo[i] = 360f * (3 + i) + result[i] * 72f;
        }
        Say("gaggingwithtape", "gamble gamble");
    }

    void Payout()
    {
        int same = result[0] == result[1] && result[1] == result[2] ? 2 : (result[0] == result[1] || result[1] == result[2] || result[0] == result[2]) ? 1 : 0;
        LastReward = same;
        if (same == 2)
        {
            Score += 5000;
            SlideHUD.Flash("JACKPOT +5,000");
            var rb = player.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 18f, rb.linearVelocity.z);
            for (int i = 0; i < 4; i++) Say(Names[rng.Next(Names.Length)], "JACKPOT W");
        }
        else if (same == 1) { Score += 500; SlideHUD.Flash("+500"); Say("sicktwinnn", "w"); }
        else { SlideHUD.Flash("L"); Say("yashen_drg", "L bu"); }
    }

    // ---- the overlay ----------------------------------------------------------------------------------------------

    void Rect(Rect r, Color c)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old;
    }

    void Text(Rect r, string s, int size, TextAnchor a, Color c, bool outline = true, Font f = null)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = false };
        if (f != null) st.font = f;
        if (outline)
        {
            st.normal.textColor = new Color(0.05f, 0.02f, 0.08f);
            int o = Mathf.Max(1, size / 16);
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) if (x != 0 || y != 0) GUI.Label(new Rect(r.x + x * o, r.y + y * o, r.width, r.height), s, st);
        }
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        // the clock and the scores
        int secs = Mathf.CeilToInt(timeLeft);
        Text(new Rect(0, 14 * k, W, 60 * k), timeLeft > 0f ? (secs / 60) + ":" + (secs % 60).ToString("00") : "TIME!", Mathf.RoundToInt(52 * k), TextAnchor.UpperCenter, Color.white, true, font);
        Text(new Rect(0, 66 * k, W, 30 * k), "MOST POINTS WINS", Mathf.RoundToInt(22 * k), TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.3f));
        Text(new Rect(30 * k, 20 * k, 600 * k, 50 * k), "SCORE  " + Score.ToString("N0"), Mathf.RoundToInt(36 * k), TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.3f), true, font);
        Text(new Rect(30 * k, 66 * k, 600 * k, 30 * k), "BEST  " + Mathf.Max(Best, Score).ToString("N0"), Mathf.RoundToInt(22 * k), TextAnchor.UpperLeft, Color.white);
        // the combo going on
        if (ComboPoints > 0f)
        {
            Text(new Rect(0, H * 0.7f, W, 50 * k), Mathf.RoundToInt(ComboPoints).ToString("N0") + " X " + Mathf.Max(1, tricks.Count), Mathf.RoundToInt(44 * k), TextAnchor.MiddleCenter, Color.white, true, font);
            string list = string.Join(" + ", tricks.Count > 6 ? tricks.GetRange(tricks.Count - 6, 6) : tricks);
            Text(new Rect(0, H * 0.7f + 46 * k, W, 30 * k), list, Mathf.RoundToInt(22 * k), TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f));
        }
        // chat, down the right
        var panel = new Rect(W - 470 * k, H * 0.22f, 450 * k, 400 * k);
        Rect(panel, new Color(0.08f, 0.03f, 0.14f, 0.55f));
        for (int i = 0; i < chat.Count; i++)
        {
            var m = chat[i];
            var r = new Rect(panel.x + 12 * k, panel.y + 8 * k + i * 38 * k, panel.width - 20 * k, 36 * k);
            Rect(new Rect(r.x, r.y + 8 * k, 18 * k, 18 * k), new Color(0.25f, 0.8f, 0.35f));
            Text(new Rect(r.x + 26 * k, r.y, r.width, r.height), m.name + ": ", Mathf.RoundToInt(22 * k), TextAnchor.MiddleLeft, m.col);
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * k), fontStyle = FontStyle.Bold };
            float nameW = st.CalcSize(new GUIContent(m.name + ": ")).x;
            Text(new Rect(r.x + 26 * k + nameW, r.y, r.width, r.height), m.msg, Mathf.RoundToInt(22 * k), TextAnchor.MiddleLeft, Color.white);
        }
        // the stream's links and name
        Text(new Rect(24 * k, H - 70 * k, 600 * k, 50 * k), "discord.gg/swagtwin", Mathf.RoundToInt(34 * k), TextAnchor.MiddleLeft, new Color(0.3f, 0.95f, 0.35f));
        Text(new Rect(W - 520 * k, H - 96 * k, 500 * k, 80 * k), "swagtwinn", Mathf.RoundToInt(70 * k), TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 1f), true, font);
        Text(new Rect(W - 900 * k, H * 0.22f - 34 * k, 880 * k, 30 * k), "twitch.tv/swagtwinn    tiktok.com/@swagtwinn", Mathf.RoundToInt(20 * k), TextAnchor.MiddleRight, new Color(0.75f, 0.45f, 1f));
    }
}
