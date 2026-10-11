using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Infinity: after a fly-through of a 3D fractal. One great tower whose top is a plaza with four arms, each
// arm flaring up at its end into a curved bank (a quarter pipe); off its four corners bridges run down to
// four towers half its size that do the same, and so on, four generations deep. The whole thing is
// copied all round you into the dark, and upside down overhead, so it seems to go on for ever, and
// everything glows in one colour that slowly cycles (cyan, green, red, white, orange...).
//
// Skating it: ride the flared banks, drop down the bridges from one generation to the next (grind their
// edges), and from the tips of the smallest towers a launch pad throws you all the way back up to the top.
public static partial class SkateWorldBuilder
{
    public const string InfinityPath = "Assets/Scenes/Infinity.unity";

    const float InfTop = 0f, InfW = 36f;   // the great tower's plaza: its height and width
    const int InfDepth = 3;               // generations below it
    static SkateMeshKit infKit;
    static Transform infRoot, infCol;
    public static readonly List<(Vector3 centre, float w, int gen)> infTowers = new List<(Vector3, float, int)>();
    public static readonly List<(Vector3 a, Vector3 b)> infBridges = new List<(Vector3, Vector3)>();
    public static readonly List<(Vector3 pad, Vector3 land)> infPads = new List<(Vector3, Vector3)>();

