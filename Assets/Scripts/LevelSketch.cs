using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// The 3D level sketcher. Press F2 in any level to rough out a layout: pick a thing from the bar along the bottom
// (platforms, buildings, walls, ramps, rails, speed rails, jump pads, vents, tall buses, notes, where you
// start), point and click to put it down, scroll to size it. Press F2 again and you're playing on it, starting
// wherever you were looking, so you can try it straight away.
//
// Everything's saved as you go, one file per level, in Layouts/ at the top of the project (Layouts/Akiba.json),
// along with any photos you take (P). That file is the brief for building the level properly: what goes
// where and how big, and notes for anything that needs saying in words.
//
// It adds itself to every level when it loads; nothing in the level builders needs to know about it.
public class LevelSketch : MonoBehaviour
{
    // ---- what gets saved ----------------------------------------------------------------------------

    [Serializable]
    public class Item
    {
        public string type;        // platform, building, wall, ramp, rail, speedrail, jumppad, vent, bus, note, start
        public Vector3 pos;        // where it stands (the middle of its bottom); a rail's or bus route's start
        public Vector3 end;        // a rail's end, or where a bus route goes to
        public float yaw;          // which way it faces, degrees round from north (+z)
        public Vector3 size;       // width, height, length (a jump pad: how high, -, how far; a bus: -, height, speed)
        public string note = "";
    }

    [Serializable]
    public class Sheet
    {
        public string level;
        public string savedAt;
        public bool levelHidden;   // drawn on a blank grid rather than over the level
        public List<Item> items = new List<Item>();
    }

    // ---- the things you can put down ------------------------------------------------------------------

    class Tool
    {
        public string type, name, help, sizeHelp;
        public Color color;
        public Vector3 size;
        public bool twoClicks;
    }

    static readonly Tool[] Tools =
    {
        new Tool { type = "platform", name = "Platform", color = new Color(0.45f, 0.72f, 1f), size = new Vector3(10f, 1f, 10f),
                   help = "Something flat to land on. Put it on the ground, or in the air (R/F or H for a layer).", sizeHelp = "Wheel: size   Shift+wheel: length   Ctrl+wheel: thickness" },
        new Tool { type = "building", name = "Building", color = new Color(0.72f, 0.66f, 0.88f), size = new Vector3(14f, 30f, 14f),
                   help = "A block: a building, a tower, a pillar.", sizeHelp = "Wheel: size   Shift+wheel: length   Ctrl+wheel: height" },
        new Tool { type = "wall", name = "Wall", color = new Color(0.92f, 0.85f, 0.68f), size = new Vector3(12f, 4f, 0.6f),
                   help = "A wall or a ledge.", sizeHelp = "Wheel: length   Ctrl+wheel: height   Shift+wheel: thickness" },
        new Tool { type = "ramp", name = "Ramp", color = new Color(1f, 0.72f, 0.28f), size = new Vector3(4f, 2f, 6f),
                   help = "A ramp, going up the way the arrow points.", sizeHelp = "Wheel: width   Shift+wheel: length   Ctrl+wheel: height" },
        new Tool { type = "rail", name = "Rail", color = new Color(1f, 0.86f, 0.15f), size = new Vector3(0.14f, 0.14f, 0f), twoClicks = true,
                   help = "A rail to jump onto and grind. Click where it starts, then where it ends (go up with R/F in between).", sizeHelp = "" },
        new Tool { type = "speedrail", name = "Speed rail", color = new Color(0.25f, 1f, 1f), size = new Vector3(0.26f, 0.26f, 0f), twoClicks = true,
                   help = "Ride into it and it grabs you and rushes you along, uphill too. Click the start, then the end.", sizeHelp = "" },
        new Tool { type = "jumppad", name = "Jump pad", color = new Color(1f, 0.3f, 0.75f), size = new Vector3(20f, 0f, 25f),
                   help = "Throws you up and along the way the arrow points. The dotted line shows where you'll go.", sizeHelp = "Wheel: how high   Shift+wheel: how far" },
        new Tool { type = "vent", name = "Vent", color = new Color(0.6f, 1f, 0.85f), size = new Vector3(4f, 20f, 0f),
                   help = "A column of air that carries you straight up.", sizeHelp = "Wheel: width   Ctrl+wheel: how high" },
        new Tool { type = "bus", name = "Tall bus", color = new Color(1f, 0.55f, 0.55f), size = new Vector3(3f, 12f, 3f), twoClicks = true,
                   help = "A slow bus going back and forth: click where it starts, then where it drives to (it can fly).", sizeHelp = "Ctrl+wheel: height   Shift+wheel: speed" },
        new Tool { type = "note", name = "Note", color = new Color(1f, 1f, 0.45f), size = Vector3.one,
                   help = "Write a note for whoever builds it properly: \"a neon tower here\", \"make this harder\"...", sizeHelp = "" },
        new Tool { type = "start", name = "Start", color = new Color(0.4f, 1f, 0.45f), size = Vector3.one,
                   help = "Where you start (and where R puts you back).", sizeHelp = "" },
    };

    static Tool Def(string type) { foreach (var t in Tools) if (t.type == type) return t; return Tools[0]; }

    const float G = 9.81f * 3.6f;   // the player's gravity (SlideController.gravityScale)

