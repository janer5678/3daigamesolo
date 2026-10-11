using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Akiba: an Akihabara night in the PlayStation style. A long main street packed both sides with
// buildings covered in neon (vertical signs sticking out over the street, sign bands, LED tickers,
// rooftop billboards, graffiti), shop stalls and vending machines along the pavements, traffic both
// ways down the middle, side streets strung with power lines, an elevated railway with Akihabara
// station and a train, flying cars, skyscrapers, and a web of power lines between the skyscrapers'
// pylons over the top of it all. Rails go everywhere, Jet Set Radio style: curving up from kickers to
// people's balconies, across the street, round pillars.
//
// The route the level's built round, from the start at the south end: kick off the ramp on the east
// pavement onto the curving rail, grind it up onto a balcony, ride the sign ramp up to the roof, hop
// onto the flying car that pulls up alongside, ride it across the street and up the skyscraper's face,
// wall-run along the skyscraper, catch the pipe running up its corner and grind it (hold Shift: power
// grind) into the power lines at the top of the map, then right across the city on them. There are
// lots of other ways up: ramps onto stall roofs, ramps from there onto balconies, rails between
// balconies and roofs, the station stairs and train, more flying cars.
//
// Textures come from Tools/make_akiba_textures.py (Assets/Akiba/Textures).
public static partial class SkateWorldBuilder
{
    public const string AkibaPath = "Assets/Scenes/Akiba.unity";
    const string AkibaTex = "Assets/Akiba/Textures", AkibaMats = "Assets/Akiba/Materials";

    const float AveLen = 480f, Road = 12f, Front = 20f;    // street runs along z; buildings' fronts at x = +-20
    static readonly float[] CrossZ = { -320f, -160f, 160f, 320f };
    const float CrossHalf = 8f, RailHalf = 14f, AkibaDeck = 12f;

    static readonly Dictionary<string, PSXKit> akKits = new Dictionary<string, PSXKit>();
    static readonly Dictionary<string, Material> akMats = new Dictionary<string, Material>();
    static System.Random ark;
    static Transform akRoot, akCol;
    static readonly List<Vector3> akAnchors = new List<Vector3>();   // balcony and roof-edge points rails can join up

    static float AR(float a, float b) { return a + (float)ark.NextDouble() * (b - a); }
    static int AI(int a, int b) { return ark.Next(a, b); }

    // ---- materials and textures ------------------------------------------------------------------

