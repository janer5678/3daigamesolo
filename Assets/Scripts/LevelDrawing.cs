using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// The drawing pad: press F1 in any level and the game pauses under a flat page you can scribble on, like
// Paint. Four pages, one after the other (Done goes on to the next): the level from above, from the side,
// what you were looking at when you opened it, and a blank one for anything else. Each page has the real
// level faintly underneath (switch it off for a clean page), a grid in metres and where the start is, so
// the drawing can be matched up with the level.
//
// When you press Done on the last page everything's saved in Layouts/Drawings/<level> <date>/: each page as
// a picture (and the drawing on its own), plus notes.json saying which view each page is and how it lines up
// with the level (and anything you wrote with the text tool). That's the brief for building it properly.
public class LevelDrawing : MonoBehaviour
{
    public static bool Open;

    // ---- what gets saved ------------------------------------------------------------------------------

    [Serializable] public class Label { public string text; public float x, y; public string colour; }

    [Serializable]
    public class PageNote
    {
        public string title, view, picture, drawingOnly, howItLinesUp;
        public bool drewSomething;
        public Vector3 centre, cameraPosition, cameraForward;
        public float worldWidth, worldHeight, cameraFov;
        public List<Label> labels = new List<Label>();
    }

    [Serializable]
    public class Notes
    {
        public string level, made, colours;
        public List<PageNote> pages = new List<PageNote>();
    }

    // ---- the pages --------------------------------------------------------------------------------------

    class Page
    {
        public string title, view, prompt, file;
        public Texture2D backdrop;
        public Color32[] ink;
        public readonly List<Label> labels = new List<Label>();
        public readonly List<(Color32[] ink, int labels)> undo = new List<(Color32[], int)>();
        public bool touched;
        // how it lines up with the level
        public Vector3 centre, camPos, camFwd;
        public float halfH, aspect, fov;
        public Quaternion rot = Quaternion.identity;
    }

    struct Pen { public string name, means; public Color32 c; }

    static readonly Pen[] Pens =
    {
        new Pen { name = "Black", means = "platforms, buildings, ground", c = new Color32(20, 18, 30, 255) },
        new Pen { name = "Yellow", means = "rails", c = new Color32(235, 180, 0, 255) },
        new Pen { name = "Cyan", means = "speed rails", c = new Color32(0, 190, 220, 255) },
        new Pen { name = "Pink", means = "jump pads", c = new Color32(240, 40, 160, 255) },
        new Pen { name = "Orange", means = "ramps", c = new Color32(255, 120, 20, 255) },
        new Pen { name = "Blue", means = "buses, moving things", c = new Color32(40, 80, 230, 255) },
        new Pen { name = "Green", means = "your route through it", c = new Color32(30, 170, 60, 255) },
        new Pen { name = "Red", means = "important / danger", c = new Color32(220, 30, 30, 255) },
    };

    const int TW = 1280, TH = 720;
    static readonly int[] Sizes = { 3, 7, 16 };

    readonly List<Page> pages = new List<Page>();
    bool alongZ;   // the level's long way is along z (so that's left to right on the page)
    int page, pen, sizeIx = 1;
    bool eraser, textTool;
    float backdropAlpha = 0.5f;
    Texture2D ink;
    bool inkDirty;

    // a stroke in progress
    bool drawing, straight;
    Vector2 last, lineFrom, lineTo;

    // writing
    bool typing;
    Vector2 typeAt;
    string typed = "";

    // finishing
    bool capturing, finished;
    string savedTo = "";

