using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Aero: Frutiger Aero. Bright green rolling hills under a blue sky, fluffy clouds, sun rays, soap bubbles
// and big tropical fish floating in the air, a glass city on the horizon. You're a glossy blue jelly
// person riding a CD.
//
// Skating it: from the top of the hill a long valley runs down to the glass city; carve its rolling sides,
// and through the middle stretch it narrows into a grassy halfpipe. Glowing rings along the way shoot you
// on. Bubbles bounce you (a line of them climbs to the clouds, which you can skate on), the fish swim loops
// you can ride on their backs, there's a lake you can skate across, and the glass city has a dome to ride
// up, a tower with a ramp spiralling up it, and a plaza full of ramps and rails.
public static partial class SkateWorldBuilder
{
    public const string AeroPath = "Assets/Scenes/Aero.unity";

    const float AeroX = 200f, AeroZ0 = -260f, AeroZ1 = 470f, AeroCell = 3f;
    const float AeroCityY = 8f, AeroCityZ0 = 300f;
    public static float AeroLakeY { get { return AeroH(AeroLake.x, AeroLake.z) + 2.6f; } }   // the lake's surface
    static readonly Vector3 AeroLake = new Vector3(-75f, 0f, 60f);
    static readonly Vector3 AeroSunDir = new Vector3(-0.3f, 0.7f, 0.6f).normalized;
    static Material aeroGlossy, aeroGlass, aeroBubble, aeroCloud;
    static Transform aeroRoot;
    public static readonly List<(Vector3 centre, float r)> aeroBubbles = new List<(Vector3, float)>();
    public static readonly List<(Vector3 centre, float top, float rx, float rz)> aeroClouds = new List<(Vector3, float, float, float)>();

