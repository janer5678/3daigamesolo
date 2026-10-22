using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Dream: after LSD: Dream Emulator. Six dreams, each its own little world a kilometre from the next, all in
// noisy wobbly PlayStation textures (PSX.shader, PSXCamera at a low resolution):
//   the temple: a ring of corridors with yellow paper walls and plank floors round a courtyard with a bowl in it;
//     the white doorways in the outer walls link
//   the flesh tunnel: a long winding tube of red marbled flesh to carve up the walls of; the glowing red end links
//   the shrine: a field of yellow grass, a stone path through a row of red torii, fox statues on granite plinths,
//     lanterns, a fence to grind; the blue wall at the end links, and so does falling off the far edge
//   the red field: green hills under a red sky, a long white building with a teal roof you can skate up; its dark
//     doorways link, and so do the edges
//   the neon pool: a loud purple patterned floor, a square bowl, a funbox, under a bright green sky, walled in by
//     mosaic that links if you touch it
//   the kanji void: platforms of glowing kanji floating in the dark, joined by rails; fall and you're linked
// The Gray Man turns up now and then. Get close and he's gone, and so are you (DreamLevel).
public static partial class SkateWorldBuilder
{
    public const string DreamPath = "Assets/Scenes/Dream.unity";
    const string DreamTex = "Assets/Dream/Textures", DreamMats = "Assets/Dream/Materials";
    public const float DreamSpacing = 1000f;
    public static Vector3 DreamO(int i) { return new Vector3(i * DreamSpacing, 0f, 0f); }

    static readonly Dictionary<string, Material> dMats = new Dictionary<string, Material>();
    static readonly Dictionary<string, PSXKit> dKits = new Dictionary<string, PSXKit>();
    static Transform dRoot, dCol;

