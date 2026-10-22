using UnityEngine;

// Speed, a one-line controls hint, the level's name when you arrive, short messages (checkpoints), and
// while you're flying a car, the seconds it's got left. With `aero` on it's drawn as glossy Frutiger Aero
// panels instead: a player card with a level and XP (earned by skating: distance and air), a clock, and
// bars for speed, height and air.
public class SlideHUD : MonoBehaviour
{
    public SlideController player;
    [Tooltip("Shown big for the first few seconds of the level.")]
    public string levelName;
    [Tooltip("Draw it as glossy Frutiger Aero panels.")]
    public bool aero;
    [Tooltip("The controls line shown at the start (empty: the usual one).")]
    public string hint;
    [Tooltip("Don't draw the speed in the corner (the level draws its own).")]
    public bool noSpeed;

    static readonly Color Ink = new Color(0.17f, 0.10f, 0.29f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.98f);

    static SlideHUD current;
    Font font;
    float hintTimer = 10f;
    string message;
    float messageTimer;

    void Awake() { current = this; }

    void Start() { font = Resources.Load<Font>("Bangers"); }

    // pops up a line of text in the middle of the screen for a couple of seconds
    public static void Flash(string text)
    {
        if (current == null) return;
        current.message = text;
        current.messageTimer = 2.5f;
    }

    void Update()
    {
        hintTimer -= Time.deltaTime;
        messageTimer -= Time.deltaTime;
        if (aero && player != null)
        {
            // XP: a point a metre, three a second in the air
            Vector3 p = player.transform.position;
            if (lastPos != Vector3.zero) xp += Vector3.Distance(p, lastPos) + (player.Grounded ? 0f : 3f * Time.deltaTime);
            lastPos = p;
            air = player.Grounded ? Mathf.MoveTowards(air, 0f, Time.deltaTime * 2f) : Mathf.Min(air + Time.deltaTime, 3f);
            if (startY == float.MinValue) startY = p.y;
        }
    }

    // ---- the Aero look ------------------------------------------------------------------------------

    float xp, air, startY = float.MinValue;
    Vector3 lastPos;
    Texture2D glass, barTex;

    // a glossy blue pane: lighter at the top with a bright band across the upper half, darker at the bottom
    Texture2D GlassTexture()
    {
        var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
        {
            float v = y / 63f;   // 0 at the bottom
            Color c = Color.Lerp(new Color(0.05f, 0.32f, 0.78f, 0.82f), new Color(0.45f, 0.78f, 1f, 0.82f), v);
            if (v > 0.52f) c = Color.Lerp(c, new Color(0.9f, 0.97f, 1f, 0.9f), 0.45f * (1f - (v - 0.52f) * 1.2f));
            if (y == 0 || y == 63) c = new Color(1f, 1f, 1f, 0.95f);
            for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
        }
        t.Apply();
        return t;
    }