    static void ImportAkibaTextures()
    {
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(AkibaTex, "*.png"))
        {
            var imp = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
            if (imp == null) continue;
            bool changed = imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed;
            imp.filterMode = FilterMode.Point;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = path.Contains("graffiti");
            imp.maxTextureSize = 512;
            if (changed) imp.SaveAndReimport();
        }
    }

    // style: "" lit by the moon, "e" full bright, "f" full bright and flickering, "s" full bright and scrolling, "c" cut-out (graffiti)
    static Material AkMat(string tex, string style)
    {
        string key = tex + "|" + style;
        Material m;
        if (akMats.TryGetValue(key, out m)) return m;
        Directory.CreateDirectory(AkibaMats);
        string path = AkibaMats + "/" + tex + (style == "" ? "" : "_" + style) + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("3daigamesolo/PSX/Textured");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AkibaTex + "/" + tex + ".png"));
        m.SetFloat("_Emission", style == "" || style == "c" ? 0f : 1f);
        m.SetVector("_Scroll", style == "s" ? new Vector4(0.12f, 0f, 0f, 0f) : Vector4.zero);
        m.SetVector("_Flicker", style == "f" ? new Vector4(AR(0.3f, 1.4f), AR(0.55f, 0.9f), AR(0.15f, 0.4f), 0f) : new Vector4(0f, 1f, 1f, 0f));
        m.SetFloat("_Cutoff", style == "c" ? 0.5f : 0f);
        EditorUtility.SetDirty(m);
        akMats[key] = m;
        return m;
    }

    static PSXKit AK(string tex, string style = "")
    {
        string key = tex + "|" + style;
        PSXKit k;
        if (!akKits.TryGetValue(key, out k)) { k = new PSXKit(); akKits[key] = k; AkMat(tex, style); }
        return k;
    }

    static void FlushAkiba(Transform parent)
    {
        foreach (var kv in akKits)
        {
            if (kv.Value.Empty) continue;
            var mesh = kv.Value.Build(kv.Key.Replace('|', '_'));
            SaveFeatureMesh(mesh);
            var go = new GameObject(kv.Key, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = akMats[kv.Key];
        }
        akKits.Clear();
    }

    static void AkBox(Vector3 c, Vector3 size, Quaternion? rot = null) { AddBox(akCol, "Solid", c, size, rot); }

    // for a panel facing n: the direction that's right to someone looking at it (so its text reads the right way round)
    static Vector3 Rt(Vector3 n) { return Vector3.Cross(n, Vector3.up); }

    // stretches of pavement kept clear of stalls (kickers and the route are there): x side sign, z from, z to
    static readonly List<Vector3> akNoStall = new List<Vector3>();

    // a flat wall from p0 along `along` for `w` metres, from y0 to y1, facing `n`, tiled every tu x tv metres
    static void Wall(PSXKit k, Vector3 p0, Vector3 along, float w, float y0, float y1, Vector3 n, float tu, float tv, float u0 = 0f)
    {
        Vector3 a = new Vector3(p0.x, y0, p0.z), b = a + along * w;
        k.Quad(a, new Vector3(a.x, y1, a.z), new Vector3(b.x, y1, b.z), b,
               new Vector2(u0, y0 / tv), new Vector2(u0, y1 / tv), new Vector2(u0 + w / tu, y1 / tv), new Vector2(u0 + w / tu, y0 / tv), n);
    }

    // a wall facing n, w wide centred on centre (x, z), from y0 to y1, with its texture running left to
    // right as you look at it (so any writing on it reads the right way round)
    static void FrontWall(PSXKit k, Vector3 centre, Vector3 n, float w, float y0, float y1, float tu, float tv)
    {
        Vector3 along = Rt(n);
        Wall(k, new Vector3(centre.x, 0f, centre.z) - along * (w / 2f), along, w, y0, y1, n, tu, tv);
    }

    // ---- the level --------------------------------------------------------------------------------------

    // -executeMethod SkateWorldBuilder.AkibaFromCommandLine: just this level, its test runs and screenshots
    public static void AkibaFromCommandLine()
    {
        try { BuildAkiba(); SlideTests.RunAkiba(); AkibaShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildAkiba()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/AkibaFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); akKits.Clear(); akMats.Clear(); akAnchors.Clear(); akNoStall.Clear();
        akNoStall.Add(new Vector3(1f, -440f, -400f));   // the route's kicker and rail on the east pavement
        ark = new System.Random(19990629);
        floorAt = (x, z) => Mathf.Abs(x) > Road && Mathf.Abs(x) < Front + 0.5f ? Kerb : 0f;
        ImportAkibaTextures();

        // ramps, rails, ledges: lit by the moon in the PSX shader (white texture, vertex colours), thin ink lines
        RampTop = new Color32(255, 200, 80, 255); RampSide = new Color32(170, 160, 190, 255); RampSide2 = new Color32(120, 220, 255, 255);
        Metal = new Color32(200, 200, 215, 255); RailCol = new Color32(255, 214, 40, 255);
        Concrete = new Color32(170, 168, 180, 255); ConcreteDark = new Color32(130, 126, 140, 255);
        toonMat = AkMat("white", "");
        outlineMat = Mat("AkibaOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.05f, 0.02f, 0.09f));
        outlineMat.SetFloat("_OutlineWidth", 1.1f);
        outlineMat.SetColor("_Haze", new Color(0.16f, 0.08f, 0.24f)); outlineMat.SetColor("_HazeHigh", new Color(0.12f, 0.06f, 0.2f));
        outlineMat.SetFloat("_FogDensity", 0.004f); outlineMat.SetFloat("_FogHeight", 900f); outlineMat.SetFloat("_FogMax", 0.95f);
        farOutlineMat = outlineMat;
        var sky = Mat("AkibaSky", "3daigamesolo/World/Sky");
        sky.SetColor("_Zenith", new Color(0.05f, 0.03f, 0.14f)); sky.SetColor("_Horizon", new Color(0.55f, 0.24f, 0.5f));
        sky.SetColor("_Below", new Color(0.16f, 0.08f, 0.24f)); sky.SetColor("_Cloud", new Color(0.42f, 0.22f, 0.5f));
        sky.SetColor("_CloudShade", new Color(0.24f, 0.11f, 0.32f)); sky.SetFloat("_CloudCover", 0.45f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        featureRoot = new GameObject("Skate features").transform;
        akRoot = new GameObject("Akiba").transform;
        akCol = new GameObject("Colliders").transform;
        akCol.SetParent(akRoot, false);

        AkStreets();
        AkRailway();
        AkBuildings();
        AkSideStreets();
        AkRoute();
        AkCurvyRails();
        AkTopLines();
        AkTraffic();
        AkWeird();
        FlushAkiba(akRoot);

        var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyGo.name = "Sky";
        Object.DestroyImmediate(skyGo.GetComponent<Collider>());
        skyGo.transform.localScale = Vector3.one * 9000f;
        skyGo.GetComponent<Renderer>().sharedMaterial = sky;

        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(new Vector3(16f, Kerb + 0.2f, -462f), Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(PSXCamera));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 9000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.08f, 0.24f);
        var psx = camGO.GetComponent<PSXCamera>();
        psx.fogColor = new Color(0.16f, 0.08f, 0.24f); psx.fogStart = 90f; psx.fogEnd = 620f;
        psx.Apply();
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Akiba";
        camGO.transform.SetPositionAndRotation(new Vector3(16f, 2.5f, -467f), Quaternion.LookRotation(Vector3.forward));
        player.board = BuildBoard(new Vector3(16f, Kerb, -460f), Quaternion.LookRotation(Vector3.forward));
        TowerCheckpoint(akRoot, new Vector3(18f, Kerb, -462f), "Akiba");

        EditorSceneManager.SaveScene(scene, AkibaPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_AKIBA_BUILT features=" + featureCount + " materials=" + akMats.Count);
    }

    // a kicker you can hit from either side: two back to back, lips meeting in the middle
    static void Hump(string name, Vector3 at, Vector3 dir, float radius, float degrees, float width, Color32 side)
    {
        float len = radius * Mathf.Sin(degrees * Mathf.Deg2Rad);
        Kicker(name, Matrix4x4.TRS(at, Quaternion.LookRotation(dir), Vector3.one), radius, degrees, width, side);
        Kicker(name, Matrix4x4.TRS(at + dir * len * 2f, Quaternion.LookRotation(-dir), Vector3.one), radius, degrees, width, side);
    }

    // ---- streets ---------------------------------------------------------------------------------------

    static void AkStreets()
    {
        // tarmac under everything, pavements a kerb up either side of the main street
        AkBox(new Vector3(0f, -0.5f, 0f), new Vector3(1400f, 1f, 1400f));
        AK("asphalt").Ground(-Road, -AveLen - 20f, Road, AveLen + 20f, 0f, 6f, 10f);
        foreach (var cz in CrossZ) AK("asphalt").Ground(Front, cz - CrossHalf, 260f, cz + CrossHalf, 0f, 6f, 10f);
        foreach (var cz in CrossZ) AK("asphalt").Ground(-260f, cz - CrossHalf, -Front, cz + CrossHalf, 0f, 6f, 10f);
        foreach (var s in new[] { -1f, 1f })
        {
            float x0 = s > 0 ? Road : -Front, x1 = s > 0 ? Front : -Road;
            AK("pavement").Ground(x0, -AveLen - 20f, x1, AveLen + 20f, Kerb, 2f, 8f);
            AK("tactile").Ground(s * (Road + 1.2f) - 0.3f, -AveLen - 20f, s * (Road + 1.2f) + 0.3f, AveLen + 20f, Kerb + 0.01f, 0.6f, 20f);
            AkBox(new Vector3(s * (Road + Front) / 2f, Kerb / 2f, 0f), new Vector3(Front - Road, Kerb, AveLen * 2f + 40f));
            AK("concrete").Box(new Vector3(s * (Road + 0.1f), Kerb / 2f, 0f), new Vector3(0.2f, Kerb, AveLen * 2f + 40f), Quaternion.identity, 4f, 4f);
        }
        // the far ground beyond the blocks
        AK("concrete").Ground(-700f, -700f, 700f, -AveLen - 20f, -0.01f, 20f, 60f);
        AK("concrete").Ground(-700f, AveLen + 20f, 700f, 700f, -0.01f, 20f, 60f);
        foreach (var s in new[] { -1f, 1f })
            AK("concrete").Ground(s > 0 ? 260f : -700f, -AveLen - 20f, s > 0 ? 700f : -260f, AveLen + 20f, -0.01f, 20f, 60f);
        // lane markings and crossings
        var w = AK("white", "e");
        for (float z = -AveLen; z < AveLen; z += 8f)
            foreach (var x in new[] { -6f, 6f })
                w.Box(new Vector3(x, 0.01f, z), new Vector3(0.18f, 0.02f, 4f), Quaternion.identity, 1f, 1f);
        var y = AK("white", "");
        foreach (var x in new[] { -0.15f, 0.15f })
            y.Box(new Vector3(x, 0.01f, 0f), new Vector3(0.12f, 0.02f, AveLen * 2f), Quaternion.identity, 1f, 1f, new Color32(255, 200, 40, 255));
        foreach (var cz in CrossZ)
            for (float x = -Road + 1f; x < Road; x += 1.6f)
                w.Box(new Vector3(x, 0.01f, cz), new Vector3(0.9f, 0.02f, 6f), Quaternion.identity, 1f, 1f);
        // big quarterpipes across both ends of the street
        foreach (var s in new[] { -1f, 1f })
            QuarterPipe("End wall", Matrix4x4.TRS(new Vector3(0f, 0f, s * (AveLen + 6f)), Quaternion.LookRotation(new Vector3(0f, 0f, s)), Vector3.one), Front * 2f, 4f, 3f);
        // street lamps along the kerbs
        for (float z = -AveLen + 10f; z < AveLen; z += 24f)
            foreach (var s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(z) < RailHalf + 2f) continue;
                Vector3 p = new Vector3(s * (Road + 0.6f), Kerb, z);
                AK("metal").Cylinder(p, p + Vector3.up * 7f, 0.12f, 6, 2f, new Color32(150, 150, 170, 255));
                AK("metal").Cylinder(p + Vector3.up * 7f, p + Vector3.up * 7f + new Vector3(-s * 1.6f, 0.3f, 0f), 0.08f, 6, 2f, new Color32(150, 150, 170, 255));
                AK("white", "e").Box(p + new Vector3(-s * 1.8f, 7.2f, 0f), new Vector3(0.7f, 0.2f, 0.4f), Quaternion.identity, 1f, 1f, new Color32(255, 240, 200, 255));
                var cap = new GameObject("Lamp post").AddComponent<CapsuleCollider>();
                cap.transform.SetParent(akCol, false); cap.transform.position = p + Vector3.up * 3.5f; cap.radius = 0.14f; cap.height = 7f;
            }
        // guard rails along the kerbs (grindable), with gaps for the crossings
        foreach (var s in new[] { -1f, 1f })
        {
            var spans = new List<Vector2>();
            float zs = -AveLen;
            var stops = new List<float>(CrossZ) { 0f };
            stops.Sort();
            foreach (var cz in stops) { float half = cz == 0f ? RailHalf : CrossHalf + 2f; spans.Add(new Vector2(zs, cz - half)); zs = cz + half; }
            spans.Add(new Vector2(zs, AveLen));
            foreach (var sp in spans)
                for (float z = sp.x + 8f; z < sp.y - 8f; z += 40f)
                {
                    float z1 = Mathf.Min(z + 24f, sp.y - 8f);
                    Rail("Guard rail", new[] { new Vector3(s * (Road + 0.35f), Kerb + 0.75f, z), new Vector3(s * (Road + 0.35f), Kerb + 0.75f, z1) });
                }
        }
    }

    // ---- the railway, the station and the train ---------------------------------------------------------

    static void AkRailway()
    {
        float half = 560f;
        var c = AK("concrete");
        c.Box(new Vector3(0f, AkibaDeck - 0.7f, 0f), new Vector3(half * 2f, 1.4f, 10f), Quaternion.identity, 4f, 4f, null, true, true);
        AkBox(new Vector3(0f, AkibaDeck - 0.7f, 0f), new Vector3(half * 2f, 1.4f, 10f));
        foreach (var s in new[] { -1f, 1f })
        {
            c.Box(new Vector3(0f, AkibaDeck + 0.6f, s * 4.85f), new Vector3(half * 2f, 1.2f, 0.3f), Quaternion.identity, 4f, 4f);
            AkBox(new Vector3(0f, AkibaDeck + 0.6f, s * 4.85f), new Vector3(half * 2f, 1.2f, 0.3f));
            AK("posters").Box(new Vector3(0f, AkibaDeck - 1f, s * 5.02f), new Vector3(half * 2f, 1.2f, 0.02f), Quaternion.identity, 3f, 1.5f);
        }
        for (float x = -half + 12f; x < half; x += 24f)
        {
            if (Mathf.Abs(x) < Road + 1f) continue;   // the street passes under clear
            c.Box(new Vector3(x, (AkibaDeck - 1.4f) / 2f, 0f), new Vector3(1.8f, AkibaDeck - 1.4f, 1.8f), Quaternion.identity, 2f, 2f);
            AkBox(new Vector3(x, (AkibaDeck - 1.4f) / 2f, 0f), new Vector3(1.8f, AkibaDeck - 1.4f, 1.8f));
            AK("graffiti_" + AI(0, 6), "c").Panel(new Vector3(x, 2.2f, -0.92f), Vector3.right * 1.2f, Vector3.up * 0.6f);
        }
        // pillars either side of the street hold the span over it
        foreach (var sx in new[] { -1f, 1f })
        {
            c.Box(new Vector3(sx * (Road + 0.8f), (AkibaDeck - 1.4f) / 2f, 0f), new Vector3(1.4f, AkibaDeck - 1.4f, 6f), Quaternion.identity, 2f, 2f);
            AkBox(new Vector3(sx * (Road + 0.8f), (AkibaDeck - 1.4f) / 2f, 0f), new Vector3(1.4f, AkibaDeck - 1.4f, 6f));
        }
        // a big sign across the bridge over the street
        foreach (var s in new[] { -1f, 1f })
            AK("station_sign", "e").Panel(new Vector3(0f, AkibaDeck - 0.6f, s * 5.05f), Vector3.right * -s * 8f, Vector3.up * 2f);
        // tracks: sleepers and rails (grindable, right across the map)
        for (float x = -half; x < half; x += 1.5f)
            foreach (var tz in new[] { -2.5f, 2.5f })
                AK("concrete").Box(new Vector3(x, AkibaDeck + 0.05f, tz), new Vector3(0.3f, 0.1f, 2.4f), Quaternion.identity, 1f, 1f);
        foreach (var tz in new[] { -2.5f, 2.5f })
            foreach (var rz in new[] { -0.72f, 0.72f })
                Rail("Track", new[] { new Vector3(-half, AkibaDeck + 0.18f, tz + rz), new Vector3(half, AkibaDeck + 0.18f, tz + rz) }, false);

        // the station: on the west side, platforms either side of the tracks, a big roof, and a building under it
        float sx0 = -150f, sx1 = -36f;
        foreach (var s in new[] { -1f, 1f })
        {
            Vector3 pc = new Vector3((sx0 + sx1) / 2f, AkibaDeck + 0.5f, s * 7.6f);
            AK("concrete").Box(pc, new Vector3(sx1 - sx0, 1f, 5f), Quaternion.identity, 2f, 2f);
            AkBox(pc, new Vector3(sx1 - sx0, 1f, 5f));
            AK("tactile").Ground(sx0, s > 0 ? 5.4f : -6.0f, sx1, s > 0 ? 6.0f : -5.4f, AkibaDeck + 1.01f, 0.6f, 10f);
            for (float x = sx0 + 6f; x < sx1; x += 12f)
            {
                AK("metal").Box(new Vector3(x, AkibaDeck + 4.5f, s * 9.6f), new Vector3(0.4f, 7f, 0.4f), Quaternion.identity, 1f, 1f);
                AkBox(new Vector3(x, AkibaDeck + 4.5f, s * 9.6f), new Vector3(0.4f, 7f, 0.4f));
                AK("station_sign", "e").Panel(new Vector3(x + 6f, AkibaDeck + 3.6f, s * 5.4f), Vector3.right * -s * 1.8f, Vector3.up * 0.45f, null, true);
            }
        }
        // roof over the whole station (a big flat top to land on)
        AK("roof").Box(new Vector3((sx0 + sx1) / 2f, AkibaDeck + 8.2f, 0f), new Vector3(sx1 - sx0 + 4f, 0.6f, 22f), Quaternion.identity, 4f, 4f, null, true, true);
        AkBox(new Vector3((sx0 + sx1) / 2f, AkibaDeck + 8.2f, 0f), new Vector3(sx1 - sx0 + 4f, 0.6f, 22f));
        AkParapet(Rect.MinMaxRect(sx0 - 2f, -11f, sx1 + 2f, 11f), AkibaDeck + 8.5f, 0f);
        // the station building underneath, with the station name and lit gates
        foreach (var s in new[] { -1f, 1f })
        {
            Vector3 sc = new Vector3((sx0 + sx1) / 2f, 0f, s * 14f);
            FrontWall(AK("facade_1"), sc, new Vector3(0f, 0f, s), sx1 - sx0, 4f, AkibaDeck - 1.4f, 4f, 3.2f);
            FrontWall(AK("shop_" + AI(0, 10), "e"), sc, new Vector3(0f, 0f, s), sx1 - sx0, 0f, 4f, 4f, 4f);
            AK("station_sign", "e").Panel(new Vector3((sx0 + sx1) / 2f, AkibaDeck - 3f, s * 14.05f), Vector3.right * -s * 10f, Vector3.up * 2.5f);
        }
        AkBox(new Vector3((sx0 + sx1) / 2f, (AkibaDeck - 1.4f) / 2f, 0f), new Vector3(sx1 - sx0, AkibaDeck - 1.4f, 28f));
        // the ticket gates' bars on the north side: slide under them into the station's stair hall
        // stairs from the street on the north side up to the platform, with handrails
        float stairX = sx1 - 8f, run = 22f, top = AkibaDeck + 1f, z0 = 14f;
        int steps = 36;
        for (int i = 0; i < steps; i++)
        {
            float sh = top * (steps - i) / steps, za = z0 + run * i / steps, zb = z0 + run * (i + 1) / steps;
            AK("concrete").Box(new Vector3(stairX, sh / 2f, (za + zb) / 2f), new Vector3(5f, sh, zb - za), Quaternion.identity, 2f, 2f);
        }
        float slope = Mathf.Atan2(top, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(top * top + run * run);
        AkBox(new Vector3(stairX, top / 2f - 0.15f, z0 + run / 2f), new Vector3(5f, 0.3f, len), Quaternion.Euler(slope, 0f, 0f));
        AK("concrete").Box(new Vector3(stairX, top - 0.5f, 11.5f), new Vector3(5f, 1f, 5f), Quaternion.identity, 2f, 2f);
        AkBox(new Vector3(stairX, top - 0.5f, 11.5f), new Vector3(5f, 1f, 5f));
        foreach (var hx in new[] { stairX - 2.4f, stairX + 2.4f })
            Rail("Stair handrail", new[] { new Vector3(hx, top + 0.9f, 10f), new Vector3(hx, top + 0.9f, z0), new Vector3(hx, 0.9f + Kerb, z0 + run), new Vector3(hx, 0.9f + Kerb, z0 + run + 2f) });
        TowerCheckpoint(akRoot, new Vector3(sx1 - 14f, AkibaDeck + 1f, 7.6f), "Akihabara station");

        // the train: back and forth, stopping at the station
        var go = new GameObject("Train", typeof(Rigidbody), typeof(Mover));
        go.transform.SetParent(akRoot, false);
        var tk = new PSXKit();
        float carLen = 19f, w = 2.9f, h = 3.5f;
        for (int i = -2; i <= 2; i++)
        {
            float cz = i * (carLen + 0.8f);
            tk.Box(new Vector3(0f, h / 2f + 0.4f, cz), new Vector3(w, h, carLen), Quaternion.identity, carLen / 1f, h * 1.15f, null, true, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, h / 2f + 0.4f, cz); bc.size = new Vector3(w, h, carLen);
        }
        var tm = tk.Build("Train");
        SaveFeatureMesh(tm);
        go.AddComponent<MeshFilter>().sharedMesh = tm;
        go.AddComponent<MeshRenderer>().sharedMaterial = AkMat("train_side", "");
        go.GetComponent<Rigidbody>().isKinematic = true;
        var m = go.GetComponent<Mover>();
        m.path = new[] { new Vector3(-520f, AkibaDeck + 0.0f, 2.5f), new Vector3((sx0 + sx1) / 2f, AkibaDeck, 2.5f), new Vector3(520f, AkibaDeck, 2.5f) };
        m.loop = false; m.speed = 22f; m.wait = 7f; m.easeIn = 60f; m.turn = false;
        m.stops = new[] { 520f + (sx0 + sx1) / 2f };
        go.transform.rotation = Quaternion.LookRotation(Vector3.right);
    }

    // ---- buildings along the main street ---------------------------------------------------------------

    struct AkLot { public float z0, z1, depth, h; public string special; }

    // the special buildings the route uses
    static readonly Rect EaLot = Rect.MinMaxRect(Front, -384f, Front + 30f, -330f);      // the porch building, 22 m
    static readonly Rect W1Lot = Rect.MinMaxRect(-Front - 32f, -306f, -Front, -196f);    // the skyscraper you wall-run, 60 m
    static readonly Rect E2Lot = Rect.MinMaxRect(Front, -150f, Front + 40f, -104f);      // 84 m
    static readonly Rect W3Lot = Rect.MinMaxRect(-Front - 40f, 40f, -Front, 100f);       // 78 m
    static readonly Rect E4Lot = Rect.MinMaxRect(Front, 196f, Front + 40f, 262f);        // 96 m
    static readonly Rect W5Lot = Rect.MinMaxRect(-Front - 36f, 336f, -Front, 404f);      // 70 m
    const float EaH = 22f, W1H = 60f, E2H = 84f, W3H = 78f, E4H = 96f, W5H = 70f;

    static void AkBuildings()
    {
        AkTower(EaLot, EaH, -1f, false);
        AkTower(W1Lot, W1H, 1f, true);
        AkTower(E2Lot, E2H, -1f, true);
        AkTower(W3Lot, W3H, 1f, true);
        AkTower(E4Lot, E4H, -1f, true);
        AkTower(W5Lot, W5H, 1f, true);
        var specials = new[] { EaLot, W1Lot, E2Lot, W3Lot, E4Lot, W5Lot };
        foreach (var side in new[] { -1f, 1f })
        {
            float z = -AveLen + 4f;
            while (z < AveLen - 6f)
            {
                float w = AR(10f, 22f);
                float z1 = Mathf.Min(z + w, AveLen - 4f);
                // skip the cross streets and the railway
                bool blocked = Mathf.Abs(z) < RailHalf + 1f || Mathf.Abs(z1) < RailHalf + 1f || (z < 0f && z1 > 0f);
                foreach (var cz in CrossZ) if (z1 > cz - CrossHalf - 1f && z < cz + CrossHalf + 1f) blocked = true;
                Rect lot = side > 0 ? Rect.MinMaxRect(Front, z, Front + AR(20f, 36f), z1) : Rect.MinMaxRect(-Front - AR(20f, 36f), z, -Front, z1);
                foreach (var sp in specials) if (sp.Overlaps(lot)) blocked = true;
                if (!blocked) AkTower(lot, AR(12f, 40f), -side, false);
                z = z1 + (ark.NextDouble() < 0.15 ? 3f : 0f);   // the odd alley
            }
        }
    }

    // a building: shops at street level, a lit facade above, signs sticking out over the street, a sign
    // band or two, an LED ticker, a billboard on the roof; stalls and vending machines on the pavement in
    // front; sometimes a balcony (a porch) with a grindable railing
    static void AkTower(Rect lot, float h, float face, bool sky)
    {
        // face: -1 front faces -x (east side of the street), +1 front faces +x (west side)
        Vector3 n = new Vector3(face, 0f, 0f);
        float fx = face < 0 ? lot.xMin : lot.xMax;
        float w = lot.height;      // along z
        AkBox(new Vector3(lot.center.x, h / 2f, lot.center.y), new Vector3(lot.width, h, lot.height));
        string fac = "facade_" + AI(0, 10), side = sky ? "facade_1" : "facade_" + AI(0, 10);
        var fk = AK(fac);
        // front: shop band then the facade
        Vector3 along = Rt(n);
        Vector3 fc = new Vector3(fx, 0f, lot.center.y);
        FrontWall(AK("shop_" + AI(0, 10), "e"), fc, n, w, 0f, 3.6f, 4f, 3.6f);
        FrontWall(fk, fc, n, w, 3.6f, h, 4f, 3.2f);
        // sides and back
        var sk = AK(side);
        Wall(sk, new Vector3(lot.xMin, 0f, lot.yMin), Vector3.right, lot.width, 0f, h, Vector3.back, 4f, 3.2f);
        Wall(sk, new Vector3(lot.xMax, 0f, lot.yMax), Vector3.left, lot.width, 0f, h, Vector3.forward, 4f, 3.2f);
        float bx = face < 0 ? lot.xMax : lot.xMin;
        FrontWall(sk, new Vector3(bx, 0f, lot.center.y), -n, w, 0f, h, 4f, 3.2f);
        AK("roof").Ground(lot.xMin, lot.yMin, lot.xMax, lot.yMax, h, 4f, 12f);
        // a low parapet with a grindable top on three sides (not the street side, so you can ride on)
        AkParapet(lot, h, face);

        // an LED ticker across the shopfront
        var led = AK("led_" + AI(0, 3), "s");
        led.Panel(new Vector3(fx + face * 0.05f, 3.85f, lot.center.y), Rt(n) * (w / 2f - 0.3f), Vector3.up * 0.22f, null, false, new Rect(0f, 0f, w / 6.4f, 1f));
        // sign bands on a couple of floors
        int bands = AI(1, 4);
        for (int b = 0; b < bands; b++)
        {
            float y = AR(5f, Mathf.Max(6f, h - 3f));
            bool special = ark.NextDouble() < 0.25;
            string tex = special ? new[] { "arcade", "maid", "ramen", "karaoke" }[AI(0, 4)] : "sign_h_" + AI(0, 16);
            float bw = Mathf.Min(w - 1f, special ? 10f : AR(6f, w - 1f));
            AK(tex, ark.NextDouble() < 0.3 ? "f" : "e").Panel(new Vector3(fx + face * 0.2f, y, lot.center.y + AR(-(w - bw) / 2f, (w - bw) / 2f)),
                Rt(n) * (bw / 2f), Vector3.up * (bw / 8f));
        }
        // vertical signs sticking out over the pavement
        int vs = Mathf.Clamp(Mathf.RoundToInt(w / 6f), 1, 4);
        for (int i = 0; i < vs; i++)
        {
            float z = Mathf.Lerp(lot.yMin + 1f, lot.yMax - 1f, (i + 0.5f) / vs);
            float sh = Mathf.Min(AR(5f, 12f), h - 5f);
            if (sh < 3f) continue;
            float y = AR(4.5f, Mathf.Max(4.6f, h - sh - 0.5f)) + sh / 2f;
            AK("sign_v_" + AI(0, 16), ark.NextDouble() < 0.35 ? "f" : "e").Panel(new Vector3(fx + face * 0.75f, y, z), new Vector3(face * 0.6f, 0f, 0f), Vector3.up * sh / 2f, null, true);
            AK("metal").Box(new Vector3(fx + face * 0.75f, y, z), new Vector3(1.24f, sh + 0.1f, 0.08f), Quaternion.identity, 1f, 1f);
        }
        // graffiti low on the front or side
        if (ark.NextDouble() < 0.4)
            AK("graffiti_" + AI(0, 6), "c").Panel(new Vector3(lot.center.x, AR(4.5f, 7f), face < 0 ? lot.yMin - 0.03f : lot.yMax + 0.03f), Rt(face < 0 ? Vector3.back : Vector3.forward) * 2.6f, Vector3.up * 1.3f);
        // a billboard on the roof
        if (!sky && ark.NextDouble() < 0.6)
        {
            float bh = AR(5f, 9f);
            Vector3 bc = new Vector3(fx - face * 2f, h + 1.5f + bh / 2f, lot.center.y);
            AK("billboard_" + AI(0, 10), ark.NextDouble() < 0.2 ? "f" : "e").Panel(bc + n * 0.06f, Rt(n) * bh / 2f, Vector3.up * bh / 2f);
            AK("metal").Box(bc, new Vector3(0.1f, bh, bh), Quaternion.identity, 2f, 2f);
            foreach (var o in new[] { -bh / 2.5f, bh / 2.5f })
                AK("metal").Box(new Vector3(bc.x - face * 0.4f, h + 0.75f, bc.z + o), new Vector3(0.25f, 1.5f, 0.25f), Quaternion.identity, 1f, 1f);
            AkBox(bc, new Vector3(0.2f, bh, bh));
        }
        // stalls and vending machines on the pavement in front (their roofs are a ledge to ride onto)
        if (!sky || ark.NextDouble() < 0.5) AkStalls(fx, lot, face);
        // a balcony or two
        if (h > 14f && ark.NextDouble() < 0.55)
            AkPorch(fx, face, AR(7f, Mathf.Min(14f, h - 4f)), lot.center.y + AR(-w / 4f, w / 4f), Mathf.Min(6f, w - 2f));
        if (sky) akAnchors.Add(new Vector3(fx, h, lot.center.y));
    }

    static void AkParapet(Rect lot, float h, float face)
    {
        var mk = AK("concrete");
        Vector3[] c = { new Vector3(lot.xMin, 0f, lot.yMin), new Vector3(lot.xMax, 0f, lot.yMin), new Vector3(lot.xMax, 0f, lot.yMax), new Vector3(lot.xMin, 0f, lot.yMax) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = c[i], b = c[(i + 1) % 4];
            if (Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.x - (face < 0 ? lot.xMin : lot.xMax)) < 0.01f) continue;   // the street side stays open
            Vector3 d = (b - a).normalized, inward = Vector3.Cross(Vector3.up, d);
            Vector3 mid = (a + b) / 2f + inward * 0.15f + Vector3.up * (h + 0.3f);
            Quaternion q = Quaternion.LookRotation(d);
            mk.Box(mid, new Vector3(0.3f, 0.6f, Vector3.Distance(a, b)), q, 2f, 2f);
            AkBox(mid, new Vector3(0.3f, 0.6f, Vector3.Distance(a, b)), q);
            Rail("Roof edge", new[] { a + inward * 0.15f + Vector3.up * (h + 0.62f) + d * 0.3f, b + inward * 0.15f + Vector3.up * (h + 0.62f) - d * 0.3f }, false);
        }
    }

    static void AkStalls(float fx, Rect lot, float face)
    {
        Vector3 n = new Vector3(face, 0f, 0f);
        float z = lot.yMin + 0.6f;
        while (z < lot.yMax - 3.2f)
        {
            bool keepClear = false;
            foreach (var ns in akNoStall) if (Mathf.Sign(ns.x) == -Mathf.Sign(face) && z > ns.y - 4f && z < ns.z) keepClear = true;
            if (keepClear) { z += 2f; continue; }
            float kind = (float)ark.NextDouble();
            if (kind < 0.55f)
            {
                // a stall: a box with a lit front, a striped awning and a flat roof
                float sw = AR(2.6f, 4f);
                Vector3 c = new Vector3(fx + face * 1.4f, 1.3f + Kerb, z + sw / 2f);
                AK("concrete").Box(c, new Vector3(2.8f, 2.6f, sw), Quaternion.identity, 2f, 2f, null, false);
                AK("shop_" + AI(0, 10), "e").Panel(c + n * 1.41f, Rt(n) * sw / 2f, Vector3.up * 1.2f);
                AK("roof").Box(c + Vector3.up * 1.4f + n * 0.4f, new Vector3(3.8f, 0.2f, sw + 0.2f), Quaternion.identity, 2f, 2f);
                AkBox(c + Vector3.up * 0.1f, new Vector3(2.8f, 2.8f, sw));
                AkBox(c + Vector3.up * 1.4f + n * 0.4f, new Vector3(3.8f, 0.2f, sw + 0.2f));
                z += sw + 0.3f;
            }
            else if (kind < 0.8f)
            {
                // a row of vending machines
                int count = AI(2, 5);
                for (int i = 0; i < count; i++)
                {
                    Vector3 c = new Vector3(fx + face * 0.45f, 0.95f + Kerb, z + 0.5f + i * 1.05f);
                    AK("vending", "e").Panel(c + n * 0.41f, Rt(n) * 0.5f, Vector3.up * 0.95f);
                    AK("metal").Box(c, new Vector3(0.8f, 1.9f, 1f), Quaternion.identity, 1f, 1f, new Color32(220, 60, 70, 255));
                    AkBox(c, new Vector3(0.8f, 1.9f, 1f));
                }
                z += count * 1.05f + 0.6f;
            }
            else
            {
                // capsule toy machines, two high
                int count = AI(3, 6);
                for (int i = 0; i < count; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        Vector3 c = new Vector3(fx + face * 0.35f, 0.45f + j * 0.9f + Kerb, z + 0.4f + i * 0.8f);
                        AK("gacha", "e").Panel(c + n * 0.31f, Rt(n) * 0.38f, Vector3.up * 0.44f);
                        AK("white").Box(c, new Vector3(0.6f, 0.88f, 0.78f), Quaternion.identity, 1f, 1f);
                    }
                AkBox(new Vector3(fx + face * 0.35f, 0.9f + Kerb, z + count * 0.4f), new Vector3(0.6f, 1.8f, count * 0.8f));
                z += count * 0.8f + 0.6f;
            }
            z += AR(0.5f, 3f);
        }
    }

    // a balcony sticking out over the pavement, with a railing (grindable)
    static void AkPorch(float fx, float face, float y, float zc, float w)
    {
        Vector3 n = new Vector3(face, 0f, 0f);
        Vector3 c = new Vector3(fx + face * 1.4f, y - 0.15f, zc);
        AK("concrete").Box(c, new Vector3(2.8f, 0.3f, w), Quaternion.identity, 2f, 2f, null, true, true);
        AkBox(c, new Vector3(2.8f, 0.3f, w));
        Vector3 edge = new Vector3(fx + face * 2.7f, y + 1f, 0f);
        Rail("Balcony rail", new[] { new Vector3(edge.x, edge.y, zc - w / 2f + 0.2f), new Vector3(edge.x, edge.y, zc + w / 2f - 0.2f) });
        // a door and a plant pot
        AK("shutter").Panel(new Vector3(fx + face * 0.02f, y + 1.1f, zc), Rt(n) * 0.8f, Vector3.up * 1.1f);
        akAnchors.Add(new Vector3(fx + face * 1.4f, y, zc));
    }

    // ---- side streets: smaller buildings, power lines on poles, slide-under barriers -----------------

    static void AkSideStreets()
    {
        foreach (var cz in CrossZ)
            foreach (var s in new[] { -1f, 1f })
            {
                // buildings along both sides of the side street
                foreach (var zs in new[] { -1f, 1f })
                {
                    float x = Front + 2f;
                    while (x < 240f)
                    {
                        float w = AR(8f, 16f), d = AR(10f, 20f), h = AR(8f, 22f);
                        Rect lot = s > 0
                            ? Rect.MinMaxRect(x, zs > 0 ? cz + CrossHalf : cz - CrossHalf - d, x + w, zs > 0 ? cz + CrossHalf + d : cz - CrossHalf)
                            : Rect.MinMaxRect(-x - w, zs > 0 ? cz + CrossHalf : cz - CrossHalf - d, -x, zs > 0 ? cz + CrossHalf + d : cz - CrossHalf);
                        AkSideBuilding(lot, h, zs > 0 ? -1f : 1f);
                        x += w + (ark.NextDouble() < 0.3 ? 3f : 0.5f);
                    }
                }
                // poles both sides of the side street with two power lines along it (grindable)
                foreach (var zs in new[] { -1f, 1f })
                {
                    var poles = new List<Vector3>();
                    for (float x = Front + 6f; x < 236f; x += 22f) poles.Add(new Vector3(s * x, 0f, cz + zs * (CrossHalf - 0.6f)));
                    foreach (var p in poles)
                    {
                        AK("metal").Cylinder(p, p + Vector3.up * 9f, 0.16f, 6, 3f, new Color32(150, 140, 130, 255));
                        AK("metal").Box(p + Vector3.up * 8.6f, new Vector3(0.14f, 0.14f, 1.8f), Quaternion.identity, 1f, 1f);
                        AK("metal").Cylinder(p + Vector3.up * 7f, p + Vector3.up * 7.8f, 0.35f, 8, 1f, new Color32(130, 130, 140, 255));
                        var cap = new GameObject("Pole").AddComponent<CapsuleCollider>();
                        cap.transform.SetParent(akCol, false); cap.transform.position = p + Vector3.up * 4.5f; cap.radius = 0.18f; cap.height = 9f;
                    }
                    foreach (var off in new[] { -0.6f, 0.6f })
                    {
                        var line = new List<Vector3>();
                        for (int i = 0; i < poles.Count - 1; i++)
                        {
                            var seg = Catenary(poles[i] + new Vector3(0f, 8.6f, off), poles[i + 1] + new Vector3(0f, 8.6f, off), 0.7f, 8);
                            for (int k = i == 0 ? 0 : 1; k < seg.Length; k++) line.Add(seg[k]);
                        }
                        Rail("Power line", line.ToArray(), false);
                        AK("white").Tube(line, 0.03f, 4, 4f, new Color32(30, 26, 40, 255));
                    }
                }
                // a construction barrier across the side street where it leaves the main street: slide under it
                float bx = s * (Front + 3f);
                AK("hazard", "").Box(new Vector3(bx, 1.25f, cz), new Vector3(0.3f, 0.5f, CrossHalf * 2f), Quaternion.identity, 1f, 0.5f);
                foreach (var zz in new[] { -CrossHalf + 0.3f, CrossHalf - 0.3f })
                    AK("metal").Box(new Vector3(bx, 0.75f, cz + zz), new Vector3(0.2f, 1.5f, 0.2f), Quaternion.identity, 1f, 1f);
                AkBox(new Vector3(bx, 1.25f, cz), new Vector3(0.3f, 0.5f, CrossHalf * 2f));
                AkBox(new Vector3(bx, 0.75f, cz - CrossHalf + 0.3f), new Vector3(0.2f, 1.5f, 0.2f));
                AkBox(new Vector3(bx, 0.75f, cz + CrossHalf - 0.3f), new Vector3(0.2f, 1.5f, 0.2f));
                // a kicker and a bank in each side street
                Hump("Kicker", new Vector3(s * 60f, 0f, cz), new Vector3(s, 0f, 0f), 4f, 26f, 4f, RampSide2);
                Hump("Kicker", new Vector3(s * 140f, 0f, cz + 3f), new Vector3(s, 0f, 0f), 5f, 30f, 4f, RampSide);
            }
    }

    static void AkSideBuilding(Rect lot, float h, float face)
    {
        // face: which way along z its front faces (towards the side street)
        AkBox(new Vector3(lot.center.x, h / 2f, lot.center.y), new Vector3(lot.width, h, lot.height));
        string fac = "facade_" + AI(0, 10);
        Vector3 n = new Vector3(0f, 0f, face);
        float fz = face < 0 ? lot.yMin : lot.yMax;
        Vector3 along = Rt(n);
        Vector3 fc = new Vector3(lot.center.x, 0f, fz);
        FrontWall(AK(ark.NextDouble() < 0.6 ? "shop_" + AI(0, 10) : "shutter", "e"), fc, n, lot.width, 0f, 3.4f, 4f, 3.4f);
        FrontWall(AK(fac), fc, n, lot.width, 3.4f, h, 4f, 3.2f);
        Wall(AK(fac), new Vector3(lot.xMin, 0f, lot.yMax), Vector3.back, lot.height, 0f, h, Vector3.left, 4f, 3.2f);
        Wall(AK(fac), new Vector3(lot.xMax, 0f, lot.yMin), Vector3.forward, lot.height, 0f, h, Vector3.right, 4f, 3.2f);
        FrontWall(AK(fac), new Vector3(lot.center.x, 0f, face < 0 ? lot.yMax : lot.yMin), -n, lot.width, 0f, h, 4f, 3.2f);
        AK("roof").Ground(lot.xMin, lot.yMin, lot.xMax, lot.yMax, h, 4f, 12f);
        if (ark.NextDouble() < 0.5)
            AK("sign_v_" + AI(0, 16), ark.NextDouble() < 0.4 ? "f" : "e").Panel(new Vector3(lot.xMin + 1f, Mathf.Min(h - 3f, 7f), fz + face * 0.7f), new Vector3(0f, 0f, face * 0.5f), Vector3.up * 2.5f, null, true);
        if (ark.NextDouble() < 0.35)
            AK("graffiti_" + AI(0, 6), "c").Panel(new Vector3(lot.center.x, 1.6f, fz + face * 0.03f), Rt(n) * 1.8f, Vector3.up * 0.9f);
        akAnchors.Add(new Vector3(lot.center.x, h, fz));
    }

    // ---- the route: kicker, curving rail, balcony, sign ramp, roof, flying car, skyscraper ----------

    static Vector3[] Spline(Vector3[] ctrl, int perSeg)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < ctrl.Length - 1; i++)
        {
            Vector3 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(ctrl.Length - 1, i + 2)];
            for (int k = 0; k < perSeg; k++)
            {
                float t = k / (float)perSeg, t2 = t * t, t3 = t2 * t;
                pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        pts.Add(ctrl[ctrl.Length - 1]);
        return pts.ToArray();
    }

    // a JSR-style rail: a bright pipe along a curve, on thin posts down to whatever's below
    static void CurvyRail(string name, Vector3[] ctrl, Color32 col, bool posts = true)
    {
        var pts = Spline(ctrl, 10);
        Rail(name, pts, false);
        AK("white", "e").Tube(pts, 0.07f, 6, 2f, col);
        if (!posts) return;
        float acc = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            if (acc < 7f) continue;
            acc = 0f;
            RaycastHit h;
            Physics.SyncTransforms();
            float floor = Physics.Raycast(pts[i] + Vector3.down * 0.3f, Vector3.down, out h, 80f) ? h.point.y : 0f;
            if (pts[i].y - floor > 0.6f) AK("metal").Cylinder(pts[i] + Vector3.down * 0.1f, new Vector3(pts[i].x, floor, pts[i].z), 0.05f, 4, 2f, new Color32(120, 120, 140, 255));
        }
    }

    // the flying car: a hover taxi on a loop through the air you can ride on
    static GameObject FlyingCar(string name, Vector3[] path, float speed, float startAt, Color32 body)
    {
        var go = new GameObject(name, typeof(Rigidbody), typeof(Mover), typeof(BoxCollider));
        go.transform.SetParent(akRoot, false);
        var k = new SkateMeshKit();
        k.Box(new Vector3(0f, 0.7f, 0f), new Vector3(2.4f, 0.8f, 4.6f), Quaternion.identity, body);
        k.Box(new Vector3(0f, 1.35f, -0.3f), new Vector3(2.2f, 0.5f, 2.6f), Quaternion.identity, new Color32(80, 200, 255, 255));
        k.Box(new Vector3(0f, 1.62f, -0.3f), new Vector3(2.2f, 0.06f, 2.6f), Quaternion.identity, body);
        foreach (var sx in new[] { -1.3f, 1.3f })
            foreach (var sz in new[] { -1.6f, 1.6f })
                k.Cylinder(new Vector3(sx, 0.3f, sz), new Vector3(sx, 0.36f, sz), 0.55f, 10, new Color32(255, 80, 200, 255));   // glowing thrusters
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { AkMat("white", "e"), outlineMat };
        var bc = go.GetComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.95f, 0f); bc.size = new Vector3(2.4f, 1.3f, 4.6f);
        go.GetComponent<Rigidbody>().isKinematic = true;   // (moved by its Mover; set now so the physics treats it that way from the start)
        var m = go.GetComponent<Mover>();
        m.path = path; m.loop = true; m.speed = speed; m.startAt = startAt;
        return go;
    }

    static void AkRoute()
    {
        // 1. the kicker on the east pavement
        Hump("Route kicker", new Vector3(15.2f, Kerb, -424f), Vector3.forward, 4f, 26f, 3.6f, RampSide2);
        // 2. the curving rail up to the balcony of the porch building
        float porchY = 11f, pz0 = EaLot.yMin + 4f, pz1 = pz0 + 12f;
        CurvyRail("Route rail", new[] { new Vector3(15.2f, 2.2f, -414f), new Vector3(15f, 4f, -404f), new Vector3(16.5f, 6.5f, -394f),
                                         new Vector3(17.5f, 9f, -386f), new Vector3(18.4f, porchY + 0.6f, pz0 - 2f), new Vector3(18.6f, porchY + 0.6f, pz0 + 2f) },
                  new Color32(255, 214, 40, 255));
        // 3. the balcony
        Vector3 pc = new Vector3(Front - 1.5f, porchY - 0.15f, (pz0 + pz1) / 2f);
        AK("concrete").Box(pc, new Vector3(3f, 0.3f, pz1 - pz0), Quaternion.identity, 2f, 2f, null, true, true);
        AkBox(pc, new Vector3(3f, 0.3f, pz1 - pz0));
        Rail("Balcony rail", new[] { new Vector3(Front - 2.9f, porchY + 1f, pz0 + 3f), new Vector3(Front - 2.9f, porchY + 1f, pz1 - 0.3f) });
        TowerCheckpoint(akRoot, new Vector3(Front - 0.6f, porchY, pz0 + 6f), "Balcony");
        // 4. the sign ramp from the balcony up along the front to the roof
        float rz0 = pz1, rz1 = EaLot.yMax - 3f, rise = EaH - porchY;
        float len = Mathf.Sqrt((rz1 - rz0) * (rz1 - rz0) + rise * rise), ang = Mathf.Atan2(rise, rz1 - rz0) * Mathf.Rad2Deg;
        Vector3 rc = new Vector3(Front - 1.5f, (porchY + EaH) / 2f - 0.15f, (rz0 + rz1) / 2f);
        Quaternion rq = Quaternion.Euler(-ang, 0f, 0f);
        AK("billboard_" + AI(0, 10), "e").Box(rc, new Vector3(3f, 0.3f, len), rq, 3f, len, null, true, true);
        AkBox(rc, new Vector3(3f, 0.3f, len), rq);
        AK("concrete").Box(new Vector3(Front - 1.5f, EaH - 0.15f, (rz1 + EaLot.yMax) / 2f + 0.5f), new Vector3(3f, 0.3f, EaLot.yMax - rz1 + 1f), Quaternion.identity, 2f, 2f);
        AkBox(new Vector3(Front - 1.5f, EaH - 0.15f, (rz1 + EaLot.yMax) / 2f + 0.5f), new Vector3(3f, 0.3f, EaLot.yMax - rz1 + 1f));
        TowerCheckpoint(akRoot, new Vector3(EaLot.xMin + 6f, EaH, EaLot.yMax - 8f), "Rooftop");
        // 5. the flying car that pulls up along the roof's street edge, crosses the street and climbs the skyscraper's face
        float carY = EaH - 1.85f;
        var route = new[] {
            new Vector3(16.5f, carY, -392f), new Vector3(16.5f, carY, -336f), new Vector3(4f, carY + 4f, -318f),
            new Vector3(W1Lot.xMax + 2.6f, 28f, -300f), new Vector3(W1Lot.xMax + 2.6f, 40f, -214f), new Vector3(-4f, 42f, -196f),
            new Vector3(9f, 34f, -232f), new Vector3(14f, 26f, -300f), new Vector3(16.5f, carY, -360f) };
        FlyingCar("Flying car", route, 11f, 0f, new Color32(255, 230, 60, 255));
        // 6. the pipe up the skyscraper's corner into the power lines over the city (part of the top lines)
    }

    // ---- curvy rails everywhere ----------------------------------------------------------------------

    static void AkCurvyRails()
    {
        Color32[] cols = { new Color32(255, 214, 40, 255), new Color32(255, 70, 160, 255), new Color32(60, 230, 255, 255), new Color32(140, 255, 120, 255), new Color32(255, 120, 40, 255) };
        // from pavement kickers up to balconies: a kicker, then a rail rising and curving to the balcony
        int made = 0;
        foreach (var a in akAnchors.ToArray())
        {
            if (made > 40) break;
            if (a.y < 6f || a.y > 15f || Mathf.Abs(Mathf.Abs(a.x) - (Front - 1.4f)) > 0.2f) continue;   // only balconies on the main street
            if (ark.NextDouble() < 0.35) continue;
            float s = Mathf.Sign(a.x), dir = ark.NextDouble() < 0.5 ? 1f : -1f;
            float kz = a.z - dir * AR(34f, 44f);
            if (Mathf.Abs(kz) > AveLen - 10f || Mathf.Abs(kz) < RailHalf + 6f) continue;
            bool clear = true;
            foreach (var cz in CrossZ) if (Mathf.Abs(kz - cz) < CrossHalf + 6f || Mathf.Abs(a.z - cz) < CrossHalf + 4f) clear = false;
            if (!clear) continue;
            float px = s * (Road + 3.2f);
            akNoStall.Add(new Vector3(s, kz - 6f, kz + 6f));
            Hump("Kicker", new Vector3(px, Kerb, kz), new Vector3(0f, 0f, dir), 4f, 26f, 3.6f, RampSide2);
            var c = cols[made % cols.Length];
            CurvyRail("Curvy rail", new[] { new Vector3(px, 2.2f, kz + dir * 10f), new Vector3(px + s * AR(-1f, 1f), AR(3.5f, 5f), kz + dir * 18f),
                                             new Vector3(s * (Front - 3.5f), a.y * 0.7f, a.z - dir * 9f), new Vector3(s * (Front - 2.6f), a.y + 0.6f, a.z - dir * 2f),
                                             new Vector3(s * (Front - 2.6f), a.y + 0.6f, a.z) }, c);
            made++;
        }
        // across the street between balconies and roofs: big S-curves over the traffic
        var tops = akAnchors.FindAll(p => p.y > 8f && p.y < 40f && Mathf.Abs(Mathf.Abs(p.x) - Front) < 3f);
        for (int i = 0; i < 18 && tops.Count > 1; i++)
        {
            var a = tops[AI(0, tops.Count)];
            var b = tops.Find(p => Mathf.Sign(p.x) != Mathf.Sign(a.x) && Mathf.Abs(p.z - a.z) > 20f && Mathf.Abs(p.z - a.z) < 70f && Mathf.Abs(p.y - a.y) < 14f);
            if (b == Vector3.zero) continue;
            float mid = (a.y + b.y) / 2f + AR(-3f, 4f);
            Vector3 aa = a + new Vector3(-Mathf.Sign(a.x) * 2.6f, 0.6f, 0f), bb = b + new Vector3(-Mathf.Sign(b.x) * 2.6f, 0.6f, 0f);
            CurvyRail("Street rail", new[] { aa, Vector3.Lerp(aa, bb, 0.3f) + new Vector3(0f, AR(1f, 5f), AR(-8f, 8f)), new Vector3(0f, mid, (a.z + b.z) / 2f),
                                              Vector3.Lerp(aa, bb, 0.7f) + new Vector3(0f, AR(1f, 5f), AR(-8f, 8f)), bb }, cols[i % cols.Length], false);
        }
        // spirals round the railway pillars by the street, from the pavement up to the tracks
        foreach (var sx in new[] { -1f, 1f })
        {
            var ctrl = new List<Vector3>();
            Vector3 c0 = new Vector3(sx * (Road + 0.8f), 0f, 0f);
            for (int k = 0; k <= 10; k++)
            {
                float a = k * 50f * Mathf.Deg2Rad;
                ctrl.Add(c0 + new Vector3(Mathf.Cos(a) * 4.2f, 1.6f + k * 1.05f, Mathf.Sin(a) * 4.2f + (k < 2 ? -6f + k * 3f : 0f)));
            }
            CurvyRail("Spiral rail", ctrl.ToArray(), new Color32(255, 70, 160, 255), false);
        }
    }

    // ---- the power lines over the top of the map, between pylons on the skyscrapers ---------------

    static Vector3 PylonTop(Rect lot, float h) { return new Vector3(lot.center.x, h + 9f, lot.center.y); }

    static void Pylon(Rect lot, float h)
    {
        Vector3 b = new Vector3(lot.center.x, h, lot.center.y);
        var k = AK("metal");
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 foot = b + new Vector3(Mathf.Cos(a) * 3f, 0f, Mathf.Sin(a) * 3f), top = b + new Vector3(Mathf.Cos(a) * 0.6f, 9f, Mathf.Sin(a) * 0.6f);
            k.Cylinder(foot, top, 0.15f, 4, 3f, new Color32(190, 190, 205, 255));
        }
        for (float y = 2f; y < 9f; y += 2.3f) k.Box(b + Vector3.up * y, new Vector3(5f - y * 0.45f, 0.15f, 5f - y * 0.45f), Quaternion.identity, 1f, 1f);
        // a red light on top, blinking
        var bl = new GameObject("Pylon light", typeof(Blinker));
        bl.transform.SetParent(akRoot, false);
        bl.GetComponent<Blinker>().durations = new[] { 0.6f, 0.9f };
        foreach (var col in new[] { new Color32(255, 40, 40, 255), new Color32(60, 10, 10, 255) })
        {
            var lk = new SkateMeshKit();
            lk.Ellipsoid(b + Vector3.up * 9.6f, Vector3.one * 0.4f, Quaternion.identity, 8, 5, col);
            var mesh = lk.Build("Light");
            SaveFeatureMesh(mesh);
            var go = new GameObject("Lamp", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(bl.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AkMat("white", "e");
        }
    }

    static void AkTopLines()
    {
        var lots = new[] { (E2Lot, E2H), (W3Lot, W3H), (E4Lot, E4H), (W5Lot, W5H) };
        foreach (var l in lots) Pylon(l.Item1, l.Item2);
        // one long line: up the corner of the wall-run skyscraper (a pipe on its face), up to the first
        // pylon, then pylon to pylon right across the city, and down to a roof at the far end
        var line = new List<Vector3>();
        float wx = W1Lot.xMax + 0.45f;
        line.AddRange(new[] { new Vector3(wx, 37f, -262f), new Vector3(wx, 40f, -230f), new Vector3(wx, 41.5f, -204f), new Vector3(wx + 2f, 45f, -190f) });
        Vector3 prev = line[line.Count - 1];
        foreach (var l in lots)
        {
            Vector3 top = PylonTop(l.Item1, l.Item2);
            var seg = Catenary(prev, top, Vector3.Distance(prev, top) * 0.03f, 16);
            for (int i = 1; i < seg.Length; i++) line.Add(seg[i]);
            prev = top;
        }
        var down = Catenary(prev, new Vector3(-30f, 31f, 440f), 2f, 12);
        for (int i = 1; i < down.Length; i++) line.Add(down[i]);
        Rail("Top line", line.ToArray(), false);
        AK("white", "e").Tube(line, 0.08f, 6, 4f, new Color32(255, 214, 40, 255));
        // a landing roof at the far end
        Rect land = Rect.MinMaxRect(-50f, 420f, -16f, 470f);
        AkTower(land, 30f, 1f, false);
        // and lines down from each pylon to the roofs across the street, for coming back down
        foreach (var l in lots)
        {
            Vector3 top = PylonTop(l.Item1, l.Item2);
            Vector3 to = new Vector3(-Mathf.Sign(top.x) * (Front + 8f), Mathf.Max(18f, top.y - 40f), top.z + 30f);
            var d2 = Catenary(top + Vector3.down * 1.5f, to, 1.5f, 14);
            Rail("Down line", d2, false);
            AK("white", "e").Tube(d2, 0.06f, 5, 4f, new Color32(60, 230, 255, 255));
        }
        TowerCheckpoint(akRoot, new Vector3(-33f, 30f, 450f), "End of the line");
    }

    // ---- traffic both ways down the main street, and more flying cars --------------------------------

    static void AkTraffic()
    {
        Color32[] cols = { new Color32(255, 255, 255, 255), new Color32(40, 40, 50, 255), new Color32(255, 214, 40, 255), new Color32(220, 40, 60, 255),
                           new Color32(60, 120, 220, 255), new Color32(200, 200, 210, 255), new Color32(120, 220, 160, 255) };
        foreach (var lane in new[] { 3f, 9f })
        {
            var path = new[] { new Vector3(-lane, 0f, -AveLen + 14f), new Vector3(-lane, 0f, AveLen - 14f), new Vector3(lane, 0f, AveLen - 14f), new Vector3(lane, 0f, -AveLen + 14f) };
            float loop = (AveLen - 14f) * 4f + lane * 4f;
            int n = lane < 5f ? 18 : 14;
            for (int i = 0; i < n; i++)
            {
                bool big = lane > 5f && i % 4 == 0;
                var v = Vehicle(akRoot, big ? "Bus" : "Car", big ? 11f : AR(3.8f, 4.6f), big ? 2.5f : 1.8f, big ? 2.7f : 1.5f, big ? new Color32(250, 250, 255, 255) : cols[i % cols.Length], new Color32(60, 180, 80, 255), big);
                v.GetComponent<MeshRenderer>().sharedMaterials = new[] { AkMat("white", ""), outlineMat };
                var m = v.GetComponent<Mover>();
                m.path = path; m.loop = true; m.speed = lane < 5f ? 15f : 11f; m.startAt = loop * i / n;
            }
        }
        // more flying cars, looping over the street at different heights
        FlyingCar("Flying car", new[] { new Vector3(-6f, 18f, -150f), new Vector3(6f, 22f, -40f), new Vector3(-6f, 26f, 90f), new Vector3(6f, 20f, 220f), new Vector3(0f, 16f, 90f), new Vector3(0f, 14f, -60f) }, 14f, 0f, new Color32(255, 80, 200, 255));
        FlyingCar("Flying car", new[] { new Vector3(10f, 30f, 120f), new Vector3(-10f, 34f, 260f), new Vector3(10f, 38f, 400f), new Vector3(-6f, 30f, 300f), new Vector3(6f, 26f, 180f) }, 13f, 40f, new Color32(60, 230, 255, 255));
        FlyingCar("Flying car", new[] { new Vector3(-4f, 12f, -440f), new Vector3(4f, 16f, -330f), new Vector3(-4f, 12f, -220f), new Vector3(4f, 10f, -330f) }, 12f, 0f, new Color32(140, 255, 120, 255));
    }

    // ---- weird stuff: lucky cats, a giant capsule, hologram signs, a blimp ----------------------------

    static void AkWeird()
    {
        // giant lucky cats on some roofs
        foreach (var at in new[] { new Vector3(34f, 0f, -250f), new Vector3(-36f, 0f, 200f), new Vector3(38f, 0f, 380f) })
        {
            RaycastHit h;
            Physics.SyncTransforms();
            float y = Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out h, 300f) ? h.point.y : 0f;
            var k = new SkateMeshKit();
            Vector3 c = new Vector3(at.x, y, at.z);
            Color32 white = new Color32(250, 250, 250, 255), red = new Color32(230, 40, 50, 255), gold = new Color32(255, 210, 60, 255);
            k.Ellipsoid(c + Vector3.up * 2.4f, new Vector3(2.2f, 2.4f, 2f), Quaternion.identity, 14, 10, white);
            k.Ellipsoid(c + Vector3.up * 5.4f, new Vector3(1.8f, 1.6f, 1.6f), Quaternion.identity, 14, 10, white);
            foreach (var sx in new[] { -1f, 1f }) k.Cone(c + new Vector3(sx * 1.1f, 6.6f, 0f), 0.6f, 0f, 1.2f, 6, white, 0f);
            k.Ellipsoid(c + new Vector3(1.6f, 6.4f, -0.4f), new Vector3(0.5f, 1.2f, 0.5f), Quaternion.Euler(0f, 0f, -15f), 8, 6, white);   // the waving paw
            k.Box(c + new Vector3(0f, 4.2f, -1.7f), new Vector3(2.4f, 0.4f, 0.3f), Quaternion.identity, red);
            k.Ellipsoid(c + new Vector3(0f, 3.6f, -1.9f), new Vector3(0.5f, 0.5f, 0.2f), Quaternion.identity, 8, 6, gold);
            foreach (var sx in new[] { -0.6f, 0.6f }) k.Ellipsoid(c + new Vector3(sx, 5.6f, -1.5f), new Vector3(0.25f, 0.12f, 0.1f), Quaternion.identity, 6, 4, new Color32(30, 30, 30, 255));
            var mesh = k.Build("Lucky cat");
            SaveFeatureMesh(mesh);
            var go = MeshObject("Lucky cat", akRoot, mesh, AkMat("white", ""), outlineMat);
            var col = new GameObject("Cat").AddComponent<CapsuleCollider>();
            col.transform.SetParent(akCol, false); col.transform.position = c + Vector3.up * 3.5f; col.radius = 2.1f; col.height = 7f;
        }
        // floating hologram signs spinning over the street
        for (int i = 0; i < 10; i++)
        {
            var go = new GameObject("Hologram", typeof(MeshFilter), typeof(MeshRenderer), typeof(Spin));
            go.transform.SetParent(akRoot, false);
            var k = new PSXKit();
            k.Panel(Vector3.zero, Vector3.right * 3f, Vector3.up * 3f, null, true);
            var mesh = k.Build("Hologram");
            SaveFeatureMesh(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AkMat("billboard_" + AI(0, 10), "f");
            go.transform.position = new Vector3(AR(-6f, 6f), AR(14f, 30f), -AveLen + 60f + i * 90f);
            var sp = go.GetComponent<Spin>(); sp.degreesPerSecond = AR(20f, 50f); sp.bob = 1f;
        }
        // a blimp with an LED sign going round the whole map
        var blimp = new GameObject("Blimp", typeof(Rigidbody), typeof(Mover));
        blimp.transform.SetParent(akRoot, false);
        var bk = new SkateMeshKit();
        bk.Ellipsoid(Vector3.zero, new Vector3(7f, 7f, 22f), Quaternion.identity, 16, 10, new Color32(240, 240, 250, 255));
        bk.Box(new Vector3(0f, -7.5f, 0f), new Vector3(2.5f, 2f, 6f), Quaternion.identity, new Color32(80, 80, 100, 255));
        foreach (var f in new[] { Quaternion.identity, Quaternion.Euler(0f, 0f, 90f) })
            bk.Box(new Vector3(0f, 0f, -19f), f * new Vector3(0.3f, 9f, 5f), f, new Color32(255, 70, 160, 255));
        var bm = bk.Build("Blimp");
        SaveFeatureMesh(bm);
        blimp.AddComponent<MeshFilter>().sharedMesh = bm;
        blimp.AddComponent<MeshRenderer>().sharedMaterials = new[] { AkMat("white", ""), outlineMat };
        var ledk = new PSXKit();
        foreach (var s in new[] { -1f, 1f }) ledk.Panel(new Vector3(s * 7.05f, 0f, 0f), Rt(new Vector3(s, 0f, 0f)) * 14f, Vector3.up * 1.5f, null, false, new Rect(0f, 0f, 4.4f, 1f));
        var lm = ledk.Build("Blimp sign");
        SaveFeatureMesh(lm);
        var sign = new GameObject("Blimp sign", typeof(MeshFilter), typeof(MeshRenderer));
        sign.transform.SetParent(blimp.transform, false);
        sign.GetComponent<MeshFilter>().sharedMesh = lm;
        sign.GetComponent<MeshRenderer>().sharedMaterial = AkMat("led_0", "s");
        blimp.GetComponent<Rigidbody>().isKinematic = true;
        var bmv = blimp.GetComponent<Mover>();
        var bp = new List<Vector3>();
        for (int i = 0; i < 16; i++) { float a = i * Mathf.PI * 2f / 16; bp.Add(new Vector3(Mathf.Cos(a) * 160f, 120f, Mathf.Sin(a) * 400f)); }
        bmv.path = bp.ToArray(); bmv.loop = true; bmv.speed = 9f;
    }

    // ---- review shots ------------------------------------------------------------------------------------

    public static void AkibaShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        var psx = cam.GetComponent<PSXCamera>();
        if (psx != null) psx.Apply();
        foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) { m.Init(); for (int i = 0; i < 30; i++) m.Step(0.05f); }
        Physics.SyncTransforms();
        Shot(cam, new Vector3(16f, 1.6f, -462f), new Vector3(0f, 8f, -300f), "a01-start.png");
        Shot(cam, new Vector3(0f, 6f, -230f), new Vector3(0f, 10f, -120f), "a02-street.png");
        Shot(cam, new Vector3(8f, 3f, -432f), new Vector3(18f, 9f, -380f), "a03-route-rail.png");
        Shot(cam, new Vector3(12f, 24f, -360f), new Vector3(-20f, 35f, -260f), "a04-flying-car-and-skyscraper.png");
        Shot(cam, new Vector3(-60f, 60f, -180f), new Vector3(30f, 70f, -100f), "a05-top-lines.png");
        Shot(cam, new Vector3(-30f, 9f, 30f), new Vector3(-90f, 14f, 0f), "a06-station.png");
        Shot(cam, new Vector3(-150f, 140f, -560f), new Vector3(0f, 0f, -100f), "a07-overview.png");
        Shot(cam, new Vector3(30f, 4f, -170f), new Vector3(120f, 6f, -160f), "a08-side-street.png");
    }
}