    // ---- set up in every level ------------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (s, m) => Attach();
        Attach();
    }

    static void Attach()
    {
        if (FindFirstObjectByType<LevelSketch>() != null) return;
        new GameObject("Level sketch").AddComponent<LevelSketch>();
    }

    Sheet sheet = new Sheet();
    readonly Dictionary<Item, GameObject> built = new Dictionary<Item, GameObject>();
    readonly List<string> undo = new List<string>();
    readonly List<LineRenderer> editOnly = new List<LineRenderer>();
    readonly List<Transform> labels = new List<Transform>();
    Transform root;
    string path, status = "";
    float statusTimer;

    bool editing;
    public static bool Editing;
    Camera cam;
    SlideController player;
    SlideHUD hud;
    float camYaw, camPitch;

    // what you're placing
    int tool;
    Vector3 size;
    float yaw, lift, layerY;
    bool layerMode;
    Vector3? firstClick;
    string carryNote = "";
    GameObject ghost, layerGrid;
    string ghostKey;
    Vector3 aim;
    bool aimOk;
    Item hovered;

    // typing a note
    bool typing;
    Vector3 noteAt;
    string noteText = "";

    // the blank canvas
    readonly List<Renderer> hiddenR = new List<Renderer>();
    readonly List<Collider> hiddenC = new List<Collider>();
    GameObject floor;

    readonly List<Rect> uiRects = new List<Rect>();
    Font font;
    bool hideUI, showHelp = true, clearArmed;
    float repeat;

    void Start()
    {
        cam = Camera.main;
        player = FindFirstObjectByType<SlideController>();
        hud = FindFirstObjectByType<SlideHUD>();
        font = Resources.Load<Font>("Bangers");
        root = new GameObject("Sketch").transform;
        path = Path.Combine(Folder(), SceneManager.GetActiveScene().name + ".json");
        size = Tools[0].size;
        string[] args = Environment.GetCommandLineArgs();
        int ti = Array.IndexOf(args, "-sketchtest");
        if (ti >= 0 && ti + 1 < args.Length) { StartCoroutine(SelfTest(args[ti + 1])); return; }
        Load();
    }

    // "-sketchtest <folder>": puts one of everything down in front of the start (saved in that folder, not in
    // Layouts), takes pictures of the editor and of it played, checks a jump pad throws you and a speed rail
    // carries you, and quits. For checking a build with nobody at the keyboard.
    System.Collections.IEnumerator SelfTest(string folder)
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, "sketch-test.json");
        yield return null;
        Transform st = player.startPoint;
        Vector3 s0 = st.position, f = st.forward, r = st.right, up = Vector3.up;
        System.Func<string, Vector3, Vector3, Vector3, string, Item> it = (type, p, end, sz, note) =>
            new Item { type = type, pos = p, end = end, size = sz == Vector3.zero ? Def(type).size : sz, yaw = Quaternion.LookRotation(f).eulerAngles.y, note = note ?? "" };
        Vector3 g = s0 - up * 0.35f;
        sheet.items.Add(it("platform", g + f * 14f + up * 6f, Vector3.zero, Vector3.zero, null));
        sheet.items.Add(it("building", g + f * 34f + r * 14f, Vector3.zero, Vector3.zero, null));
        sheet.items.Add(it("wall", g + f * 10f - r * 10f, Vector3.zero, Vector3.zero, null));
        sheet.items.Add(it("ramp", g + f * 6f - r * 5f, Vector3.zero, Vector3.zero, null));
        sheet.items.Add(it("rail", g + f * 4f + r * 5f + up * 0.9f, g + f * 16f + r * 5f + up * 3f, Vector3.zero, null));
        sheet.items.Add(it("speedrail", g + f * 3f - r * 2.5f + up * 0.6f, g + f * 24f - r * 2.5f + up * 10f, Vector3.zero, null));
        sheet.items.Add(it("jumppad", g + f * 4f + r * 1.5f, Vector3.zero, new Vector3(18f, 0f, 14f), null));
        sheet.items.Add(it("vent", g + f * 22f + r * 7f, Vector3.zero, Vector3.zero, null));
        sheet.items.Add(it("bus", g + f * 30f - r * 9f, g + f * 70f - r * 9f, Vector3.zero, null));
        sheet.items.Add(it("note", g + f * 12f + r * 1f, Vector3.zero, Vector3.zero, "a big neon tower here"));
        sheet.items.Add(it("start", g, Vector3.zero, Vector3.zero, null));
        RebuildAll();
        Save();
        cam.transform.SetPositionAndRotation(s0 - f * 14f + up * 12f + r * 6f, Quaternion.LookRotation(f * 30f - up * 12f - r * 6f));
        SetEditing(true);
        cam.transform.SetPositionAndRotation(s0 - f * 14f + up * 12f + r * 6f, Quaternion.LookRotation(f * 30f - up * 12f - r * 6f));
        Vector3 e = cam.transform.eulerAngles; camYaw = e.y; camPitch = e.x > 180f ? e.x - 360f : e.x;
        Pick(4); firstClick = g + f * 8f + up * 0.9f;
        for (int i = 0; i < 20; i++) yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "sketch-edit.png"));
        yield return new WaitForSeconds(0.5f);
        firstClick = null;
        HideLevel(true);
        for (int i = 0; i < 5; i++) yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "sketch-blank.png"));
        yield return new WaitForSeconds(0.5f);
        HideLevel(false);

        // played: onto the jump pad, then into the speed rail
        cam.transform.rotation = Quaternion.LookRotation(f);
        SetEditing(false);
        Vector3 pad = g + f * 4f + r * 1.5f;
        var spawn = GameObject.Find("Sketch spawn").transform;
        spawn.SetPositionAndRotation(pad + up * 0.6f, Quaternion.LookRotation(f));
        player.Respawn();
        float best = 0f;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime) { best = Mathf.Max(best, player.transform.position.y - pad.y); yield return null; }
        Debug.Log("SKETCH_TEST jump pad threw you " + best.ToString("F1") + "m up " + (best > 12f ? "PASS" : "FAIL"));
        Vector3 sr = g + f * 1f - r * 2.5f;
        spawn.SetPositionAndRotation(sr + up * 0.1f, Quaternion.LookRotation(f));
        player.Respawn();
        float t0 = Time.time; bool ground = false;
        best = 0f;
        while (Time.time - t0 < 3f)
        {
            // ride forward into it
            player.GetComponent<Rigidbody>().AddForce(f * 30f, ForceMode.Acceleration);
            ground |= player.Grinding;
            best = Mathf.Max(best, player.transform.position.y - g.y);
            yield return null;
        }
        Debug.Log("SKETCH_TEST speed rail " + (ground ? "grabbed you" : "DIDN'T GRAB YOU") + ", got you " + best.ToString("F1") + "m up " + (ground && best > 6f ? "PASS" : "FAIL"));
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "sketch-play.png"));
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }

    static string Folder()
    {
        string top = Path.GetFullPath(Application.isEditor ? Path.Combine(Application.dataPath, "..") : Path.Combine(Application.dataPath, "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(top, "Assets"))) top = Application.persistentDataPath;
        return Path.Combine(top, "Layouts");
    }

    // ---- saving and loading -------------------------------------------------------------------------------

    void Load()
    {
        if (!File.Exists(path)) return;
        try { sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(path)) ?? new Sheet(); }
        catch (Exception e) { Debug.LogWarning("Couldn't read " + path + ": " + e.Message); sheet = new Sheet(); }
        RebuildAll();
        if (sheet.levelHidden) HideLevel(true);
    }

    void Save()
    {
        sheet.level = SceneManager.GetActiveScene().name;
        sheet.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(sheet, true));
            Say("Saved");
        }
        catch (Exception e) { Say("COULDN'T SAVE: " + e.Message); }
    }

    void Say(string s) { status = s; statusTimer = 2.5f; }

    // remember how it was (for Ctrl+Z), then save once it's changed
    void BeforeChange() { undo.Add(JsonUtility.ToJson(sheet)); if (undo.Count > 200) undo.RemoveAt(0); }

    void Undo()
    {
        if (undo.Count == 0) { Say("Nothing to undo"); return; }
        bool wasHidden = sheet.levelHidden;
        sheet = JsonUtility.FromJson<Sheet>(undo[undo.Count - 1]);
        undo.RemoveAt(undo.Count - 1);
        RebuildAll();
        if (sheet.levelHidden != wasHidden) HideLevel(sheet.levelHidden);
        Save();
        Say("Undone");
    }

    void Add(Item it) { BeforeChange(); sheet.items.Add(it); built[it] = Build(it, false); AfterBuild(); Save(); }

    void Remove(Item it)
    {
        BeforeChange();
        sheet.items.Remove(it);
        if (built.TryGetValue(it, out var go)) { Destroy(go); built.Remove(it); }
        AfterBuild();
        Save();
    }

    void RebuildAll()
    {
        foreach (var go in built.Values) if (go != null) Destroy(go);
        built.Clear(); editOnly.Clear(); labels.Clear();
        foreach (var it in sheet.items) built[it] = Build(it, false);
        AfterBuild();
    }

    // the player picks up new rails and vents, and the start moves if there's a start marker
    void AfterBuild()
    {
        editOnly.RemoveAll(l => l == null);
        labels.RemoveAll(l => l == null);
        foreach (var l in editOnly) l.enabled = editing;
        if (player == null) return;
        foreach (var it in sheet.items)
            if (it.type == "start" && player.startPoint != null)
                player.startPoint.SetPositionAndRotation(it.pos + Vector3.up * 0.2f, Quaternion.Euler(0f, it.yaw, 0f));
        if (player.enabled) player.Setup();
    }

    // ---- switching between editing and playing ------------------------------------------------------------

    void SetEditing(bool on)
    {
        editing = on;
        Editing = on;
        typing = false; firstClick = null;
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (on)
            {
                player.enabled = false;
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
                player.enabled = true;
                player.Setup();
                // start playing where you were looking (or just where the camera is, if that's nowhere)
                var spawn = GameObject.Find("Sketch spawn") ?? new GameObject("Sketch spawn");
                Vector3 at = cam.transform.position;
                if (SolidRay(new Ray(cam.transform.position, cam.transform.forward), 600f, out RaycastHit hit)) at = hit.point + Vector3.up * 0.3f;
                spawn.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, camYaw, 0f));
                player.checkpoint = spawn.transform;
                player.Respawn();
            }
        }
        if (hud != null) hud.enabled = !on;
        if (on)
        {
            Vector3 e = cam.transform.eulerAngles;
            camYaw = e.y; camPitch = e.x > 180f ? e.x - 360f : e.x;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            if (ghost != null) Destroy(ghost);
            if (layerGrid != null) layerGrid.SetActive(false);
            ghostKey = null;
        }
        foreach (var l in editOnly) if (l != null) l.enabled = on;
    }

    void Update()
    {
        if (statusTimer > 0f) statusTimer -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.F2) && !typing && !LevelDrawing.Open) SetEditing(!editing);
        if (!editing) return;
        if (hideUI) hideUI = false;
        FlyCamera();
        if (typing) return;
        Keys();
        Aim();
        Ghost();
        Clicks();
    }

    void LateUpdate()
    {
        if (cam == null) return;
        foreach (var l in labels) if (l != null) l.rotation = Quaternion.LookRotation(l.position - cam.transform.position);
    }

    // hold the right mouse button to look round; WASD to fly, Space and C to go up and down, Shift to go fast
    void FlyCamera()
    {
        bool look = Input.GetMouseButton(1);
        Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !look;
        if (look)
        {
            camYaw += Input.GetAxisRaw("Mouse X") * 2.5f;
            camPitch = Mathf.Clamp(camPitch - Input.GetAxisRaw("Mouse Y") * 2.5f, -89f, 89f);
        }
        if (typing) return;
        var t = cam.transform;
        t.rotation = Quaternion.Euler(camPitch, camYaw, 0f);
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += t.forward;
        if (Input.GetKey(KeyCode.S)) move -= t.forward;
        if (Input.GetKey(KeyCode.D)) move += t.right;
        if (Input.GetKey(KeyCode.A)) move -= t.right;
        if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
        if (Input.GetKey(KeyCode.C)) move -= Vector3.up;
        bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        t.position += move * (fast ? 90f : 25f) * Time.unscaledDeltaTime;
    }

    bool Ctrl { get { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }
    bool Shift { get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }

    void Keys()
    {
        for (int i = 0; i < 10 && i < Tools.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + (i == 9 ? -1 : i)) || (i == 9 && Input.GetKeyDown(KeyCode.Alpha0))) Pick(i);
        if (Input.GetKeyDown(KeyCode.Escape)) firstClick = null;
        if (Ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
        if (Input.GetKeyDown(KeyCode.Q)) yaw -= Shift ? 90f : 15f;
        if (Input.GetKeyDown(KeyCode.E)) yaw += Shift ? 90f : 15f;
        if (Input.GetKeyDown(KeyCode.H)) ToggleLayer();
        if (Input.GetKeyDown(KeyCode.P)) Photo();

        // R/F: up and down (held, it keeps going)
        int ud = (Input.GetKey(KeyCode.R) ? 1 : 0) - (Input.GetKey(KeyCode.F) ? 1 : 0);
        if (ud == 0) repeat = 0f;
        else
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F)) { Lift(ud); repeat = 0.35f; }
            else if ((repeat -= Time.unscaledDeltaTime) <= 0f) { Lift(ud); repeat = 0.06f; }
        }

        // the wheel sizes it (in steps that get bigger as it does)
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !OverUI())
        {
            int dir = wheel > 0f ? 1 : -1;
            string type = Tools[tool].type;
            System.Func<float, float, float, float> step = (v, min, max) => Mathf.Clamp(v + dir * Mathf.Max(1f, Mathf.Round(Mathf.Abs(v) * 0.1f)), min, max);
            if (type == "bus") { if (Ctrl) size.y = step(size.y, 3f, 60f); else if (Shift) size.z = Mathf.Clamp(size.z + dir * 0.5f, 0.5f, 15f); }
            else if (type == "jumppad") { if (Shift) size.z = step(size.z, 0f, 150f); else size.x = step(size.x, 2f, 150f); }
            else if (type == "vent") { if (Ctrl) size.y = step(size.y, 3f, 200f); else size.x = step(size.x, 2f, 12f); }
            else if (type == "wall") { if (Ctrl) size.y = step(size.y, 0.5f, 100f); else if (Shift) size.z = Mathf.Clamp(size.z + dir * 0.2f, 0.2f, 6f); else size.x = step(size.x, 1f, 300f); }
            else if (!Tools[tool].twoClicks && type != "note" && type != "start")
            {
                if (Ctrl) size.y = step(size.y, 0.5f, 400f);
                else if (Shift) size.z = step(size.z, 1f, 300f);
                else { size.x = step(size.x, 1f, 300f); size.z = step(size.z, 1f, 300f); }
            }
        }

        // pointing at something you've put down: X deletes it, G picks it up to move, middle click copies it
        if (hovered != null)
        {
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Delete)) { Remove(hovered); hovered = null; Say("Deleted"); }
            else if (Input.GetKeyDown(KeyCode.G)) { var h = hovered; Carry(h); Remove(h); hovered = null; }
            else if (Input.GetMouseButtonDown(2)) { Carry(hovered); Say("Copied: click to put another one down"); }
        }
    }

    void Lift(int ud)
    {
        float s = Shift ? 5f : 1f;
        if (layerMode) layerY = Mathf.Max(0f, layerY + ud * s);
        else lift = Mathf.Max(0f, lift + ud * s);
    }

    void ToggleLayer()
    {
        layerMode = !layerMode;
        if (layerMode) layerY = Mathf.Round(aimOk ? aim.y : cam.transform.position.y - 10f);
        Say(layerMode ? "Layer on: placing on an invisible floor (R/F moves it)" : "Layer off: placing on whatever you point at");
    }

    void Pick(int i)
    {
        if (tool != i) { tool = i; size = Tools[i].size; carryNote = ""; }
        firstClick = null;
    }

    void Carry(Item it)
    {
        int i = Array.FindIndex(Tools, t => t.type == it.type);
        tool = Mathf.Max(0, i);
        size = it.size; yaw = it.yaw; carryNote = it.note;
        firstClick = null;
    }

    // ---- where you're pointing ---------------------------------------------------------------------------

    bool SolidRay(Ray r, float dist, out RaycastHit best)
    {
        best = default;
        float bd = float.MaxValue;
        bool any = false;
        foreach (var h in Physics.RaycastAll(r, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (player != null && h.collider.transform.IsChildOf(player.transform)) continue;
            if (h.distance < bd) { bd = h.distance; best = h; any = true; }
        }
        return any;
    }

    void Aim()
    {
        aimOk = false;
        hovered = null;
        if (OverUI()) return;
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        // what's under the mouse that you've put down (rails and notes have pick-up boxes)
        if (firstClick == null)
        {
            float bd = float.MaxValue, wall = SolidRay(r, 3000f, out RaycastHit first) && ItemOf(first.collider.transform) == null ? first.distance + 0.5f : float.MaxValue;
            foreach (var h in Physics.RaycastAll(r, 1500f, ~0, QueryTriggerInteraction.Collide))
            {
                var it = ItemOf(h.collider.transform);
                if (it != null && h.distance < bd && h.distance < wall) { bd = h.distance; hovered = it; }
            }
        }
        if (layerMode)
        {
            if (Mathf.Abs(r.direction.y) < 1e-4f) return;
            float t = (layerY - r.origin.y) / r.direction.y;
            if (t < 0f || t > 3000f) return;
            aim = r.origin + r.direction * t;
            aimOk = true;
        }
        else if (SolidRay(r, 3000f, out RaycastHit hit))
        {
            aim = hit.point + Vector3.up * lift;
            aimOk = true;
        }
        else
        {
            // pointing at the sky: on a level with the ground, far off
            if (r.direction.y >= -0.01f) return;
            aim = r.origin + r.direction * (-r.origin.y / r.direction.y) + Vector3.up * lift;
            aimOk = true;
        }
        // on a half-metre grid across, so things line up
        aim.x = Mathf.Round(aim.x * 2f) / 2f;
        aim.z = Mathf.Round(aim.z * 2f) / 2f;
        if (layerMode) aim.y = layerY;
    }

    Item ItemOf(Transform t)
    {
        while (t != null && t.parent != root) t = t.parent;
        if (t == null) return null;
        foreach (var kv in built) if (kv.Value != null && kv.Value.transform == t) return kv.Key;
        return null;
    }

    bool OverUI()
    {
        Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        foreach (var r in uiRects) if (r.Contains(m)) return true;
        return typing;
    }

    // a rail sits a bit above what you click on: high enough to jump onto, a speed rail low enough to ride into
    Vector3 RailPoint(Vector3 p) { return p + Vector3.up * (Tools[tool].type == "rail" ? 0.9f : Tools[tool].type == "speedrail" ? 0.6f : 0f); }

    Item Current()
    {
        var t = Tools[tool];
        var it = new Item { type = t.type, yaw = yaw, size = size, note = carryNote };
        if (t.twoClicks)
        {
            it.pos = firstClick ?? RailPoint(aim);
            it.end = firstClick != null ? RailPoint(aim) : it.pos + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.01f;
        }
        else it.pos = aim;
        return it;
    }

    void Ghost()
    {
        if (layerGrid == null)
        {
            layerGrid = Part(null, PrimitiveType.Cube, Vector3.zero, new Vector3(400f, 0.02f, 400f), 0f, new Color(0.4f, 0.9f, 1f, 0.06f), false, true, 0.9f);
            layerGrid.name = "Sketch layer";
        }
        layerGrid.SetActive(layerMode);
        if (layerMode) layerGrid.transform.position = new Vector3(Mathf.Round(cam.transform.position.x / 5f) * 5f, layerY - 0.01f, Mathf.Round(cam.transform.position.z / 5f) * 5f);

        // (no preview while you're pointing at something you've put down: that's for X, G and copying)
        if (!aimOk || hovered != null && firstClick == null)
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }
        var it = Current();
        // things that stay the same shape wherever you point are built once at the origin and moved about
        bool moves = !Tools[tool].twoClicks && it.type != "jumppad";
        string key = it.type + it.size + it.yaw + carryNote + (moves ? "" : it.pos.ToString() + it.end);
        if (key != ghostKey || ghost == null)
        {
            if (ghost != null) Destroy(ghost);
            if (moves) it.pos = Vector3.zero;
            ghost = Build(it, true);
            ghost.name = "Sketch ghost";
            ghostKey = key;
        }
        ghost.SetActive(true);
        if (moves) ghost.transform.position = aim;
    }

    void Clicks()
    {
        if (!Input.GetMouseButtonDown(0) || OverUI() || !aimOk) return;
        clearArmed = false;
        var t = Tools[tool];
        if (t.twoClicks)
        {
            if (firstClick == null) { firstClick = RailPoint(aim); return; }
            var it = Current();
            if (Vector3.Distance(it.pos, it.end) < 1f) return;
            Add(it);
            firstClick = null;
            return;
        }
        if (t.type == "note")
        {
            noteAt = aim; noteText = carryNote; typing = true;
            return;
        }
        if (t.type == "start")
        {
            // only one start: it moves
            BeforeChange();
            foreach (var old in sheet.items.FindAll(x => x.type == "start")) { sheet.items.Remove(old); if (built.TryGetValue(old, out var g)) Destroy(g); built.Remove(old); }
            var st = Current();
            sheet.items.Add(st); built[st] = Build(st, false); AfterBuild(); Save();
            return;
        }
        Add(Current());
    }

    // ---- building what's on the sheet --------------------------------------------------------------------

    static Shader shader;
    static readonly Dictionary<Color, Material> solids = new Dictionary<Color, Material>(), ghosts = new Dictionary<Color, Material>();

    static Material Mat(Color c, bool seeThrough, float grid = 0.35f)
    {
        if (shader == null) shader = Shader.Find("3daigamesolo/Sketch");
        var cache = seeThrough ? ghosts : solids;
        Color key = new Color(c.r, c.g, c.b, c.a + grid * 0.001f);
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(shader);
        m.SetColor("_Color", seeThrough ? new Color(c.r, c.g, c.b, c.a < 1f ? c.a : 0.45f) : c);
        m.SetFloat("_Grid", grid);
        if (seeThrough)
        {
            m.SetFloat("_Src", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_Dst", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = 3000;
        }
        cache[key] = m;
        return m;
    }

    // a primitive shape in `parent`, coloured, with or without its collider
    static GameObject Part(Transform parent, PrimitiveType shape, Vector3 at, Vector3 scale, float yaw, Color c, bool collide, bool seeThrough, float grid = 0.35f)
    {
        var go = GameObject.CreatePrimitive(shape);
        if (!collide) DestroyImmediate(go.GetComponent<Collider>());
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, seeThrough, grid);
        return go;
    }

    // a bar from a to b
    static GameObject Bar(Transform parent, Vector3 a, Vector3 b, float thick, Color c, bool seeThrough)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        Vector3 d = b - a;
        go.transform.SetPositionAndRotation((a + b) / 2f, d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : Quaternion.identity);
        go.transform.localScale = new Vector3(thick, thick, d.magnitude);
        go.GetComponent<Renderer>().sharedMaterial = Mat(c, seeThrough, 0f);
        return go;
    }

    LineRenderer Line(Transform parent, IList<Vector3> pts, Color c, float width, bool always)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
        lr.widthMultiplier = width;
        lr.sharedMaterial = Mat(new Color(c.r, c.g, c.b, 0.9f), true, 0f);
        lr.sharedMaterial.SetFloat("_Lit", 0f);
        if (!always) { editOnly.Add(lr); lr.enabled = editing; }
        return lr;
    }

    // where a jump pad throws you: the arc, and where it comes down (if it hits anything)
    static Vector3 PadVelocity(Item it)
    {
        float vy = Mathf.Sqrt(2f * G * Mathf.Max(0.5f, it.size.x));
        float t = 2f * vy / G;
        return Quaternion.Euler(0f, it.yaw, 0f) * Vector3.forward * (it.size.z / t) + Vector3.up * vy;
    }

    static List<Vector3> PadArc(Item it, out Vector3? lands)
    {
        Vector3 v = PadVelocity(it), p = it.pos + Vector3.up * 0.5f;
        var pts = new List<Vector3> { p };
        lands = null;
        for (int i = 0; i < 600; i++)
        {
            Vector3 q = p + v * 0.02f;
            v += Vector3.down * G * 0.02f;
            if (i > 10 && Physics.Linecast(p, q, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore)) { pts.Add(h.point); lands = h.point; break; }
            p = q;
            if (i % 3 == 0) pts.Add(p);
            if (p.y < it.pos.y - 120f) break;
        }
        return pts;
    }

    GameObject Build(Item it, bool seeThrough)
    {
        var t = Def(it.type);
        bool solid = !seeThrough;
        var go = new GameObject(t.name);
        go.transform.SetParent(seeThrough ? null : root, false);
        Color c = t.color;
        Vector3 s = it.size;
        Vector3 fwd = Quaternion.Euler(0f, it.yaw, 0f) * Vector3.forward;
        switch (it.type)
        {
            case "platform":
            case "building":
                Part(go.transform, PrimitiveType.Cube, it.pos + Vector3.up * s.y / 2f, s, it.yaw, c, solid, seeThrough);
                break;
            case "wall":
                Part(go.transform, PrimitiveType.Cube, it.pos + Vector3.up * s.y / 2f, new Vector3(s.x, s.y, s.z), it.yaw, c, solid, seeThrough);
                break;
            case "ramp":
            {
                var w = new GameObject("Ramp", typeof(MeshFilter), typeof(MeshRenderer));
                w.transform.SetParent(go.transform, false);
                w.transform.SetPositionAndRotation(it.pos, Quaternion.Euler(0f, it.yaw, 0f));
                var mesh = Wedge(s.x, s.y, s.z);
                w.GetComponent<MeshFilter>().sharedMesh = mesh;
                w.GetComponent<MeshRenderer>().sharedMaterial = Mat(c, seeThrough);
                if (solid) w.AddComponent<MeshCollider>().sharedMesh = mesh;
                // an arrow up the slope
                Bar(go.transform, it.pos + Vector3.up * (s.y * 0.5f + 0.15f) - fwd * s.z * 0.2f, it.pos + Vector3.up * (s.y * 0.75f + 0.15f) + fwd * s.z * 0.2f, 0.25f, new Color(1f, 1f, 1f), seeThrough);
                break;
            }
            case "rail":
            case "speedrail":
            {
                bool speed = it.type == "speedrail";
                Bar(go.transform, it.pos, it.end, speed ? 0.26f : 0.14f, c, seeThrough);
                // little posts down from each end, and (speed rails) chevrons along it the way it goes
                foreach (var e in new[] { it.pos, it.end })
                    Bar(go.transform, e, e + Vector3.down * 0.9f, 0.08f, new Color(0.6f, 0.6f, 0.7f), seeThrough);
                if (speed)
                {
                    Vector3 d = (it.end - it.pos).normalized;
                    float len = Vector3.Distance(it.pos, it.end);
                    for (float x = 2f; x < len - 1f; x += 3f)
                    {
                        Vector3 p = it.pos + d * x;
                        Vector3 side = Vector3.Cross(d, Vector3.up).normalized * 0.35f;
                        Bar(go.transform, p + side - d * 0.4f, p, 0.1f, new Color(1f, 0.3f, 0.75f), seeThrough);
                        Bar(go.transform, p - side - d * 0.4f, p, 0.1f, new Color(1f, 0.3f, 0.75f), seeThrough);
                    }
                }
                if (solid)
                {
                    var rail = go.AddComponent<SkateRail>();
                    rail.points = new[] { it.pos, it.end };
                    rail.radius = 0.05f;
                    if (speed) { rail.catchRadius = 1.6f; rail.forceSpeed = 14f; }

                    PickBox(go.transform, it.pos, it.end, 0.8f);
                }
                break;
            }
            case "jumppad":
            {
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * 0.1f, new Vector3(6f, 0.1f, 6f), 0f, new Color(0.25f, 0.15f, 0.35f), false, seeThrough, 0f);
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * 0.13f, new Vector3(5f, 0.1f, 5f), 0f, c, false, seeThrough, 0f);
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * 0.16f, new Vector3(3f, 0.1f, 3f), 0f, new Color(0.3f, 1f, 1f), false, seeThrough, 0f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    Vector3 side = Vector3.Cross(fwd, Vector3.up) * sd;
                    Bar(go.transform, it.pos + Vector3.up * 0.3f + side * 0.9f - fwd * 0.4f, it.pos + Vector3.up * 0.3f + fwd * 1.1f, 0.3f, Color.white, seeThrough);
                }
                var arc = PadArc(it, out Vector3? lands);
                Line(go.transform, arc, c, 0.35f, false);
                if (lands.HasValue) Part(go.transform, PrimitiveType.Sphere, lands.Value, Vector3.one * 1.2f, 0f, c, false, true, 0f);
                if (solid)
                {
                    var pad = Part(go.transform, PrimitiveType.Cube, it.pos + Vector3.up * 0.1f, new Vector3(5f, 0.2f, 5f), it.yaw, c, true, false);
                    pad.GetComponent<Renderer>().enabled = false;
                    var lp = pad.AddComponent<LaunchPad>();
                    lp.velocity = PadVelocity(it);
                    lp.target = lands ?? arc[arc.Count - 1];
                }
                break;
            }
            case "vent":
            {
                float r = s.x / 2f;
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * 0.08f, new Vector3(s.x, 0.08f, s.x), 0f, new Color(0.4f, 0.45f, 0.5f), solid, seeThrough);
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * s.y / 2f, new Vector3(s.x, s.y / 2f, s.x), 0f, new Color(c.r, c.g, c.b, 0.12f), false, true, 0.6f);
                if (solid)
                {
                    var up = new GameObject("Updraft").AddComponent<Updraft>();
                    up.transform.SetParent(go.transform, false);
                    up.transform.position = it.pos;
                    up.radius = r; up.height = s.y; up.lift = Mathf.Clamp(s.y * 0.7f, 14f, 30f);
                    var pick = go.AddComponent<BoxCollider>();
                    pick.isTrigger = true; pick.center = it.pos + Vector3.up * 1f; pick.size = new Vector3(s.x, 2f, s.x);
                }
                break;
            }
            case "bus":
            {
                // the bus moves; drawn at the start of its route, with the route marked
                var bus = new GameObject("Tall bus");
                bus.SetActive(false);
                bus.transform.SetParent(go.transform, false);
                Vector3 route = it.end - it.pos; route.y = 0f;
                bus.transform.SetPositionAndRotation(it.pos, route.sqrMagnitude > 0.01f ? Quaternion.LookRotation(route) : Quaternion.identity);
                float h = s.y;
                var body = Part(bus.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one, 0f, c, false, seeThrough);
                body.transform.localPosition = new Vector3(0f, h / 2f + 0.4f, 0f); body.transform.localRotation = Quaternion.identity; body.transform.localScale = new Vector3(3f, h, 10f);
                int decks = Mathf.Max(1, Mathf.RoundToInt(h / 3f));
                for (int d = 0; d < decks; d++)
                {
                    var win = Part(bus.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one, 0f, new Color(0.45f, 0.8f, 1f), false, seeThrough, 0f);
                    win.transform.localPosition = new Vector3(0f, 0.4f + (d + 0.6f) * h / decks, 0f); win.transform.localRotation = Quaternion.identity;
                    win.transform.localScale = new Vector3(3.05f, h / decks * 0.35f, 9f);
                }
                Line(go.transform, new[] { it.pos + Vector3.up * 0.3f, it.end + Vector3.up * 0.3f }, c, 0.3f, seeThrough);
                Part(go.transform, PrimitiveType.Cube, it.end + Vector3.up * (h / 2f + 0.4f), new Vector3(3f, h, 10f), Quaternion.LookRotation(route.sqrMagnitude > 0.01f ? route : Vector3.forward).eulerAngles.y, c, false, true, 0f)
                    .GetComponent<Renderer>().sharedMaterial = Mat(new Color(c.r, c.g, c.b, 0.15f), true, 0f);
                if (solid)
                {
                    var bc = bus.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, h / 2f + 0.4f, 0f); bc.size = new Vector3(3f, h, 10f);
                    var mover = bus.AddComponent<Mover>();
                    mover.path = new[] { it.pos, it.end };
                    mover.loop = false; mover.speed = Mathf.Max(0.5f, s.z); mover.wait = 3f; mover.easeIn = 6f; mover.turn = false;
                    PickBox(go.transform, it.pos, it.end, 1.5f);
                }
                bus.SetActive(true);
                break;
            }
            case "note":
            case "start":
            {
                bool start = it.type == "start";
                Part(go.transform, PrimitiveType.Cylinder, it.pos + Vector3.up * 1.5f, new Vector3(0.12f, 1.5f, 0.12f), 0f, new Color(0.8f, 0.8f, 0.85f), false, seeThrough, 0f);
                Part(go.transform, PrimitiveType.Cube, it.pos + Vector3.up * 2.7f + fwd * 0.5f, new Vector3(0.1f, 0.6f, 1f), it.yaw, c, false, seeThrough, 0f);
                var label = new GameObject("Label");
                label.transform.SetParent(go.transform, false);
                label.transform.position = it.pos + Vector3.up * 3.6f;
                var tm = label.AddComponent<TextMesh>();
                tm.text = start ? "START" : Wrap(string.IsNullOrEmpty(it.note) ? "(note)" : it.note, 28);
                tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
                tm.fontSize = 64; tm.characterSize = 0.14f; tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
                tm.color = start ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 1f, 0.5f);
                labels.Add(label.transform);
                if (solid)
                {
                    var pick = go.AddComponent<SphereCollider>();
                    pick.isTrigger = true; pick.center = it.pos + Vector3.up * 2.5f; pick.radius = 1.4f;
                }
                break;
            }
        }
        if (seeThrough)
            foreach (var col in go.GetComponentsInChildren<Collider>()) DestroyImmediate(col);
        return go;
    }

    // a box round a rail or a route you can point at to pick it up (not solid)
    static void PickBox(Transform parent, Vector3 a, Vector3 b, float thick)
    {
        var go = new GameObject("Pick");
        go.transform.SetParent(parent, false);
        Vector3 d = b - a;
        go.transform.SetPositionAndRotation((a + b) / 2f, d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : Quaternion.identity);
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.size = new Vector3(thick, thick, d.magnitude);
    }

    static string Wrap(string s, int width)
    {
        var words = s.Split(' ');
        var lines = new List<string>();
        string line = "";
        foreach (var w in words)
        {
            if (line.Length + w.Length + 1 > width && line.Length > 0) { lines.Add(line); line = ""; }
            line = line.Length == 0 ? w : line + " " + w;
        }
        if (line.Length > 0) lines.Add(line);
        return string.Join("\n", lines);
    }

    // a wedge `w` wide, rising to `h` over `l`, its low edge at -l/2 and its high back at +l/2
    static Mesh Wedge(float w, float h, float l)
    {
        float x = w / 2f, z = l / 2f;
        Vector3 a = new Vector3(-x, 0f, -z), b = new Vector3(x, 0f, -z), c = new Vector3(x, 0f, z), d = new Vector3(-x, 0f, z), e = new Vector3(x, h, z), f = new Vector3(-x, h, z);
        var v = new List<Vector3>(); var t = new List<int>();
        System.Action<Vector3[]> face = q =>
        {
            int i0 = v.Count;
            v.AddRange(q);
            for (int i = 1; i + 1 < q.Length; i++) { t.Add(i0); t.Add(i0 + i); t.Add(i0 + i + 1); }
        };
        face(new[] { a, f, e, b });     // the slope
        face(new[] { c, e, f, d });     // the back
        face(new[] { b, e, c });        // the sides
        face(new[] { a, d, f });
        face(new[] { a, b, c, d });     // underneath
        var m = new Mesh { name = "Ramp" };
        m.SetVertices(v); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ---- the blank canvas ----------------------------------------------------------------------------------

    // hides everything of the level's own (not you, the camera or the sky), leaving a big grid floor
    void HideLevel(bool hide)
    {
        if (!hide)
        {
            foreach (var r in hiddenR) if (r != null) r.enabled = true;
            foreach (var c in hiddenC) if (c != null) c.enabled = true;
            hiddenR.Clear(); hiddenC.Clear();
            if (floor != null) Destroy(floor);
            return;
        }
        if (floor != null) return;
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.transform == root || go == gameObject || go.name == "Sky" || go.name.StartsWith("Sketch")) continue;   // (the preview, the layer)
            if (go.GetComponentInChildren<SlideController>(true) != null || go.GetComponentInChildren<Camera>(true) != null) continue;
            if (player != null && player.board != null && player.board.IsChildOf(go.transform)) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hiddenR.Add(r); }
            foreach (var c in go.GetComponentsInChildren<Collider>()) if (c.enabled) { c.enabled = false; hiddenC.Add(c); }
        }
        floor = Part(null, PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(3000f, 1f, 3000f), 0f, new Color(0.5f, 0.52f, 0.58f), true, false, 0.5f);
        floor.name = "Sketch floor";
    }

    void Photo()
    {
        Directory.CreateDirectory(Folder());
        string file = Path.Combine(Folder(), SceneManager.GetActiveScene().name + " " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".png");
        hideUI = true;
        ScreenCapture.CaptureScreenshot(file);
        Say("Photo saved");
    }

    // ---- the screen -------------------------------------------------------------------------------------------

    static Texture2D white;
    GUIStyle Style(int sz, TextAnchor a, Color c, bool bangers = false)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = sz, alignment = a, wordWrap = true, richText = true };
        if (bangers && font != null) st.font = font;
        st.normal.textColor = c;
        return st;
    }

    void Box(Rect r, Color c)
    {
        if (white == null) white = Texture2D.whiteTexture;
        var keep = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = keep;
    }

    bool Button(Rect r, string text, Color bg, int sz = 22)
    {
        bool over = r.Contains(Event.current.mousePosition);
        Box(r, over ? Color.Lerp(bg, Color.white, 0.25f) : bg);
        GUI.Label(r, text, Style(sz, TextAnchor.MiddleCenter, Color.white));
        return GUI.Button(r, "", GUIStyle.none);
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
        float W = Screen.width / k;
        uiRects.Clear();
        System.Action<Rect> ui = r => uiRects.Add(new Rect(r.x * k, r.y * k, r.width * k, r.height * k));

        if (!editing)
        {
            if (!LevelDrawing.Open) GUI.Label(new Rect(W - 430f, 1040f, 410f, 30f), "F1  draw your level     F2  3D editor", Style(20, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.55f)));
            return;
        }
        if (hideUI) return;
        Color panel = new Color(0.08f, 0.05f, 0.14f, 0.82f), pink = new Color(1f, 0.3f, 0.7f);
        var t = Tools[tool];

        // top left: what this is, and how to get back to playing
        Rect head = new Rect(20f, 20f, 560f, 118f);
        Box(head, panel); ui(head);
        GUI.Label(new Rect(36f, 26f, 540f, 50f), "LEVEL EDITOR", Style(44, TextAnchor.UpperLeft, pink, true));
        GUI.Label(new Rect(36f, 76f, 540f, 30f), "<b>F2</b>  play it (you start where you're looking)", Style(20, TextAnchor.UpperLeft, Color.white));
        string saved = statusTimer > 0f ? status : sheet.items.Count + " things   saved in Layouts/" + Path.GetFileName(path);
        GUI.Label(new Rect(36f, 104f, 540f, 30f), saved, Style(18, TextAnchor.UpperLeft, new Color(0.6f, 1f, 0.8f)));

        // top right: buttons
        float bx = W - 20f - 220f;
        Rect side = new Rect(bx - 10f, 20f, 240f, 330f);
        Box(side, panel); ui(side);
        if (Button(new Rect(bx, 30f, 220f, 46f), "Undo  (Ctrl+Z)", new Color(0.3f, 0.25f, 0.45f))) Undo();
        if (Button(new Rect(bx, 82f, 220f, 46f), layerMode ? "Layer: ON  (H)" : "Layer: off  (H)", layerMode ? new Color(0.1f, 0.55f, 0.65f) : new Color(0.3f, 0.25f, 0.45f))) ToggleLayer();
        if (Button(new Rect(bx, 134f, 220f, 46f), sheet.levelHidden ? "Show the level" : "Blank canvas", new Color(0.3f, 0.25f, 0.45f)))
        {
            BeforeChange(); sheet.levelHidden = !sheet.levelHidden; HideLevel(sheet.levelHidden); Save();
        }
        if (Button(new Rect(bx, 186f, 220f, 46f), "Photo  (P)", new Color(0.3f, 0.25f, 0.45f))) Photo();
        if (Button(new Rect(bx, 238f, 220f, 46f), clearArmed ? "Sure? Click again" : "Clear everything", clearArmed ? new Color(0.8f, 0.15f, 0.25f) : new Color(0.3f, 0.25f, 0.45f)))
        {
            if (!clearArmed) clearArmed = true;
            else { BeforeChange(); sheet.items.Clear(); RebuildAll(); Save(); clearArmed = false; Say("Cleared (Ctrl+Z brings it back)"); }
        }
        if (Button(new Rect(bx, 290f, 220f, 46f), showHelp ? "Hide help" : "Help", new Color(0.3f, 0.25f, 0.45f))) showHelp = !showHelp;

        if (showHelp)
        {
            Rect help = new Rect(W - 20f - 430f, 360f, 430f, 330f);
            Box(help, panel); ui(help);
            GUI.Label(new Rect(help.x + 16f, help.y + 10f, 400f, 320f),
                "<b>MOVING ROUND</b>\nHold <b>right mouse</b>: look\n<b>WASD</b>: fly    <b>Space / C</b>: up / down\n<b>Shift</b>: faster\n\n" +
                "<b>PUTTING THINGS DOWN</b>\n<b>Left click</b>: put it there\n<b>Q / E</b>: turn it    <b>R / F</b>: higher / lower\n<b>H</b>: layer (an invisible floor to build up in the sky)\n\n" +
                "<b>POINT AT SOMETHING YOU'VE PUT DOWN</b>\n<b>X</b>: delete    <b>G</b>: pick up and move\n<b>Middle click</b>: copy it",
                Style(18, TextAnchor.UpperLeft, Color.white));
        }

        // along the bottom: the things you can put down
        float bw = 150f, bh = 92f, gap = 8f, total = Tools.Length * (bw + gap) - gap;
        float scale = Mathf.Min(1f, (W - 40f) / total);
        bw *= scale; gap *= scale; total *= scale;
        float x0 = (W - total) / 2f, y0 = 1080f - bh - 18f;
        Rect bar = new Rect(x0 - 10f, y0 - 10f, total + 20f, bh + 20f);
        Box(bar, panel); ui(bar);
        for (int i = 0; i < Tools.Length; i++)
        {
            Rect r = new Rect(x0 + i * (bw + gap), y0, bw, bh);
            bool on = i == tool;
            Box(r, on ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.2f, 0.17f, 0.3f));
            Box(new Rect(r.x + 8f, r.y + 8f, r.width - 16f, 34f), Tools[i].color);
            GUI.Label(new Rect(r.x, r.y + 44f, r.width, 26f), Tools[i].name, Style(Mathf.RoundToInt(21 * scale + 1), TextAnchor.MiddleCenter, on ? Color.black : Color.white));
            string key = i < 9 ? (i + 1).ToString() : i == 9 ? "0" : "";
            GUI.Label(new Rect(r.x, r.y + 68f, r.width, 22f), key, Style(16, TextAnchor.MiddleCenter, on ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.8f)));
            if (GUI.Button(r, "", GUIStyle.none)) Pick(i);
        }

        // above it: what the chosen thing is and how to size it, and its size now
        Rect info = new Rect(x0 - 10f, y0 - 128f, Mathf.Max(total + 20f, 900f), 108f);
        info.x = (W - info.width) / 2f;
        Box(info, panel); ui(info);
        GUI.Label(new Rect(info.x + 16f, info.y + 8f, info.width - 32f, 30f), "<b>" + t.name.ToUpper() + "</b>   " + t.help, Style(20, TextAnchor.UpperLeft, Color.white));
        GUI.Label(new Rect(info.x + 16f, info.y + 42f, info.width - 32f, 28f), SizeText(), Style(22, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.3f)));
        string hint = (t.sizeHelp.Length > 0 ? t.sizeHelp + "     " : "") + (t.type == "note" || t.type == "start" ? "" : "Q/E: turn     ") + (layerMode ? "R/F: move the layer" : "R/F: higher / lower");
        if (t.twoClicks) hint = (firstClick == null ? "Click where it STARTS" : "Now click where it ENDS (Esc: cancel)") + "     " + hint;
        GUI.Label(new Rect(info.x + 16f, info.y + 74f, info.width - 32f, 28f), hint, Style(18, TextAnchor.UpperLeft, new Color(0.75f, 0.85f, 1f)));

        // by the mouse: how high it's going, or what you're pointing at
        Vector2 m = Event.current.mousePosition;
        if (hovered != null && firstClick == null)
            Tip(m, Def(hovered.type).name + (hovered.type == "note" ? ": " + hovered.note : "") + "\n<b>X</b> delete   <b>G</b> move   <b>middle click</b> copy");
        else if (aimOk && !OverUI())
        {
            float ground = aim.y;
            if (SolidRay(new Ray(aim + Vector3.up * 0.05f, Vector3.down), 3000f, out RaycastHit below)) ground = below.point.y;
            string h = Mathf.RoundToInt(aim.y) + " m up" + (aim.y - ground > 0.5f ? "  (" + Mathf.RoundToInt(aim.y - ground) + " m above what's under it)" : "");
            if (layerMode) h = "Layer at " + Mathf.RoundToInt(layerY) + " m";
            else if (lift > 0f) h += "\nraised " + lift + " m (R/F)";
            Tip(m, h);
        }

        // typing a note
        if (typing)
        {
            Rect nb = new Rect(W / 2f - 380f, 380f, 760f, 200f);
            Box(nb, new Color(0.08f, 0.05f, 0.14f, 0.95f)); ui(nb);
            GUI.Label(new Rect(nb.x + 20f, nb.y + 14f, 720f, 34f), "Write a note: what should go here?", Style(26, TextAnchor.UpperLeft, pink, true));
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool esc = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            GUI.SetNextControlName("note");
            var fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 22 };
            noteText = GUI.TextField(new Rect(nb.x + 20f, nb.y + 60f, 720f, 44f), noteText, 300, fieldStyle);
            GUI.FocusControl("note");
            if (Button(new Rect(nb.x + 20f, nb.y + 124f, 200f, 50f), "OK (Enter)", new Color(0.15f, 0.6f, 0.35f)) || enter)
            {
                typing = false;
                if (noteText.Trim().Length > 0) Add(new Item { type = "note", pos = noteAt, yaw = yaw, size = Vector3.one, note = noteText.Trim() });
                carryNote = "";
                if (enter) Event.current.Use();
            }
            if (Button(new Rect(nb.x + 240f, nb.y + 124f, 200f, 50f), "Cancel (Esc)", new Color(0.4f, 0.2f, 0.3f)) || esc) { typing = false; if (esc) Event.current.Use(); }
        }
    }

    void Tip(Vector2 m, string text)
    {
        var st = Style(18, TextAnchor.UpperLeft, Color.white);
        Vector2 sz = st.CalcSize(new GUIContent(text.Replace("<b>", "").Replace("</b>", "")));
        Rect r = new Rect(m.x + 22f, m.y + 18f, sz.x + 20f, sz.y + 12f);
        Box(r, new Color(0f, 0f, 0f, 0.7f));
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, sz.x + 4f, sz.y + 4f), text, st);
    }

    string SizeText()
    {
        var t = Tools[tool];
        Vector3 s = size;
        switch (t.type)
        {
            case "platform": return "Size: " + s.x + " x " + s.z + " m,  " + s.y + " m thick";
            case "building": return "Size: " + s.x + " x " + s.z + " m,  " + s.y + " m tall";
            case "wall": return "Size: " + s.x + " m long,  " + s.y + " m tall,  " + s.z + " m thick";
            case "ramp": return "Size: " + s.x + " m wide,  " + s.z + " m long,  " + s.y + " m high";
            case "jumppad": return "Throws you " + s.x + " m up and " + s.z + " m along";
            case "vent": return "Size: " + s.x + " m across, blows you " + s.y + " m up";
            case "bus": return "A " + s.y + " m tall bus, going " + s.z + " m/s" + (firstClick != null && aimOk ? ",  route " + Mathf.RoundToInt(Vector3.Distance(firstClick.Value, aim)) + " m" : "");
            case "rail":
            case "speedrail":
                if (firstClick != null && aimOk)
                {
                    Vector3 e = RailPoint(aim);
                    return "Length " + Mathf.RoundToInt(Vector3.Distance(firstClick.Value, e)) + " m,  climbs " + Mathf.RoundToInt(e.y - firstClick.Value.y) + " m";
                }
                return t.type == "rail" ? "Sits 0.9 m above what you click on" : "Sits 0.6 m above what you click on (ride into it)";
            default: return "";
        }
    }
}