    static float S01(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    // the ground's height
    public static float AeroH(float x, float z)
    {
        // the hill you start on
        float h = 32f * Mathf.Exp(-(x * x + (z + 130f) * (z + 130f)) / (2f * 48f * 48f));
        // the valley's sides rise away from the middle; through the halfpipe stretch they rise sharply
        float pipe = S01(-30f, 10f, z) * (1f - S01(130f, 170f, z));
        h += 0.0009f * x * x + pipe * 0.0045f * Mathf.Min(x * x, 3600f);
        // the lake's basin (smooth: no bumps in it)
        float lx = x - AeroLake.x, lz = z - AeroLake.z;
        float lake = Mathf.Exp(-(lx * lx + lz * lz) / (2f * 26f * 26f));
        // rolling bumps (gentle in the halfpipe and the lake)
        h += (1f - pipe * 0.8f) * (1f - lake) * (3.2f * Mathf.Sin(x * 0.045f + 1.3f) * Mathf.Sin(z * 0.035f) + 1.8f * Mathf.Sin(x * 0.09f + z * 0.07f));
        h -= 9f * lake;
        // the plateau the city's on: flat in the middle
        float city = S01(AeroCityZ0 - 40f, AeroCityZ0, z) * (1f - S01(90f, 120f, Mathf.Abs(x)));
        h = Mathf.Lerp(h, AeroCityY, city);
        return h;
    }

    public static void AeroFromCommandLine()
    {
        try { BuildAero(); SlideTests.RunAero(); AeroShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildAero()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/AeroFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); aeroBubbles.Clear(); aeroClouds.Clear();
        floorAt = AeroH;
        RampTop = new Color32(235, 250, 255, 255); RampSide = new Color32(90, 190, 255, 255); RampSide2 = new Color32(120, 230, 140, 255);
        Metal = new Color32(225, 235, 245, 255); RailCol = new Color32(230, 240, 255, 255);
        Concrete = new Color32(235, 245, 255, 255); ConcreteDark = new Color32(170, 205, 235, 255);

        Vector4 sun = new Vector4(AeroSunDir.x, AeroSunDir.y, AeroSunDir.z, 0f);
        aeroGlossy = Mat("AeroGlossy", "3daigamesolo/World/Glossy");
        aeroGlossy.SetVector("_SunDir", sun);
        var grass = Mat("AeroGrass", "3daigamesolo/World/Glossy");
        grass.SetVector("_SunDir", sun); grass.SetFloat("_Gloss", 0.12f); grass.SetFloat("_Spec", 0.15f); grass.SetFloat("_SpecPower", 12f); grass.SetFloat("_Rim", 0.25f);
        aeroGlass = Mat("AeroGlass", "3daigamesolo/World/Glossy");
        aeroGlass.SetVector("_SunDir", sun); aeroGlass.SetFloat("_Gloss", 0.65f); aeroGlass.SetFloat("_Spec", 1.4f); aeroGlass.SetFloat("_SpecPower", 120f);
        aeroBubble = Mat("AeroBubble", "3daigamesolo/World/Bubble");
        aeroBubble.SetVector("_SunDir", sun);
        aeroCloud = Mat("AeroCloud", "3daigamesolo/World/Glossy");
        aeroCloud.SetVector("_SunDir", sun); aeroCloud.SetFloat("_Gloss", 0.15f); aeroCloud.SetFloat("_Spec", 0.25f); aeroCloud.SetFloat("_SpecPower", 8f); aeroCloud.SetFloat("_Rim", 0.5f);
        aeroCloud.SetColor("_Color", new Color(1.28f, 1.3f, 1.36f, 1f));
        aeroCloud.SetColor("_SkyLow", new Color(1f, 1f, 1f)); aeroCloud.SetColor("_SkyTop", new Color(0.85f, 0.93f, 1f));
        toonMat = aeroGlossy;
        outlineMat = Mat("AeroOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.1f, 0.35f, 0.7f));
        outlineMat.SetFloat("_OutlineWidth", 0.7f);
        outlineMat.SetColor("_Haze", new Color(0.82f, 0.93f, 1f)); outlineMat.SetColor("_HazeHigh", new Color(0.62f, 0.84f, 1f));
        outlineMat.SetFloat("_FogDensity", 0.0012f); outlineMat.SetFloat("_FogHeight", 300f); outlineMat.SetFloat("_FogMax", 0.85f);
        farOutlineMat = outlineMat;
        var sky = Mat("AeroSky", "3daigamesolo/World/Sky");
        sky.SetColor("_Zenith", new Color(0.12f, 0.5f, 0.95f)); sky.SetColor("_Horizon", new Color(0.72f, 0.9f, 1f));
        sky.SetColor("_Below", new Color(0.72f, 0.9f, 1f)); sky.SetColor("_Cloud", Color.white);
        sky.SetColor("_CloudShade", new Color(0.82f, 0.9f, 1f)); sky.SetFloat("_CloudCover", 0.5f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        featureRoot = new GameObject("Skate features").transform;
        aeroRoot = new GameObject("Aero").transform;

        AeroGround(grass);
        AeroLakeWater();
        AeroHillStuff();
        AeroCity();
        AeroClouds();
        AeroBubbles();
        AeroRings();
        AeroFish();
        AeroSky(sky);

        Vector3 startAt = new Vector3(0f, AeroH(0f, -130f) + 0.2f, -130f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt, Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 5000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.72f, 0.9f, 1f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -40f;
        // you: a glossy blue jelly person on a CD
        Object.DestroyImmediate(player.rider.gameObject);
        player.rider = JellyRider(player.transform);
        player.thirdPerson = true;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "Aero"; hud.aero = true;
        camGO.transform.SetPositionAndRotation(startAt + new Vector3(0f, 2.5f, -4f), Quaternion.LookRotation(Vector3.forward));
        player.board = CDBoard(startAt + Vector3.forward * 2f, Quaternion.LookRotation(Vector3.forward));
        TowerCheckpoint(aeroRoot, startAt + new Vector3(-3f, -0.2f, -5f), "Aero");

        EditorSceneManager.SaveScene(scene, AeroPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_AERO_BUILT features=" + featureCount + " bubbles=" + aeroBubbles.Count + " clouds=" + aeroClouds.Count);
    }

    // ---- the hills ------------------------------------------------------------------------------------------

    static void AeroGround(Material grass)
    {
        int nx = Mathf.RoundToInt(AeroX * 2f / AeroCell), nz = Mathf.RoundToInt((AeroZ1 - AeroZ0) / AeroCell);
        var verts = new Vector3[(nx + 1) * (nz + 1)];
        var cols = new Color32[verts.Length];
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = -AeroX + i * AeroCell, z = AeroZ0 + j * AeroCell, h = AeroH(x, z);
                verts[j * (nx + 1) + i] = new Vector3(x, h, z);
                // bright green, lighter up the slopes and in patches
                float lift = Mathf.Clamp01((h + 2f) / 34f), patch = Mathf.PerlinNoise(x * 0.02f + 3f, z * 0.02f + 7f);
                Color g = Color.Lerp(new Color(0.16f, 0.72f, 0.12f), new Color(0.55f, 0.95f, 0.25f), lift * 0.6f + patch * 0.4f);
                if (z > AeroCityZ0 - 5f && Mathf.Abs(x) < 95f) g = Color.Lerp(g, new Color(0.8f, 0.92f, 1f), S01(AeroCityZ0 - 5f, AeroCityZ0 + 5f, z) * (1f - S01(85f, 95f, Mathf.Abs(x))));   // the city's pale paving
                cols[j * (nx + 1) + i] = g;
            }
        var tris = new List<int>();
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        var mesh = new Mesh { name = "Hills", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.colors32 = cols; mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveFeatureMesh(mesh);
        var go = MeshObject("Hills", aeroRoot, mesh, grass, null);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        // a big floor far underneath the edges, and green ground running off to the horizon
        var far = new SkateMeshKit();
        far.Quad(new Vector3(-3000f, -1f, -3000f), new Vector3(-3000f, -1f, 3000f), new Vector3(3000f, -1f, 3000f), new Vector3(3000f, -1f, -3000f), Vector3.up, new Color32(70, 190, 50, 255));
        var fm = far.Build("Far fields");
        SaveFeatureMesh(fm);
        MeshObject("Far fields", aeroRoot, fm, grass, null);
        // and low walls of hill round the edge so you stay in
        foreach (var s in new[] { -1f, 1f })
        {
            AddBox(aeroRoot, "Edge", new Vector3(s * (AeroX + 0.5f), 40f, (AeroZ0 + AeroZ1) / 2f), new Vector3(1f, 120f, AeroZ1 - AeroZ0));
            AddBox(aeroRoot, "Edge", new Vector3(0f, 40f, s > 0 ? AeroZ1 + 0.5f : AeroZ0 - 0.5f), new Vector3(AeroX * 2f, 120f, 1f));
        }
    }

    static void AeroLakeWater()
    {
        var water = Mat("AeroWater", "3daigamesolo/World/Water");
        water.SetColor("_Water", new Color(0.25f, 0.75f, 1f)); water.SetColor("_WaterDeep", new Color(0.1f, 0.45f, 0.95f));
        water.SetColor("_Haze", new Color(0.82f, 0.93f, 1f)); water.SetColor("_HazeHigh", new Color(0.62f, 0.84f, 1f));
        var k = new SkateMeshKit();
        int seg = 40;
        Vector3 c = new Vector3(AeroLake.x, AeroLakeY, AeroLake.z);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            k.Tri(c, c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 34f, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 34f, Vector3.up, new Color32(255, 255, 255, 255));
        }
        var mesh = k.Build("Lake");
        SaveFeatureMesh(mesh);
        MeshObject("Lake", aeroRoot, mesh, water, null);
        // you can skate across it
        var col = new GameObject("Lake surface").AddComponent<MeshCollider>();
        col.transform.SetParent(aeroRoot, false);
        col.sharedMesh = mesh;
        TowerCheckpoint(aeroRoot, AeroAt(AeroLake.x + 30f, AeroLake.z - 20f), "Lake");
    }

    static Vector3 AeroAt(float x, float z) { return new Vector3(x, AeroH(x, z), z); }

    // kickers on the crests, a quarter pipe at the bottom of the hill, rails down the valley
    static void AeroHillStuff()
    {
        foreach (var z in new[] { -60f, 20f, 110f, 200f })
        {
            Vector3 at = AeroAt(AR2(z, -14f, 14f), z);
            Kicker("Kicker", Matrix4x4.TRS(at + Vector3.down * 0.1f, Quaternion.LookRotation(Vector3.forward), Vector3.one), 4f, 26f, 3.6f, RampSide);
        }
        foreach (var (x, z) in new[] { (-34f, 70f), (34f, 90f), (-30f, 240f), (30f, 255f) })
        {
            Vector3 a = AeroAt(x, z), b = AeroAt(x, z + 24f);
            Rail("Chrome rail", new[] { a + Vector3.up * 0.8f, b + Vector3.up * 0.8f });
        }
        Funbox("Funbox", Matrix4x4.TRS(AeroAt(-20f, 30f), Quaternion.identity, Vector3.one), 3.6f);
        Funbox("Funbox", Matrix4x4.TRS(AeroAt(22f, 180f), Quaternion.identity, Vector3.one), 3.6f);
    }

    static float AR2(float seed, float a, float b) { return a + (Mathf.Sin(seed * 12.9898f) * 43758.5453f % 1f + 1f) % 1f * (b - a); }

    // ---- the glass city ----------------------------------------------------------------------------------------

    static void AeroCity()
    {
        float y = AeroCityY;
        var k = new SkateMeshKit();
        var glass = new SkateMeshKit();
        Color32[] glassCols = { new Color32(120, 220, 255, 255), new Color32(90, 200, 230, 255), new Color32(200, 240, 255, 255), new Color32(110, 235, 210, 255) };
        // towers round the plaza
        var towers = new[] { (-60f, 330f, 14f, 46f), (-62f, 380f, 16f, 70f), (-55f, 430f, 14f, 38f), (58f, 340f, 15f, 56f), (64f, 395f, 18f, 88f), (52f, 440f, 12f, 34f),
                             (-24f, 450f, 12f, 52f), (22f, 455f, 14f, 64f) };
        int ti = 0;
        foreach (var (x, z, half, h) in towers)
        {
            Color32 col = glassCols[ti++ % glassCols.Length];
            // glass with pale bands at each floor
            for (float fy = 0f; fy < h; fy += 4f)
            {
                float fh = Mathf.Min(4f, h - fy);
                glass.Box(new Vector3(x, y + fy + fh * 0.5f - 0.3f, z), new Vector3(half * 2f, fh - 0.6f, half * 2f), Quaternion.identity, col);
                k.Box(new Vector3(x, y + fy + fh - 0.3f, z), new Vector3(half * 2f + 0.3f, 0.6f, half * 2f + 0.3f), Quaternion.identity, new Color32(245, 250, 255, 255));
            }
            AddBox(aeroRoot, "Tower", new Vector3(x, y + h / 2f, z), new Vector3(half * 2f, h, half * 2f));
        }
        // the spiral ramp up the tallest tower to a kicker off its roof
        var (sx, sz, shalf, sh) = towers[4];
        AeroSpiral(new Vector3(sx, y, sz), shalf + 1.5f, 6f, sh, k);
        // the dome in the middle of the plaza: a smooth glass hill, flat at its edge and its top and never
        // steeper than about 25 degrees, so you can ride right up and over it
        var dome = new SkateMeshKit();
        Vector3 dc = new Vector3(0f, y, 380f);
        float domeH = 9f, domeR = 30f;
        int dseg = 40, drings = 14;
        System.Func<float, float> dh = rr => domeH * Mathf.Pow(Mathf.Cos(Mathf.Clamp01(rr / domeR) * Mathf.PI * 0.5f), 2f);
        for (int ri = 0; ri < drings; ri++)
        {
            float ra = domeR * ri / drings, rb = domeR * (ri + 1) / drings;
            for (int si = 0; si < dseg; si++)
            {
                float a0 = si * Mathf.PI * 2f / dseg, a1 = (si + 1) * Mathf.PI * 2f / dseg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                System.Func<Vector3, float, Vector3> pt = (dd, rr) => dc + dd * rr + Vector3.up * dh(rr);
                System.Func<Vector3, float, Vector3> nm = (dd, rr) =>
                {
                    float slope = (dh(rr + 0.05f) - dh(Mathf.Max(0f, rr - 0.05f))) / 0.1f;
                    return new Vector3(-dd.x * slope, 1f, -dd.z * slope).normalized;
                };
                dome.SmoothQuad(pt(d0, ra), pt(d0, rb), pt(d1, rb), pt(d1, ra), nm(d0, ra), nm(d0, rb), nm(d1, rb), nm(d1, ra), new Color32(170, 230, 255, 255));
            }
        }
        var dm = dome.Build("Dome");
        SaveFeatureMesh(dm);
        var domeGo = MeshObject("Dome", aeroRoot, dm, aeroGlass, outlineMat);
        domeGo.AddComponent<MeshCollider>().sharedMesh = dm;

        // the plaza's skate stuff: quarter pipes, funboxes, ledges and rails
        QuarterPipe("Quarter pipe", Matrix4x4.TRS(new Vector3(-30f, y, 318f), Quaternion.LookRotation(Vector3.back), Vector3.one), 14f, 3.5f, 1.5f);
        QuarterPipe("Quarter pipe", Matrix4x4.TRS(new Vector3(30f, y, 318f), Quaternion.LookRotation(Vector3.back), Vector3.one), 14f, 3.5f, 1.5f);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(-28f, y, 360f), Quaternion.identity, Vector3.one), 4f);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(30f, y, 400f), Quaternion.LookRotation(Vector3.back), Vector3.one), 4f);
        foreach (var (lx, lz, yaw) in new[] { (-14f, 330f, 0f), (14f, 345f, 0f), (-36f, 410f, 90f), (36f, 360f, 90f) })
            Ledge("Glass ledge", new Vector3(lx, y, lz), Quaternion.Euler(0f, yaw, 0f), 8f, 0.6f, 0.8f, new Color32(150, 225, 255, 255));
        Rail("Chrome rail", new[] { new Vector3(-6f, y + 0.8f, 316f), new Vector3(6f, y + 0.8f, 316f) });
        Rail("Chrome rail", new[] { new Vector3(-45f, y + 0.8f, 345f), new Vector3(-45f, y + 0.8f, 365f) });
        var cm = k.Build("City");
        SaveFeatureMesh(cm);
        var cityGo = MeshObject("City", aeroRoot, cm, aeroGlossy, outlineMat);
        cityGo.AddComponent<MeshCollider>().sharedMesh = cm;
        var gm = glass.Build("City glass");
        SaveFeatureMesh(gm);
        MeshObject("City glass", aeroRoot, gm, aeroGlass, outlineMat);
        TowerCheckpoint(aeroRoot, new Vector3(0f, y, 312f), "Glass city");
        // the skyline beyond, glass towers into the haze
        var sky = new SkateMeshKit();
        var rnd = new System.Random(5);
        for (int i = 0; i < 40; i++)
        {
            float x = (float)rnd.NextDouble() * 900f - 450f, z = 560f + (float)rnd.NextDouble() * 500f, w = 15f + (float)rnd.NextDouble() * 25f, h = 40f + (float)rnd.NextDouble() * 170f;
            sky.Box(new Vector3(x, h / 2f, z), new Vector3(w, h, w), Quaternion.identity, glassCols[i % glassCols.Length]);
        }
        var skm = sky.Build("Skyline");
        SaveFeatureMesh(skm);
        MeshObject("Skyline", aeroRoot, skm, aeroGlass, null);
    }

