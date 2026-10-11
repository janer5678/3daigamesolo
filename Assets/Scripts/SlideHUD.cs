using UnityEngine;

// Speed, a one-line controls hint, the level's name when you arrive, the grind you're doing, and
// short messages (checkpoints).
public class SlideHUD : MonoBehaviour
{
    public SlideController player;
    [Tooltip("Shown big for the first few seconds of the level.")]
    public string levelName;

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
        if (player != null)
            Outlined(new Rect(W - 330 * k, 20 * k, 300 * k, 50 * k), Mathf.RoundToInt(player.Speed * 3.6f) + " km/h", Style(Mathf.RoundToInt(34 * k), TextAnchor.UpperRight), Cream);
        if (hintTimer > 0f)
        {
            Outlined(new Rect(0, H - 90 * k, W, 50 * k), "WASD move    Q board    SHIFT push    SPACE jump (hold: higher)    C slide    MOUSE manual    V camera    TAB level    R checkpoint",
                     Style(Mathf.RoundToInt(30 * k), TextAnchor.MiddleCenter), Cream);
            if (!string.IsNullOrEmpty(levelName))
                Outlined(new Rect(0, 60 * k, W, 90 * k), levelName, Style(Mathf.RoundToInt(72 * k), TextAnchor.MiddleCenter), Cream);
        }
        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
            Outlined(new Rect(0, H * 0.3f, W, 80 * k), message, Style(Mathf.RoundToInt(60 * k), TextAnchor.MiddleCenter), Cream);
        // the trick you're doing: a grind (A/D switches it), a wallride or a manual
        if (player != null && player.TrickName != null)
            Outlined(new Rect(0, H * 0.62f, W, 70 * k), player.TrickName, Style(Mathf.RoundToInt(56 * k), TextAnchor.MiddleCenter), Cream);
    }
}
