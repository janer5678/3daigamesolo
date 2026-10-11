using UnityEngine;

// Speed, a one-line controls hint, and the faint television scanlines over everything (the same
// 1x2 texture trick as the Pururin Room).
public class SlideHUD : MonoBehaviour
{
    public SlideController player;
    [Tooltip("Screen pixels per scanline pair.")]
    public float scanlinePitch = 4f;
    [Range(0f, 1f)] public float scanlineStrength = 0.16f;

    static readonly Color Ink = new Color(0.17f, 0.10f, 0.29f);
    static readonly Color Cream = new Color(1f, 0.97f, 0.98f);

    Texture2D scan;
    Font font;
    float hintTimer = 10f;

    void Start()
    {
        scan = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        scan.SetPixel(0, 0, new Color(0f, 0f, 0f, scanlineStrength));
        scan.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        scan.Apply();
        font = Resources.Load<Font>("Bangers");
    }

    void Update() { hintTimer -= Time.deltaTime; }

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
            Outlined(new Rect(0, H - 90 * k, W, 50 * k), "WASD walk    hold SHIFT to ride the board    SPACE jump    R restart",
                     Style(Mathf.RoundToInt(30 * k), TextAnchor.MiddleCenter), Cream);
        if (scan != null && scanlineStrength > 0f)
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), scan, new Rect(0, 0, 1, H / scanlinePitch));
    }
}
