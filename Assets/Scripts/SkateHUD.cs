using UnityEngine;

// Score, the combo being built, callouts, the balance meter, the controls card, and the faint
// television scanlines over everything (the same 1x2 texture trick as the Pururin Room).
public class SkateHUD : MonoBehaviour
{
    public SkateController skater;
    [Tooltip("Screen pixels per scanline pair.")]
    public float scanlinePitch = 4f;
    [Range(0f, 1f)] public float scanlineStrength = 0.16f;

    Texture2D scan, white;
    Font font;
    GUIStyle big, mid, small, card;
    bool showHelp = true;
    float helpTimer = 14f;

    static readonly Color Ink = new Color(0.17f, 0.10f, 0.29f);
    static readonly Color Pink = new Color(0.97f, 0.55f, 0.75f);
    static readonly Color Cyan = new Color(0.36f, 0.80f, 0.95f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.98f);

    void Start()
    {
        scan = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        scan.SetPixel(0, 0, new Color(0f, 0f, 0f, scanlineStrength));
        scan.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        scan.Apply();
        white = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Bangers");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) { showHelp = !showHelp; helpTimer = 9999f; }
        helpTimer -= Time.deltaTime;
        if (helpTimer < 0f) showHelp = false;
    }

    GUIStyle Style(int size, TextAnchor a)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, wordWrap = false, richText = true };
        if (font != null) s.font = font;
        return s;
    }

    // text with a chunky ink outline
    void Outlined(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = Ink;
        int o = Mathf.Max(2, s.fontSize / 14);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0) GUI.Label(new Rect(r.x + x * o, r.y + y * o, r.width, r.height), text, s);
        s.normal.textColor = c;
        GUI.Label(r, text, s);
        s.normal.textColor = old;
    }

    void OnGUI()
    {
        if (skater == null) return;
        float k = Screen.height / 1080f;
        big = Style(Mathf.RoundToInt(64 * k), TextAnchor.MiddleCenter);
        mid = Style(Mathf.RoundToInt(38 * k), TextAnchor.MiddleCenter);
        small = Style(Mathf.RoundToInt(26 * k), TextAnchor.UpperLeft);
        float W = Screen.width, H = Screen.height;
        var combo = skater.combo;
        float now = skater.SimTime;

        // score
        var left = Style(Mathf.RoundToInt(44 * k), TextAnchor.UpperLeft);
        Outlined(new Rect(30 * k, 20 * k, 900 * k, 60 * k), combo.totalScore.ToString("N0"), left, Cream);
        Outlined(new Rect(30 * k, 70 * k, 900 * k, 40 * k), "best combo  " + combo.bestCombo.ToString("N0"), small, Pink);
        var right = Style(Mathf.RoundToInt(34 * k), TextAnchor.UpperRight);
        Outlined(new Rect(W - 330 * k, 20 * k, 300 * k, 50 * k), Mathf.RoundToInt(skater.Speed * 3.6f) + " km/h", right, Cream);

        // the combo being built
        if (combo.Active)
        {
            Outlined(new Rect(0, H - 210 * k, W, 50 * k), combo.Describe(5), mid, Cream);
            string val = combo.basePoints.ToString("N0") + "  x  " + combo.Multiplier;
            Outlined(new Rect(0, H - 160 * k, W, 70 * k), val, big, Pink);
        }
        else if (now - combo.lastResultTime < 2.2f)
        {
            string head = combo.lastResultLost ? combo.lastResult : "+" + combo.lastResultValue.ToString("N0");
            Outlined(new Rect(0, H - 170 * k, W, 70 * k), head, big, combo.lastResultLost ? new Color(0.95f, 0.4f, 0.55f) : Cyan);
            if (!combo.lastResultLost) Outlined(new Rect(0, H - 215 * k, W, 50 * k), combo.lastResult, mid, Cream);
        }

        // callout in the middle
        float age = now - skater.calloutTime;
        if (age < 1.1f)
        {
            float pop = 1f + Mathf.Max(0f, 0.25f - age) * 1.2f;
            var cs = Style(Mathf.RoundToInt(72 * k * pop), TextAnchor.MiddleCenter);
            Outlined(new Rect(0, H * 0.28f, W, 100 * k), skater.callout, cs, skater.calloutColor);
        }

        // balance meter for grinds and manuals
        if (skater.mode == SkateController.Mode.Grind || skater.Manual)
        {
            bool grind = skater.mode == SkateController.Mode.Grind;
            float bw = 420 * k, bh = 22 * k;
            Rect bar = new Rect((W - bw) / 2f, H * 0.62f, bw, bh);
            if (!grind) bar = new Rect(W / 2f + 200 * k, H * 0.45f - 160 * k, bh, 320 * k);
            DrawRect(new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), Ink);
            DrawRect(bar, new Color(0.93f, 0.86f, 0.97f));
            float b = Mathf.Clamp(skater.Balance, -1f, 1f);
            if (grind)
            {
                DrawRect(new Rect(bar.center.x - bw * 0.125f, bar.y, bw * 0.25f, bh), new Color(0.6f, 0.92f, 0.86f));
                float x = bar.center.x + b * bw * 0.5f;
                DrawRect(new Rect(x - 4 * k, bar.y - 8 * k, 8 * k, bh + 16 * k), Pink);
                Outlined(new Rect(0, bar.y + 26 * k, W, 40 * k), "A / D", Style(Mathf.RoundToInt(24 * k), TextAnchor.UpperCenter), Cream);
            }
            else
            {
                DrawRect(new Rect(bar.x, bar.center.y - 320 * k * 0.125f, bh, 320 * k * 0.25f), new Color(0.6f, 0.92f, 0.86f));
                float y = bar.center.y - b * 160 * k;
                DrawRect(new Rect(bar.x - 8 * k, y - 4 * k, bh + 16 * k, 8 * k), Pink);
                Outlined(new Rect(bar.x - 40 * k, bar.yMax + 8 * k, 120 * k, 40 * k), "W / S", Style(Mathf.RoundToInt(24 * k), TextAnchor.UpperLeft), Cream);
            }
        }

        // controls
        if (showHelp)
        {
            string help =
                "W  push      S  brake      A / D  steer\n" +
                "SPACE  hold to crouch, let go to ollie (hold longer = higher)\n" +
                "AIR:  A / D spin   LMB kickflip   RMB heelflip   Q / E shove-its\n" +
                "        + W or S with a flip for harder flips   SHIFT grab\n" +
                "RAILS:  land on one to grind, A / D to balance, SPACE to pop off\n" +
                "SHIFT on the ground = manual (W / S balance) - links combos\n" +
                "Land straight for PERFECT (+speed, +1x)      R  restart      H  hide";
            var hs = Style(Mathf.RoundToInt(24 * k), TextAnchor.UpperLeft);
            Vector2 size = new Vector2(860 * k, 250 * k);
            Rect r = new Rect(30 * k, 130 * k, size.x, size.y);
            DrawRect(r, new Color(0.17f, 0.10f, 0.29f, 0.55f));
            Outlined(new Rect(r.x + 18 * k, r.y + 12 * k, r.width, r.height), help, hs, Cream);
        }
        else
        {
            Outlined(new Rect(30 * k, 120 * k, 400 * k, 40 * k), "H  controls", small, new Color(1f, 1f, 1f, 0.7f));
        }

        // scanlines over the lot
        if (scan != null && scanlineStrength > 0f)
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), scan, new Rect(0, 0, 1, H / scanlinePitch));
    }

    void DrawRect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }
}