    SlideController player;
    SlideHUD hud;
    Camera cam;
    bool wasKinematic;
    Rect canvas;
    Font font;
    static Texture2D white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Open = false;
        SceneManager.sceneLoaded += (s, m) => { Open = false; Attach(); };
        Attach();
    }

    static void Attach()
    {
        if (FindFirstObjectByType<LevelDrawing>() != null) return;
        new GameObject("Level drawing").AddComponent<LevelDrawing>();
    }

    void Start()
    {
        cam = Camera.main;
        player = FindFirstObjectByType<SlideController>();
        hud = FindFirstObjectByType<SlideHUD>();
        font = Resources.Load<Font>("Bangers");
        string[] args = Environment.GetCommandLineArgs();
        int ti = Array.IndexOf(args, "-drawtest");
        if (ti >= 0 && ti + 1 < args.Length) StartCoroutine(SelfTest(args[ti + 1]));
    }

    void OnDestroy() { if (Open) { Open = false; Time.timeScale = 1f; } }

    // ---- opening and closing ------------------------------------------------------------------------------

    void Show(bool on)
    {
        if (on == Open) return;
        if (on && LevelSketch.Editing) return;
        Open = on;
        Time.timeScale = on ? 0f : 1f;
        if (hud != null) hud.enabled = !on;
        if (player != null) player.enabled = !on;
        if (on)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            finished = false;
            if (pages.Count == 0) MakePages();
            else
            {
                // "what you saw" is always what you were just looking at
                pages[2].backdrop = Snap(cam, null, out _);
                pages[2].camPos = cam.transform.position; pages[2].camFwd = cam.transform.forward; pages[2].fov = cam.fieldOfView;
            }
            page = Mathf.Min(page, pages.Count - 1);
            LoadInk();
        }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; typing = false; drawing = false; }
    }

    void Update()
    {
        if (!Open)
        {
            if (Input.GetKeyDown(KeyCode.F1) && !LevelSketch.Editing) Show(true);
            return;
        }
        if (inkDirty) { ink.SetPixels32(pages[page].ink); ink.Apply(); inkDirty = false; }
        if (typing || finished) return;
        if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Escape)) { Show(false); return; }
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
        for (int i = 0; i < Pens.Length; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { pen = i; eraser = textTool = false; }
        if (Input.GetKeyDown(KeyCode.E)) { eraser = true; textTool = false; }
        if (Input.GetKeyDown(KeyCode.T)) { textTool = true; eraser = false; }
    }

    // ---- the pages, and pictures of the level for under them --------------------------------------------

    void MakePages()
    {
        pages.Clear();
        Bounds b = LevelBounds();
        float aspect = TW / (float)TH;
        // from above: looking straight down, the start end on the left (the level's long way across)
        var top = new Page { title = "FROM ABOVE", view = "above", file = "1-from-above",
            prompt = "Draw the map, as if you're looking down on it. The green flag is the start. Each square is 10 m." };
        alongZ = b.size.z >= b.size.x;
        top.camFwd = Vector3.down;
        top.rot = Quaternion.LookRotation(Vector3.down, alongZ ? Vector3.left : Vector3.forward);
        top.aspect = aspect;
        top.halfH = Mathf.Max((alongZ ? b.extents.z : b.extents.x) / aspect, alongZ ? b.extents.x : b.extents.z) * 1.05f;
        top.centre = new Vector3(b.center.x, b.max.y + 20f, b.center.z);
        top.backdrop = Ortho(top.centre, top.rot, top.halfH, b.size.y + 60f);
        pages.Add(top);
        // from the side: cut through the middle, looking across, the start end on the left, the ground at the bottom
        var side = new Page { title = "FROM THE SIDE", view = "side", file = "2-from-the-side",
            prompt = "Draw how high things go. The lines are heights, 10 m apart; the start end is on the left." };
        float halfH = (alongZ ? b.extents.z : b.extents.x) / aspect * 1.05f;
        halfH = Mathf.Max(halfH, 40f);
        side.aspect = aspect;
        side.halfH = halfH;
        Vector3 across = alongZ ? Vector3.left : Vector3.forward;
        side.camFwd = across;
        side.rot = Quaternion.LookRotation(across, Vector3.up);
        side.centre = new Vector3(b.center.x, b.min.y + halfH - 6f, b.center.z);
        side.backdrop = Ortho(side.centre, side.rot, halfH, 2000f, true);
        pages.Add(side);
        // what you were looking at
        var view = new Page { title = "WHAT YOU SAW", view = "your view", file = "3-what-you-saw",
            prompt = "This is what you were looking at. Draw on it: what should be here?" };
        view.backdrop = Snap(cam, null, out _);
        view.camPos = cam.transform.position; view.camFwd = cam.transform.forward; view.fov = cam.fieldOfView;
        pages.Add(view);
        pages.Add(new Page { title = "ANYTHING ELSE", view = "blank", file = "4-anything-else",
            prompt = "Close-ups of one bit, ideas, the feel of it... anything. The Text tool (T) is for writing." });
        foreach (var p in pages) p.ink = new Color32[TW * TH];
        if (ink == null) { ink = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; }
    }

    void LoadInk() { ink.SetPixels32(pages[page].ink); ink.Apply(); }

    // the size of the level: everything solid, bar the huge things (the ground, the sky)
    static Bounds LevelBounds()
    {
        bool any = false;
        Bounds b = new Bounds();
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!c.enabled || c.isTrigger) continue;
            Bounds cb = c.bounds;
            if (cb.size.x > 400f || cb.size.z > 400f || cb.size.y > 400f) continue;
            if (!any) { b = cb; any = true; } else b.Encapsulate(cb);
        }
        if (!any) b = new Bounds(Vector3.zero, new Vector3(200f, 50f, 200f));
        b.min = new Vector3(b.min.x, Mathf.Max(b.min.y, -20f), b.min.z);
        return b;
    }

    // a picture straight down or straight across (orthographic), the fog pushed right back so it's clear
    Texture2D Ortho(Vector3 at, Quaternion rot, float halfH, float far, bool fromMiddle = false)
    {
        var go = new GameObject("Drawing camera");
        var c = go.AddComponent<Camera>();
        c.CopyFrom(cam);
        c.orthographic = true;
        c.orthographicSize = halfH;
        c.nearClipPlane = fromMiddle ? 0.01f : 0.3f;
        c.farClipPlane = far;
        go.transform.SetPositionAndRotation(at, rot);
        Vector4 fog = Shader.GetGlobalVector("_PSXFog");
        Shader.SetGlobalVector("_PSXFog", new Vector4(5000f, 10000f, 0f, 0f));
        var tex = Snap(c, null, out _);
        Shader.SetGlobalVector("_PSXFog", fog);
        Destroy(go);
        return tex;
    }

    static Texture2D Snap(Camera c, RenderTexture into, out RenderTexture used)
    {
        var rt = into ?? new RenderTexture(TW, TH, 24);
        var keep = c.targetTexture;
        float aspect = c.aspect;
        c.targetTexture = rt;
        c.aspect = TW / (float)TH;
        c.Render();
        c.targetTexture = keep;
        c.aspect = aspect;
        if (keep == null) c.ResetAspect();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(TW, TH, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, TW, TH), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        used = rt;
        if (into == null) rt.Release();
        return tex;
    }

    // a point in the level to where it is on the page (0..1 across, 0..1 down), for the above and side pages
    Vector2 ToPage(Page p, Vector3 w)
    {
        Vector3 local = Quaternion.Inverse(p.rot) * (w - p.centre);
        return new Vector2(0.5f + local.x / (2f * p.halfH * p.aspect), 0.5f - local.y / (2f * p.halfH));
    }


    // ---- drawing ---------------------------------------------------------------------------------------------

    Vector2 ToTex(Vector2 gui) { return new Vector2((gui.x - canvas.x) / canvas.width * TW, (1f - (gui.y - canvas.y) / canvas.height) * TH); }

    void PushUndo()
    {
        var p = pages[page];
        p.undo.Add(((Color32[])p.ink.Clone(), p.labels.Count));
        if (p.undo.Count > 25) p.undo.RemoveAt(0);
    }

    void Undo()
    {
        var p = pages[page];
        if (p.undo.Count == 0) return;
        var (u, n) = p.undo[p.undo.Count - 1];
        p.undo.RemoveAt(p.undo.Count - 1);
        p.ink = u;
        if (p.labels.Count > n) p.labels.RemoveRange(n, p.labels.Count - n);
        inkDirty = true;
    }

    void Stamp(Vector2 c)
    {
        var p = pages[page];
        int r = eraser ? Sizes[sizeIx] * 3 : Sizes[sizeIx];
        Color32 col = eraser ? new Color32(0, 0, 0, 0) : Pens[pen].c;
        int x0 = Mathf.Max(0, (int)(c.x - r)), x1 = Mathf.Min(TW - 1, (int)(c.x + r)), y0 = Mathf.Max(0, (int)(c.y - r)), y1 = Mathf.Min(TH - 1, (int)(c.y + r));
        float rr = r * r;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - c.x, dy = y - c.y;
                if (dx * dx + dy * dy <= rr) p.ink[y * TW + x] = col;
            }
        p.touched = true;
        inkDirty = true;
    }

    void StampLine(Vector2 a, Vector2 b)
    {
        float step = Mathf.Max(1f, (eraser ? Sizes[sizeIx] * 3 : Sizes[sizeIx]) * 0.35f);
        int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
        for (int i = 0; i <= n; i++) Stamp(Vector2.Lerp(a, b, i / (float)n));
    }

    void Canvas(Event e)
    {
        if (typing || finished || capturing) return;
        Vector2 m = e.mousePosition;
        bool inside = canvas.Contains(m);
        if (e.type == EventType.MouseDown && e.button == 0 && inside)
        {
            if (textTool) { typing = true; typeAt = new Vector2((m.x - canvas.x) / canvas.width, (m.y - canvas.y) / canvas.height); typed = ""; e.Use(); return; }
            PushUndo();
            drawing = true;
            straight = e.shift;
            last = lineFrom = lineTo = ToTex(m);
            if (!straight) Stamp(last);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && drawing)
        {
            Vector2 p = ToTex(m);
            if (straight) lineTo = p;
            else { StampLine(last, p); last = p; }
            e.Use();
        }
        else if (e.type == EventType.MouseUp && drawing)
        {
            if (straight) StampLine(lineFrom, ToTex(m));
            drawing = false;
            e.Use();
        }
    }

    // ---- turning pages and finishing -------------------------------------------------------------------------

    void Done()
    {
        if (page < pages.Count - 1) { page++; LoadInk(); return; }
        StartCoroutine(Finish());
    }

    void Back() { if (page > 0) { page--; LoadInk(); } }

    IEnumerator Finish()
    {
        string level = SceneManager.GetActiveScene().name;
        string dir = Path.Combine(Folder(), level + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm"));
        Directory.CreateDirectory(dir);
        var notes = new Notes { level = level, made = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
        var legend = new List<string>();
        foreach (var p in Pens) legend.Add(p.name.ToLower() + " = " + p.means);
        notes.colours = string.Join("; ", legend);
        int keep = page;
        capturing = true;
        for (int i = 0; i < pages.Count; i++)
        {
            page = i; LoadInk();
            yield return null;
            yield return new WaitForEndOfFrame();
            // the page as you saw it (level underneath, grid, drawing, writing)
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            float k = Screen.height / 1080f;
            int sx = Mathf.RoundToInt(canvas.x * k), sw = Mathf.RoundToInt(canvas.width * k), sh = Mathf.RoundToInt(canvas.height * k);
            int sy = Screen.height - Mathf.RoundToInt((canvas.y + canvas.height) * k);
            sx = Mathf.Clamp(sx, 0, shot.width - 1); sy = Mathf.Clamp(sy, 0, shot.height - 1);
            sw = Mathf.Min(sw, shot.width - sx); sh = Mathf.Min(sh, shot.height - sy);
            var crop = new Texture2D(sw, sh, TextureFormat.RGB24, false);
            crop.SetPixels(shot.GetPixels(sx, sy, sw, sh));
            crop.Apply();
            Destroy(shot);
            var p = pages[i];
            File.WriteAllBytes(Path.Combine(dir, p.file + ".png"), crop.EncodeToPNG());
            Destroy(crop);
            File.WriteAllBytes(Path.Combine(dir, p.file + " (drawing only).png"), ink.EncodeToPNG());
            var pn = new PageNote { title = p.title, view = p.view, picture = p.file + ".png", drawingOnly = p.file + " (drawing only).png", drewSomething = p.touched || p.labels.Count > 0, labels = new List<Label>(p.labels) };
            if (p.view == "above")
            {
                pn.centre = p.centre; pn.worldWidth = 2f * p.halfH * p.aspect; pn.worldHeight = 2f * p.halfH;
                pn.howItLinesUp = alongZ ? "Looking straight down. Left to right on the page is +z in the level (the start end on the left); top of the page is -x. The middle of the page is centre (x, z)."
                                           : "Looking straight down. Left to right on the page is +x in the level; top of the page is +z. The middle of the page is centre (x, z).";
            }
            else if (p.view == "side")
            {
                pn.centre = p.centre; pn.worldWidth = 2f * p.halfH * p.aspect; pn.worldHeight = 2f * p.halfH;
                pn.howItLinesUp = (alongZ ? "Looking across the level towards -x, cut through the middle. Left to right on the page is +z" : "Looking across the level towards +z, cut through the middle. Left to right on the page is +x")
                                  + "; up the page is up. The middle of the page is centre (its y is the height there).";
            }
            else if (p.view == "your view")
            {
                pn.cameraPosition = p.camPos; pn.cameraForward = p.camFwd; pn.cameraFov = p.fov;
                pn.howItLinesUp = "The view from cameraPosition looking along cameraForward (vertical field of view cameraFov).";
            }
            else pn.howItLinesUp = "A blank page.";
            notes.pages.Add(pn);
        }
        capturing = false;
        page = keep; LoadInk();
        File.WriteAllText(Path.Combine(dir, "notes.json"), JsonUtility.ToJson(notes, true));
        File.WriteAllText(Path.Combine(Folder(), "latest.txt"), dir);
        savedTo = dir;
        finished = true;
    }

    static string Folder()
    {
        string top = Path.GetFullPath(Application.isEditor ? Path.Combine(Application.dataPath, "..") : Path.Combine(Application.dataPath, "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(top, "Assets"))) top = Application.persistentDataPath;
        return Path.Combine(top, "Layouts", "Drawings");
    }

    // ---- the screen ------------------------------------------------------------------------------------------

    void Fill(Rect r, Color c)
    {
        if (white == null) white = Texture2D.whiteTexture;
        var keep = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = keep;
    }

    GUIStyle Style(int sz, TextAnchor a, Color c, bool bangers = false)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = sz, alignment = a, wordWrap = true, richText = true };
        if (bangers && font != null) st.font = font;
        st.normal.textColor = c;
        return st;
    }

    bool Button(Rect r, string text, Color bg, int sz = 22, bool on = false)
    {
        bool over = r.Contains(Event.current.mousePosition);
        Fill(r, on ? Color.white : over ? Color.Lerp(bg, Color.white, 0.25f) : bg);
        GUI.Label(r, text, Style(sz, TextAnchor.MiddleCenter, on ? Color.black : Color.white));
        return GUI.Button(r, "", GUIStyle.none);
    }

    void Line(Vector2 a, Vector2 b, float w, Color c)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        var keepM = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        Fill(new Rect(a.x, a.y - w / 2f, len, w), c);
        GUI.matrix = keepM;
    }

    void OnGUI()
    {
        if (!Open) return;
        float k = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
        float W = Screen.width / k;
        Color panel = new Color(0.1f, 0.06f, 0.16f, 1f), pink = new Color(1f, 0.3f, 0.7f), dim = new Color(0.25f, 0.2f, 0.38f);
        var p = pages[page];

        // the page: as big as fits between the panels, the same shape as the screen
        float left = 300f, topY = 112f, bottom = 104f;
        float aw = W - left - 24f, ah = 1080f - topY - bottom;
        float cw = Mathf.Min(aw, ah * TW / TH), ch = cw * TH / TW;
        canvas = new Rect(left + (aw - cw) / 2f, topY + (ah - ch) / 2f, cw, ch);

        Fill(new Rect(0f, 0f, W, 1080f), panel);
        // paper, the level faintly underneath, the guides, the drawing, the writing
        Fill(canvas, new Color(0.97f, 0.96f, 0.93f));
        if (p.backdrop != null && backdropAlpha > 0f)
        {
            var keep = GUI.color; GUI.color = new Color(1f, 1f, 1f, backdropAlpha);
            GUI.DrawTexture(canvas, p.backdrop);
            GUI.color = keep;
        }
        Guides(p);
        GUI.DrawTexture(canvas, ink);
        if (drawing && straight) Line(FromTex(lineFrom), FromTex(lineTo), Mathf.Max(2f, Sizes[sizeIx] * 2f * canvas.width / TW), Pens[pen].c);
        foreach (var l in p.labels)
        {
            Vector2 at = new Vector2(canvas.x + l.x * canvas.width, canvas.y + l.y * canvas.height);
            var st = Style(26, TextAnchor.UpperLeft, ColourOf(l.colour));
            st.wordWrap = false; st.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(at.x, at.y - 16f, 900f, 40f), l.text, st);
        }
        if (capturing) { Canvas(Event.current); return; }

        // along the top: which page, and what to draw
        GUI.Label(new Rect(24f, 10f, W - 48f, 56f), "DRAW YOUR LEVEL   <color=#ffffff99>page " + (page + 1) + " of " + pages.Count + ":</color>  " + p.title, Style(46, TextAnchor.UpperLeft, pink, true));
        GUI.Label(new Rect(26f, 66f, W - 48f, 36f), p.prompt, Style(22, TextAnchor.UpperLeft, Color.white));

        // down the left: pens (and what each colour's for), sizes, eraser, text, undo, the level underneath
        float y = 120f;
        GUI.Label(new Rect(20f, y, 260f, 30f), "PENS  (1-8)", Style(22, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 1f), true));
        y += 32f;
        for (int i = 0; i < Pens.Length; i++)
        {
            Rect r = new Rect(20f, y, 260f, 46f);
            bool on = pen == i && !eraser && !textTool;
            Fill(r, on ? Color.white : dim);
            Fill(new Rect(r.x + 6f, r.y + 6f, 34f, 34f), Pens[i].c);
            GUI.Label(new Rect(r.x + 48f, r.y + 2f, 210f, 24f), "<b>" + (i + 1) + "  " + Pens[i].name + "</b>", Style(18, TextAnchor.UpperLeft, on ? Color.black : Color.white));
            GUI.Label(new Rect(r.x + 48f, r.y + 22f, 210f, 24f), Pens[i].means, Style(15, TextAnchor.UpperLeft, on ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.75f, 0.75f, 0.85f)));
            if (GUI.Button(r, "", GUIStyle.none)) { pen = i; eraser = textTool = false; }
            y += 50f;
        }
        y += 6f;
        string[] sizeNames = { "Thin", "Mid", "Fat" };
        for (int i = 0; i < 3; i++)
            if (Button(new Rect(20f + i * 88f, y, 84f, 42f), sizeNames[i], dim, 18, sizeIx == i)) sizeIx = i;
        y += 50f;
        if (Button(new Rect(20f, y, 128f, 46f), "Eraser (E)", dim, 18, eraser)) { eraser = true; textTool = false; }
        if (Button(new Rect(152f, y, 128f, 46f), "Text (T)", dim, 18, textTool)) { textTool = true; eraser = false; }
        y += 52f;
        if (Button(new Rect(20f, y, 128f, 46f), "Undo", dim, 18)) Undo();
        if (Button(new Rect(152f, y, 128f, 46f), "Clear page", dim, 18)) { PushUndo(); p.ink = new Color32[TW * TH]; p.labels.Clear(); inkDirty = true; }
        y += 52f;
        if (p.backdrop != null)
        {
            string under = backdropAlpha >= 0.99f ? "Level underneath: full" : backdropAlpha > 0f ? "Level underneath: faint" : "Level underneath: off";
            if (Button(new Rect(20f, y, 260f, 46f), under, dim, 18)) backdropAlpha = backdropAlpha <= 0f ? 0.5f : backdropAlpha < 0.99f ? 1f : 0f;
            y += 52f;
        }
        GUI.Label(new Rect(20f, y + 4f, 260f, 120f), "Hold <b>Shift</b> while you drag for a straight line.\n<b>Ctrl+Z</b> undo.\n<b>Esc</b> back to the game (it's kept).", Style(16, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.9f)));

        // along the bottom: back, the pages, done
        float by = 1080f - 86f;
        if (page > 0 && Button(new Rect(left, by, 220f, 66f), "<  Back", dim, 26)) Back();
        for (int i = 0; i < pages.Count; i++)
        {
            Rect dot = new Rect(W / 2f - pages.Count * 120f / 2f + i * 120f, by + 10f, 112f, 46f);
            Fill(dot, i == page ? pink : i < page ? new Color(0.5f, 0.35f, 0.6f) : dim);
            GUI.Label(dot, pages[i].title.Substring(0, 1) + pages[i].title.Substring(1).ToLower(), Style(14, TextAnchor.MiddleCenter, Color.white));
            if (GUI.Button(dot, "", GUIStyle.none)) { page = i; LoadInk(); }
        }
        string done = page < pages.Count - 1 ? "Done  >" : "Done: send it!";
        if (Button(new Rect(W - 24f - 300f, by, 300f, 66f), done, new Color(0.85f, 0.2f, 0.55f), 28)) Done();

        // writing
        if (typing)
        {
            Vector2 at = new Vector2(canvas.x + typeAt.x * canvas.width, canvas.y + typeAt.y * canvas.height);
            Rect box = new Rect(Mathf.Min(at.x, W - 540f), at.y - 22f, 520f, 44f);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool esc = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            Fill(new Rect(box.x - 4f, box.y - 30f, box.width + 8f, box.height + 34f), new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(box.x, box.y - 28f, box.width, 26f), "Type, then Enter (Esc: never mind)", Style(16, TextAnchor.UpperLeft, Color.white));
            GUI.SetNextControlName("drawtext");
            typed = GUI.TextField(box, typed, 200, new GUIStyle(GUI.skin.textField) { fontSize = 24 });
            GUI.FocusControl("drawtext");
            if (enter)
            {
                if (typed.Trim().Length > 0) { PushUndo(); p.labels.Add(new Label { text = typed.Trim(), x = typeAt.x, y = typeAt.y, colour = Pens[pen].name.ToLower() }); p.touched = true; }
                typing = false; Event.current.Use();
            }
            if (esc) { typing = false; Event.current.Use(); }
        }
        else Canvas(Event.current);

        if (finished)
        {
            Fill(new Rect(0f, 0f, W, 1080f), new Color(0.05f, 0.03f, 0.1f, 0.92f));
            GUI.Label(new Rect(0f, 330f, W, 90f), "SENT!", Style(90, TextAnchor.MiddleCenter, pink, true));
            GUI.Label(new Rect(W / 2f - 700f, 430f, 1400f, 190f),
                "Your drawings are saved in <b>Layouts/Drawings/" + Path.GetFileName(savedTo) + "</b>\n\nNow tell Claude: \"I drew it\" and it'll build the level from them.",
                Style(26, TextAnchor.UpperCenter, Color.white));
            if (Button(new Rect(W / 2f - 160f, 660f, 320f, 70f), "Back to the game", new Color(0.85f, 0.2f, 0.55f), 28)) { Show(false); pages.Clear(); page = 0; }
        }
    }

    Vector2 FromTex(Vector2 t) { return new Vector2(canvas.x + t.x / TW * canvas.width, canvas.y + (1f - t.y / TH) * canvas.height); }

    static Color ColourOf(string name)
    {
        foreach (var p in Pens) if (p.name.ToLower() == name) return p.c;
        return Pens[0].c;
    }

    // the grid (from above), the height lines (from the side), and where the start is
    void Guides(Page p)
    {
        if (p.view != "above" && p.view != "side") return;
        Color line = new Color(0.2f, 0.2f, 0.35f, 0.22f), strong = new Color(0.2f, 0.2f, 0.35f, 0.45f);
        float pxPerM = canvas.height / (2f * p.halfH);
        if (p.view == "above")
        {
            // squares every 10 m, lined up with the level's own metres
            Vector2 o = ToPage(p, new Vector3(0f, 0f, 0f));
            float step = 10f * pxPerM;
            if (step > 6f)
            {
                float ox = canvas.x + o.x * canvas.width, oy = canvas.y + o.y * canvas.height;
                for (float x = ox - Mathf.Ceil((ox - canvas.x) / step) * step; x <= canvas.xMax; x += step)
                    if (x >= canvas.x) Fill(new Rect(x, canvas.y, 1f, canvas.height), Mathf.Abs(Mathf.Round((x - ox) / step)) % 5 == 0 ? strong : line);
                for (float yy = oy - Mathf.Ceil((oy - canvas.y) / step) * step; yy <= canvas.yMax; yy += step)
                    if (yy >= canvas.y) Fill(new Rect(canvas.x, yy, canvas.width, 1f), Mathf.Abs(Mathf.Round((yy - oy) / step)) % 5 == 0 ? strong : line);
            }
            // a scale bar
            Fill(new Rect(canvas.x + 20f, canvas.yMax - 30f, 50f * pxPerM, 5f), new Color(0.1f, 0.1f, 0.2f, 0.8f));
            GUI.Label(new Rect(canvas.x + 20f, canvas.yMax - 58f, 200f, 26f), "50 m", Style(16, TextAnchor.UpperLeft, new Color(0.1f, 0.1f, 0.2f)));
        }
        else
        {
            // height lines every 10 m, labelled
            for (float h = 0f; h < 1000f; h += 10f)
            {
                Vector2 v = ToPage(p, new Vector3(p.centre.x, h, p.centre.z));
                float yy = canvas.y + v.y * canvas.height;
                if (yy < canvas.y) break;
                if (yy > canvas.yMax) continue;
                bool big = Mathf.RoundToInt(h) % 50 == 0;
                Fill(new Rect(canvas.x, yy, canvas.width, big ? 2f : 1f), big ? strong : line);
                GUI.Label(new Rect(canvas.x + 6f, yy - 22f, 120f, 22f), Mathf.RoundToInt(h) + " m", Style(15, TextAnchor.LowerLeft, new Color(0.15f, 0.15f, 0.3f, big ? 0.95f : 0.6f)));
            }
        }
        // the start: a green flag
        if (player != null && player.startPoint != null)
        {
            Vector2 s = ToPage(p, player.startPoint.position);
            Vector2 at = new Vector2(canvas.x + s.x * canvas.width, canvas.y + s.y * canvas.height);
            if (canvas.Contains(at))
            {
                Fill(new Rect(at.x - 1.5f, at.y - 34f, 3f, 34f), new Color(0.1f, 0.4f, 0.1f));
                Fill(new Rect(at.x + 1.5f, at.y - 34f, 22f, 14f), new Color(0.2f, 0.85f, 0.3f));
                GUI.Label(new Rect(at.x + 4f, at.y - 58f, 120f, 24f), "START", Style(18, TextAnchor.UpperLeft, new Color(0.1f, 0.5f, 0.15f), true));
            }
        }
    }

    // ---- "-drawtest <folder>": draws a bit on every page, writes something, presses Done all the way through,
    // checks it all got saved, and quits ---------------------------------------------------------------------------
    IEnumerator SelfTest(string folder)
    {
        Application.runInBackground = true;
        for (int i = 0; i < 10; i++) yield return null;
        Show(true);
        for (int i = 0; i < 5; i++) yield return null;
        for (int pg = 0; pg < pages.Count; pg++)
        {
            for (int s = 0; s < 3; s++)
            {
                pen = (pg + s) % Pens.Length; sizeIx = s;
                PushUndo();
                Vector2 a = new Vector2(200f + s * 250f, 150f + pg * 60f), b = new Vector2(500f + s * 250f, 500f - s * 80f);
                StampLine(a, b);
            }
            pages[pg].labels.Add(new Label { text = "test writing on page " + (pg + 1), x = 0.3f, y = 0.2f, colour = "red" });
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "pad-" + (pg + 1) + ".png"));
            yield return null;
            if (pg < pages.Count - 1) Done();
            yield return null;
        }
        Done();
        float t0 = Time.realtimeSinceStartup;
        while (!finished && Time.realtimeSinceStartup - t0 < 20f) yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "pad-sent.png"));
        int files = finished ? Directory.GetFiles(savedTo).Length : 0;
        Debug.Log("DRAW_TEST " + (finished && files == 9 ? "PASS" : "FAIL") + " saved " + files + " files in " + savedTo);
        yield return new WaitForSecondsRealtime(1f);
        // (a test run leaves no drawings behind)
        if (finished) { Directory.Delete(savedTo, true); File.Delete(Path.Combine(Folder(), "latest.txt")); }
        Application.Quit();
    }
}
