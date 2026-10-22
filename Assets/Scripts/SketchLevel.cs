using UnityEngine;

// The Sketchbook level: the courtyard's all pencil on paper, and whatever you skate on gets washed with
// green watercolour. The floor's painted a patch at a time (`cells` across by `rows` deep, each `cell` metres),
// everything else (Paintable) all at once when you ride on it or grind it. The HUD counts it up; colour in the
// whole page and the paper itself turns green.
[DefaultExecutionOrder(100)]
[ExecuteAlways]   // (so the review shots get the paper look too)
public class SketchLevel : MonoBehaviour
{
    public SlideController player;
    public Material sketch, post;
    [Tooltip("The floor: its collider, its patches (row by row), where its corner is and how big a patch is.")]
    public Collider floor;
    public Renderer[] patches;
    public int cells = 1, rows = 1;
    public Vector2 corner;
    public float cell = 8f;
    public Paintable[] things;
    [Tooltip("How long the wash takes to spread.")]
    public float spread = 0.8f;

    float[] patchPaint;
    MaterialPropertyBlock block;
    Font font;
    bool finished;
    float all;

    public int PatchesPainted { get; private set; }
    public int ThingsPainted { get; private set; }
    public bool PatchPainted(int i) { return patchPaint != null && patchPaint[i] > 0f; }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        font = Resources.Load<Font>("Bangers");
        Shader.SetGlobalFloat("_SketchAll", 0f);
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (post != null) Graphics.Blit(src, dst, post); else Graphics.Blit(src, dst);
    }

    // paints what you're on (or grinding), and lets the wash spread (public so the tests can run it)
    public void Tick(float dt)
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (patchPaint == null || patchPaint.Length != patches.Length) patchPaint = new float[patches.Length];
        var on = player.StandingOn;
        if (on != null && on == floor)
        {
            Vector3 p = player.transform.position;
            int cx = Mathf.FloorToInt((p.x - corner.x) / cell), cz = Mathf.FloorToInt((p.z - corner.y) / cell);
            if (cx >= 0 && cx < cells && cz >= 0 && cz < rows && patchPaint[cz * cells + cx] <= 0f)
            {
                patchPaint[cz * cells + cx] = 0.001f;
                PatchesPainted++;
            }
        }
        else if (on != null) Touch(on.GetComponentInParent<Paintable>());
        if (player.GrindRail != null) Touch(player.GrindRail.GetComponentInParent<Paintable>());

        for (int i = 0; i < patchPaint.Length; i++)
            if (patchPaint[i] > 0f && patchPaint[i] < 1f)
            {
                patchPaint[i] = Mathf.Min(1f, patchPaint[i] + dt / spread);
                Set(patches[i], patchPaint[i]);
            }
        foreach (var t in things)
            if (t.paint > 0f && t.paint < 1f)
            {
                t.paint = Mathf.Min(1f, t.paint + dt / spread);
                foreach (var r in t.Renderers) Set(r, t.paint);
            }

        // the whole page done: the paper itself goes green
        if (!finished && PatchesPainted == patches.Length && ThingsPainted == things.Length) { finished = true; SlideHUD.Flash("THE PAGE IS FINISHED"); }
        all = Mathf.MoveTowards(all, finished ? 1f : 0f, dt * 0.3f);
        Shader.SetGlobalFloat("_SketchAll", all);
    }

    void Touch(Paintable t)
    {
        if (t == null || t.paint > 0f) return;
        t.paint = 0.001f;
        ThingsPainted++;
        if (ThingsPainted % 5 == 0 || ThingsPainted == things.Length) SlideHUD.Flash("COLOURED IN " + ThingsPainted + " / " + things.Length);
    }

    void Set(Renderer r, float p)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetFloat("_Paint", p);
        r.SetPropertyBlock(block);
    }

    void OnGUI()
    {
        float k = Screen.height / 1080f;
        var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(38 * k), alignment = TextAnchor.UpperLeft };
        if (font != null) st.font = font;
        string text = "GROUND " + Mathf.FloorToInt(100f * PatchesPainted / Mathf.Max(1, patches.Length)) + "%     THINGS " + ThingsPainted + " / " + things.Length;
        var r = new Rect(30 * k, 24 * k, 900 * k, 60 * k);
        st.normal.textColor = new Color(0.97f, 0.96f, 0.9f);
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) GUI.Label(new Rect(r.x + x * 2, r.y + y * 2, r.width, r.height), text, st);
        st.normal.textColor = new Color(0.25f, 0.45f, 0.08f);
        GUI.Label(r, text, st);
    }
}
