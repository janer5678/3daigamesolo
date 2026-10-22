using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Stream: after swagtwinn's stream (gambling, THUG Pro, Roblox, Minecraft, FL Studio). It plays like THUG Pro's
// "most points wins" (StreamLevel): two minutes, tricks into combos, combos banked when you land. The middle is a
// concrete THUG-style park under a blue sky: a big bank wall, a deck with a stair set and handrails, ledges, a pool,
// a halfpipe, a funbox, kickers. Round it, the stream itself: chat as a towering wall whose messages are lifts
// you can ride up to a rail all the way back down; the swagtwinn logo in chrome with black gothic stars; black thorn
// swords like the overlay's edges; a slot machine to spin; a Minecraft corner with a pink cherry tree; a Roblox obby
// (mind the red bricks); an FL Studio piano roll as a bank with its notes to grind. The overlay (chat, links, the
// logo) is drawn over everything.
public static partial class SkateWorldBuilder
{
    public const string StreamPath = "Assets/Scenes/Stream.unity";
    const string StreamTex = "Assets/Stream/Textures";
    public const float StHX = 90f, StHZ = 80f;
    public static readonly Vector3 StKicker = new Vector3(20f, 0f, -30f);
    public static readonly Vector3 StRail = new Vector3(-20f, 0.55f, -40f);   // a flat rail along +z from here, 16 m
    public static readonly Vector3 StSlotPad = new Vector3(-62f, 0f, -52f);
    public const float StChatX = 74f;

    static Transform stRoot;
    static readonly Dictionary<string, Material> stMats = new Dictionary<string, Material>();

