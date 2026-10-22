using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// "Me": after the meme where SoundCloud kisses you on one cheek and Discord on the other. A pastel pink arena
// floating in a sparkly pink sky, with the captions as huge chunky white letters with black outlines hanging
// over it. SoundCloud's half (left as you start) is orange and cream: a waveform you skate along like a pump
// track, with a playhead running down it that pushes you along if you keep up with it; play-button kickers; a
// repost loop rail; the cloud and its bars as the end wall. Discord's half (right) is dark grey and blurple: a
// tower of server icons spiralling up to a ping, with a long channel rail back down; message bubbles stepping up;
// voice-channel rings that bounce you; a # made of rails. In the middle: you, "ME" written on the floor in
// ledges, a spine with a heart on it, and blush pads that shoot you off to either side. Both of them want your
// attention (MeLevel): ride through a side's hearts to fill its meter, which slowly drains.
public static partial class SkateWorldBuilder
{
    public const string MePath = "Assets/Scenes/Me.unity";

    const float MeHX = 140f, MeHZ = 100f;
    public static readonly Vector3 MeTrackStart = new Vector3(-72f, 0f, -70f);
    public const float MeTrackLength = 140f, MeTrackWidth = 10f;
    public static readonly Vector3 MeTowerCentre = new Vector3(95f, 0f, 45f);
    public const int MeTowerSteps = 16;
    public const float MeTowerRise = 1.05f, MeTowerRadius = 5.5f, MeTowerTurn = 38f, MeTowerPad = 3.4f;
    public static readonly Vector3 MeHash = new Vector3(60f, 0f, -35f);

    static readonly Color32 MePink = new Color32(255, 196, 220, 255), MePinkDeep = new Color32(246, 128, 176, 255), MePinkPale = new Color32(255, 228, 238, 255);
    static readonly Color32 MeOrange = new Color32(255, 85, 0, 255), MeOrangeSoft = new Color32(255, 160, 96, 255), MeCream = new Color32(255, 236, 218, 255);
    static readonly Color32 MeBlurple = new Color32(88, 101, 242, 255), MeDark = new Color32(49, 51, 56, 255), MeDarker = new Color32(30, 31, 34, 255), MeGrey = new Color32(70, 72, 80, 255);
    static readonly Color32 MeGreen = new Color32(35, 165, 90, 255), MeRed = new Color32(237, 66, 69, 255), MeWhite = new Color32(255, 255, 255, 255);

    static Transform meRoot;
    static readonly List<Transform> meHearts = new List<Transform>();
    static readonly List<int> meHeartSide = new List<int>();