    public static void InfinityFromCommandLine()
    {
        try { BuildInfinity(); SlideTests.RunInfinity(); InfinityShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildInfinity()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/InfinityFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        infTowers.Clear(); infBridges.Clear(); infPads.Clear();
        floorAt = (x, z) => InfTop;
        RampTop = new Color32(255, 255, 255, 255); RampSide = new Color32(150, 150, 150, 255); RampSide2 = new Color32(200, 200, 200, 255);
        Metal = new Color32(255, 255, 255, 255); RailCol = new Color32(255, 255, 255, 255);
        Concrete = new Color32(180, 180, 180, 255); ConcreteDark = new Color32(120, 120, 120, 255);

        toonMat = Mat("InfinityFractal", "3daigamesolo/World/Fractal");
        outlineMat = Mat("InfinityOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", Color.black);
        outlineMat.SetFloat("_OutlineWidth", 1.2f);
        outlineMat.SetColor("_Haze", Color.black); outlineMat.SetColor("_HazeHigh", Color.black);
        outlineMat.SetFloat("_FogDensity", 0.0065f); outlineMat.SetFloat("_FogHeight", 100000f); outlineMat.SetFloat("_FogMax", 1f);
        farOutlineMat = outlineMat;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        featureRoot = new GameObject("Skate features").transform;
        infRoot = new GameObject("Infinity").transform;
        infCol = new GameObject("Colliders").transform;
        infCol.SetParent(infRoot, false);
        infKit = new SkateMeshKit();

        // the fractal: the great tower, and its children, and theirs...
        InfTower(new Vector3(0f, InfTop, 0f), InfW, 0, Vector2.zero);
        var mesh = infKit.Build("Fractal");
        SaveFeatureMesh(mesh);
        var main = MeshObject("Fractal", infRoot, mesh, toonMat, outlineMat);
        main.AddComponent<MeshCollider>().sharedMesh = mesh;
        // copies of it all round, into the dark, and upside down overhead (just to look at)
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f + 30f;
            Vector3 at = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 210f + Vector3.up * (i % 2 == 0 ? -45f : 35f);
            Copy(at, Quaternion.Euler(0f, a + 45f, 0f), 1f);
            Vector3 far = Quaternion.Euler(0f, a + 30f, 0f) * Vector3.forward * 470f + Vector3.up * (i % 2 == 0 ? 60f : -90f);
            Copy(far, Quaternion.Euler(0f, a, 0f), 2.2f);
        }
        Copy(new Vector3(0f, 110f, 0f), Quaternion.Euler(180f, 45f, 0f), 1f);
        Copy(new Vector3(0f, -230f, 0f), Quaternion.Euler(0f, 45f, 0f), 2.5f);
        // on the great plaza: a funbox and two kickers in the middle
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(0f, InfTop, -4.4f), Quaternion.identity, Vector3.one), 4f);
        Hump("Kicker", new Vector3(-9f, InfTop, -6f), Vector3.forward, 3.5f, 24f, 3f, RampSide2);
        Hump("Kicker", new Vector3(9f, InfTop, 6f), Vector3.back, 3.5f, 24f, 3f, RampSide2);
        // the launch pads home, from the tips
        Physics.SyncTransforms();
        RefreshRails();
        InfPads();

        // a black void with a few far stars
        var spaceMat = Mat("InfinitySpace", "3daigamesolo/World/Space");
        spaceMat.SetColor("_Deep", Color.black); spaceMat.SetColor("_Mid", new Color(0.01f, 0.01f, 0.02f));
        spaceMat.SetColor("_NebulaA", new Color(0.02f, 0.03f, 0.06f)); spaceMat.SetColor("_NebulaB", new Color(0.05f, 0.02f, 0.06f));
        spaceMat.SetFloat("_Stars", 0.25f); spaceMat.SetFloat("_BigStars", 0.1f);
        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.name = "Void";
        Object.DestroyImmediate(sky.GetComponent<Collider>());
        sky.transform.localScale = Vector3.one * 6000f;
        sky.GetComponent<Renderer>().sharedMaterial = spaceMat;

        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(new Vector3(0f, InfTop + 0.2f, -12f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 4000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -120f;
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Infinity";
        camGO.transform.SetPositionAndRotation(new Vector3(0f, InfTop + 2.5f, -16f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        player.board = BuildBoard(new Vector3(0f, InfTop, -10f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        TowerCheckpoint(infRoot, new Vector3(-12f, InfTop, -12f), "Infinity");

        EditorSceneManager.SaveScene(scene, InfinityPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_INFINITY_BUILT towers=" + infTowers.Count + " bridges=" + infBridges.Count + " pads=" + infPads.Count + " verts=" + mesh.vertexCount);
    }

    static void Copy(Vector3 at, Quaternion rot, float scale)
    {
        var mesh = infRoot.Find("Fractal").GetComponent<MeshFilter>().sharedMesh;
        var go = MeshObject("Fractal copy", infRoot, mesh, toonMat, null);
        go.transform.SetPositionAndRotation(at, rot);
        go.transform.localScale = Vector3.one * scale;
    }

    // a tower: a plaza w across at `top`, four arms flaring up at their ends, a shaft flaring out under it
    // down into the dark; and off its corners (bar the one it was reached from) bridges down to towers half
    // its size
    static void InfTower(Vector3 c, float w, int gen, Vector2 fromDiag)
    {
        infTowers.Add((c, w, gen));
        Color32 face = Shade(1f - gen * 0.12f, gen);
        float h = w * 0.5f;
        // the plaza (a slab), its underside, the shaft flaring into it
        infKit.Box(c + Vector3.down * 0.5f, new Vector3(w, 1f, w), Quaternion.identity, face);
        var rings = new List<(float y, float r)>();
        for (int i = 0; i <= 8; i++)
        {
            float t = i / 8f;
            rings.Add((c.y - 1f - t * w * 0.7f, Mathf.Lerp(h, h * 0.32f, Mathf.Sqrt(t))));
        }
        rings.Add((c.y - 280f, h * 0.32f));
        for (int i = 0; i < rings.Count - 1; i++)
        {
            var (y0, r0) = rings[i];
            var (y1, r1) = rings[i + 1];
            for (int s = 0; s < 4; s++)
            {
                Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
                Vector3 a0 = c + q * new Vector3(-r0, 0f, r0), b0 = c + q * new Vector3(r0, 0f, r0);
                Vector3 a1 = c + q * new Vector3(-r1, 0f, r1), b1 = c + q * new Vector3(r1, 0f, r1);
                a0.y = b0.y = y0; a1.y = b1.y = y1;
                infKit.Quad(a0, b0, b1, a1, q * Vector3.forward, Shade(0.75f - i * 0.05f, gen));
            }
        }
        // the four arms, each ending in a curved bank with a coping to grind
        float armLen = w * 0.45f, armW = w * 0.4f, r = Mathf.Max(1.6f, w * 0.2f);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            Vector3 d = q * Vector3.forward, side = q * Vector3.right;
            Vector3 a = c + d * (h + armLen * 0.5f);
            infKit.Box(a + Vector3.down * 0.5f, new Vector3(armW, 1f, armLen), q, face);
            Vector3 end = c + d * (h + armLen);
            InfFlare(end, d, side, armW, r, face, gen);
        }
        // children off the corners, a generation down
        if (gen >= InfDepth) return;
        float cw = w * 0.5f, drop = w * 0.28f, off = w * 1.15f;
        foreach (var diag in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
        {
            if (gen > 0 && diag == -fromDiag) continue;   // not back the way we came
            Vector3 dd = new Vector3(diag.x, 0f, diag.y);
            Vector3 cc = c + dd * off + Vector3.down * drop;
            // the bridge: from just inside this plaza's corner down to just inside the child's
            float bw = Mathf.Max(2.6f, w * 0.18f);
            Vector3 from = c + dd * (h - bw * 0.35f), to = cc - dd * (cw * 0.5f - bw * 0.35f);
            InfBridge(from, to, bw, gen);
            InfTower(cc, cw, gen + 1, diag);
        }
    }

    // a curved bank at the end of an arm: up from flat to vertical, a short deck at the top, coping on it
    static void InfFlare(Vector3 end, Vector3 d, Vector3 side, float width, float r, Color32 col, int gen)
    {
        int segs = 10;
        float hw = width * 0.5f;
        var pts = new List<Vector3>(); var nrm = new List<Vector3>();
        for (int i = 0; i <= segs; i++)
        {
            float a = i * 0.5f * Mathf.PI / segs;
            pts.Add(end + d * (r * Mathf.Sin(a)) + Vector3.up * (r * (1f - Mathf.Cos(a))));
            nrm.Add((Vector3.up * Mathf.Cos(a) - d * Mathf.Sin(a)).normalized);
        }
        Vector3 lip = end + d * r + Vector3.up * (r + 0.3f);
        pts.Add(lip); nrm.Add(-d);
        pts.Add(lip + d * 0.8f); nrm.Add(Vector3.up);
        for (int i = 0; i < pts.Count - 1; i++)
        {
            infKit.SmoothQuad(pts[i] - side * hw, pts[i + 1] - side * hw, pts[i + 1] + side * hw, pts[i] + side * hw, nrm[i], nrm[i + 1], nrm[i + 1], nrm[i], col);
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 p0 = pts[i] + side * s * hw, p1 = pts[i + 1] + side * s * hw;
                infKit.Quad(p0, p1, new Vector3(p1.x, end.y - 1f, p1.z), new Vector3(p0.x, end.y - 1f, p0.z), side * s, Shade(0.7f, gen));
            }
        }
        infKit.Quad(lip + d * 0.8f - side * hw, lip + d * 0.8f + side * hw, end + d * (r + 0.8f) + side * hw + Vector3.down, end + d * (r + 0.8f) - side * hw + Vector3.down, d, Shade(0.6f, gen));
        Rail("Coping", new[] { lip - side * (hw - 0.3f) + Vector3.up * 0.03f, lip + side * (hw - 0.3f) + Vector3.up * 0.03f }, false);
    }

    // a bridge sloping down from one plaza to the next, with a rail along each edge
    static void InfBridge(Vector3 a, Vector3 b, float width, int gen)
    {
        infBridges.Add((a, b));
        Vector3 along = b - a, flat = new Vector3(along.x, 0f, along.z);
        Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * (width * 0.5f);
        Vector3 down = Vector3.Cross(along.normalized, side.normalized);
        if (down.y > 0f) down = -down;
        Color32 top = Shade(0.95f - gen * 0.1f, gen + 1), under = Shade(0.55f, gen + 1);
        infKit.Quad(a - side, b - side, b + side, a + side, Vector3.up, top);
        infKit.Quad(a - side + down * 0.7f, b - side + down * 0.7f, b + side + down * 0.7f, a + side + down * 0.7f, Vector3.down, under);
        foreach (var s in new[] { -1f, 1f })
        {
            infKit.Quad(a + side * s, b + side * s, b + side * s + down * 0.7f, a + side * s + down * 0.7f, side * s, under);
            Rail("Bridge rail", new[] { a + side * s * 0.92f + Vector3.up * 0.55f + flat.normalized * 1.2f, b + side * s * 0.92f + Vector3.up * 0.55f - flat.normalized * 1.2f }, false);
        }
    }

    // vertex colour: a brightness (the shader does the hue), alpha shifting the hue a little per generation
    static Color32 Shade(float bright, int gen)
    {
        byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(bright * 255f), 30, 255);
        return new Color32(v, v, v, (byte)(gen * 50));
    }

    // a launch pad in the middle of every smallest tower, throwing you back up onto the great plaza
    static void InfPads()
    {
        foreach (var (c, w, gen) in infTowers)
        {
            if (gen != InfDepth) continue;
            Vector3 pad = c;
            Vector3 land = new Vector3(c.x, InfTop, c.z).normalized * 6f;
            land.y = InfTop;
            Vector3 dir = FlatDir(land - pad);
            Vector3 p0 = pad - dir * 0.8f + Vector3.up * 0.45f, p1 = land + Vector3.up * 0.35f;
            foreach (var over in new[] { 8f, 14f, 20f, 28f })
            {
                if (!Ballistic(p0, p1, over, out var vel, out var path) || !PathClear(path, 2f, 3f, 0.6f)) continue;
                GlowPad(pad, vel, path);
                infPads.Add((pad, land));
                break;
            }
        }
    }

    // a launch pad (a glowing disc with an arrow and a short trail of dots), for the toon/glow levels
    static void GlowPad(Vector3 pad, Vector3 vel, List<Vector3> path)
    {
        var k = new SkateMeshKit();
        Vector3 dir = FlatDir(vel), side = Rt(dir);
        k.Cylinder(pad, pad + Vector3.up * 0.1f, 1.25f, 16, new Color32(90, 90, 90, 255));
        k.Cylinder(pad + Vector3.up * 0.1f, pad + Vector3.up * 0.13f, 1.05f, 16, new Color32(255, 255, 255, 255));
        for (int i = 0; i < 2; i++)
        {
            Vector3 cc = pad + Vector3.up * 0.16f + dir * (i * 0.55f - 0.2f);
            foreach (var s in new[] { -1f, 1f })
                k.Box(cc + side * s * 0.24f, new Vector3(0.14f, 0.04f, 0.62f), Quaternion.LookRotation(dir + side * s * 0.9f), new Color32(40, 40, 40, 255));
        }
        for (int i = 2, n = 0; i < path.Count / 3 && n < 6; i += 2, n++)
            k.Box(path[i] + Vector3.down * 0.35f, Vector3.one * (0.22f - n * 0.025f), Quaternion.identity, new Color32(255, 255, 255, 255));
        var mesh = k.Build("Launch pad");
        SaveFeatureMesh(mesh);
        MeshObject("Launch pad", featureRoot, mesh, toonMat, null);
        var col = new GameObject("Launch pad").AddComponent<BoxCollider>();
        col.transform.SetParent(featureRoot, false);
        col.transform.SetPositionAndRotation(pad + Vector3.up * 0.05f, Quaternion.LookRotation(dir));
        col.size = new Vector3(2.1f, 0.1f, 2.1f);
        var lp = col.gameObject.AddComponent<LaunchPad>();
        lp.velocity = vel; lp.target = path[path.Count - 1];
    }

    public static void InfinityShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2f, -14f), new Vector3(30f, -6f, 30f), "i01-start.png");
        Shot(cam, new Vector3(-40f, 30f, -60f), new Vector3(20f, -10f, 20f), "i02-overview.png");
        Shot(cam, new Vector3(30f, -8f, 25f), new Vector3(70f, -18f, 30f), "i03-down-the-generations.png");
        Shot(cam, new Vector3(5f, -20f, 5f), new Vector3(0f, 60f, 0f), "i04-looking-up.png");
        Shot(cam, new Vector3(20f, 3f, 0f), new Vector3(40f, 4f, 0f), "i05-flare.png");
        Shot(cam, new Vector3(60f, -12f, 60f), new Vector3(200f, 0f, 100f), "i06-copies.png");
    }
}