    public static void DreamFromCommandLine()
    {
        try { BuildDream(); SlideTests.RunDream(); DreamShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static Material DMat(string tex, bool bright)
    {
        string key = tex + (bright ? "_e" : "");
        if (dMats.TryGetValue(key, out var m)) return m;
        Directory.CreateDirectory(DreamMats);
        string path = DreamMats + "/" + key + ".mat";
        var sh = Shader.Find("3daigamesolo/PSX/Textured");
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(DreamTex + "/" + tex + ".png"));
        m.SetFloat("_Emission", bright ? 1f : 0f);
        m.SetVector("_Scroll", Vector4.zero); m.SetVector("_Flicker", new Vector4(0f, 1f, 1f, 0f)); m.SetFloat("_Cutoff", 0f);
        EditorUtility.SetDirty(m);
        dMats[key] = m;
        return m;
    }

    // a kit drawn in one texture; `solid` ones get a mesh collider too
    static PSXKit DK(string tex, bool bright = false, bool solid = false)
    {
        string key = tex + (bright ? "_e" : "") + (solid ? "|solid" : "");
        if (!dKits.TryGetValue(key, out var k)) { k = new PSXKit(); dKits[key] = k; DMat(tex, bright); }
        return k;
    }

    static void FlushDream(Transform parent, bool link = false)
    {
        foreach (var kv in dKits)
        {
            if (kv.Value.Empty) continue;
            string[] parts = kv.Key.Split('|');
            var mesh = kv.Value.Build(parent.name + " " + parts[0]);
            SaveFeatureMesh(mesh);
            var go = new GameObject(parts[0], typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = dMats[parts[0]];
            if (parts.Length > 1) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            if (link) go.AddComponent<LinkWall>();
        }
        dKits.Clear();
    }

    static Transform DGroup(string name, int dream)
    {
        var g = new GameObject(name).transform;
        g.SetParent(dRoot, false);
        return g;
    }

    static void DBox(Transform parent, Vector3 c, Vector3 size, Quaternion? rot = null) { AddBox(parent, "Solid", c, size, rot); }

    // something that links: drawn bright, and a collider marked LinkWall
    static void DLink(Transform parent, string tex, Vector3 c, Vector3 size, Quaternion? rot = null)
    {
        var g = new GameObject("Link").transform;
        g.SetParent(parent, false);
        g.gameObject.AddComponent<LinkWall>();
        DK(tex, true).Box(c, size, rot ?? Quaternion.identity, 2f, 2f);
        AddBox(g, "Link", c, size, rot);
    }

    public static void BuildDream()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/DreamFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); dMats.Clear(); dKits.Clear();
        floorAt = (x, z) => 0f;
        RampTop = new Color32(200, 190, 150, 255); RampSide = new Color32(150, 120, 80, 255); RampSide2 = new Color32(120, 100, 70, 255);
        Metal = new Color32(150, 150, 160, 255); RailCol = new Color32(200, 60, 40, 255); Concrete = new Color32(170, 165, 150, 255); ConcreteDark = new Color32(110, 105, 95, 255);

        toonMat = DMat("white", false);   // the skate features: vertex colours, PSX-shaded
        outlineMat = Mat("DreamOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.05f, 0.04f, 0.05f)); outlineMat.SetFloat("_OutlineWidth", 0.25f);
        outlineMat.SetFloat("_FogDensity", 0.02f);
        farOutlineMat = outlineMat;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;
        dRoot = new GameObject("Dreams").transform;

        var dreams = new List<DreamLevel.Dream>
        {
            DTemple(0), DFlesh(1), DShrine(2), DRedField(3), DNeon(4), DKanji(5),
        };
        var grayMan = DGrayMan();

        // you wake up in the temple
        Transform start = dreams[0].spawn;
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(PSXCamera), typeof(DreamLevel));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 900f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = dreams[0].sky;
        var psx = camGO.GetComponent<PSXCamera>();
        psx.fogColor = dreams[0].fog; psx.fogStart = dreams[0].fogStart; psx.fogEnd = dreams[0].fogEnd;
        psx.height = 300; psx.snapGrid = new Vector2(320f, 180f); psx.affine = 0.45f;
        psx.ambient = new Color(0.7f, 0.68f, 0.66f); psx.moonStrength = 0.4f; psx.moonDirection = new Vector3(-0.3f, 0.8f, -0.4f);
        psx.Apply();
        var player = BuildPlayer(start, camGO.transform);
        player.fallLimit = -400f;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "Dream";
        camGO.transform.SetPositionAndRotation(start.position + start.rotation * new Vector3(0f, 2.5f, -4f), start.rotation);
        player.board = BuildBoard(start.position + start.forward * 2f, start.rotation);
        var level = camGO.GetComponent<DreamLevel>();
        level.player = player; level.dreams = dreams.ToArray(); level.grayMan = grayMan;

        EditorSceneManager.SaveScene(scene, DreamPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_DREAM_BUILT features=" + featureCount + " dreams=" + dreams.Count + " materials=" + dMats.Count);
    }

    static DreamLevel.Dream DNew(string name, int i, Vector3 spawnLocal, float yaw, Color sky, Color fog, float fogStart, float fogEnd, Vector3 grayLocal, bool gray = true)
    {
        var s = new GameObject("Dream " + i + " start").transform;
        s.SetParent(dRoot, false);
        s.SetPositionAndRotation(DreamO(i) + spawnLocal, Quaternion.Euler(0f, yaw, 0f));
        return new DreamLevel.Dream { name = name, spawn = s, sky = sky, fog = fog, fogStart = fogStart, fogEnd = fogEnd, grayManAt = gray ? DreamO(i) + grayLocal : new Vector3(0f, -9999f, 0f), floorY = 0f };
    }

    // ---- the temple: a ring of yellow corridors round a courtyard ------------------------------------------------

    static DreamLevel.Dream DTemple(int i)
    {
        Vector3 o = DreamO(i);
        var g = DGroup("Temple", i);
        float outer = 34f, inner = 20f, h = 6f;
        DK("wood", false, true).Ground(o.x - outer, o.z - outer, o.x + outer, o.z + outer, 0f, 3f);
        // outer walls (white doorways in the middle of each, which link), inner walls with gaps to the courtyard
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            Vector3 n = q * Vector3.back, along = q * Vector3.right;
            Vector3 wallC = o + q * new Vector3(0f, h * 0.5f, outer);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = wallC + along * side * (outer * 0.5f + 2f);
                DK("yellowwall").Box(c, Abs(q * new Vector3(outer - 4f, h, 0.6f)), Quaternion.identity, 4f, h);
                DBox(g, c, Abs(q * new Vector3(outer - 4f, h, 0.6f)));
            }
            DLink(g, "white", wallC + n * 0.1f, Abs(q * new Vector3(8.2f, h, 0.6f)));
            // the inner wall, with a gap in the middle into the courtyard
            Vector3 innerC = o + q * new Vector3(0f, h * 0.5f, inner);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = innerC + along * side * (inner * 0.5f + 2f);
                DK("yellowwall").Box(c, Abs(q * new Vector3(inner - 4f, h, 0.5f)), Quaternion.identity, 4f, h);
                DBox(g, c, Abs(q * new Vector3(inner - 4f, h, 0.5f)));
            }
            // a ceiling over the corridor
            DK("ceiling").Box(o + q * new Vector3(0f, h + 0.2f, (outer + inner) * 0.5f), Abs(q * new Vector3(outer * 2f, 0.4f, outer - inner)), Quaternion.identity, 4f, 4f, null, true, true);
            // kickers and a low rail down each side of the corridor
            Kicker("Temple kicker", Matrix4x4.TRS(o + q * new Vector3(-outer + 12f, 0f, (outer + inner) * 0.5f), q * Quaternion.LookRotation(Vector3.right), Vector3.one), 3f, 22f, 3f, RampSide);
            RailCol = new Color32(150, 74, 30, 255);
            Rail("Temple rail", new[] { o + q * new Vector3(4f, 0.45f, inner + 1.2f), o + q * new Vector3(outer - 6f, 0.45f, inner + 1.2f) });
        }
        // the courtyard: a little bowl, open to the sky
        RampTop = new Color32(214, 196, 130, 255); RampSide = new Color32(150, 120, 80, 255);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            // two halves each side, leaving the doorway in the middle open
            foreach (float side in new[] { -1f, 1f })
                QuarterPipe("Courtyard bowl", Matrix4x4.TRS(o + q * new Vector3(side * 9.25f, 0f, inner - 5.3f), q, Vector3.one), 10.5f, 2.4f, 2.5f);
        }
        FlushDream(g);
        return DNew("THE TEMPLE", i, new Vector3(-outer + 6f, 0.2f, (outer + inner) * 0.5f - 2f), 90f, new Color(0.25f, 0.5f, 0.75f), new Color(0.6f, 0.55f, 0.3f), 15f, 90f, new Vector3(outer - 6f, 0f, -(outer + inner) * 0.5f));
    }

    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

    // ---- the flesh tunnel -------------------------------------------------------------------------------------

    public const float FleshR = 5f, FleshLength = 220f;
    public static Vector3 FleshPath(float d) { return DreamO(1) + new Vector3(Mathf.Sin(d * 0.03f) * 12f, FleshR, d); }

    static DreamLevel.Dream DFlesh(int i)
    {
        var g = DGroup("Flesh", i);
        var k = DK("flesh", false, true);
        int sides = 20; float step = 4f;
        for (float d = 0f; d < FleshLength - 0.01f; d += step)
        {
            Vector3 a = FleshPath(d), b = FleshPath(d + step);
            Vector3 fa = (FleshPath(d + 0.5f) - FleshPath(d - 0.5f)).normalized, fb = (FleshPath(d + step + 0.5f) - FleshPath(d + step - 0.5f)).normalized;
            Vector3 ra = Vector3.Cross(Vector3.up, fa).normalized, rb = Vector3.Cross(Vector3.up, fb).normalized;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 p00 = a + (ra * Mathf.Sin(a0) - Vector3.up * Mathf.Cos(a0)) * FleshR, p01 = a + (ra * Mathf.Sin(a1) - Vector3.up * Mathf.Cos(a1)) * FleshR;
                Vector3 p10 = b + (rb * Mathf.Sin(a0) - Vector3.up * Mathf.Cos(a0)) * FleshR, p11 = b + (rb * Mathf.Sin(a1) - Vector3.up * Mathf.Cos(a1)) * FleshR;
                Vector3 inward = -(((p00 + p11) * 0.5f) - (a + b) * 0.5f).normalized;
                k.Quad(p00, p10, p11, p01, new Vector2(s / 4f, d / 8f), new Vector2(s / 4f, (d + step) / 8f), new Vector2((s + 1) / 4f, (d + step) / 8f), new Vector2((s + 1) / 4f, d / 8f), inward);
            }
        }
        // the start end's closed; the far end glows, and links
        Vector3 s0 = FleshPath(0f);
        DK("flesh", false, true).Box(s0 + Vector3.back * 0.3f, new Vector3(FleshR * 2.2f, FleshR * 2.2f, 0.6f), Quaternion.identity, 3f, 3f);
        Vector3 e = FleshPath(FleshLength);
        DLink(g, "fleshglow", e + Vector3.forward * 0.3f, new Vector3(FleshR * 2.2f, FleshR * 2.2f, 0.6f));
        FlushDream(g);
        return DNew("THE FLESH TUNNEL", i, new Vector3(FleshPath(6f).x - DreamO(i).x, 0.3f, 6f), 0f, new Color(0.08f, 0.02f, 0.02f), new Color(0.25f, 0.04f, 0.04f), 10f, 70f, Vector3.zero, false);
    }

    // ---- the shrine -----------------------------------------------------------------------------------------------

    static DreamLevel.Dream DShrine(int i)
    {
        Vector3 o = DreamO(i);
        var g = DGroup("Shrine", i);
        float hx = 50f, hz = 60f;
        DK("grassyellow", false, true).Ground(o.x - hx, o.z - hz, o.x + hx, o.z + hz, 0f, 4f);
        DK("stonepath", false, true).Ground(o.x - 3f, o.z - hz + 6f, o.x + 3f, o.z + hz - 14f, 0.02f, 3f);
        // a row of torii over the path
        for (int t = 0; t < 6; t++)
        {
            float z = o.z + 6f + t * 7f;
            foreach (float s in new[] { -1f, 1f })
            {
                DK("lacquer").Cylinder(new Vector3(o.x + s * 3.5f, 0f, z), new Vector3(o.x + s * 3.5f, 6f, z), 0.3f, 8, 2f);
                DBox(g, new Vector3(o.x + s * 3.5f, 3f, z), new Vector3(0.6f, 6f, 0.6f));
            }
            DK("lacquer").Box(new Vector3(o.x, 5.2f, z), new Vector3(8.4f, 0.4f, 0.4f), Quaternion.identity, 2f, 1f);
            DK("lacquer").Box(new Vector3(o.x, 6.1f, z), new Vector3(10f, 0.5f, 0.7f), Quaternion.identity, 2f, 1f);
        }
        // the blue wall at the end of the path links
        DLink(g, "mosaic", new Vector3(o.x, 3f, o.z + hz - 10f), new Vector3(16f, 6f, 1f));
        // fox statues on granite plinths (the plinths are ledges to grind on), lanterns
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 p = new Vector3(o.x + s * 9f, 0f, o.z - 10f);
            DK("granite").Box(p + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 4f), Quaternion.identity, 2f, 2f);
            DBox(g, p + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 4f));
            Rail("Plinth edge", new[] { p + new Vector3(-1.9f, 1.23f, -1.9f), p + new Vector3(1.9f, 1.23f, -1.9f) }, false);
            p += Vector3.down * 0.8f;   // (the fox sits on the lower plinth)
            // the fox: a seated low-poly animal, ears up
            var fox = DK("white");
            fox.Box(p + new Vector3(0f, 3f, 0.3f), new Vector3(1.2f, 2f, 1.6f), Quaternion.Euler(-10f, 0f, 0f), 1f, 1f);
            fox.Box(p + new Vector3(0f, 4.5f, -0.3f), new Vector3(1f, 1f, 1.3f), Quaternion.identity, 1f, 1f);
            fox.Box(p + new Vector3(0f, 4.3f, -1.1f), new Vector3(0.5f, 0.45f, 0.8f), Quaternion.identity, 1f, 1f);
            foreach (float e in new[] { -1f, 1f }) fox.Box(p + new Vector3(e * 0.35f, 5.3f, -0.2f), new Vector3(0.25f, 0.8f, 0.3f), Quaternion.Euler(0f, 0f, e * -12f), 1f, 1f);
            fox.Box(p + new Vector3(s * 0.7f, 3.5f, 1.2f), new Vector3(0.4f, 2.2f, 0.4f), Quaternion.Euler(30f, 0f, s * 20f), 1f, 1f);
            DK("lacquer").Box(p + new Vector3(0f, 4.55f, -0.96f), new Vector3(1.02f, 0.12f, 0.2f), Quaternion.identity, 1f, 1f);
            for (int l = 0; l < 3; l++)
            {
                Vector3 lp = new Vector3(o.x + s * 6f, 0f, o.z + 4f + l * 14f);
                DK("granite").Box(lp + Vector3.up * 0.8f, new Vector3(0.6f, 1.6f, 0.6f), Quaternion.identity, 1f, 1f);
                DK("granite").Box(lp + Vector3.up * 1.9f, new Vector3(1.1f, 0.7f, 1.1f), Quaternion.identity, 1f, 1f);
                DBox(g, lp + Vector3.up * 1.1f, new Vector3(1.1f, 2.2f, 1.1f));
            }
        }
        // a rocky wall down one side, with a bank up to the top of it
        DK("rock").Box(new Vector3(o.x + hx - 4f, 2f, o.z), new Vector3(8f, 4f, 80f), Quaternion.identity, 4f, 4f);
        DBox(g, new Vector3(o.x + hx - 4f, 2f, o.z), new Vector3(8f, 4f, 80f));
        RampTop = new Color32(170, 150, 100, 255); RampSide = new Color32(110, 80, 60, 255);
        QuarterPipe("Shrine bank", Matrix4x4.TRS(new Vector3(o.x + hx - 8f - 3.6f, 0f, o.z), Quaternion.LookRotation(Vector3.right), Vector3.one), 60f, 3.6f, 0.3f);
        // a long wooden fence along the far side: rails to grind (falling off past it links you)
        RailCol = new Color32(130, 120, 110, 255);
        Rail("Fence", new[] { new Vector3(o.x - hx + 2f, 0.9f, o.z - 40f), new Vector3(o.x - hx + 2f, 0.9f, o.z + 40f) });
        for (float z = -40f; z <= 40f; z += 4f) DK("granite").Box(new Vector3(o.x - hx + 2f, 0.45f, o.z + z), new Vector3(0.2f, 0.9f, 0.2f), Quaternion.identity, 1f, 1f);
        // bare trees
        for (int t = 0; t < 8; t++)
        {
            Vector3 tp = new Vector3(o.x - 30f + (t % 4) * 9f + (t / 4) * 4f, 0f, o.z - 30f + (t / 4) * 50f);
            DK("rock").Cylinder(tp, tp + Vector3.up * 5f, 0.25f, 6, 2f);
            DK("rock").Cylinder(tp + Vector3.up * 3f, tp + new Vector3(1.6f, 5.5f, 0.4f), 0.12f, 5, 2f);
            DK("rock").Cylinder(tp + Vector3.up * 3.6f, tp + new Vector3(-1.2f, 6f, -0.6f), 0.1f, 5, 2f);
            DBox(g, tp + Vector3.up * 2.5f, new Vector3(0.5f, 5f, 0.5f));
        }
        Kicker("Shrine kicker", Matrix4x4.TRS(new Vector3(o.x - 20f, 0f, o.z - 30f), Quaternion.identity, Vector3.one), 3.5f, 24f, 3.2f, RampSide);
        FlushDream(g);
        return DNew("THE SHRINE", i, new Vector3(0f, 0.3f, -hz + 8f), 0f, new Color(0.3f, 0.55f, 0.72f), new Color(0.55f, 0.7f, 0.8f), 30f, 160f, new Vector3(-20f, 0f, 20f));
    }

    // ---- the red field ----------------------------------------------------------------------------------------------

    public static float RedFieldH(float x, float z)
    {
        float h = 3.5f * Mathf.Sin(x * 0.045f) * Mathf.Sin(z * 0.04f + 0.6f) + 2f * Mathf.Sin(x * 0.02f + z * 0.03f);
        float flat = Mathf.Clamp01((Mathf.Abs(z - 40f) - 12f) / 10f);   // the building's plot is flat
        float spawn = Mathf.Clamp01((new Vector2(x, z + 60f).magnitude - 8f) / 10f);
        return h * flat * spawn;
    }

    static DreamLevel.Dream DRedField(int i)
    {
        Vector3 o = DreamO(i);
        var g = DGroup("Red field", i);
        float hx = 80f, hz = 80f, cell = 4f;
        var gk = DK("grassgreen", false, true);
        for (float z = -hz; z < hz; z += cell)
            for (float x = -hx; x < hx; x += cell)
            {
                Vector3 a = o + new Vector3(x, RedFieldH(x, z), z), b = o + new Vector3(x, RedFieldH(x, z + cell), z + cell);
                Vector3 c = o + new Vector3(x + cell, RedFieldH(x + cell, z + cell), z + cell), d = o + new Vector3(x + cell, RedFieldH(x + cell, z), z);
                gk.Quad(a, b, c, d, new Vector2(x / 4f, z / 4f), new Vector2(x / 4f, (z + cell) / 4f), new Vector2((x + cell) / 4f, (z + cell) / 4f), new Vector2((x + cell) / 4f, z / 4f), Vector3.up);
            }
        // the long white building, its teal roof sloping down to the ground on the near side: skate up it
        Vector3 bc = o + new Vector3(0f, 0f, 40f);
        float bl = 60f, bw = 10f, bh = 5f;
        DK("plaster").Box(bc + Vector3.up * bh * 0.5f, new Vector3(bl, bh, bw), Quaternion.identity, 4f, bh);
        DBox(g, bc + Vector3.up * bh * 0.5f, new Vector3(bl, bh, bw));
        var roof = DK("roof", false, true);
        Vector3 ridge = bc + new Vector3(0f, bh + 3f, 0f);
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 eave = bc + new Vector3(0f, s < 0 ? 0.05f : bh, s * (bw * 0.5f + (s < 0 ? 9f : 1f)));
            roof.Quad(ridge + Vector3.left * bl * 0.5f, ridge + Vector3.right * bl * 0.5f, eave + Vector3.right * bl * 0.5f, eave + Vector3.left * bl * 0.5f,
                      Vector2.zero, new Vector2(bl / 4f, 0f), new Vector2(bl / 4f, 3f), new Vector2(0f, 3f), Vector3.up);
        }
        // its doorways: dark, and they link
        for (int dd = 0; dd < 3; dd++) DLink(g, "darkcity", bc + new Vector3(-20f + dd * 20f, 1.5f, bw * 0.5f + 0.35f), new Vector3(3f, 3f, 0.6f));
        // the edges link
        foreach (float s in new[] { -1f, 1f })
        {
            DLink(g, "fleshglow", o + new Vector3(s * (hx + 0.5f), 2f, 0f), new Vector3(1f, 30f, hz * 2f));
            DLink(g, "fleshglow", o + new Vector3(0f, 2f, s * (hz + 0.5f)), new Vector3(hx * 2f, 30f, 1f));
        }
        // kickers on the hills, and a big floating head watching
        Kicker("Field kicker", Matrix4x4.TRS(o + new Vector3(-15f, RedFieldH(-15f, -30f), -30f), Quaternion.identity, Vector3.one), 4f, 26f, 4f, RampSide);
        Kicker("Field kicker", Matrix4x4.TRS(o + new Vector3(25f, RedFieldH(25f, -10f), -10f), Quaternion.LookRotation(Vector3.right), Vector3.one), 4f, 26f, 4f, RampSide);
        DHead(g, o + new Vector3(-30f, 22f, 60f), 7f, "plaster");
        FlushDream(g);
        return DNew("THE RED FIELD", i, new Vector3(0f, 0.3f, -60f), 0f, new Color(0.45f, 0.02f, 0.02f), new Color(0.5f, 0.05f, 0.05f), 40f, 200f, new Vector3(30f, RedFieldH(30f, 10f), 10f));
    }

    // ---- the neon pool -----------------------------------------------------------------------------------------------

    static DreamLevel.Dream DNeon(int i)
    {
        Vector3 o = DreamO(i);
        var g = DGroup("Neon", i);
        float h = 36f;
        DK("purplefloor", false, true).Ground(o.x - h, o.z - h, o.x + h, o.z + h, 0f, 6f);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            DLink(g, "mosaic", o + q * new Vector3(0f, 3f, h + 0.5f), Abs(q * new Vector3(h * 2f + 2f, 6f, 1f)));
        }
        RampTop = new Color32(250, 220, 60, 255); RampSide = new Color32(220, 40, 120, 255); RampSide2 = new Color32(40, 180, 220, 255);
        float b = 9f;
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            QuarterPipe("Pool wall", Matrix4x4.TRS(o + new Vector3(0f, 0f, 8f) + q * new Vector3(0f, 0f, b - 3f), q, Vector3.one), 2f * b - 6.4f, 3f, 2f);
        }
        Funbox("Neon funbox", Matrix4x4.TRS(o + new Vector3(-20f, 0f, -20f), Quaternion.identity, Vector3.one), 6f);
        RailCol = new Color32(250, 230, 40, 255);
        Rail("Neon rail", new[] { o + new Vector3(18f, 0.5f, -26f), o + new Vector3(18f, 0.5f, -6f) });
        Rail("Neon rail", new[] { o + new Vector3(24f, 0.8f, -26f), o + new Vector3(24f, 1.6f, -6f) });
        Kicker("Neon kicker", Matrix4x4.TRS(o + new Vector3(0f, 0f, -24f), Quaternion.identity, Vector3.one), 3.5f, 24f, 3.5f, RampSide2);
        DHead(g, o + new Vector3(0f, 18f, 8f), 5f, "mosaic");
        FlushDream(g);
        return DNew("THE NEON POOL", i, new Vector3(0f, 0.3f, -30f), 0f, new Color(0.1f, 0.95f, 0.1f), new Color(0.5f, 1f, 0.4f), 30f, 140f, new Vector3(-20f, 0f, 20f));
    }

    // ---- the kanji void ------------------------------------------------------------------------------------------------

    // its platforms: centre and half size
    public static readonly Vector4[] KanjiPlatforms = { new Vector4(0f, 0f, 0f, 8f), new Vector4(0f, 0f, 30f, 6f), new Vector4(26f, 2f, 48f, 6f), new Vector4(-22f, 1f, 52f, 5f), new Vector4(0f, 4f, 78f, 8f) };

    static DreamLevel.Dream DKanji(int i)
    {
        Vector3 o = DreamO(i);
        var g = DGroup("Kanji", i);
        foreach (var p in KanjiPlatforms)
        {
            Vector3 c = o + new Vector3(p.x, p.y, p.z);
            DK("kanji", true).Box(c + Vector3.down * 0.5f, new Vector3(p.w * 2f, 1f, p.w * 2f), Quaternion.identity, 4f, 4f);
            DBox(g, c + Vector3.down * 0.5f, new Vector3(p.w * 2f, 1f, p.w * 2f));
        }
        // rails between them, over the void
        RailCol = new Color32(255, 160, 30, 255);
        System.Action<int, int> join = (a, b2) =>
        {
            Vector3 pa = o + new Vector3(KanjiPlatforms[a].x, KanjiPlatforms[a].y, KanjiPlatforms[a].z), pb = o + new Vector3(KanjiPlatforms[b2].x, KanjiPlatforms[b2].y, KanjiPlatforms[b2].z);
            Vector3 d = (pb - pa); d.y = 0f; d.Normalize();
            Rail("Void rail", new[] { pa + d * (KanjiPlatforms[a].w - 1.5f) + Vector3.up * 0.5f, pb - d * (KanjiPlatforms[b2].w - 1.5f) + Vector3.up * 0.5f }, false);
        };
        join(0, 1); join(1, 2); join(1, 3); join(2, 4); join(3, 4);
        // the dark city far off on every side
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            Vector3 n = q * Vector3.back;
            DK("darkcity", true).Panel(o + new Vector3(0f, 0f, 40f) + q * new Vector3(0f, 10f, 220f), Vector3.Cross(n, Vector3.up) * 220f, Vector3.up * 40f);
        }
        DHead(g, o + new Vector3(16f, 14f, 30f), 4f, "kanji");
        FlushDream(g);
        var d0 = DNew("THE KANJI VOID", i, new Vector3(0f, 0.3f, -4f), 0f, new Color(0.03f, 0.02f, 0.06f), new Color(0.05f, 0.02f, 0.08f), 40f, 260f, Vector3.zero, false);
        d0.floorY = -2f;
        return d0;
    }

    // a big floating head: a lumpy ball with dark eyes and a mouth, turning slowly
    static void DHead(Transform parent, Vector3 c, float r, string tex)
    {
        var k = new PSXKit();
        k.Ellipsoid(Vector3.zero, new Vector3(r, r * 1.2f, r), 10, 8);
        foreach (float s in new[] { -1f, 1f }) k.Ellipsoid(new Vector3(s * r * 0.35f, r * 0.2f, -r * 0.9f), new Vector3(r * 0.18f, r * 0.12f, r * 0.12f), 6, 4, new Color32(20, 20, 20, 255));
        k.Ellipsoid(new Vector3(0f, -r * 0.45f, -r * 0.88f), new Vector3(r * 0.3f, r * 0.06f, r * 0.1f), 6, 4, new Color32(60, 10, 10, 255));
        var mesh = k.Build("Head");
        SaveFeatureMesh(mesh);
        var go = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer), typeof(Spin));
        go.transform.SetParent(parent, false);
        go.transform.position = c;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = DMat(tex, false);
        go.GetComponent<Spin>().degreesPerSecond = 8f;
    }

    // the Gray Man: tall, grey, in a hat and a long coat
    static Transform DGrayMan()
    {
        var k = new PSXKit();
        Color32 grey = new Color32(110, 110, 112, 255);
        k.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.45f), Quaternion.identity, 1f, 1f, grey);
        k.Box(new Vector3(0f, 2.45f, 0f), new Vector3(0.4f, 0.45f, 0.4f), Quaternion.identity, 1f, 1f, grey);
        k.Box(new Vector3(0f, 2.72f, 0f), new Vector3(0.8f, 0.06f, 0.8f), Quaternion.identity, 1f, 1f, grey);
        k.Box(new Vector3(0f, 2.86f, 0f), new Vector3(0.42f, 0.3f, 0.42f), Quaternion.identity, 1f, 1f, grey);
        var mesh = k.Build("Gray Man");
        SaveFeatureMesh(mesh);
        var go = new GameObject("Gray Man", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(dRoot, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = DMat("white", false);
        return go.transform;
    }

    public static void DreamShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        var level = Object.FindFirstObjectByType<DreamLevel>();
        var psx = cam.GetComponent<PSXCamera>();
        for (int i = 0; i < level.dreams.Length; i++)
        {
            var d = level.dreams[i];
            cam.backgroundColor = d.sky;
            psx.fogColor = d.fog; psx.fogStart = d.fogStart; psx.fogEnd = d.fogEnd; psx.Apply();
            Transform s = d.spawn;
            Shot(cam, s.position + Vector3.up * 2f - s.forward * 3f, s.position + s.forward * 30f + Vector3.up * 1.5f, "d0" + (i + 1) + "-" + d.name.ToLower().Replace(' ', '-') + ".png");
        }
        var d0 = level.dreams[0];
        cam.backgroundColor = d0.sky; psx.fogColor = d0.fog; psx.fogStart = d0.fogStart; psx.fogEnd = d0.fogEnd; psx.Apply();
    }
}
