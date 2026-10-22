using UnityEngine;

// The Dream level, after LSD: Dream Emulator. Several dreams, each its own little world a long way from the
// others (each with its own sky and fog). Bump into one of the things that "link" (LinkWall), or fall off the
// edge of a dream, and the screen flashes white and you wake up somewhere else: a different dream. Sometimes
// the Gray Man is standing about; get close and he's gone, and so are you.
[DefaultExecutionOrder(100)]
public class DreamLevel : MonoBehaviour
{
    [System.Serializable]
    public class Dream
    {
        public string name;
        public Transform spawn;
        public Color sky, fog;
        public float fogStart = 20f, fogEnd = 160f;
        public Vector3 grayManAt;   // where the Gray Man can stand in it (y < -1000: never here)
        public float floorY;        // fall this far below it and you're linked
    }

    public SlideController player;
    public Dream[] dreams;
    public Transform grayMan;
    [Tooltip("How close the Gray Man lets you get.")]
    public float grayManNear = 7f;
    public int seed = 7;

    public int Current { get; private set; }
    public int Links { get; private set; }
    public bool MetGrayMan { get; private set; }

    System.Random rng;
    float flash, cooldown;
    Texture2D white;

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<SlideController>();
        rng = new System.Random(System.Environment.TickCount);
        Current = Nearest(player.transform.position);
        Apply(Current);
        PlaceGrayMan();
    }

    void FixedUpdate() { Tick(Time.fixedDeltaTime); }

    // which dream p is in (they're laid out far apart along x)
    public int Nearest(Vector3 p)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < dreams.Length; i++)
        {
            float d = Mathf.Abs(dreams[i].spawn.position.x - p.x);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // links you when you bump into a link or fall off, or meet the Gray Man (public so the tests can run it)
    public void Tick(float dt)
    {
        if (rng == null) rng = new System.Random(seed);
        cooldown -= dt;
        flash = Mathf.Max(0f, flash - dt * 1.6f);
        if (cooldown > 0f) return;
        Vector3 p = player.transform.position;
        Current = Nearest(p);
        var d = dreams[Current];
        if (p.y < d.floorY - 25f) { Link("FELL"); return; }
        foreach (var col in Physics.OverlapSphere(p + Vector3.up * 0.9f, 0.75f, ~0, QueryTriggerInteraction.Ignore))
            if (col.GetComponentInParent<LinkWall>() != null) { Link("LINK"); return; }
        if (grayMan != null && grayMan.gameObject.activeSelf && (grayMan.position - p).sqrMagnitude < grayManNear * grayManNear)
        {
            MetGrayMan = true;
            grayMan.gameObject.SetActive(false);
            Link("GRAY MAN");
        }
    }

    // wake up in another dream
    public void Link(string why)
    {
        int next = Current;
        if (dreams.Length > 1) while (next == Current) next = rng.Next(dreams.Length);
        Current = next;
        Links++;
        player.checkpoint = dreams[next].spawn;
        player.Respawn();
        Apply(next);
        flash = 1f;
        cooldown = 1f;
        SlideHUD.Flash(dreams[next].name);
        PlaceGrayMan();
    }

    void Apply(int i)
    {
        var cam = GetComponent<Camera>();
        if (cam != null) cam.backgroundColor = dreams[i].sky;
        var psx = GetComponent<PSXCamera>();
        if (psx != null) { psx.fogColor = dreams[i].fog; psx.fogStart = dreams[i].fogStart; psx.fogEnd = dreams[i].fogEnd; psx.Apply(); }
    }

    // the Gray Man turns up in a dream you're not in, now and then
    void PlaceGrayMan()
    {
        if (grayMan == null) return;
        int tries = 0, i;
        do { i = rng.Next(dreams.Length); tries++; } while ((i == Current || dreams[i].grayManAt.y < -1000f) && tries < 20);
        bool show = dreams[i].grayManAt.y > -1000f && i != Current && rng.NextDouble() < 0.7;
        grayMan.gameObject.SetActive(show);
        if (show) grayMan.position = dreams[i].grayManAt;
    }

    void OnGUI()
    {
        if (flash <= 0f) return;
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, flash);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = old;
    }
}