    public static void StreamFromCommandLine()
    {
        try { BuildStream(); SlideTests.RunStream(); StreamShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    // a textured material (pastel-shaded, so it fits with the vertex-coloured bits); `pixel`: point-sampled
    static Material StMat(string tex, bool pixel = false, float cutoff = 0f, float glow = 0f)
    {
        string key = tex + (glow > 0f ? "_glow" : "");
        if (stMats.TryGetValue(key, out var m)) return m;
        string file = StreamTex + "/" + tex + ".png";
        var imp = AssetImporter.GetAtPath(file) as TextureImporter;
        if (imp != null)
        {
            var want = pixel ? FilterMode.Point : FilterMode.Bilinear;
            if (imp.filterMode != want || imp.mipmapEnabled == pixel) { imp.filterMode = want; imp.mipmapEnabled = !pixel; imp.SaveAndReimport(); }
        }
        m = Mat("Stream_" + key, "3daigamesolo/World/Pastel");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(file));
        m.SetColor("_Shade", new Color(0.66f, 0.66f, 0.78f)); m.SetColor("_Back", new Color(0.62f, 0.8f, 0.95f));
        m.SetFloat("_Fade", 900f); m.SetFloat("_Cutoff", cutoff); m.SetFloat("_Glow", glow);
        stMats[key] = m;
        return m;
    }

    static Mesh StSave(Mesh mesh) { SaveFeatureMesh(mesh); return mesh; }

    static GameObject StObj(string name, Mesh mesh, Material mat, bool collide)
    {
        SaveFeatureMesh(mesh);
        var go = MeshObject(name, stRoot, mesh, mat, null);
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    public static void BuildStream()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/StreamFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); stMats.Clear();
        floorAt = (x, z) => 0f;
        RampTop = new Color32(178, 178, 172, 255); RampSide = new Color32(140, 140, 136, 255); RampSide2 = new Color32(120, 120, 118, 255);
        Metal = new Color32(200, 200, 205, 255); RailCol = new Color32(214, 214, 220, 255); Concrete = new Color32(170, 170, 166, 255); ConcreteDark = new Color32(120, 120, 116, 255);

        toonMat = Mat("StreamToon", "3daigamesolo/World/Toon");
        toonMat.SetColor("_ShadeTint", new Color(0.68f, 0.68f, 0.8f));
        toonMat.SetColor("_Haze", new Color(0.62f, 0.8f, 0.95f)); toonMat.SetColor("_HazeHigh", new Color(0.55f, 0.75f, 0.95f)); toonMat.SetFloat("_FogDensity", 0.0015f);
        outlineMat = Mat("StreamOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.08f, 0.05f, 0.1f)); outlineMat.SetFloat("_OutlineWidth", 0.6f);
        outlineMat.SetColor("_Haze", new Color(0.62f, 0.8f, 0.95f)); outlineMat.SetColor("_HazeHigh", new Color(0.55f, 0.75f, 0.95f)); outlineMat.SetFloat("_FogDensity", 0.0015f);
        farOutlineMat = outlineMat;
        var sky = Mat("StreamSky", "3daigamesolo/World/Sky");
        sky.SetColor("_Zenith", new Color(0.25f, 0.5f, 0.9f)); sky.SetColor("_Horizon", new Color(0.66f, 0.82f, 0.96f));
        sky.SetColor("_Below", new Color(0.66f, 0.82f, 0.96f)); sky.SetColor("_Cloud", Color.white);
        sky.SetColor("_CloudShade", new Color(0.8f, 0.86f, 0.95f)); sky.SetFloat("_CloudCover", 0.4f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;
        stRoot = new GameObject("Stream").transform;

        StFloor();
        StPark();
        StChatWall();
        StLogo();
        StSwords();
        var reels = StSlots();
        StMinecraft();
        var obbyStart = StObby();
        StPianoRoll();
        StStickers();

        var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyGo.name = "Sky";
        Object.DestroyImmediate(skyGo.GetComponent<Collider>());
        skyGo.transform.localScale = Vector3.one * 6000f;
        skyGo.GetComponent<Renderer>().sharedMaterial = sky;

        Vector3 startAt = new Vector3(0f, 0.2f, -55f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt, Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(StreamLevel));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 7000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.8f, 0.95f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "swagtwinn LIVE";
        camGO.transform.SetPositionAndRotation(startAt + new Vector3(0f, 2.5f, -4f), Quaternion.LookRotation(Vector3.forward));
        player.board = BuildBoard(startAt + Vector3.forward * 2f, Quaternion.LookRotation(Vector3.forward));
        var level = camGO.GetComponent<StreamLevel>();
        level.player = player; level.reels = reels; level.obbyStart = obbyStart;

        EditorSceneManager.SaveScene(scene, StreamPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_STREAM_BUILT features=" + featureCount);
    }

    // ---- the floor -------------------------------------------------------------------------------------------------

    static void StFloor()
    {
        var k = new PSXKit();
        k.Ground(-StHX, -StHZ, StHX, StHZ, 0f, 8f, 10f, new Color32(255, 255, 255, 255));
        StObj("Concrete", k.Build("Concrete"), StMat("concrete"), false);
        AddBox(stRoot, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(StHX * 2f, 1f, StHZ * 2f));
        foreach (var s in new[] { -1f, 1f })
        {
            AddBox(stRoot, "Edge", new Vector3(s * (StHX + 0.5f), 15f, 0f), new Vector3(1f, 40f, StHZ * 2f));
            AddBox(stRoot, "Edge", new Vector3(0f, 15f, s * (StHZ + 0.5f)), new Vector3(StHX * 2f, 40f, 1f));
        }
    }

    // ---- the THUG park -----------------------------------------------------------------------------------------------

    static void StPark()
    {
        // the big bank wall down the west side
        QuarterPipe("Bank wall", Matrix4x4.TRS(new Vector3(-56f, 0f, 0f), Quaternion.LookRotation(Vector3.left), Vector3.one), 70f, 5f, 3f);
        // the deck, ramped up to from the north, a stair set down off its south side with handrails
        float deckH = 2.4f, z0 = 14f, z1 = 26f;
        var deck = new SkateMeshKit();
        deck.Box(new Vector3(0f, deckH * 0.5f, (z0 + z1) * 0.5f), new Vector3(20f, deckH, z1 - z0), Quaternion.identity, Concrete);
        AddBox(stRoot, "Deck", new Vector3(0f, deckH * 0.5f, (z0 + z1) * 0.5f), new Vector3(20f, deckH, z1 - z0));
        for (int i = 0; i < 8; i++)
        {
            float h = 0.3f * (i + 1), zs = z0 - (8 - i) * 0.6f;
            deck.Box(new Vector3(0f, h * 0.5f, (zs + z0) * 0.5f), new Vector3(10f, h, z0 - zs), Quaternion.identity, i % 2 == 0 ? Concrete : ConcreteDark);
            AddBox(stRoot, "Stair", new Vector3(0f, h * 0.5f, (zs + z0) * 0.5f), new Vector3(10f, h, z0 - zs));
        }
        MeshObject("Deck", stRoot, StSave(deck.Build("Deck")), toonMat, outlineMat);
        foreach (float x in new[] { -5.4f, 0f, 5.4f })
            Rail("Handrail", new[] { new Vector3(x, deckH + 0.9f, z0 + 0.4f), new Vector3(x, 0.9f, z0 - 8f * 0.6f - 0.3f) });
        var ramp = new List<Vector2> { new Vector2(-0.2f, -0.05f), new Vector2(0f, 0f), new Vector2(9f, deckH) };
        ProfileRamp("Deck ramp", Matrix4x4.TRS(new Vector3(0f, 0f, z1 + 9f), Quaternion.LookRotation(Vector3.back), Vector3.one), ramp, 10f, RampTop, RampSide, -0.05f);
        // ledges, a flat rail, kickers, a funbox, a manual pad
        Ledge("Ledge", new Vector3(-24f, 0f, 0f), Quaternion.identity, 20f, 0.5f, 1.2f, Concrete);
        Ledge("Ledge", new Vector3(24f, 0f, 0f), Quaternion.identity, 20f, 0.6f, 1.2f, Concrete);
        Rail("Flat rail", new[] { StRail, StRail + Vector3.forward * 16f });
        Kicker("Kicker", Matrix4x4.TRS(StKicker, Quaternion.identity, Vector3.one), 4.5f, 30f, 4f, RampSide);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(-36f, 0f, 30f), Quaternion.LookRotation(Vector3.right), Vector3.one), 4f, 26f, 3.6f, RampSide);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(0f, 0f, -18f), Quaternion.identity, Vector3.one), 7f);
        Ledge("Manual pad", new Vector3(40f, 0f, 20f), Quaternion.identity, 10f, 0.2f, 2f, ConcreteDark);
        // a pool
        Vector3 pool = new Vector3(36f, 0f, -36f);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            QuarterPipe("Pool wall", Matrix4x4.TRS(pool + q * new Vector3(0f, 0f, 5f), q, Vector3.one), 10.4f, 3.2f, 2f);
        }
        // a halfpipe
        QuarterPipe("Halfpipe", Matrix4x4.TRS(new Vector3(-30f, 0f, -36f), Quaternion.LookRotation(Vector3.back), Vector3.one), 16f, 3.4f, 2.5f);
        QuarterPipe("Halfpipe", Matrix4x4.TRS(new Vector3(-30f, 0f, -26f), Quaternion.LookRotation(Vector3.forward), Vector3.one), 16f, 3.4f, 2.5f);
    }

    // ---- chat: a towering wall, its messages lifts up to a deck at the top, and a rail back down ---------------------------

    public static Vector3 StLiftBottom(int i) { return new Vector3(StChatX - 2.4f, -0.4f, -36f + i * 10f); }
    public const float StLiftTop = 31f;

    static void StChatWall()
    {
        float top = 32f;
        var k = new SkateMeshKit();
        Color32 purple = new Color32(40, 22, 60, 255);
        k.Box(new Vector3(StChatX + 4f, top * 0.5f, 0f), new Vector3(8f, top, 90f), Quaternion.identity, purple);
        k.Box(new Vector3(StChatX + 4f, top + 0.25f, 0f), new Vector3(8.4f, 0.5f, 90.4f), Quaternion.identity, new Color32(150, 90, 230, 255));
        MeshObject("Chat wall", stRoot, StSave(k.Build("Chat wall")), toonMat, outlineMat);
        AddBox(stRoot, "Chat wall", new Vector3(StChatX + 4f, top * 0.5f, 0f), new Vector3(8f, top, 90f));
        AddBox(stRoot, "Chat deck", new Vector3(StChatX + 4f, top + 0.25f, 0f), new Vector3(8.4f, 0.5f, 90.4f));
        // the messages: lifts going up and down the wall's face
        for (int i = 0; i < 8; i++)
        {
            Vector3 b = StLiftBottom(i);
            var lift = new GameObject("Chat lift", typeof(Rigidbody), typeof(Mover), typeof(BoxCollider));
            lift.transform.SetParent(stRoot, false);
            lift.transform.position = b;
            var rb = lift.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var box = lift.GetComponent<BoxCollider>(); box.size = new Vector3(4.6f, 1f, 8f); box.center = new Vector3(0f, 0.5f, 0f);
            var m = lift.GetComponent<Mover>();
            m.path = new[] { b, new Vector3(b.x, StLiftTop, b.z) }; m.loop = false; m.speed = 3.5f; m.wait = 2f; m.easeIn = 2f; m.turn = false;
            m.startAt = (i * 7.3f) % (StLiftTop - 0.1f);
            var pk = new PSXKit();
            pk.Quad(new Vector3(-2.3f, 1f, 4f), new Vector3(-2.3f, 1f, -4f), new Vector3(2.3f, 1f, -4f), new Vector3(2.3f, 1f, 4f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector3.up);
            // (the face towards the park: reads left to right looking at it)
            pk.Quad(new Vector3(-2.3f, 0f, 4f), new Vector3(-2.3f, 1f, 4f), new Vector3(-2.3f, 1f, -4f), new Vector3(-2.3f, 0f, -4f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.left);
            var mesh = pk.Build("Chat message");
            SaveFeatureMesh(mesh);
            var vis = MeshObject("Message", lift.transform, mesh, StMat("chat" + i, false, 0f, 0.6f), null);
            vis.transform.localPosition = Vector3.zero;
        }
        // from the top, a long rail all the way back down into the park
        Rail("Chat rail", new[] { new Vector3(StChatX + 1.2f, top + 1f, 40f), new Vector3(StChatX - 4f, top - 6f, 30f), new Vector3(40f, 2.5f, 30f), new Vector3(30f, 1f, 30f) });
    }

    // ---- the logo, chrome, with black gothic stars round it ------------------------------------------------------------------

    static void StLogo()
    {
        var chrome = Mat("StreamChrome", "3daigamesolo/World/Glossy");
        chrome.SetVector("_SunDir", new Vector4(-0.3f, 0.7f, -0.6f, 0f)); chrome.SetFloat("_Gloss", 0.7f); chrome.SetFloat("_Spec", 1.6f); chrome.SetFloat("_SpecPower", 60f);
        chrome.SetColor("_GridColor", new Color(1f, 1f, 1f, 0f));
        var k = new SkateMeshKit();
        float px = 1.2f;
        MeText(k, "SWAGTWINN", new Vector3(-53f * px * 0.5f, 5.05f, 70f), Vector3.right, Vector3.up, px, -1.6f, new Color32(220, 220, 236, 255));
        var bold = Mat("StreamLogoLine", "3daigamesolo/World/Outline");
        bold.SetColor("_OutlineColor", new Color(0.35f, 0.1f, 0.5f)); bold.SetFloat("_OutlineWidth", 2.4f); bold.SetFloat("_FogDensity", 0f);
        var mesh = k.Build("Logo"); SaveFeatureMesh(mesh);
        MeshObject("Logo", stRoot, mesh, chrome, bold);
        // a black ring behind it and spiky stars round it
        var b = new SkateMeshKit();
        Color32 black = new Color32(14, 10, 18, 255);
        for (int i = 0; i < 64; i++)
        {
            float a0 = i * Mathf.PI * 2f / 64f, a1 = (i + 1) * Mathf.PI * 2f / 64f;
            Vector3 c = new Vector3(0f, 10f, 72f);
            b.Cylinder(c + new Vector3(Mathf.Cos(a0) * 36f, Mathf.Sin(a0) * 13f, 0f), c + new Vector3(Mathf.Cos(a1) * 36f, Mathf.Sin(a1) * 13f, 0f), 0.45f, 6, black, false);
        }
        foreach (var at in new[] { new Vector3(-42f, 16f, 71f), new Vector3(42f, 16f, 71f), new Vector3(-30f, 24f, 72f), new Vector3(30f, 2f, 72f), new Vector3(0f, 25f, 72f) })
        {
            b.Box(at, new Vector3(0.7f, 9f, 0.3f), Quaternion.identity, black);
            b.Box(at, new Vector3(5f, 0.6f, 0.3f), Quaternion.identity, black);
            b.Box(at, new Vector3(1.6f, 1.6f, 0.32f), Quaternion.Euler(0f, 0f, 45f), black);
        }
        b.Box(new Vector3(0f, 2.5f, 70.8f), new Vector3(70f, 5f, 2f), Quaternion.identity, black);
        AddBox(stRoot, "Logo plinth", new Vector3(0f, 2.5f, 70.8f), new Vector3(70f, 5f, 2f));
        MeshObject("Gothic", stRoot, StSave(b.Build("Gothic")), toonMat, outlineMat);
        Rail("Logo plinth edge", new[] { new Vector3(-34.5f, 5.05f, 69.85f), new Vector3(34.5f, 5.05f, 69.85f) }, false);
    }

    // ---- black thorn swords standing at the west edge, like the overlay's -------------------------------------------------------

    static void StSwords()
    {
        var k = new SkateMeshKit();
        Color32 black = new Color32(12, 10, 16, 255);
        foreach (float z in new[] { -40f, 40f })
        {
            Vector3 b = new Vector3(-StHX + 4f, 0f, z);
            k.Box(b + Vector3.up * 20f, new Vector3(1.4f, 40f, 0.5f), Quaternion.identity, black);
            k.Box(b + Vector3.up * 30f, new Vector3(9f, 1.2f, 0.8f), Quaternion.identity, black);
            k.Box(b + Vector3.up * 36f, new Vector3(1f, 8f, 0.8f), Quaternion.identity, black);
            for (int t = 0; t < 12; t++)
            {
                float y = 3f + t * 2.2f, s = t % 2 == 0 ? 1f : -1f;
                k.Box(b + new Vector3(s * 1.1f, y, 0f), new Vector3(1.4f, 0.3f, 0.3f), Quaternion.Euler(0f, 0f, s * 35f), black);
            }
            AddBox(stRoot, "Sword", b + Vector3.up * 20f, new Vector3(1.4f, 40f, 0.5f));
        }
        MeshObject("Swords", stRoot, StSave(k.Build("Swords")), toonMat, outlineMat);
    }

    // ---- the slot machine -------------------------------------------------------------------------------------------------

    static Transform[] StSlots()
    {
        Vector3 c = StSlotPad + new Vector3(0f, 0f, -8f);
        var k = new SkateMeshKit();
        k.Box(c + new Vector3(0f, 6f, -2.5f), new Vector3(10f, 12f, 5f), Quaternion.identity, new Color32(90, 30, 140, 255));
        k.Box(c + new Vector3(0f, 12.4f, -2.5f), new Vector3(10.6f, 1f, 5.4f), Quaternion.identity, new Color32(240, 190, 60, 255));
        k.Box(c + new Vector3(0f, 7f, 0.05f), new Vector3(8.6f, 4.6f, 0.2f), Quaternion.identity, new Color32(240, 190, 60, 255));
        k.Cylinder(c + new Vector3(5.4f, 5f, -1.5f), c + new Vector3(5.4f, 10f, -0.5f), 0.25f, 8, new Color32(200, 200, 210, 255));
        k.Ellipsoid(c + new Vector3(5.4f, 10.4f, -0.4f), new Vector3(0.8f, 0.8f, 0.8f), Quaternion.identity, 12, 8, new Color32(230, 30, 40, 255));
        MeshObject("Slot machine", stRoot, StSave(k.Build("Slot machine")), toonMat, outlineMat);
        AddBox(stRoot, "Slot machine", c + new Vector3(0f, 6f, -2.5f), new Vector3(10f, 12f, 5f));
        // the pad: ride onto it to spin
        var pad = new SkateMeshKit();
        pad.Box(StSlotPad + Vector3.up * 0.05f, new Vector3(5f, 0.1f, 5f), Quaternion.identity, new Color32(250, 210, 60, 255));
        pad.Box(StSlotPad + Vector3.up * 0.11f, new Vector3(3.6f, 0.02f, 0.6f), Quaternion.identity, new Color32(230, 30, 40, 255));
        var padGo = MeshObject("Spin pad", stRoot, StSave(pad.Build("Spin pad")), toonMat, outlineMat);
        padGo.AddComponent<SlotPad>();
        var col = padGo.AddComponent<BoxCollider>(); col.center = StSlotPad + Vector3.up * 0.05f; col.size = new Vector3(5f, 0.1f, 5f);
        // three reels, each a drum of symbols turning about x
        var reels = new Transform[3];
        var mat = StMat("reels");
        for (int i = 0; i < 3; i++)
        {
            var pk = new PSXKit();
            int segs = 20; float r = 1.4f, hw = 1.1f;
            for (int s = 0; s < segs; s++)
            {
                float a0 = s * Mathf.PI * 2f / segs, a1 = (s + 1) * Mathf.PI * 2f / segs;
                Vector3 p0 = new Vector3(0f, Mathf.Sin(a0) * r, -Mathf.Cos(a0) * r), p1 = new Vector3(0f, Mathf.Sin(a1) * r, -Mathf.Cos(a1) * r);
                float v0 = 1f - s / (float)segs, v1 = 1f - (s + 1f) / segs;
                pk.Quad(p0 + Vector3.left * hw, p1 + Vector3.left * hw, p1 + Vector3.right * hw, p0 + Vector3.right * hw, new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0), (p0 + p1).normalized);
            }
            var mesh = pk.Build("Reel"); SaveFeatureMesh(mesh);
            var go = new GameObject("Reel " + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(stRoot, false);
            go.transform.position = c + new Vector3((i - 1) * 2.6f, 7f, 0.2f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            reels[i] = go.transform;
        }
        return reels;
    }

    // ---- the Minecraft corner: a hill of blocks and a pink cherry tree ----------------------------------------------------------

    public static int StBlockH(int x, int z)
    {
        float d = new Vector2(x - 6f, z - 6f).magnitude;
        return Mathf.Clamp(Mathf.RoundToInt(5f - d * 0.55f + Mathf.Sin(x * 0.9f) * 0.6f), 0, 5);
    }
    public static readonly Vector3 StMineO = new Vector3(-82f, 0f, 44f);

    static void StMinecraft()
    {
        var top = new PSXKit(); var side = new PSXKit(); var dirt = new PSXKit();
        for (int x = 0; x < 14; x++)
            for (int z = 0; z < 14; z++)
            {
                int h = StBlockH(x, z);
                if (h <= 0) continue;
                Vector3 b = StMineO + new Vector3(x + 0.5f, 0f, z + 0.5f);
                AddBox(stRoot, "Block", b + Vector3.up * h * 0.5f, new Vector3(1f, h, 1f));
                top.Quad(b + new Vector3(-0.5f, h, -0.5f), b + new Vector3(-0.5f, h, 0.5f), b + new Vector3(0.5f, h, 0.5f), b + new Vector3(0.5f, h, -0.5f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), Vector3.up);
                foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    int nx = x + Mathf.RoundToInt(n.x), nz = z + Mathf.RoundToInt(n.z);
                    int nh = nx < 0 || nz < 0 || nx >= 14 || nz >= 14 ? 0 : StBlockH(nx, nz);
                    Vector3 r = Vector3.Cross(Vector3.up, n);
                    for (int y = nh; y < h; y++)
                    {
                        var kit = y == h - 1 ? side : dirt;
                        Vector3 f = b + n * 0.5f + Vector3.up * y;
                        kit.Quad(f - r * 0.5f, f - r * 0.5f + Vector3.up, f + r * 0.5f + Vector3.up, f + r * 0.5f, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), n);
                    }
                }
            }
        StObj("Grass", top.Build("Grass"), StMat("grasstop", true), false);
        StObj("Grass side", side.Build("Grass side"), StMat("grassside", true), false);
        StObj("Dirt", dirt.Build("Dirt"), StMat("dirt", true), false);
        // the cherry tree on top
        Vector3 t = StMineO + new Vector3(6.5f, StBlockH(6, 6), 6.5f);
        var log = new PSXKit(); var leaves = new PSXKit();
        for (int y = 0; y < 6; y++) log.Box(t + Vector3.up * (y + 0.5f), Vector3.one, Quaternion.identity, 1f, 1f);
        AddBox(stRoot, "Log", t + Vector3.up * 3f, new Vector3(1f, 6f, 1f));
        var rng = new System.Random(4);
        for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
                for (int y = 5; y <= 8; y++)
                {
                    if (x * x + z * z + (y - 6.5f) * (y - 6.5f) * 2f > 11f || rng.NextDouble() < 0.12) continue;
                    leaves.Box(t + new Vector3(x, y + 0.5f, z), Vector3.one, Quaternion.identity, 1f, 1f, null, true, true);
                }
        StObj("Log", log.Build("Log"), StMat("log", true), false);
        StObj("Pink leaves", leaves.Build("Pink leaves"), StMat("pinkleaves", true), false);
    }

    // ---- the Roblox obby -------------------------------------------------------------------------------------------------------

    public static readonly Vector4[] StObbySteps =
    {
        new Vector4(44f, 0.6f, 40f, 4f), new Vector4(47.5f, 1.4f, 44.5f, 2.6f), new Vector4(51f, 2.2f, 48f, 2.6f), new Vector4(54.5f, 3f, 51.5f, 2.6f),
        new Vector4(58f, 3.8f, 55f, 2.6f), new Vector4(60f, 4.6f, 59.5f, 2.4f), new Vector4(62f, 5.4f, 64f, 2.4f), new Vector4(64f, 6.2f, 68.5f, 5f),
    };

    static Transform StObby()
    {
        var k = new PSXKit();
        Color32[] cols = { new Color32(40, 140, 250, 255), new Color32(250, 210, 40, 255), new Color32(60, 200, 80, 255), new Color32(250, 120, 40, 255), new Color32(170, 80, 240, 255) };
        for (int i = 0; i < StObbySteps.Length; i++)
        {
            var s = StObbySteps[i];
            Vector3 c = new Vector3(s.x, s.y - 0.4f, s.z);
            k.Box(c, new Vector3(s.w, 0.8f, s.w), Quaternion.identity, 2f, 2f, cols[i % cols.Length], true, true);
            AddBox(stRoot, "Obby", c, new Vector3(s.w, 0.8f, s.w));
        }
        StObj("Obby", k.Build("Obby"), StMat("studs"), false);
        // red bricks under the gaps: land on one and you're back at the start
        var red = new PSXKit();
        for (int i = 1; i < StObbySteps.Length; i++)
        {
            var a = StObbySteps[i - 1]; var b = StObbySteps[i];
            Vector3 m = new Vector3((a.x + b.x) * 0.5f, 0.15f, (a.z + b.z) * 0.5f);
            red.Box(m, new Vector3(2.4f, 0.3f, 2.4f), Quaternion.identity, 2f, 2f, new Color32(255, 40, 40, 255), true, true);
            var g = new GameObject("Kill brick").transform;
            g.SetParent(stRoot, false);
            g.gameObject.AddComponent<KillBrick>();
            AddBox(g, "Kill brick", m, new Vector3(2.4f, 0.3f, 2.4f));
        }
        StObj("Kill bricks", red.Build("Kill bricks"), StMat("studs", false, 0f, 1f), false);
        // a golden W at the top
        var w = new SkateMeshKit();
        var top = StObbySteps[StObbySteps.Length - 1];
        MeText(w, "W", new Vector3(top.x - 1.5f, top.y + 0.1f, top.z + 1.5f), Vector3.right, Vector3.up, 0.6f, 0.4f, new Color32(250, 200, 40, 255));
        MeshObject("Obby W", stRoot, StSave(w.Build("Obby W")), toonMat, outlineMat);
        var start = new GameObject("Obby start").transform;
        start.SetParent(stRoot, false);
        start.SetPositionAndRotation(new Vector3(StObbySteps[0].x, StObbySteps[0].y, StObbySteps[0].z), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        return start;
    }

    // ---- the piano roll: a bank with its notes to grind --------------------------------------------------------------------------

    static void StPianoRoll()
    {
        float z0 = -62f, z1 = -78f, h = 7f, hw = 30f;
        var k = new PSXKit();
        Vector3 a = new Vector3(-hw, 0f, z0), b = new Vector3(hw, 0f, z0), c = new Vector3(hw, h, z1), d = new Vector3(-hw, h, z1);
        k.Quad(a, d, c, b, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 1), new Vector2(3, 0), Vector3.Cross(d - a, b - a).y > 0 ? Vector3.Cross(d - a, b - a) : -Vector3.Cross(d - a, b - a));
        StObj("Piano roll", k.Build("Piano roll"), StMat("pianoroll"), true);
        AddBox(stRoot, "Piano roll back", new Vector3(0f, h * 0.5f, z1 - 0.5f), new Vector3(hw * 2f, h, 1f));
        // the notes: green bars lying on it, each a rail along its top
        var notes = new SkateMeshKit();
        var rng = new System.Random(9);
        RailCol = new Color32(110, 230, 110, 255);
        for (int i = 0; i < 9; i++)
        {
            float t = 0.15f + i * 0.09f, x0 = -hw + 3f + (float)rng.NextDouble() * 30f, len = 6f + (float)rng.NextDouble() * 12f;
            Vector3 p = Vector3.Lerp(a, d, t), up = Vector3.Cross(Vector3.right, (d - a).normalized);
            if (up.y < 0f) up = -up;
            Vector3 s0 = new Vector3(x0, p.y, p.z) + up * 0.15f, s1 = s0 + Vector3.right * Mathf.Min(len, hw - 1f - x0);
            notes.Box((s0 + s1) * 0.5f, new Vector3((s1 - s0).magnitude, 0.3f, 0.5f), Quaternion.LookRotation((d - a).normalized, up), new Color32(110, 230, 110, 255));
            Rail("Note", new[] { s0 + up * 0.16f, s1 + up * 0.16f }, false);
        }
        MeshObject("Notes", stRoot, StSave(notes.Build("Notes")), toonMat, outlineMat);
        RailCol = new Color32(214, 214, 220, 255);
        // a keyboard along the bottom
        var keys = new SkateMeshKit();
        for (int i = 0; i < 40; i++)
        {
            float x = -hw + 0.75f + i * 1.5f;
            keys.Box(new Vector3(x, 0.05f, z0 + 1.6f), new Vector3(1.4f, 0.1f, 3f), Quaternion.identity, new Color32(245, 245, 245, 255));
            if (i % 7 != 2 && i % 7 != 6) keys.Box(new Vector3(x + 0.75f, 0.12f, z0 + 0.9f), new Vector3(0.8f, 0.1f, 1.6f), Quaternion.identity, new Color32(20, 20, 24, 255));
        }
        MeshObject("Keys", stRoot, StSave(keys.Build("Keys")), toonMat, null);
    }

    // ---- pink sakura stickers about the place -----------------------------------------------------------------------------------

    static void StStickers()
    {
        var mat = StMat("sakura", false, 0.4f, 1f);
        var rng = new System.Random(12);
        for (int i = 0; i < 18; i++)
        {
            Vector3 at; Vector3 right, up;
            if (i < 8) { at = new Vector3(StChatX - 0.05f, 6f + (float)rng.NextDouble() * 22f, -40f + (float)rng.NextDouble() * 80f); right = Vector3.forward; up = Vector3.up; }
            else if (i < 13) { at = new Vector3(-40f + (float)rng.NextDouble() * 80f, 9f + (float)rng.NextDouble() * 12f, 70f); right = Vector3.right; up = Vector3.up; }
            else { at = new Vector3(-60f + (float)rng.NextDouble() * 110f, 0.03f, -50f + (float)rng.NextDouble() * 100f); right = Vector3.right; up = Vector3.forward; }
            float s = 1.5f + (float)rng.NextDouble() * 2.5f;
            var k = new PSXKit();
            k.Panel(at, right * s, up * s);
            StObj("Sticker", k.Build("Sticker"), mat, false);
        }
    }

    public static void StreamShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2.2f, -60f), new Vector3(0f, 4f, 20f), "t01-start.png");
        Shot(cam, new Vector3(-20f, 45f, -100f), new Vector3(0f, 0f, 10f), "t02-overview.png");
        Shot(cam, new Vector3(50f, 6f, -10f), new Vector3(StChatX, 12f, 10f), "t03-chat-wall.png");
        Shot(cam, StSlotPad + new Vector3(4f, 3f, 10f), StSlotPad + new Vector3(0f, 6f, -8f), "t04-slots.png");
        Shot(cam, StMineO + new Vector3(22f, 8f, -10f), StMineO + new Vector3(6f, 4f, 6f), "t05-minecraft.png");
        Shot(cam, new Vector3(30f, 5f, 30f), new Vector3(56f, 3f, 55f), "t06-obby.png");
        Shot(cam, new Vector3(0f, 4f, -45f), new Vector3(0f, 3f, -70f), "t07-piano-roll.png");
    }
}