    public static void MeFromCommandLine()
    {
        try { BuildMe(); SlideTests.RunMe(); MeShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    // the waveform track's height, d metres along it
    public static float MeWave(float d)
    {
        float ends = Mathf.SmoothStep(0f, 1f, d / 8f) * Mathf.SmoothStep(0f, 1f, (MeTrackLength - d) / 8f);
        return ends * (1.3f + 0.75f * Mathf.Sin(d * 0.33f) + 0.45f * Mathf.Sin(d * 0.81f + 1f));
    }

    // the top of server-icon step n (the last one's the ping)
    public static Vector3 MeTowerStep(int n)
    {
        float a = n * MeTowerTurn * Mathf.Deg2Rad;
        return MeTowerCentre + new Vector3(Mathf.Cos(a) * MeTowerRadius, 0.8f + n * MeTowerRise, Mathf.Sin(a) * MeTowerRadius);
    }

    public static void BuildMe()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/MeFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); meHearts.Clear(); meHeartSide.Clear();
        floorAt = (x, z) => 0f;
        RampTop = MePinkPale; RampSide = MePink; RampSide2 = MePinkDeep;
        Metal = MeWhite; RailCol = MePinkDeep; Concrete = MePinkPale; ConcreteDark = MePink;

        Color haze = new Color(1f, 0.86f, 0.93f), hazeHigh = new Color(0.98f, 0.9f, 1f);
        toonMat = Mat("MeToon", "3daigamesolo/World/Toon");
        toonMat.SetColor("_ShadeTint", new Color(0.86f, 0.7f, 0.92f));
        toonMat.SetColor("_Haze", haze); toonMat.SetColor("_HazeHigh", hazeHigh);
        toonMat.SetFloat("_FogDensity", 0.0016f);
        var floorMat = Mat("MeFloor", "3daigamesolo/World/Toon");
        floorMat.SetColor("_ShadeTint", new Color(0.86f, 0.7f, 0.92f));
        floorMat.SetColor("_DotColor", new Color(1f, 1f, 1f, 0.45f)); floorMat.SetFloat("_DotScale", 0.8f);
        floorMat.SetColor("_Haze", haze); floorMat.SetColor("_HazeHigh", hazeHigh); floorMat.SetFloat("_FogDensity", 0.0016f);
        var shiny = Mat("MeShiny", "3daigamesolo/World/Toon");
        shiny.SetColor("_ShadeTint", new Color(0.86f, 0.7f, 0.92f)); shiny.SetFloat("_Glitter", 0.45f);
        shiny.SetColor("_Haze", haze); shiny.SetColor("_HazeHigh", hazeHigh); shiny.SetFloat("_FogDensity", 0.0016f);
        outlineMat = Mat("MeOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.12f, 0.04f, 0.1f));
        outlineMat.SetFloat("_OutlineWidth", 1.1f);
        outlineMat.SetColor("_Haze", haze); outlineMat.SetColor("_HazeHigh", hazeHigh); outlineMat.SetFloat("_FogDensity", 0.0016f);
        farOutlineMat = outlineMat;
        var bold = Mat("MeOutlineBold", "3daigamesolo/World/Outline");   // the captions: thick black outlines
        bold.SetColor("_OutlineColor", Color.black); bold.SetFloat("_OutlineWidth", 3.2f);
        bold.SetColor("_Haze", haze); bold.SetColor("_HazeHigh", hazeHigh); bold.SetFloat("_FogDensity", 0f);
        var sky = Mat("MeSky", "3daigamesolo/World/Sky");
        sky.SetColor("_Zenith", new Color(0.86f, 0.62f, 0.92f)); sky.SetColor("_Horizon", new Color(1f, 0.84f, 0.92f));
        sky.SetColor("_Below", new Color(1f, 0.8f, 0.9f)); sky.SetColor("_Cloud", Color.white);
        sky.SetColor("_CloudShade", new Color(1f, 0.86f, 0.94f)); sky.SetFloat("_CloudCover", 0.35f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;
        meRoot = new GameObject("Me").transform;

        MeFloor(floorMat);
        MeCaptions(bold);
        MeMiddle(shiny);
        MeSoundCloud(shiny);
        MeDiscord(shiny);
        MeHeartsAll(shiny);

        var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyGo.name = "Sky";
        Object.DestroyImmediate(skyGo.GetComponent<Collider>());
        skyGo.transform.localScale = Vector3.one * 8000f;
        skyGo.GetComponent<Renderer>().sharedMaterial = sky;
        MeSparkles();

        // you, at the front of the middle, SoundCloud on your left and Discord on your right
        Vector3 startAt = new Vector3(0f, 0.2f, -62f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt, Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(MeLevel));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 9000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(1f, 0.84f, 0.92f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "Me";
        camGO.transform.SetPositionAndRotation(startAt + new Vector3(0f, 2.5f, -4f), Quaternion.LookRotation(Vector3.forward));
        player.board = BuildBoard(startAt + Vector3.forward * 2f, Quaternion.LookRotation(Vector3.forward));

        var me = camGO.GetComponent<MeLevel>();
        me.player = player;
        me.hearts = meHearts.ToArray();
        me.heartSide = meHeartSide.ToArray();
        me.trackStart = MeTrackStart + Vector3.up * MeWave(0f);
        me.trackLength = MeTrackLength; me.trackWidth = MeTrackWidth;
        me.playhead = MePlayhead();

        EditorSceneManager.SaveScene(scene, MePath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_ME_BUILT features=" + featureCount + " hearts=" + meHearts.Count);
    }

    // ---- the floor and the walls round it ---------------------------------------------------------------

    static void MeFloor(Material floorMat)
    {
        var k = new SkateMeshKit();
        // SoundCloud's half cream and orange, the middle pink, Discord's half dark
        k.Box(new Vector3(-(MeHX + 40f) * 0.5f, -0.5f, 0f), new Vector3(MeHX - 20f, 1f, MeHZ * 2f), Quaternion.identity, MeCream);
        k.Box(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, MeHZ * 2f), Quaternion.identity, MePink);
        k.Box(new Vector3((MeHX + 40f) * 0.5f, -0.5f, 0f), new Vector3(MeHX - 20f, 1f, MeHZ * 2f), Quaternion.identity, MeDark);
        // stripes where they meet
        k.Box(new Vector3(-30f, 0.01f, 0f), new Vector3(1.2f, 0.02f, MeHZ * 2f), Quaternion.identity, MeOrange);
        k.Box(new Vector3(30f, 0.01f, 0f), new Vector3(1.2f, 0.02f, MeHZ * 2f), Quaternion.identity, MeBlurple);
        Solid("Floor", meRoot, k, floorMat, false);
        AddBox(meRoot, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(MeHX * 2f + 20f, 1f, MeHZ * 2f + 20f));
        // the walls: quarterpipes all round (SoundCloud's end is orange, Discord's blurple)
        RampTop = MeCream; RampSide = MeOrangeSoft;
        QuarterPipe("SoundCloud wall", Matrix4x4.TRS(new Vector3(-MeHX + 5f, 0f, 0f), Quaternion.LookRotation(Vector3.left), Vector3.one), MeHZ * 2f, 4.5f, 3f);
        RampTop = MeGrey; RampSide = MeBlurple;
        QuarterPipe("Discord wall", Matrix4x4.TRS(new Vector3(MeHX - 5f, 0f, 0f), Quaternion.LookRotation(Vector3.right), Vector3.one), MeHZ * 2f, 4.5f, 3f);
        RampTop = MePinkPale; RampSide = MePink;
        QuarterPipe("Back wall", Matrix4x4.TRS(new Vector3(0f, 0f, MeHZ - 5f), Quaternion.LookRotation(Vector3.forward), Vector3.one), MeHX * 2f - 4f, 3.5f, 3f);
        QuarterPipe("Front wall", Matrix4x4.TRS(new Vector3(0f, 0f, -MeHZ + 5f), Quaternion.LookRotation(Vector3.back), Vector3.one), MeHX * 2f - 4f, 3.5f, 3f);
        foreach (var s in new[] { -1f, 1f })
        {
            AddBox(meRoot, "Edge", new Vector3(s * (MeHX + 4f), 10f, 0f), new Vector3(2f, 30f, MeHZ * 2f + 20f));
            AddBox(meRoot, "Edge", new Vector3(0f, 10f, s * (MeHZ + 4f)), new Vector3(MeHX * 2f + 20f, 30f, 2f));
        }
    }

    // ---- the captions: chunky pixel letters --------------------------------------------------------------

    static readonly Dictionary<char, string[]> MeFont = new Dictionary<char, string[]>
    {
        { 'S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." } },
        { 'O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'N', new[] { "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" } },
        { 'D', new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." } },
        { 'C', new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
        { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
        { 'M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" } },
        { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
        { 'W', new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" } },
        { 'A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'G', new[] { ".####", "#....", "#....", "#..##", "#...#", "#...#", ".###." } },
        { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." } },
    };

    // pixel text: rows of runs as boxes, from `origin` (its bottom left) along `right`, rows going `up`, `depth`
    // thick back from the face (right and up along the world's axes)
    static List<(Vector3 centre, Vector3 size, int runLength)> MeText(SkateMeshKit k, string text, Vector3 origin, Vector3 right, Vector3 up, float px, float depth, Color32 col)
    {
        var boxes = new List<(Vector3, Vector3, int)>();
        Vector3 fwd = Vector3.Cross(right, up);
        for (int c = 0; c < text.Length; c++)
        {
            if (!MeFont.TryGetValue(text[c], out var rows)) continue;
            for (int r = 0; r < 7; r++)
            {
                string row = rows[6 - r];
                int x = 0;
                while (x < 5)
                {
                    if (row[x] != '#') { x++; continue; }
                    int x1 = x;
                    while (x1 < 5 && row[x1] == '#') x1++;
                    float left = (c * 6 + x) * px, width = (x1 - x) * px;
                    Vector3 centre = origin + right * (left + width * 0.5f) + up * ((r + 0.5f) * px) - fwd * (depth * 0.5f);
                    Vector3 a = right * width, b = up * px, f = fwd * depth;
                    Vector3 size = new Vector3(Mathf.Abs(a.x) + Mathf.Abs(b.x) + Mathf.Abs(f.x), Mathf.Abs(a.y) + Mathf.Abs(b.y) + Mathf.Abs(f.y), Mathf.Abs(a.z) + Mathf.Abs(b.z) + Mathf.Abs(f.z));
                    k.Box(centre, size, Quaternion.identity, col);
                    boxes.Add((centre, size, x1 - x));
                    x = x1;
                }
            }
        }
        return boxes;
    }

    static void MeCaptions(Material bold)
    {
        var k = new SkateMeshKit();
        // SOUNDCLOUD over the left, DISCORD over the right, ME high in the middle, hanging in the sky facing you
        float px = 1.3f;
        MeText(k, "SOUNDCLOUD", new Vector3(-48f - 59f * px * 0.5f, 22f, 84f), Vector3.right, Vector3.up, px, -1.6f, MeWhite);
        MeText(k, "DISCORD", new Vector3(55f - 41f * px * 0.5f, 22f, 84f), Vector3.right, Vector3.up, px, -1.6f, MeWhite);
        MeText(k, "ME", new Vector3(-11f, 34f, 92f), Vector3.right, Vector3.up, 2f, -2f, MeWhite);
        var mesh = k.Build("Captions");
        SaveFeatureMesh(mesh);
        var white = Mat("MeCaption", "3daigamesolo/World/Toon");   // crisp white, no haze
        white.SetColor("_ShadeTint", new Color(0.92f, 0.9f, 0.95f)); white.SetFloat("_FogDensity", 0f);
        MeshObject("Captions", meRoot, mesh, white, bold);
    }

    // ---- the middle: ME on the floor, a spine with a heart on it, blush pads --------------------------------

    static void MeMiddle(Material shiny)
    {
        // ME written on the floor in pink ledges, half a metre high, rails along the long tops
        var k = new SkateMeshKit();
        float px = 2.4f;
        var boxes = MeText(k, "ME", new Vector3(-11f * px * 0.5f, 0f, -40f), Vector3.right, Vector3.forward, px, 0.5f, MePinkDeep);
        foreach (var b in boxes)
        {
            AddBox(meRoot, "ME ledge", b.centre, b.size);
            if (b.runLength >= 3)
                foreach (var s in new[] { -1f, 1f })
                    Rail("ME edge", new[] { new Vector3(b.centre.x - b.size.x * 0.5f + 0.2f, 0.53f, b.centre.z + s * (px * 0.5f - 0.08f)), new Vector3(b.centre.x + b.size.x * 0.5f - 0.2f, 0.53f, b.centre.z + s * (px * 0.5f - 0.08f)) }, false);
        }
        Solid("ME ledges", meRoot, k, shiny, false);

        // a spine across the middle (two quarterpipes back to back), a big heart standing on it
        RampTop = MePinkPale; RampSide = MePink;
        QuarterPipe("Spine south", Matrix4x4.TRS(new Vector3(0f, 0f, 12f), Quaternion.LookRotation(Vector3.forward), Vector3.one), 16f, 3f, 0.3f);
        QuarterPipe("Spine north", Matrix4x4.TRS(new Vector3(0f, 0f, 12f + 6.6f), Quaternion.LookRotation(Vector3.back), Vector3.one), 16f, 3f, 0.3f);
        var h = new SkateMeshKit();
        Heart(h, new Vector3(0f, 9f, 15.3f), 7f, Quaternion.identity, MePinkDeep);
        h.Cylinder(new Vector3(-3f, 3.3f, 15.3f), new Vector3(-2f, 6.5f, 15.3f), 0.2f, 8, MeWhite);
        h.Cylinder(new Vector3(3f, 3.3f, 15.3f), new Vector3(2f, 6.5f, 15.3f), 0.2f, 8, MeWhite);
        Solid("Heart", meRoot, h, shiny, false).AddComponent<Spin>().degreesPerSecond = 0f;

        // blush pads: pink ovals on the floor that shoot you off to either side
        foreach (var s in new[] { -1, 1 })
        {
            var pad = new SkateMeshKit();
            Vector3 c = new Vector3(s * 18f, 0.03f, -15f);
            pad.Cylinder(c, c + Vector3.up * 0.06f, 2.2f, 24, MePinkDeep);
            for (int n = 0; n < 3; n++)
                pad.Box(c + new Vector3(s * (n - 1) * 1.1f, 0.08f, 0f), new Vector3(0.25f, 0.04f, 1.6f), Quaternion.Euler(0f, s * 20f, 0f), MeWhite);
            var go = Solid("Blush pad", meRoot, pad, shiny, true);
            var bp = go.AddComponent<BoostPad>();
            bp.direction = new Vector3(s, 0f, 0f); bp.speed = 26f;
        }

        // kickers either side of the spine
        Kicker("Middle kicker", Matrix4x4.TRS(new Vector3(-12f, 0f, 30f), Quaternion.LookRotation(Vector3.forward), Vector3.one), 4f, 26f, 4f, MePink);
        Kicker("Middle kicker", Matrix4x4.TRS(new Vector3(12f, 0f, 30f), Quaternion.LookRotation(Vector3.forward), Vector3.one), 4f, 26f, 4f, MePink);
    }

    // ---- SoundCloud's half ---------------------------------------------------------------------------------

    static void MeSoundCloud(Material shiny)
    {
        // the waveform: a pump track along +z, orange on top, with the waveform's bars down both sides
        var prof = new List<Vector2>();
        for (float d = 0f; d <= MeTrackLength + 0.01f; d += 0.5f) prof.Add(new Vector2(d, MeWave(d)));
        RampTop = MeOrange; RampSide = MeOrangeSoft;
        ProfileRamp("Waveform", Matrix4x4.TRS(MeTrackStart, Quaternion.identity, Vector3.one), prof, MeTrackWidth, MeOrange, MeCream, -0.05f);
        var bars = new SkateMeshKit();
        for (float d = 1f; d < MeTrackLength - 1f; d += 1.2f)
        {
            float hgt = MeWave(d) + 0.6f + 1.4f * Mathf.Abs(Mathf.Sin(d * 1.7f) * Mathf.Sin(d * 0.23f));
            foreach (var s in new[] { -1f, 1f })
                bars.Box(MeTrackStart + new Vector3(s * (MeTrackWidth * 0.5f + 0.6f), hgt * 0.5f, d), new Vector3(0.3f, hgt, 0.7f), Quaternion.identity, (int)(d / 1.2f) % 2 == 0 ? MeOrange : MeOrangeSoft);
        }
        Solid("Waveform bars", meRoot, bars, shiny, false);

        // the cloud behind the end wall, with its bars
        var cloud = new SkateMeshKit();
        Vector3 cc = new Vector3(-MeHX - 10f, 14f, 0f);
        foreach (var b in new[] { new Vector4(0f, 0f, 0f, 9f), new Vector4(0f, 3f, 10f, 12f), new Vector4(0f, -2f, 22f, 8f), new Vector4(0f, 1f, -9f, 7f) })
            cloud.Ellipsoid(cc + new Vector3(b.x, b.y, b.z), new Vector3(6f, b.w, b.w), Quaternion.identity, 20, 12, MeOrange);
        for (int n = 0; n < 7; n++)
        {
            float hgt = 5f + n * 1.6f;
            cloud.Box(cc + new Vector3(0f, -6f + hgt * 0.5f, -16f - n * 2.2f), new Vector3(2f, hgt, 1.1f), Quaternion.identity, MeOrange);
        }
        Solid("Cloud", meRoot, cloud, shiny, false);

        // play-button kickers: a kicker with an orange triangle on each side
        foreach (var at in new[] { new Vector3(-40f, 0f, -45f), new Vector3(-108f, 0f, -20f), new Vector3(-40f, 0f, 40f) })
        {
            Kicker("Play kicker", Matrix4x4.TRS(at, Quaternion.LookRotation(Vector3.forward), Vector3.one), 4.5f, 28f, 4.5f, MeOrange);
            var tri = new SkateMeshKit();
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 c = at + new Vector3(s * 2.32f, 0.9f, 1.4f);
                tri.Tri(c + new Vector3(0f, -0.7f, -0.6f), c + new Vector3(0f, 0.7f, -0.6f), c + new Vector3(0f, 0f, 0.6f), new Vector3(s, 0f, 0f), MeWhite);
            }
            Solid("Play", meRoot, tri, shiny, false);
        }

        // the repost loop: a rail round a rounded rectangle, with an arrowhead at each end of the loop
        var pts = new List<Vector3>();
        Vector3 rc = new Vector3(-100f, 0.6f, 45f);
        for (int n = 0; n <= 48; n++)
        {
            float a = n / 48f * Mathf.PI * 2f;
            float x = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.4f) * 9f, z = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.4f) * 5f;
            pts.Add(rc + new Vector3(x, 0f, z));
        }
        RailCol = MeOrange;
        Rail("Repost loop", pts.ToArray());
        RailCol = MePinkDeep;
        // a couple of flat orange rails along the open floor
        Rail("SoundCloud rail", new[] { new Vector3(-50f, 0.5f, -10f), new Vector3(-50f, 0.5f, 20f) });
        Ledge("SoundCloud ledge", new Vector3(-95f, 0f, -55f), Quaternion.LookRotation(Vector3.forward), 14f, 0.5f, 1.2f, MeOrangeSoft);
    }

    // the playhead: a tall glowing orange band across the waveform, moved along it by MeLevel
    static Transform MePlayhead()
    {
        var k = new SkateMeshKit();
        k.Box(new Vector3(0f, 2.5f, 0f), new Vector3(MeTrackWidth + 2f, 5f, 0.4f), Quaternion.identity, new Color32(255, 120, 40, 255));
        k.Box(new Vector3(0f, 5.2f, 0f), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.Euler(0f, 0f, 45f), MeWhite);
        var go = Solid("Playhead", meRoot, k, toonMat, false);
        go.transform.position = MeTrackStart;
        return go.transform;
    }

    // ---- Discord's half --------------------------------------------------------------------------------------

    static void MeDiscord(Material shiny)
    {
        // the server tower: round-cornered icons spiralling up, each a hop up from the last, to a ping at the top
        var k = new SkateMeshKit();
        Color32[] icons = { MeBlurple, MeGreen, new Color32(250, 168, 26, 255), new Color32(235, 69, 158, 255), new Color32(87, 242, 135, 255), MeGrey };
        for (int n = 0; n < MeTowerSteps; n++)
        {
            Vector3 t = MeTowerStep(n);
            bool ping = n == MeTowerSteps - 1;
            float size = ping ? 6f : MeTowerPad;
            if (ping) k.Cylinder(t + Vector3.down * 0.7f, t, size * 0.5f, 28, MeRed);
            else
            {
                k.Box(t + Vector3.down * 0.35f, new Vector3(size - 0.8f, 0.7f, size), Quaternion.identity, icons[n % icons.Length]);
                k.Box(t + Vector3.down * 0.35f, new Vector3(size, 0.7f, size - 0.8f), Quaternion.identity, icons[n % icons.Length]);
            }
            k.Cylinder(new Vector3(t.x, 0f, t.z), t + Vector3.down * 0.7f, 0.15f, 6, MeGrey);
            if (ping) AddBox(meRoot, "Ping", t + Vector3.down * 0.35f, new Vector3(size * 0.86f, 0.7f, size * 0.86f));
            else AddBox(meRoot, "Server icon", t + Vector3.down * 0.35f, new Vector3(size, 0.7f, size));
        }
        // the ping's number
        MeText(k, "I", MeTowerStep(MeTowerSteps - 1) + new Vector3(-1.5f, 0.06f, -2.1f), Vector3.right, Vector3.forward, 0.6f, 0.06f, MeWhite);
        k.Cylinder(MeTowerCentre, MeTowerCentre + Vector3.up * (MeTowerSteps * MeTowerRise), 1.6f, 16, MeDarker);
        AddBox(meRoot, "Tower core", MeTowerCentre + Vector3.up * (MeTowerSteps * MeTowerRise * 0.5f), new Vector3(2.2f, MeTowerSteps * MeTowerRise, 2.2f));
        Solid("Server tower", meRoot, k, shiny, false);
        // the channel rail: from the ping all the way back down to the floor
        Vector3 top = MeTowerStep(MeTowerSteps - 1);
        RailCol = MeBlurple;
        Rail("#general", new[] { top + new Vector3(-1f, 0.4f, -3.5f), new Vector3(top.x - 14f, 6f, top.z - 40f), new Vector3(top.x - 18f, 0.6f, top.z - 70f) });

        // message bubbles stepping up, then down
        var bub = new SkateMeshKit();
        for (int n = 0; n < 9; n++)
        {
            float up = n < 5 ? 0.9f + n * 0.9f : 0.9f + (8 - n) * 0.9f;
            Vector3 c = new Vector3(45f + n * 6.5f, up, -70f + (n % 2) * 2.5f);
            bub.Box(c + Vector3.down * 0.3f, new Vector3(5f, 0.6f, 2.6f), Quaternion.identity, MeGrey);
            bub.Box(c + new Vector3(-1.4f, 0.01f, 0f), new Vector3(1.6f, 0.04f, 0.3f), Quaternion.identity, MeBlurple);
            bub.Box(c + new Vector3(0.6f, 0.01f, 0.5f), new Vector3(2.8f, 0.04f, 0.25f), Quaternion.identity, new Color32(220, 221, 222, 255));
            bub.Box(c + new Vector3(0.2f, 0.01f, -0.4f), new Vector3(2.0f, 0.04f, 0.25f), Quaternion.identity, new Color32(220, 221, 222, 255));
            AddBox(meRoot, "Message", c + Vector3.down * 0.3f, new Vector3(5f, 0.6f, 2.6f));
        }
        Solid("Messages", meRoot, bub, shiny, false);

        // voice channel: green rings that bounce you
        for (int n = 0; n < 3; n++)
        {
            var ring = new SkateMeshKit();
            Vector3 c = new Vector3(55f + n * 9f, 0f, 25f);
            ring.Cylinder(c, c + Vector3.up * 0.25f, 2.2f, 28, MeDarker);
            ring.Cylinder(c + Vector3.up * 0.25f, c + Vector3.up * 0.3f, 2.4f, 28, MeGreen, false);
            var go = Solid("Voice ring", meRoot, ring, shiny, true);
            go.AddComponent<Bouncer>().speed = 20f + n * 3f;
        }

        // the #: four rails crossing, half a metre up
        RailCol = MeBlurple;
        foreach (var s in new[] { -1f, 1f })
        {
            Rail("# rail", new[] { MeHash + new Vector3(s * 3f, 0.55f, -10f), MeHash + new Vector3(s * 3f, 0.55f, 10f) });
            Rail("# rail", new[] { MeHash + new Vector3(-10f, 0.85f, s * 3f), MeHash + new Vector3(10f, 0.85f, s * 3f) });
        }
        RailCol = MePinkDeep;
        Kicker("Discord kicker", Matrix4x4.TRS(new Vector3(100f, 0f, -20f), Quaternion.LookRotation(Vector3.back), Vector3.one), 4.5f, 28f, 4.5f, MeBlurple);
        Ledge("Discord ledge", new Vector3(110f, 0f, -60f), Quaternion.LookRotation(Vector3.right), 16f, 0.5f, 1.2f, MeGrey);
    }

    // ---- the hearts --------------------------------------------------------------------------------------------

    static void MeHeartsAll(Material shiny)
    {
        // SoundCloud's (orange): along the waveform, over the kickers, round the loop
        var sc = new List<Vector3> { MeTrackStart + new Vector3(0f, MeWave(30f) + 1.4f, 30f), MeTrackStart + new Vector3(0f, MeWave(95f) + 1.4f, 95f), new Vector3(-40f, 4f, -36f),
                                     new Vector3(-108f, 4f, -11f), new Vector3(-100f, 1.4f, 45f), new Vector3(-50f, 1.6f, 5f), new Vector3(-95f, 1.6f, -55f) };
        // Discord's (blurple): up the tower, on the bubbles, over the rings, in the #
        var dc = new List<Vector3> { MeTowerStep(7) + Vector3.up * 1.2f, MeTowerStep(MeTowerSteps - 1) + Vector3.up * 1.2f, new Vector3(45f + 4 * 6.5f, 4.5f + 1.2f, -70f),
                                     new Vector3(64f, 7f, 25f), MeHash + Vector3.up * 1.4f, new Vector3(100f, 4f, -28f), new Vector3(110f, 1.6f, -60f) };
        foreach (var p in sc) MeHeart(p, MeOrange, 0, shiny);
        foreach (var p in dc) MeHeart(p, MeBlurple, 1, shiny);
    }

    static void MeHeart(Vector3 at, Color32 col, int side, Material shiny)
    {
        var k = new SkateMeshKit();
        Heart(k, Vector3.zero, 1.4f, Quaternion.identity, col);
        var mesh = k.Build("Heart pickup");
        SaveFeatureMesh(mesh);
        var holder = new GameObject("Heart " + (side == 0 ? "SoundCloud" : "Discord"));
        holder.transform.SetParent(meRoot, false);
        holder.transform.position = at;
        var go = MeshObject("Heart", holder.transform, mesh, shiny, outlineMat);
        go.transform.localPosition = Vector3.zero;
        meHearts.Add(holder.transform);
        meHeartSide.Add(side);
    }

    static void MeSparkles()
    {
        var go = new GameObject("Sparkles", typeof(ParticleSystem));
        go.transform.SetParent(meRoot, false);
        go.transform.position = new Vector3(0f, 16f, 0f);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.75f, 0.88f));
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 180f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(MeHX * 2f, 30f, MeHZ * 2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mat("MeSparkle", "Sprites/Default");
    }

    public static void MeShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2.2f, -66f), new Vector3(0f, 6f, 40f), "m01-start.png");
        Shot(cam, new Vector3(0f, 60f, -120f), new Vector3(0f, 0f, 10f), "m02-overview.png");
        Shot(cam, MeTrackStart + new Vector3(-8f, 5f, -6f), MeTrackStart + new Vector3(0f, 1f, 40f), "m03-waveform.png");
        Shot(cam, MeTowerCentre + new Vector3(-25f, 8f, -25f), MeTowerCentre + Vector3.up * 9f, "m04-server-tower.png");
        Shot(cam, new Vector3(40f, 6f, -85f), new Vector3(70f, 2f, -65f), "m05-messages.png");
        Shot(cam, new Vector3(-12f, 5f, -55f), new Vector3(0f, 0f, -36f), "m06-me-ledges.png");
    }
}