    // a ramp spiralling up round a tower: `width` wide, from the ground to its roof, a kicker off the top
    static void AeroSpiral(Vector3 c, float r, float width, float h, SkateMeshKit k)
    {
        var ramp = new SkateMeshKit();
        float climb = 0.2f;   // metres up per metre along: about 11 degrees
        float turns = h / (2f * Mathf.PI * (r + width * 0.5f) * climb);
        int segs = Mathf.CeilToInt(turns * 48f);
        float a0 = -Mathf.PI / 2f;
        for (int i = 0; i < segs; i++)
        {
            float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
            float a = a0 + t0 * turns * Mathf.PI * 2f, b = a0 + t1 * turns * Mathf.PI * 2f;
            float y0 = c.y + t0 * h, y1 = c.y + t1 * h;
            Vector3 i0 = c + new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r) - Vector3.up * c.y, o0 = c + new Vector3(Mathf.Cos(a) * (r + width), y0, Mathf.Sin(a) * (r + width)) - Vector3.up * c.y;
            Vector3 i1 = c + new Vector3(Mathf.Cos(b) * r, y1, Mathf.Sin(b) * r) - Vector3.up * c.y, o1 = c + new Vector3(Mathf.Cos(b) * (r + width), y1, Mathf.Sin(b) * (r + width)) - Vector3.up * c.y;
            Color32 col = i % 2 == 0 ? new Color32(255, 255, 255, 255) : new Color32(210, 240, 255, 255);
            ramp.Quad(i0, o0, o1, i1, Vector3.up, col);
            ramp.Quad(i0 + Vector3.down * 0.5f, o0 + Vector3.down * 0.5f, o1 + Vector3.down * 0.5f, i1 + Vector3.down * 0.5f, Vector3.down, new Color32(150, 210, 245, 255));
            Vector3 outN = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            ramp.Quad(o0, o1, o1 + Vector3.down * 0.5f, o0 + Vector3.down * 0.5f, outN, new Color32(90, 190, 255, 255));
            // a low glass wall along the outside edge
            k.Quad(o0, o1, o1 + Vector3.up * 0.8f, o0 + Vector3.up * 0.8f, outN, new Color32(120, 220, 255, 255));
        }
        var mesh = ramp.Build("Spiral ramp");
        SaveFeatureMesh(mesh);
        var go = MeshObject("Spiral ramp", aeroRoot, mesh, aeroGlossy, outlineMat);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        TowerCheckpoint(aeroRoot, c + Vector3.up * h + new Vector3(0f, 0f, -r + 4f), "Tower top");
    }

    // ---- clouds you can skate on --------------------------------------------------------------------------

    static void AeroClouds()
    {
        var k = new SkateMeshKit();
        var defs = new[] { (new Vector3(-30f, 34f, 40f), 14f, 10f), (new Vector3(10f, 44f, 70f), 12f, 9f), (new Vector3(-10f, 52f, 120f), 16f, 11f),
                           (new Vector3(28f, 40f, 160f), 13f, 10f), (new Vector3(0f, 46f, 230f), 18f, 12f), (new Vector3(-40f, 60f, 300f), 14f, 10f) };
        var rnd = new System.Random(11);
        foreach (var (c, rx, rz) in defs)
        {
            // puffs round the edge and underneath, a flat-topped middle to skate on
            k.Ellipsoid(c + Vector3.down * 1.2f, new Vector3(rx, 2.6f, rz), Quaternion.identity, 20, 8, new Color32(255, 255, 255, 255));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f + (float)rnd.NextDouble() * 0.3f;
                float pr = 2.5f + (float)rnd.NextDouble() * 2.5f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * rx * 0.95f, -1.5f + (float)rnd.NextDouble() * 1.2f, Mathf.Sin(a) * rz * 0.95f);
                k.Ellipsoid(p, Vector3.one * pr, Quaternion.identity, 12, 7, new Color32(250, 252, 255, 255));
            }
            for (int i = 0; i < 5; i++)
                k.Ellipsoid(c + new Vector3(((float)rnd.NextDouble() - 0.5f) * rx, -3.5f, ((float)rnd.NextDouble() - 0.5f) * rz), Vector3.one * (3f + (float)rnd.NextDouble() * 3f), Quaternion.identity, 12, 7, new Color32(225, 238, 255, 255));
            // its top: a flat oval you can stand on
            var top = new GameObject("Cloud top").AddComponent<MeshCollider>();
            top.transform.SetParent(aeroRoot, false);
            var tk = new SkateMeshKit();
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI * 2f / 24f, a1 = (i + 1) * Mathf.PI * 2f / 24f;
                tk.Tri(c + Vector3.up * 1.35f, c + Vector3.up * 1.35f + new Vector3(Mathf.Cos(a0) * rx * 0.92f, 0f, Mathf.Sin(a0) * rz * 0.92f),
                       c + Vector3.up * 1.35f + new Vector3(Mathf.Cos(a1) * rx * 0.92f, 0f, Mathf.Sin(a1) * rz * 0.92f), Vector3.up, Color.white);
                tk.Tri(c + Vector3.up * 1.35f, c + Vector3.up * 1.35f + new Vector3(Mathf.Cos(a0) * rx * 0.92f, 0f, Mathf.Sin(a0) * rz * 0.92f),
                       c + Vector3.up * 1.35f + new Vector3(Mathf.Cos(a1) * rx * 0.92f, 0f, Mathf.Sin(a1) * rz * 0.92f), Vector3.down, Color.white);
            }
            var tm = tk.Build("Cloud top");
            SaveFeatureMesh(tm);
            top.sharedMesh = tm;
            aeroClouds.Add((c, c.y + 1.35f, rx * 0.92f, rz * 0.92f));
            // a rainbow rail across it
            Color32[] rainbow = { new Color32(255, 80, 80, 255), new Color32(255, 170, 60, 255), new Color32(255, 240, 80, 255), new Color32(90, 230, 110, 255), new Color32(80, 170, 255, 255), new Color32(170, 110, 255, 255) };
            Vector3 ra = c + new Vector3(-rx * 0.55f, 1.35f + 0.7f, 0f), rb = c + new Vector3(rx * 0.55f, 1.35f + 0.7f, 0f);
            Rail("Rainbow rail", new[] { ra, rb }, false);
            for (int i = 0; i < 6; i++)
                k.Cylinder(Vector3.Lerp(ra, rb, i / 6f) + Vector3.down * 0.06f, Vector3.Lerp(ra, rb, (i + 1) / 6f) + Vector3.down * 0.06f, 0.07f, 8, rainbow[i]);
            foreach (var e in new[] { ra, rb }) k.Cylinder(e, e + Vector3.down * 0.7f, 0.04f, 6, new Color32(255, 255, 255, 255));
        }
        var mesh = k.Build("Clouds");
        SaveFeatureMesh(mesh);
        MeshObject("Clouds", aeroRoot, mesh, aeroCloud, null);
        TowerCheckpoint(aeroRoot, defs[2].Item1 + new Vector3(0f, 1.35f, -6f), "Cloud");
    }

    // ---- bubbles: land on one and it bounces you -----------------------------------------------------------

    static void AeroBubbles()
    {
        var k = new SkateMeshKit();
        // a staircase of bubbles up from the valley floor to the first cloud
        Vector3 cloud = aeroClouds[0].centre;
        Vector3 foot = AeroAt(-6f, 10f);
        int steps = 7;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            Vector3 p = Vector3.Lerp(foot + Vector3.up * 2.2f, cloud + new Vector3(10f, -5f, -8f), t) + new Vector3(Mathf.Sin(i * 1.7f) * 2f, 0f, 0f);
            AeroBubble(k, p, 1.8f, 19f);
        }
        // bubbles between the clouds
        for (int c = 0; c + 1 < aeroClouds.Count; c++)
        {
            Vector3 a = aeroClouds[c].centre, b = aeroClouds[c + 1].centre;
            Vector3 mid = Vector3.Lerp(a, b, 0.5f) + Vector3.down * 4f;
            AeroBubble(k, mid, 2.2f, Mathf.Sqrt(2f * 9.81f * 3.6f * Mathf.Max(6f, b.y - mid.y + 5f)));
        }
        // and a scatter of them over the hills
        var rnd = new System.Random(23);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)rnd.NextDouble() * 160f - 80f, z = -40f + (float)rnd.NextDouble() * 320f;
            float r = 1.4f + (float)rnd.NextDouble() * 1.6f;
            Vector3 p = AeroAt(x, z) + Vector3.up * (r + 1.5f + (float)rnd.NextDouble() * 10f);
            if (Mathf.Abs(x) < 10f && z > 0f && z < 150f) continue;   // keep the halfpipe clear
            AeroBubble(k, p, r, 17f + (float)rnd.NextDouble() * 6f);
        }
        // little ones just floating, for looks
        for (int i = 0; i < 80; i++)
        {
            float x = (float)rnd.NextDouble() * 360f - 180f, z = AeroZ0 + (float)rnd.NextDouble() * (AeroZ1 - AeroZ0);
            Vector3 p = AeroAt(x, z) + Vector3.up * (4f + (float)rnd.NextDouble() * 40f);
            k.Ellipsoid(p, Vector3.one * (0.3f + (float)rnd.NextDouble() * 0.6f), Quaternion.identity, 10, 6, new Color32(200, 235, 255, 255));
        }
        var mesh = k.Build("Bubbles");
        SaveFeatureMesh(mesh);
        MeshObject("Bubbles", aeroRoot, mesh, aeroBubble, null);
    }

    static void AeroBubble(SkateMeshKit k, Vector3 c, float r, float bounce)
    {
        k.Ellipsoid(c, Vector3.one * r, Quaternion.identity, 16, 10, new Color32(190, 230, 255, 255));
        var sc = new GameObject("Bubble").AddComponent<SphereCollider>();
        sc.transform.SetParent(aeroRoot, false);
        sc.transform.position = c; sc.radius = r;
        sc.gameObject.AddComponent<Bouncer>().speed = bounce;
        aeroBubbles.Add((c, r));
    }

    // ---- rings that shoot you on ------------------------------------------------------------------------------

    static void AeroRings()
    {
        var k = new SkateMeshKit();
        // down the valley, low
        foreach (var z in new[] { -95f, -10f, 60f, 140f, 260f })
        {
            Vector3 g = AeroAt(0f, z);
            Vector3 g2 = AeroAt(0f, z + 4f);
            Vector3 fwd = (g2 - g).normalized;
            AeroRing(k, g + Vector3.up * 2.6f, Quaternion.LookRotation(fwd), 2.4f, 26f);
        }
        // up off the halfpipe's sides, out to the clouds
        AeroRing(k, AeroAt(-38f, 90f) + Vector3.up * 3f, Quaternion.LookRotation(new Vector3(0.1f, 0.8f, 0.6f)), 2.6f, 30f);
        AeroRing(k, AeroAt(36f, 140f) + Vector3.up * 3f, Quaternion.LookRotation(new Vector3(-0.15f, 0.75f, 0.65f)), 2.6f, 32f);
        var mesh = k.Build("Rings");
        SaveFeatureMesh(mesh);
        MeshObject("Rings", aeroRoot, mesh, aeroGlossy, null);
    }

    static void AeroRing(SkateMeshKit k, Vector3 c, Quaternion q, float r, float speed)
    {
        int seg = 24;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 p0 = c + q * new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * r, p1 = c + q * new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * r;
            k.Cylinder(p0, p1, 0.16f, 8, i % 2 == 0 ? new Color32(80, 255, 140, 255) : new Color32(200, 255, 220, 255), false);
        }
        var go = new GameObject("Boost ring", typeof(BoostRing));
        go.transform.SetParent(aeroRoot, false);
        go.transform.SetPositionAndRotation(c, q);
        var ring = go.GetComponent<BoostRing>();
        ring.radius = r; ring.speed = speed;
    }

    // ---- tropical fish swimming through the air: ride on their backs -----------------------------------------

    static void AeroFish()
    {
        var kinds = new[] {
            (new Color32(255, 140, 30, 255), new Color32(255, 255, 255, 255), 7f),    // a clownfish
            (new Color32(40, 90, 230, 255), new Color32(255, 220, 40, 255), 8f),      // a blue tang
            (new Color32(250, 250, 240, 255), new Color32(30, 30, 40, 255), 9f),      // an angelfish
            (new Color32(255, 220, 40, 255), new Color32(60, 70, 200, 255), 6.5f) };
        var loops = new[] {
            (new Vector3(0f, 14f, -40f), 40f, 30f, 6f), (new Vector3(-50f, 18f, 80f), 30f, 45f, 7f), (new Vector3(30f, 22f, 150f), 45f, 35f, 6f),
            (new Vector3(0f, 10f, 210f), 35f, 30f, 8f), (new Vector3(-20f, 28f, 270f), 50f, 40f, 6f), (new Vector3(10f, 30f, 360f), 40f, 30f, 7f), (new Vector3(-70f, 9f, 60f), 22f, 22f, 6f) };
        for (int i = 0; i < loops.Length; i++)
        {
            var (body, stripe, len) = kinds[i % kinds.Length];
            var (c, rx, rz, speed) = loops[i];
            var go = new GameObject("Fish", typeof(Rigidbody), typeof(Mover), typeof(BoxCollider), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(aeroRoot, false);
            go.GetComponent<Rigidbody>().isKinematic = true;
            var mesh = FishMesh(body, stripe, len, i % kinds.Length);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { aeroGlossy, outlineMat };
            var bc = go.GetComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0f, 0f); bc.size = new Vector3(len * 0.22f, len * 0.36f, len * 0.75f);
            // a loop that rises and falls a little as it goes round (the ground's well below)
            var path = new List<Vector3>();
            for (int k = 0; k < 20; k++)
            {
                float a = k * Mathf.PI * 2f / 20f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a * 2f) * 3f, Mathf.Sin(a) * rz);
                p.y = Mathf.Max(p.y, AeroH(p.x, p.z) + 6f);
                path.Add(p);
            }
            var m = go.GetComponent<Mover>();
            m.path = path.ToArray(); m.loop = true; m.speed = speed; m.startAt = i * 13f;
        }
    }

    // a fish: a tall flattened body, a forked tail, fins top and bottom, stripes, eyes; nose along +z
    static Mesh FishMesh(Color32 body, Color32 stripe, float len, int kind)
    {
        var k = new SkateMeshKit();
        float h = len * (kind == 2 ? 0.45f : 0.28f), w = len * 0.12f;
        k.Ellipsoid(Vector3.zero, new Vector3(w, h * 0.5f, len * 0.4f), Quaternion.identity, 16, 10, body);
        // stripes: thin bands round the body
        foreach (var z in new[] { -0.15f, 0.12f })
            k.Ellipsoid(new Vector3(0f, 0f, z * len), new Vector3(w * 1.04f, h * 0.48f, len * 0.05f), Quaternion.identity, 14, 8, stripe);
        // tail
        Vector3 tb = new Vector3(0f, 0f, -len * 0.38f);
        foreach (var s in new[] { -1f, 1f })
            k.Tri(tb, tb + new Vector3(0f, s * h * 0.55f, -len * 0.22f), tb + new Vector3(0f, s * h * 0.1f, -len * 0.14f), Vector3.right, stripe);
        foreach (var s in new[] { -1f, 1f })
            k.Tri(tb, tb + new Vector3(0f, s * h * 0.55f, -len * 0.22f), tb + new Vector3(0f, s * h * 0.1f, -len * 0.14f), Vector3.left, stripe);
        // fins: up top and underneath, longer on an angelfish
        float fin = kind == 2 ? h * 0.9f : h * 0.35f;
        foreach (var s in new[] { 1f, -1f })
            foreach (var side in new[] { Vector3.right, Vector3.left })
                k.Tri(new Vector3(0f, s * h * 0.4f, len * 0.15f), new Vector3(0f, s * (h * 0.4f + fin), -len * 0.2f), new Vector3(0f, s * h * 0.35f, -len * 0.25f), side, body);
        // eyes
        foreach (var s in new[] { -1f, 1f })
        {
            k.Ellipsoid(new Vector3(s * w * 0.75f, h * 0.12f, len * 0.28f), Vector3.one * len * 0.035f, Quaternion.identity, 8, 6, new Color32(255, 255, 255, 255));
            k.Ellipsoid(new Vector3(s * w * 0.85f, h * 0.12f, len * 0.29f), Vector3.one * len * 0.02f, Quaternion.identity, 8, 6, new Color32(20, 20, 30, 255));
        }
        var mesh = k.Build("Fish");
        SaveFeatureMesh(mesh);
        return mesh;
    }

    // ---- the sky: the sun and its rays ---------------------------------------------------------------------------

    static void AeroSky(Material skyMat)
    {
        var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyGo.name = "Sky";
        Object.DestroyImmediate(skyGo.GetComponent<Collider>());
        skyGo.transform.localScale = Vector3.one * 8000f;
        skyGo.GetComponent<Renderer>().sharedMaterial = skyMat;
        var rays = Mat("AeroRays", "3daigamesolo/World/Rays");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Sun rays";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(aeroRoot, false);
        quad.transform.position = AeroSunDir * 2600f + new Vector3(0f, 0f, 100f);
        quad.transform.localScale = Vector3.one * 1900f;
        quad.GetComponent<Renderer>().sharedMaterial = rays;
        quad.AddComponent<Billboard>();
        quad.transform.rotation = Quaternion.LookRotation(quad.transform.position);
    }

    // ---- you: a glossy blue jelly person on a CD ------------------------------------------------------------------

    static Transform JellyRider(Transform parent)
    {
        var mat = Mat("AeroJelly", "3daigamesolo/World/Bubble");
        mat.SetColor("_Color", new Color(0.25f, 0.6f, 1f, 1f)); mat.SetFloat("_Alpha", 0.6f); mat.SetFloat("_Rainbow", 0.25f);
        mat.SetVector("_SunDir", new Vector4(AeroSunDir.x, AeroSunDir.y, AeroSunDir.z, 0f));
        var root = new GameObject("Rider").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, -0.35f, 0f);
        var k = new SkateMeshKit();
        Color32 blue = new Color32(255, 255, 255, 255);
        k.Ellipsoid(new Vector3(0f, -0.42f, 0f), new Vector3(0.36f, 0.5f, 0.32f), Quaternion.identity, 20, 12, blue);   // the body, a soft dome
        k.Ellipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.identity, 20, 12, blue);   // the head
        var mesh = k.Build("Jelly");
        SaveFeatureMesh(mesh);
        var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.GetComponent<MeshFilter>().sharedMesh = mesh;
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        root.gameObject.SetActive(false);
        return root;
    }

    static Transform CDBoard(Vector3 at, Quaternion rot)
    {
        var mat = Mat("AeroCD", "3daigamesolo/World/Bubble");
        mat.SetColor("_Color", new Color(0.86f, 0.9f, 0.96f, 1f)); mat.SetFloat("_Alpha", 1f); mat.SetFloat("_Rainbow", 1f);
        mat.SetVector("_SunDir", new Vector4(AeroSunDir.x, AeroSunDir.y, AeroSunDir.z, 0f));
        var k = new SkateMeshKit();
        int seg = 40;
        float r0 = 0.09f, r1 = 0.62f, y = 0.1f;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Color32 col = new Color32(255, 255, 255, 255);
            k.Quad(d0 * r0 + Vector3.up * y, d0 * r1 + Vector3.up * y, d1 * r1 + Vector3.up * y, d1 * r0 + Vector3.up * y, Vector3.up, col);
            k.Quad(d0 * r0 + Vector3.up * (y - 0.015f), d0 * r1 + Vector3.up * (y - 0.015f), d1 * r1 + Vector3.up * (y - 0.015f), d1 * r0 + Vector3.up * (y - 0.015f), Vector3.down, col);
            k.Quad(d0 * r1 + Vector3.up * y, d1 * r1 + Vector3.up * y, d1 * r1 + Vector3.up * (y - 0.015f), d0 * r1 + Vector3.up * (y - 0.015f), d0 + d1, col);
        }
        var mesh = k.Build("CD");
        SaveFeatureMesh(mesh);
        var go = new GameObject("Board", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetPositionAndRotation(at, rot);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    public static void AeroShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) { m.Init(); for (int i = 0; i < 30; i++) m.Step(0.05f); }
        Physics.SyncTransforms();
        Vector3 s = new Vector3(0f, AeroH(0f, -130f), -130f);
        Shot(cam, s + new Vector3(0f, 3f, -6f), s + new Vector3(0f, -10f, 80f), "e01-start.png");
        Shot(cam, new Vector3(-60f, 60f, -80f), new Vector3(0f, 10f, 150f), "e02-overview.png");
        Shot(cam, AeroAt(0f, 40f) + new Vector3(0f, 3f, 0f), AeroAt(0f, 120f) + new Vector3(0f, 6f, 0f), "e03-halfpipe.png");
        Shot(cam, new Vector3(-20f, 30f, 10f), new Vector3(-30f, 34f, 40f), "e04-cloud.png");
        Shot(cam, new Vector3(0f, 20f, 280f), new Vector3(0f, 20f, 400f), "e05-city.png");
        Shot(cam, AeroAt(-40f, 30f) + new Vector3(0f, 4f, 0f), new Vector3(-75f, 2f, 60f), "e06-lake.png");
    }
}