    void Pane(Rect r)
    {
        if (glass == null) glass = GlassTexture();
        GUI.DrawTexture(r, glass, ScaleMode.StretchToFill, true);
        var edge = new Color(1f, 1f, 1f, 0.8f);
        DrawRect(new Rect(r.x, r.y, r.width, 2f), edge);
        DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), edge);
        DrawRect(new Rect(r.x, r.y, 2f, r.height), edge);
        DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), edge);
    }

    void DrawRect(Rect r, Color c)
    {
        if (barTex == null) { barTex = new Texture2D(1, 1); barTex.SetPixel(0, 0, Color.white); barTex.Apply(); }
        var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, barTex); GUI.color = old;
    }

    void Bar(Rect r, float f, Color c)
    {
        DrawRect(r, new Color(0f, 0.1f, 0.3f, 0.6f));
        DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height), c);
        DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height * 0.4f), new Color(1f, 1f, 1f, 0.35f));
    }

    void Label(Rect r, string text, int size, TextAnchor a, bool bold = true)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
        s.normal.textColor = new Color(0f, 0.15f, 0.4f, 0.6f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, s);
        s.normal.textColor = Color.white;
        GUI.Label(r, text, s);
    }

    void AeroGUI(float k, float W, float H)
    {
        int level = 1 + Mathf.FloorToInt(Mathf.Sqrt(xp / 120f));
        float lo = (level - 1) * (level - 1) * 120f, hi = level * level * 120f;
        // the player card
        var card = new Rect(24 * k, 24 * k, 330 * k, 86 * k);
        Pane(card);
        DrawRect(new Rect(card.x + 12 * k, card.y + 12 * k, 52 * k, 52 * k), new Color(0.3f, 0.85f, 0.4f, 0.95f));
        Label(new Rect(card.x + 12 * k, card.y + 12 * k, 52 * k, 52 * k), level.ToString(), Mathf.RoundToInt(26 * k), TextAnchor.MiddleCenter);
        Label(new Rect(card.x + 76 * k, card.y + 8 * k, 240 * k, 30 * k), "AERO PLAYER", Mathf.RoundToInt(22 * k), TextAnchor.MiddleLeft);
        Label(new Rect(card.x + 76 * k, card.y + 34 * k, 240 * k, 20 * k), "XP " + Mathf.FloorToInt(xp) + " / " + Mathf.FloorToInt(hi), Mathf.RoundToInt(14 * k), TextAnchor.MiddleLeft, false);
        Bar(new Rect(card.x + 76 * k, card.y + 58 * k, 236 * k, 12 * k), (xp - lo) / Mathf.Max(1f, hi - lo), new Color(0.35f, 0.95f, 0.45f));
        // the clock
        var clock = new Rect(W - 260 * k, 24 * k, 236 * k, 86 * k);
        Pane(clock);
        var now = System.DateTime.Now;
        Label(new Rect(clock.x, clock.y + 6 * k, clock.width - 16 * k, 44 * k), now.ToString("h:mm tt"), Mathf.RoundToInt(32 * k), TextAnchor.MiddleRight);
        Label(new Rect(clock.x, clock.y + 48 * k, clock.width - 16 * k, 26 * k), now.ToString("dddd, MMM d").ToUpper(), Mathf.RoundToInt(14 * k), TextAnchor.MiddleRight, false);
        DrawRect(new Rect(clock.x + 18 * k, clock.y + 20 * k, 40 * k, 40 * k), new Color(1f, 0.85f, 0.2f, 0.95f));
        // speed, height, air
        var stats = new Rect(24 * k, H - 130 * k, 300 * k, 106 * k);
        Pane(stats);
        float h = player.transform.position.y - (startY == float.MinValue ? 0f : startY);
        string[] names = { "SPEED", "HEIGHT", "AIR" };
        float[] fill = { player.Speed / 30f, Mathf.Abs(h) / 60f, air / 3f };
        Color[] cols = { new Color(0.35f, 0.95f, 0.45f), new Color(0.35f, 0.75f, 1f), new Color(1f, 0.8f, 0.25f) };
        string[] vals = { Mathf.RoundToInt(player.Speed * 3.6f) + " km/h", Mathf.RoundToInt(h) + " m", air.ToString("0.0") + " s" };
        for (int i = 0; i < 3; i++)
        {
            float y = stats.y + 12 * k + i * 30 * k;
            Label(new Rect(stats.x + 12 * k, y - 4 * k, 80 * k, 24 * k), names[i], Mathf.RoundToInt(13 * k), TextAnchor.MiddleLeft);
            Bar(new Rect(stats.x + 84 * k, y + 2 * k, 120 * k, 12 * k), fill[i], cols[i]);
            Label(new Rect(stats.x + 210 * k, y - 4 * k, 84 * k, 24 * k), vals[i], Mathf.RoundToInt(13 * k), TextAnchor.MiddleLeft, false);
        }
    }

    void Outlined(Rect r, string text, GUIStyle s, Color c)
    {
        s.normal.textColor = Ink;
        int o = Mathf.Max(2, s.fontSize / 14);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0) GUI.Label(new Rect(r.x + x * o, r.y + y * o, r.width, r.height), text, s);
        s.normal.textColor = c;
        GUI.Label(r, text, s);
    }

    GUIStyle Style(int size, TextAnchor a)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a };
        if (font != null) s.font = font;
        return s;
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (aero && player != null) AeroGUI(k, W, H);
        else if (player != null && !noSpeed)
            Outlined(new Rect(W - 330 * k, 20 * k, 300 * k, 50 * k), Mathf.RoundToInt(player.Speed * 3.6f) + " km/h", Style(Mathf.RoundToInt(34 * k), TextAnchor.UpperRight), Cream);
        if (hintTimer > 0f)
        {
            if (!string.IsNullOrEmpty(hint)) Outlined(new Rect(0, H - 90 * k, W, 50 * k), hint, Style(Mathf.RoundToInt(30 * k), TextAnchor.MiddleCenter), Cream);
            else Outlined(new Rect(0, H - 90 * k, W, 50 * k), "WASD move    SHIFT faster    SPACE jump (hold: higher, again in the air: kickflip)    C slide / powerslide    RIGHT CLICK off/on the board    land on a flying car to fly it: SPACE up, C down, W/S speed    V camera    TAB level    R checkpoint",
                     Style(Mathf.RoundToInt(30 * k), TextAnchor.MiddleCenter), Cream);
            if (!string.IsNullOrEmpty(levelName))
                Outlined(new Rect(0, 60 * k, W, 90 * k), levelName, Style(Mathf.RoundToInt(72 * k), TextAnchor.MiddleCenter), Cream);
        }
        // flying a car: how long it's got left before it falls apart
        if (player != null && player.Piloting)
            Outlined(new Rect(0, H * 0.16f, W, 90 * k), player.PilotFuse.ToString("0.0"), Style(Mathf.RoundToInt(80 * k), TextAnchor.MiddleCenter),
                     player.PilotFuse < 1.5f ? new Color(1f, 0.35f, 0.3f) : Cream);
        // the grind you're doing (A/D switches it)
        if (player != null && player.TrickName != null)
            Outlined(new Rect(0, H * 0.62f, W, 70 * k), player.TrickName, Style(Mathf.RoundToInt(56 * k), TextAnchor.MiddleCenter), Cream);
        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
            Outlined(new Rect(0, H * 0.3f, W, 80 * k), message, Style(Mathf.RoundToInt(60 * k), TextAnchor.MiddleCenter), Cream);
    }
}
