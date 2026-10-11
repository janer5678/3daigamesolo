using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sakura City: a small Japanese city in the same pastel toon style, lighter. A ring road with cars and
// buses going round it (ride on their roofs), an elevated railway over the main boulevard with a train
// running back and forth through a station, utility poles with power lines all along the ring (grind
// them on the board), shop streets inside with awnings, vending machines and alleys, a parking garage
// to skate up, a skate plaza, and a tall goal tower in the middle of it all.
//
// Getting to the top of the tower is meant to take a bit of work, and there's more than one way:
//  - skate: up the parking garage ramps to its roof (15 m), grind the power line up to the roof of the
//    building beside the tower (22 m), climb the billboard frame there and jump the alley onto the
//    tower's terrace (30 m);
//  - parkour: wall-jump up the narrow alley between that building and the tower to the same roof;
//  - train: up the station stairs, onto the train's roof as it comes in, across to the station canopy,
//    over to the rooftops north of the station, up their roof clutter and the sign frame, and over;
// and from the terrace, either the staggered balconies up the tower's south face or the wall-jump shaft
// on its north face, to the helipad on top (58 m).
public static partial class SkateWorldBuilder
{
    public const string CityPath = "Assets/Scenes/SakuraCity.unity";

    const float RingD = 100f;                 // ring road centre line, from the middle of the city
    const float InnerEdge = 89f, OuterEdge = 111f;
    const float Kerb = 0.15f;                 // pavements are this high
    const float ViaductTop = 9f;

    static void CityPalette()
    {
        RampTop = new Color32(250, 222, 230, 255);
        RampSide = new Color32(236, 232, 248, 255);
        RampSide2 = new Color32(214, 238, 248, 255);
        Metal = new Color32(236, 236, 246, 255);
        RailCol = new Color32(150, 150, 176, 255);
        Concrete = new Color32(244, 240, 246, 255);
        ConcreteDark = new Color32(220, 214, 232, 255);
    }

    static readonly Color32 CAsphalt = new Color32(206, 202, 222, 255), CLine = new Color32(255, 255, 255, 255), CPave = new Color32(248, 236, 238, 255);
    static readonly Color32 CKerb = new Color32(232, 220, 232, 255), CGlass = new Color32(196, 222, 248, 255), CLit = new Color32(255, 244, 196, 255);
    static readonly Color32 CPole = new Color32(204, 202, 218, 255), CCable = new Color32(96, 90, 118, 255), CTrim = new Color32(255, 255, 255, 255);
    static readonly Color32[] CWalls = {
        new Color32(255, 246, 232, 255), new Color32(255, 224, 234, 255), new Color32(216, 244, 232, 255), new Color32(216, 232, 250, 255),
        new Color32(234, 224, 250, 255), new Color32(255, 230, 210, 255), new Color32(248, 248, 255, 255) };
    static readonly Color32[] CSigns = {
        new Color32(255, 118, 168, 255), new Color32(110, 214, 236, 255), new Color32(255, 222, 110, 255),
        new Color32(240, 92, 104, 255), new Color32(124, 210, 152, 255), new Color32(170, 140, 240, 255) };
    static readonly Color32[] CCars = {
        new Color32(255, 255, 255, 255), new Color32(255, 196, 210, 255), new Color32(184, 226, 250, 255), new Color32(255, 230, 150, 255),
        new Color32(200, 240, 210, 255), new Color32(220, 200, 250, 255), new Color32(250, 140, 150, 255), new Color32(140, 150, 180, 255) };

    static Material cityGround;
    static System.Random crng;
    static readonly List<Rect> cityLots = new List<Rect>();        // buildings' footprints (x, z), for poles to find roofs
    static readonly List<float> cityLotHeights = new List<float>();

    public static void BuildCity()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Scenes");
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/CityFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        cityLots.Clear(); cityLotHeights.Clear();
        CityPalette();
        floorAt = (x, z) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < InnerEdge + 4f || Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > OuterEdge - 4f ? Kerb : 0f;
        crng = new System.Random(20261013);

        // a lighter take on the valley's look: paler shading, a pale bright haze
        Color haze = new Color(0.99f, 0.95f, 0.97f), hazeHigh = new Color(0.90f, 0.96f, 1.00f);
        toonMat = Mat("CityToon", "3daigamesolo/World/Toon");
        toonMat.SetColor("_ShadeTint", new Color(0.86f, 0.84f, 0.97f));
        CityHaze(toonMat, haze, hazeHigh, 0.0011f);
        cityGround = Mat("CityGround", "3daigamesolo/World/Toon");
        cityGround.SetColor("_ShadeTint", new Color(0.86f, 0.84f, 0.97f));
        cityGround.SetColor("_DotColor", new Color(1f, 1f, 1f, 0.22f));   // a faint halftone over the tarmac and paving
        cityGround.SetFloat("_DotScale", 2.2f);
        CityHaze(cityGround, haze, hazeHigh, 0.0011f);
        var shiny = Mat("CityShiny", "3daigamesolo/World/Toon");
        shiny.SetColor("_ShadeTint", new Color(0.88f, 0.86f, 0.98f));
        shiny.SetFloat("_Glitter", 0.2f);
        CityHaze(shiny, haze, hazeHigh, 0.0011f);
        outlineMat = Mat("CityOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.36f, 0.28f, 0.50f));
        outlineMat.SetFloat("_OutlineWidth", 1.4f);
        CityHaze(outlineMat, haze, hazeHigh, 0.0011f);
        farOutlineMat = Mat("CityOutlineFar", "3daigamesolo/World/Outline");
        farOutlineMat.SetColor("_OutlineColor", new Color(0.36f, 0.28f, 0.50f));
        farOutlineMat.SetFloat("_OutlineWidth", 1f);
        CityHaze(farOutlineMat, haze, hazeHigh, 0.0011f);
        var sky = Mat("Sky", "3daigamesolo/World/Sky");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        featureRoot = new GameObject("Skate features").transform;
        var city = new GameObject("City").transform;

        Streets(city);
        Viaduct(city);
        GoalTower(city, shiny);
        Garage(city);
        SkatePlaza(city);
        Blocks(city, shiny);
        OuterRing(city);
        PowerLines(city);
        Traffic(city);
        Train(city);
        CityDressing(city, shiny);

        var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyGo.name = "Sky";
        Object.DestroyImmediate(skyGo.GetComponent<Collider>());
        skyGo.transform.localScale = Vector3.one * 9000f;
        skyGo.GetComponent<Renderer>().sharedMaterial = sky;

        // you start on the boulevard under the railway, the board lying just ahead
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(new Vector3(-72f, Kerb + 0.2f, -7f), Quaternion.LookRotation(Vector3.right));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 9000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.99f, 0.95f, 0.97f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Sakura City";
        camGO.transform.SetPositionAndRotation(new Vector3(-77f, 2.5f, -7f), Quaternion.LookRotation(Vector3.right));
        player.board = BuildBoard(new Vector3(-64f, Kerb, -7f), Quaternion.LookRotation(Vector3.right));

        EditorSceneManager.SaveScene(scene, CityPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_CITY_BUILT features=" + featureCount + " lots=" + cityLots.Count);
    }

    static void CityHaze(Material m, Color low, Color high, float density)
    {
        m.SetColor("_Haze", low);
        m.SetColor("_HazeHigh", high);
        m.SetFloat("_FogDensity", density);
        m.SetFloat("_FogHeight", 400f);
        m.SetFloat("_FogMax", 0.75f);
        EditorUtility.SetDirty(m);
    }

    static float R(float a, float b) { return a + (float)crng.NextDouble() * (b - a); }
    static T Pick<T>(T[] from) { return from[crng.Next(from.Length)]; }

    // a solid box: drawn and with a collider
    static void SolidBox(SkateMeshKit k, Transform parent, Vector3 centre, Vector3 size, Color32 col, Quaternion? rot = null)
    {
        k.Box(centre, size, rot ?? Quaternion.identity, col);
        AddBox(parent, "Box", centre, size, rot);
    }

    static GameObject Ground(string name, Transform parent, SkateMeshKit k)
    {
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        return MeshObject(name, parent, mesh, cityGround, outlineMat);
    }

    // ---- streets ---------------------------------------------------------------------------------

    static void Streets(Transform city)
    {
        var root = new GameObject("Streets").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        // tarmac everywhere, and the pavements raised a kerb's height on top
        SolidBox(k, root, new Vector3(0f, -0.5f, 0f), new Vector3(900f, 1f, 900f), CAsphalt);
        SolidBox(k, root, new Vector3(0f, Kerb / 2f, 0f), new Vector3(InnerEdge * 2f + 8f, Kerb, InnerEdge * 2f + 8f), CPave);   // inside the ring
        foreach (var s in new[] { 1f, -1f })
        {
            SolidBox(k, root, new Vector3(0f, Kerb / 2f, s * (OuterEdge + 92f)), new Vector3(OuterEdge * 2f + 200f, Kerb, 200f), CPave);   // outside the ring
            SolidBox(k, root, new Vector3(s * (OuterEdge + 92f), Kerb / 2f, 0f), new Vector3(200f, Kerb, OuterEdge * 2f - 0.1f), CPave);
        }
        // kerb stones along the inside pavement
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            k.Box(q * new Vector3(0f, Kerb + 0.01f, InnerEdge + 3.9f), new Vector3(InnerEdge * 2f + 8f, 0.02f, 0.25f), q, CKerb);
            k.Box(q * new Vector3(0f, Kerb + 0.01f, OuterEdge - 3.9f), new Vector3(OuterEdge * 2f - 8f, 0.02f, 0.25f), q, CKerb);
        }
        // road markings: the centre line (dashed), and zebra crossings where the boulevards meet the ring
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            for (float x = -RingD + 4f; x < RingD - 4f; x += 6f)
                k.Box(q * new Vector3(x, 0.01f, RingD), new Vector3(3f, 0.02f, 0.2f), q, CLine);
            for (float x = -6f; x <= 6f; x += 1.5f)
                k.Box(q * new Vector3(x, 0.01f, RingD), new Vector3(0.8f, 0.02f, 9f), q, CLine);
        }
        // paving pattern on the boulevards: lines of lighter slabs
        for (float x = -InnerEdge; x < InnerEdge; x += 4f)
        {
            k.Box(new Vector3(x, Kerb + 0.005f, 0f), new Vector3(0.12f, 0.01f, 22f), Quaternion.identity, CKerb);
            k.Box(new Vector3(0f, Kerb + 0.005f, x), new Vector3(12f, 0.01f, 0.12f), Quaternion.identity, CKerb);
        }
        Ground("Streets", root, k);
    }

    // ---- the elevated railway along the boulevard, and its station -----------------------------

    static void Viaduct(Transform city)
    {
        var root = new GameObject("Railway").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        float half = 260f;
        Color32 conc = new Color32(240, 236, 246, 255), beam = new Color32(250, 214, 226, 255);
        // the deck, its side beams, and pillars every 20 m (in the middle of the boulevard)
        SolidBox(k, root, new Vector3(0f, ViaductTop - 0.6f, 0f), new Vector3(half * 2f, 1.2f, 9f), conc);
        foreach (var s in new[] { -1f, 1f })
        {
            k.Box(new Vector3(0f, ViaductTop - 1f, s * 4.6f), new Vector3(half * 2f, 0.6f, 0.3f), Quaternion.identity, beam);
            SolidBox(k, root, new Vector3(0f, ViaductTop + 0.5f, s * 4.4f), new Vector3(half * 2f, 1f, 0.2f), conc);   // low side walls
        }
        for (float x = -half + 10f; x < half; x += 20f)
        {
            if (Mathf.Abs(Mathf.Abs(x) - RingD) < 9f) continue;   // not in the ring road
            SolidBox(k, root, new Vector3(x, (ViaductTop - 1.2f) / 2f, 0f), new Vector3(1.6f, ViaductTop - 1.2f, 1.6f), conc);
            k.Box(new Vector3(x, ViaductTop - 1.6f, 0f), new Vector3(2.4f, 0.8f, 8f), Quaternion.identity, conc);
        }
        // sleepers, and the rails on them (grindable: four long rails right across the city)
        for (float x = -half; x < half; x += 1.2f)
            foreach (var tz in new[] { -2.2f, 2.2f })
                k.Box(new Vector3(x, ViaductTop + 0.05f, tz), new Vector3(0.3f, 0.1f, 2.4f), Quaternion.identity, new Color32(200, 190, 210, 255));
        Solid("Viaduct", root, k, toonMat, false);
        foreach (var tz in new[] { -2.2f, 2.2f })
            foreach (var rz in new[] { -0.72f, 0.72f })
                Rail("Track", new[] { new Vector3(-half, ViaductTop + 0.18f, tz + rz), new Vector3(half, ViaductTop + 0.18f, tz + rz) }, false);

        // the station: platforms either side of the tracks at train-floor height, a canopy over each
        // (not over the tracks, so you can ride the train through), and stairs down to the boulevard
        var s2 = new SkateMeshKit();
        float sx0 = -30f, sx1 = 30f;
        foreach (var s in new[] { -1f, 1f })
        {
            SolidBox(s2, root, new Vector3(0f, ViaductTop + 0.5f, s * 6.6f), new Vector3(sx1 - sx0, 1f, 4.2f), new Color32(250, 244, 236, 255));
            s2.Box(new Vector3(0f, ViaductTop + 1.01f, s * 4.85f), new Vector3(sx1 - sx0, 0.02f, 0.4f), Quaternion.identity, new Color32(255, 222, 110, 255));   // the yellow line
            SolidBox(s2, root, new Vector3(0f, 12.35f, s * 6.8f), new Vector3(sx1 - sx0 + 2f, 0.3f, 5f), new Color32(250, 214, 226, 255));   // canopy
            s2.Box(new Vector3(0f, 12.55f, s * 6.8f), new Vector3(sx1 - sx0 + 2.1f, 0.12f, 5.1f), Quaternion.identity, CTrim);
            for (float x = sx0 + 2f; x < sx1; x += 8f)
                SolidBox(s2, root, new Vector3(x, 11.1f, s * 7.8f), new Vector3(0.3f, 2.2f, 0.3f), CTrim);
            // station sign
            s2.Box(new Vector3(0f, 11.6f, s * 5.2f), new Vector3(6f, 0.7f, 0.15f), Quaternion.identity, CTrim);
            s2.Box(new Vector3(0f, 11.6f, s * 5.12f), new Vector3(5.6f, 0.5f, 0.02f), Quaternion.identity, new Color32(124, 210, 152, 255));
        }
        // stairs down to the boulevard on the north side, east of the station, with handrails
        float stairTop = ViaductTop + 1f, run = 28f, z0 = 9f, z1 = 12f;
        int steps = 40;
        for (int i = 0; i < steps; i++)
        {
            float h = stairTop * (steps - i) / steps;
            float xa = sx1 + run * i / steps, xb = sx1 + run * (i + 1) / steps;
            s2.Box(new Vector3((xa + xb) / 2f, h / 2f, (z0 + z1) / 2f), new Vector3(xb - xa, h, z1 - z0), Quaternion.identity, i % 2 == 0 ? CTrim : new Color32(240, 234, 244, 255));
        }
        float slope = Mathf.Atan2(stairTop, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(stairTop * stairTop + run * run);
        AddBox(root, "Stairs slope", new Vector3(sx1 + run / 2f, stairTop / 2f - 0.15f, (z0 + z1) / 2f), new Vector3(len, 0.3f, z1 - z0), Quaternion.Euler(0f, 0f, -slope));
        s2.Box(new Vector3(sx1 - 1.5f, stairTop - 0.5f, 10.5f), new Vector3(3f, 1f, 3f), Quaternion.identity, CTrim);   // the landing at the top
        AddBox(root, "Stairs landing", new Vector3(sx1 - 1.5f, stairTop - 0.5f, 10.5f), new Vector3(3f, 1f, 3f));
        Solid("Station", root, s2, toonMat, false);
        foreach (var hz in new[] { z0 + 0.15f, z1 - 0.15f })
            Rail("Stair handrail", new[] { new Vector3(sx1 - 1f, stairTop + 0.9f, hz), new Vector3(sx1, stairTop + 0.9f, hz), new Vector3(sx1 + run, 0.9f + Kerb, hz), new Vector3(sx1 + run + 1.5f, 0.9f + Kerb, hz) });
        TowerCheckpoint(root, new Vector3(-20f, ViaductTop + 1f, 7.5f), "Station");
    }

    // ---- the goal tower ---------------------------------------------------------------------------

    // lower block: x 40..62, z 40..62, up to the terrace at 30 m; upper block 44..58, up to 58 m
    static readonly Rect TowerLow = Rect.MinMaxRect(40f, 40f, 62f, 62f), TowerHigh = Rect.MinMaxRect(44f, 44f, 58f, 58f);
    const float TowerTerrace = 30f, TowerTop = 58f;

    static void GoalTower(Transform city, Material shiny)
    {
        var root = new GameObject("Goal tower").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        Color32 wall = new Color32(255, 236, 244, 255), wall2 = new Color32(236, 228, 252, 255);
        Building(k, root, TowerLow, TowerTerrace, wall, false, false);
        Building(k, root, TowerHigh, TowerTop, wall2, false, false);
        cityLots.Add(TowerLow); cityLotHeights.Add(TowerTerrace);

        // the terrace: a railing round its edge (grindable)
        Parapet(k, root, TowerLow, TowerTerrace, 0.6f);

        // south face, terrace to top: staggered balconies, each 1.4 m above the last, zig-zagging across
        float[] bx = { 46.6f, 51f, 55.4f, 51f };
        int n = 0;
        for (float y = TowerTerrace + 1.4f; y < TowerTop - 0.3f; y += 1.4f, n++)
        {
            Vector3 c = new Vector3(bx[n % 4], y - 0.15f, TowerHigh.yMin - 0.9f);
            SolidBox(k, root, c, new Vector3(3.2f, 0.3f, 1.8f), n % 2 == 0 ? CTrim : new Color32(250, 214, 226, 255));
        }
        // north face, terrace to top: a wall-jump shaft between two fins
        foreach (var fx in new[] { 49.5f, 52.5f })
            SolidBox(k, root, new Vector3(fx + (fx < 51f ? -0.3f : 0.3f), (TowerTerrace + TowerTop) / 2f, TowerHigh.yMax + 2f), new Vector3(0.6f, TowerTop - TowerTerrace, 4f), new Color32(250, 214, 226, 255));

        // on top: a helipad, a big heart sign and the goal
        k.Cylinder(new Vector3(51f, TowerTop, 51f), new Vector3(51f, TowerTop + 0.05f, 51f), 5f, 32, new Color32(140, 150, 180, 255));
        k.Box(new Vector3(51f, TowerTop + 0.06f, 51f), new Vector3(0.6f, 0.02f, 4f), Quaternion.identity, CTrim);
        k.Box(new Vector3(51f, TowerTop + 0.06f, 51f), new Vector3(3f, 0.02f, 0.6f), Quaternion.identity, CTrim);
        Solid("Goal tower", root, k, toonMat, false);
        var hk = new SkateMeshKit();
        Heart(hk, Vector3.zero, 8f, Quaternion.identity, new Color32(255, 118, 168, 255));
        var heart = Solid("Tower heart", root, hk, shiny, false);
        heart.transform.position = new Vector3(51f, TowerTop + 9f, 51f);
        heart.AddComponent<Spin>().degreesPerSecond = 35f;
        TowerCheckpoint(root, new Vector3(42.5f, TowerTerrace, 42.5f), "TowerTerrace (30 m)");
        TowerCheckpoint(root, new Vector3(54f, TowerTop, 54f), "Top of the city!");
    }

    // ---- the parking garage: four decks joined by ramps you can skate up, open to the air ----------

    static readonly Rect GarageLot = Rect.MinMaxRect(64f, 13f, 88f, 37f);

    static void Garage(Transform city)
    {
        var root = new GameObject("Parking garage").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        Color32 deck = new Color32(234, 232, 244, 255), edge = new Color32(250, 214, 226, 255);
        float x0 = GarageLot.xMin, x1 = GarageLot.xMax, za = GarageLot.yMin, zb = GarageLot.yMax;
        // decks at 5 and 10 m and the roof at 15 m, each leaving room for the ramps
        SolidBox(k, root, new Vector3((x0 + x1) / 2f, 5f - 0.25f, (za + 4f + zb) / 2f), new Vector3(x1 - x0, 0.5f, zb - za - 4f), deck);
        SolidBox(k, root, new Vector3((x0 + x1) / 2f, 10f - 0.25f, (za + 4f + zb - 4f) / 2f), new Vector3(x1 - x0, 0.5f, zb - za - 8f), deck);
        SolidBox(k, root, new Vector3((x0 + x1) / 2f, 15f - 0.25f, (za + 4f + zb) / 2f), new Vector3(x1 - x0, 0.5f, zb - za - 4f), deck);
        // ramps: ground->5 along the south side going east, 5->10 along the north side going west, 10->15 south side east
        GarageRamp(k, root, new Vector3(x0 + 1f, 0f, za + 2f), new Vector3(x1 - 1f, 5f, za + 2f), 4f, deck);
        GarageRamp(k, root, new Vector3(x1 - 1f, 5f, zb - 2f), new Vector3(x0 + 1f, 10f, zb - 2f), 4f, deck);
        GarageRamp(k, root, new Vector3(x0 + 1f, 10f, za + 2f), new Vector3(x1 - 1f, 15f, za + 2f), 4f, deck);
        // columns, and painted bays
        foreach (var cx in new[] { x0 + 0.4f, (x0 + x1) / 2f, x1 - 0.4f })
            foreach (var cz in new[] { za + 4.4f, zb - 4.4f })
                SolidBox(k, root, new Vector3(cx, 7.5f, cz), new Vector3(0.7f, 15f, 0.7f), CTrim);
        foreach (var y in new[] { 5f, 10f, 15f })
            for (float x = x0 + 2f; x < x1 - 1f; x += 2.6f)
                k.Box(new Vector3(x, y + 0.005f, (za + zb) / 2f), new Vector3(0.12f, 0.01f, 5f), Quaternion.identity, CTrim);
        // a low wall round the roof, with a grindable top (open on the south side, where the ramp comes up)
        Parapet(k, root, Rect.MinMaxRect(x0, za + 4f, x1, zb), 15f, 0.9f, 0);
        // a big sign on the roof
        k.Box(new Vector3(x1 - 2f, 18f, (za + zb) / 2f), new Vector3(0.3f, 4f, 10f), Quaternion.identity, new Color32(110, 214, 236, 255));
        k.Box(new Vector3(x1 - 1.8f, 18f, (za + zb) / 2f), new Vector3(0.05f, 2.4f, 2.4f), Quaternion.identity, CTrim);
        AddBox(root, "Sign", new Vector3(x1 - 2f, 18f, (za + zb) / 2f), new Vector3(0.3f, 4f, 10f));
        Solid("Parking garage", root, k, toonMat, false);
        cityLots.Add(GarageLot); cityLotHeights.Add(15f);
        TowerCheckpoint(root, new Vector3(x0 + 3f, 15f, zb - 6f), "Garage roof (15 m)");

        // the power line from the garage roof up to the roof of the building next to the tower (22 m)
        PowerPole(root, new Vector3(x0 + 3f, 15f, zb - 1.5f), 2.2f);
        var cable = Catenary(new Vector3(x0 + 3f, 16.5f, zb - 1.5f), new Vector3(BLot.xMin + 3f, 23.4f, BLot.yMin + 3f), 0.5f, 12);
        Rail("Power line", cable, false);
        PowerPole(root, new Vector3(BLot.xMin + 3f, 22f, BLot.yMin + 3f), 2f);
    }

    static void GarageRamp(SkateMeshKit k, Transform root, Vector3 from, Vector3 to, float width, Color32 col)
    {
        Vector3 d = to - from, mid = (from + to) / 2f;
        float len = d.magnitude;
        Quaternion q = Quaternion.LookRotation(d.normalized, Vector3.up);
        Vector3 c = mid + q * new Vector3(0f, -0.25f, 0f);
        SolidBox(k, root, c, new Vector3(width, 0.5f, len + 0.6f), col, q);
        for (float t = 0.05f; t < 1f; t += 0.08f)   // ribs, so it reads as a ramp
            k.Box(Vector3.Lerp(from, to, t) + q * new Vector3(0f, 0.01f, 0f), new Vector3(width, 0.02f, 0.15f), q, CTrim);
    }

    // ---- the building beside the tower (B), its billboard frame, and the alley between them ---------

    static readonly Rect BLot = Rect.MinMaxRect(64f, 44f, 88f, 70f);
    // C and D: the rooftops north of the station, on the train route
    static readonly Rect CLot = Rect.MinMaxRect(8f, 13f, 32f, 36f), DLot = Rect.MinMaxRect(8f, 39f, 36f, 62f);

    static void SpecialBuildings(Transform root, SkateMeshKit k)
    {
        // B: 22 m, 2 m from the tower's east face (wall-jump up between them)
        Building(k, root, BLot, 22f, CWalls[3], false, true);
        Parapet(k, root, BLot, 22f, 0.5f);
        cityLots.Add(BLot); cityLotHeights.Add(22f);
        // its billboard frame, at the tower end of the roof: cross-bars every 1.4 m to climb, up to 30 m
        BillboardFrame(k, root, new Vector3(BLot.xMin + 1.2f, 22f, 51f), 31f, Quaternion.LookRotation(Vector3.left));
        TowerCheckpoint(root, new Vector3(BLot.xMin + 6f, 22f, BLot.yMin + 8f), "Rooftop (22 m)");

        // C: 13.5 m, just north of the station canopy; its roof clutter steps up towards D
        Building(k, root, CLot, 13.5f, CWalls[1], true, true);
        Parapet(k, root, CLot, 13.5f, 0.5f);
        cityLots.Add(CLot); cityLotHeights.Add(13.5f);
        for (int i = 0; i < 3; i++)
            SolidBox(k, root, new Vector3(20f + i * 3.2f, 13.5f + 1.4f * (i + 1) / 2f, CLot.yMax - 1.5f - (2 - i) * 2.2f), new Vector3(2.4f, 1.4f * (i + 1), 1.8f), new Color32(236, 236, 246, 255));
        TowerCheckpoint(root, new Vector3(12f, 13.5f, 17f), "Rooftop (13.5 m)");
        // D: 18 m, across the alley north of C; its sign frame climbs to 30 m, beside the tower's west face
        Building(k, root, DLot, 18f, CWalls[5], true, true);
        Parapet(k, root, DLot, 18f, 0.5f);
        cityLots.Add(DLot); cityLotHeights.Add(18f);
        BillboardFrame(k, root, new Vector3(DLot.xMax - 1.2f, 18f, 51f), 31f, Quaternion.LookRotation(Vector3.right));
    }

    // a big sign frame on a roof: two uprights and cross-bars every 1.4 m you can jump up, the sign on the back
    static void BillboardFrame(SkateMeshKit k, Transform root, Vector3 baseCentre, float topY, Quaternion facing)
    {
        Vector3 right = facing * Vector3.right, back = facing * Vector3.back;
        float h = topY - baseCentre.y;
        foreach (var s in new[] { -3.6f, 3.6f })
            SolidBox(k, root, baseCentre + right * s + Vector3.up * h / 2f, new Vector3(0.4f, h, 0.4f), CPole, facing);
        int bar = 0;
        for (float y = 1.4f; y <= h + 0.01f; y += 1.4f, bar++)
        {
            float off = bar % 2 == 0 ? -1.6f : 1.6f;   // alternate sides, so it's a climb rather than a ladder
            SolidBox(k, root, baseCentre + right * off + Vector3.up * (y - 0.1f), new Vector3(4f, 0.2f, 1.6f), CPole, facing);
        }
        // the sign itself, behind the bars
        k.Box(baseCentre + back * 1.1f + Vector3.up * (h * 0.6f), new Vector3(7.6f, h * 0.7f, 0.2f), facing, Pick(CSigns));
        k.Box(baseCentre + back * 1.0f + Vector3.up * (h * 0.6f), new Vector3(5f, h * 0.35f, 0.05f), facing, CTrim);
        AddBox(root, "Billboard", baseCentre + back * 1.1f + Vector3.up * (h * 0.6f), new Vector3(7.6f, h * 0.7f, 0.2f), facing);
    }

    // ---- buildings ----------------------------------------------------------------------------------

    // a box of a building with windows on every side (some lit), a shopfront with an awning (a ledge
    // you can get up on) on the ground floor, and a ring of trim at the top
    static void Building(SkateMeshKit k, Transform root, Rect lot, float h, Color32 wall, bool shop, bool roofStuff)
    {
        Vector3 c = new Vector3(lot.center.x, h / 2f, lot.center.y);
        k.Box(c, new Vector3(lot.width, h, lot.height), Quaternion.identity, wall);
        AddBox(root, "Building", c, new Vector3(lot.width, h, lot.height));
        k.Box(new Vector3(c.x, h - 0.25f, c.z), new Vector3(lot.width + 0.2f, 0.5f, lot.height + 0.2f), Quaternion.identity, CTrim);
        // windows: rows from the first floor up, on all four sides
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side % 2 == 0;
            float span = alongX ? lot.width : lot.height;
            float sgn = side < 2 ? 1f : -1f;
            Vector3 n = alongX ? new Vector3(0f, 0f, sgn) : new Vector3(sgn, 0f, 0f);
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 faceC = new Vector3(c.x, 0f, c.z) + n * ((alongX ? lot.height : lot.width) / 2f + 0.02f);
            int cols = Mathf.Max(1, Mathf.FloorToInt((span - 2f) / 2.6f));
            for (float y = 4.2f; y < h - 1.2f; y += 3.2f)
                for (int i = 0; i < cols; i++)
                {
                    float t = (i + 0.5f) / cols - 0.5f;
                    Vector3 p = faceC + along * (t * (span - 2f)) + Vector3.up * y;
                    k.Box(p, alongX ? new Vector3(1.5f, 1.6f, 0.04f) : new Vector3(0.04f, 1.6f, 1.5f), Quaternion.identity, crng.NextDouble() < 0.25 ? CLit : CGlass);
                }
            // shopfront: a coloured fascia, a big window, and an awning sticking out over the pavement
            if (shop && h > 5f)
            {
                Color32 sc = Pick(CSigns);
                k.Box(faceC + Vector3.up * 3.4f + n * 0.05f, alongX ? new Vector3(span - 0.4f, 0.6f, 0.1f) : new Vector3(0.1f, 0.6f, span - 0.4f), Quaternion.identity, sc);
                k.Box(faceC + Vector3.up * 1.4f + n * 0.01f, alongX ? new Vector3(span - 2f, 2.2f, 0.04f) : new Vector3(0.04f, 2.2f, span - 2f), Quaternion.identity, CGlass);
                Vector3 aw = faceC + n * 0.8f + Vector3.up * 3.05f;
                Vector3 awSize = alongX ? new Vector3(span * 0.6f, 0.2f, 1.6f) : new Vector3(1.6f, 0.2f, span * 0.6f);
                SolidBox(k, root, aw, awSize, Pick(CWalls));
                for (int st = 0; st < 6; st++)   // stripes on the awning
                {
                    float t = (st + 0.5f) / 6f - 0.5f;
                    k.Box(aw + along * (t * span * 0.6f) + Vector3.up * 0.11f, alongX ? new Vector3(span * 0.05f, 0.02f, 1.6f) : new Vector3(1.6f, 0.02f, span * 0.05f), Quaternion.identity, sc);
                }
                // an air-con unit or two higher up the wall: ledges, if you can get to them
                if (h > 8f && crng.NextDouble() < 0.6)
                    for (float y = 5.6f; y < Mathf.Min(h - 1f, 13f); y += 2.8f)
                        SolidBox(k, root, faceC + n * 0.35f + along * R(-span * 0.3f, span * 0.3f) + Vector3.up * y, alongX ? new Vector3(1f, 0.7f, 0.7f) : new Vector3(0.7f, 0.7f, 1f), new Color32(240, 240, 248, 255));
            }
            // a vertical sign down the corner on some
            if (h > 9f && crng.NextDouble() < 0.4)
            {
                Vector3 sp = faceC + along * (span / 2f - 0.8f) + n * 0.6f + Vector3.up * (h * 0.55f);
                Vector3 ss = alongX ? new Vector3(0.9f, h * 0.45f, 0.9f) : new Vector3(0.9f, h * 0.45f, 0.9f);
                k.Box(sp, ss, Quaternion.identity, Pick(CSigns));
                for (float y = -h * 0.2f; y < h * 0.2f; y += 1.2f)
                    k.Box(sp + Vector3.up * y + n * 0.46f, alongX ? new Vector3(0.6f, 0.6f, 0.02f) : new Vector3(0.02f, 0.6f, 0.6f), Quaternion.identity, CTrim);
            }
        }
        if (roofStuff) RoofClutter(k, root, lot, h);
    }

    // water tanks, air-con boxes and a spinning fan or two on a roof
    static void RoofClutter(SkateMeshKit k, Transform root, Rect lot, float h)
    {
        if (lot.width < 8f || lot.height < 8f) return;
        Vector3 t = new Vector3(R(lot.xMin + 3f, lot.xMax - 3f), h, R(lot.yMin + 3f, lot.yMax - 3f));
        SolidBox(k, root, t + Vector3.up * 0.7f, new Vector3(2.2f, 1.4f, 1.6f), new Color32(236, 236, 246, 255));
        if (crng.NextDouble() < 0.6)
        {
            Vector3 w = new Vector3(R(lot.xMin + 3f, lot.xMax - 3f), h, R(lot.yMin + 3f, lot.yMax - 3f));
            foreach (var o in new[] { new Vector3(-0.8f, 0f, -0.8f), new Vector3(0.8f, 0f, -0.8f), new Vector3(-0.8f, 0f, 0.8f), new Vector3(0.8f, 0f, 0.8f) })
                k.Cylinder(w + o, w + o + Vector3.up * 1.6f, 0.08f, 6, CPole);
            k.Cylinder(w + Vector3.up * 1.6f, w + Vector3.up * 3.6f, 1.3f, 16, new Color32(214, 232, 250, 255));
            k.Cone(w + Vector3.up * 3.6f, 1.3f, 0f, 0.5f, 16, new Color32(214, 232, 250, 255), 0f);
            var cap = new GameObject("Water tank").AddComponent<CapsuleCollider>();
            cap.transform.SetParent(root, false);
            cap.transform.position = w + Vector3.up * 2.6f;
            cap.radius = 1.3f; cap.height = 2.6f;
        }
    }

    // a low wall round a roof (or deck) edge, with a grindable rail along its top
    static void Parapet(SkateMeshKit k, Transform root, Rect lot, float y, float h, int skip = -1)
    {
        Vector3[] corners = { new Vector3(lot.xMin, 0f, lot.yMin), new Vector3(lot.xMax, 0f, lot.yMin), new Vector3(lot.xMax, 0f, lot.yMax), new Vector3(lot.xMin, 0f, lot.yMax) };
        for (int i = 0; i < 4; i++)
        {
            if (i == skip) continue;
            Vector3 a = corners[i], b = corners[(i + 1) % 4];
            Vector3 dir = (b - a).normalized, inward = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = (a + b) / 2f + inward * 0.15f + Vector3.up * (y + h / 2f);
            Quaternion q = Quaternion.LookRotation(dir);
            SolidBox(k, root, mid, new Vector3(0.3f, h, Vector3.Distance(a, b)), CTrim, q);
            Rail("Roof edge", new[] { a + inward * 0.15f + Vector3.up * (y + h + 0.02f) + dir * 0.3f, b + inward * 0.15f + Vector3.up * (y + h + 0.02f) - dir * 0.3f }, false);
        }
    }

    // the four blocks inside the ring: shop streets of buildings with alleys between them
    static void Blocks(Transform city, Material shiny)
    {
        var root = new GameObject("Blocks").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        SpecialBuildings(root, k);

        // keep clear of the special places
        var keep = new List<Rect> { Rect.MinMaxRect(36f, 36f, 92f, 92f), GarageLot, BLot, CLot, DLot,
                                    Rect.MinMaxRect(-64f, 16f, -14f, 54f),   // the skate plaza
                                    Rect.MinMaxRect(-92f, -14f, 92f, 14f) };  // the boulevard
        float[] heights = { 4.6f, 6f, 8f, 10f, 12f, 14f, 16f, 20f };
        for (int qx = -1; qx <= 1; qx += 2)
            for (int qz = -1; qz <= 1; qz += 2)
            {
                // lots along each quadrant in rows, 3 m alleys between (narrow enough to wall-jump up)
                float xa = qx > 0 ? 8f : -InnerEdge + 1f, xb = qx > 0 ? InnerEdge - 1f : -8f;
                float za = qz > 0 ? 14f : -InnerEdge + 1f, zb = qz > 0 ? InnerEdge - 1f : -14f;
                for (float x = xa; x < xb - 6f; )
                {
                    float w = Mathf.Min(R(9f, 16f), xb - x);
                    for (float z = za; z < zb - 6f; )
                    {
                        float d = Mathf.Min(R(9f, 18f), zb - z);
                        var lot = Rect.MinMaxRect(x, z, x + w, z + d);
                        bool clear = true;
                        foreach (var r in keep) if (r.Overlaps(lot)) { clear = false; break; }
                        // leave the odd lot empty: a little square with trees and vending machines
                        if (clear && crng.NextDouble() < 0.12) { PocketPark(k, root, lot); clear = false; }
                        if (clear)
                        {
                            // buildings facing the ring road are low shops (a bus roof gets you up); taller further in
                            bool front = Mathf.Max(Mathf.Abs(lot.xMax), Mathf.Abs(lot.xMin), Mathf.Abs(lot.yMax), Mathf.Abs(lot.yMin)) > InnerEdge - 3f;
                            float h = front ? (crng.NextDouble() < 0.6 ? 4.6f : 8f) : Pick(heights);
                            Building(k, root, lot, h, Pick(CWalls), true, true);
                            Parapet(k, root, lot, h, 0.5f);
                            cityLots.Add(lot); cityLotHeights.Add(h);
                            // low shops get an air-con box on the roof edge facing the ring: a step up towards the power lines
                            if (front && h < 5f)
                            {
                                Vector3 toRing = Mathf.Abs(lot.center.x) > Mathf.Abs(lot.center.y) ? new Vector3(Mathf.Sign(lot.center.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(lot.center.y));
                                Vector3 at = new Vector3(lot.center.x, h, lot.center.y) + Vector3.Scale(toRing, new Vector3(lot.width / 2f - 1.2f, 0f, lot.height / 2f - 1.2f));
                                SolidBox(k, root, at + Vector3.up * 0.7f, new Vector3(1.8f, 1.4f, 1.8f), new Color32(236, 236, 246, 255));
                            }
                        }
                        z += d + 3f;
                    }
                    x += w + 3f;
                }
            }
        Solid("Buildings", root, k, toonMat, false);
    }

    static void PocketPark(SkateMeshKit k, Transform root, Rect lot)
    {
        Vector3 c = new Vector3(lot.center.x, Kerb, lot.center.y);
        // a cherry tree
        k.Cylinder(c, c + Vector3.up * 2.4f, 0.25f, 8, new Color32(170, 130, 150, 255));
        k.Ellipsoid(c + Vector3.up * 3.6f, new Vector3(2.6f, 1.8f, 2.6f), Quaternion.identity, 12, 8, new Color32(255, 200, 220, 255));
        k.Ellipsoid(c + new Vector3(1.4f, 3.2f, 0.6f), new Vector3(1.6f, 1.2f, 1.6f), Quaternion.identity, 10, 6, new Color32(255, 214, 230, 255));
        var cap = new GameObject("Tree").AddComponent<CapsuleCollider>();
        cap.transform.SetParent(root, false);
        cap.transform.position = c + Vector3.up * 1.2f; cap.radius = 0.3f; cap.height = 2.4f;
        // a row of vending machines and a bench
        for (int i = 0; i < 3; i++)
            VendingMachine(k, root, new Vector3(lot.xMin + 1.2f + i * 1.1f, Kerb, lot.yMin + 0.6f), Quaternion.identity);
        SolidBox(k, root, new Vector3(c.x, Kerb + 0.25f, lot.yMax - 1.5f), new Vector3(3f, 0.5f, 0.6f), new Color32(250, 214, 226, 255));
        Rail("Bench edge", new[] { new Vector3(c.x - 1.4f, Kerb + 0.52f, lot.yMax - 1.2f), new Vector3(c.x + 1.4f, Kerb + 0.52f, lot.yMax - 1.2f) }, false);
    }

    static void VendingMachine(SkateMeshKit k, Transform root, Vector3 at, Quaternion q)
    {
        Color32 body = Pick(new[] { new Color32(240, 92, 104, 255), new Color32(110, 170, 236, 255), new Color32(250, 250, 255, 255), new Color32(124, 210, 152, 255) });
        SolidBox(k, root, at + Vector3.up * 0.95f, new Vector3(1f, 1.9f, 0.8f), body, q);
        k.Box(at + q * new Vector3(0f, 1.25f, -0.41f), new Vector3(0.8f, 0.8f, 0.02f), q, CGlass);
        for (int i = 0; i < 4; i++)
            k.Box(at + q * new Vector3(-0.3f + i * 0.2f, 1.25f, -0.43f), new Vector3(0.12f, 0.5f, 0.02f), q, Pick(CSigns));
    }

    // ---- the skate plaza (north-west block) --------------------------------------------------------

    static void SkatePlaza(Transform city)
    {
        Quaternion east = Quaternion.LookRotation(Vector3.right), west = Quaternion.LookRotation(Vector3.left);
        Quaternion north = Quaternion.LookRotation(Vector3.forward);
        float y = Kerb;
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(-52f, y, 22f), north, Vector3.one), 4f, 24f, 3.2f, RampSide2);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(-30f, y, 22f), north, Vector3.one), 4f, 24f, 3.2f, RampSide2);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(-40f, y, 30f), north, Vector3.one), 7f);
        QuarterPipe("Quarterpipe", Matrix4x4.TRS(new Vector3(-40f, y, 47f), north, Vector3.one), 22f, 3f, 2.5f);
        Ledge("Ledge", new Vector3(-58f, y, 36f), north, 12f, 0.5f, 1f, Concrete);
        Ledge("Ledge", new Vector3(-20f, y, 36f), north, 12f, 0.45f, 1f, Concrete);
        Rail("Flat rail", new[] { new Vector3(-50f, y + 0.5f, 40f), new Vector3(-50f, y + 0.5f, 28f) });
        Rail("Kinked rail", new[] { new Vector3(-28f, y + 0.5f, 42f), new Vector3(-28f, y + 0.5f, 36f), new Vector3(-28f, y + 1.3f, 30f), new Vector3(-28f, y + 1.3f, 26f) });
    }

    // ---- outside the ring: tall buildings all round, a far skyline, and a snowy mountain -----------

    static void OuterRing(Transform city)
    {
        var root = new GameObject("Outer buildings").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        for (int side = 0; side < 4; side++)
        {
            for (float t = -OuterEdge - 40f; t < OuterEdge + 40f; )
            {
                float w = R(12f, 24f), d = R(14f, 30f), h = R(16f, 46f);
                Rect lot = side switch
                {
                    0 => Rect.MinMaxRect(t, OuterEdge + 1f, t + w, OuterEdge + 1f + d),
                    1 => Rect.MinMaxRect(OuterEdge + 1f, t, OuterEdge + 1f + d, t + w),
                    2 => Rect.MinMaxRect(t, -OuterEdge - 1f - d, t + w, -OuterEdge - 1f),
                    _ => Rect.MinMaxRect(-OuterEdge - 1f - d, t, -OuterEdge - 1f, t + w),
                };
                // where the railway passes out of the city, leave a gap
                bool rail = (side == 1 || side == 3) && lot.yMin < 8f && lot.yMax > -8f;
                if (!rail) Building(k, root, lot, h, Pick(CWalls), true, false);
                t += w + R(2f, 5f);
            }
        }
        Solid("Outer buildings", root, k, toonMat, false);

        // the far skyline: plain boxes fading into the haze, and a snowy mountain in the distance
        var f = new SkateMeshKit();
        for (int i = 0; i < 260; i++)
        {
            float a = R(0f, Mathf.PI * 2f), r = R(220f, 700f);
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float h = R(15f, 80f) * (r < 350f ? 1f : 1.3f);
            f.Box(p + Vector3.up * h / 2f, new Vector3(R(14f, 36f), h, R(14f, 36f)), Quaternion.Euler(0f, R(0f, 90f), 0f), Pick(CWalls));
        }
        Vector3 fuji = new Vector3(-600f, 0f, 1700f);
        f.Cone(fuji, 900f, 60f, 520f, 40, new Color32(196, 206, 240, 255), 0f);
        f.Cone(fuji + Vector3.up * 340f, 330f, 60f, 180f, 40, new Color32(252, 252, 255, 255), 0f);
        var mesh = f.Build("Skyline");
        SaveFeatureMesh(mesh);
        MeshObject("Skyline", city, mesh, toonMat, farOutlineMat);
    }

    // ---- utility poles round the ring with power lines between them, and lines up to rooftops -------

    // a short pole a power line starts or ends at (no collider, so it doesn't knock you off the line)
    static void PowerPole(Transform root, Vector3 foot, float h)
    {
        var k = new SkateMeshKit();
        k.Cylinder(foot, foot + Vector3.up * h, 0.14f, 8, CPole);
        k.Box(foot + Vector3.up * (h - 0.3f), new Vector3(1.6f, 0.12f, 0.12f), Quaternion.identity, CPole);
        Solid("Pole", root, k, toonMat, false);
    }

    // a sagging line between two points
    static Vector3[] Catenary(Vector3 a, Vector3 b, float sag, int segs)
    {
        var pts = new Vector3[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            pts[i] = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
        }
        return pts;
    }

    static void PowerLines(Transform city)
    {
        var root = new GameObject("Power lines").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        float d = InnerEdge + 2.6f, poleH = 8.6f, step = 22f;
        var poles = new List<Vector3>();
        // round the inside of the ring road, along the pavement
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            for (float t = -d; t < d - 1f; t += step)
            {
                Vector3 p = q * new Vector3(t, 0f, d);
                if (Mathf.Abs(p.x) < 14f && Mathf.Abs(p.z) > 80f) continue;   // not across the crossings
                if (Mathf.Abs(p.z) < 14f && Mathf.Abs(p.x) > 80f) continue;
                poles.Add(new Vector3(p.x, Kerb, p.z));
            }
        }
        foreach (var p in poles)
        {
            k.Cylinder(p, p + Vector3.up * poleH, 0.16f, 8, CPole);
            k.Box(p + Vector3.up * (poleH - 0.4f), new Vector3(1.8f, 0.14f, 0.14f), Quaternion.LookRotation(new Vector3(p.x, 0f, p.z).normalized), CPole);
            k.Cylinder(p + Vector3.up * (poleH - 1.6f), p + Vector3.up * (poleH - 0.8f), 0.32f, 10, new Color32(180, 180, 196, 255));   // transformer
            var cap = new GameObject("Pole").AddComponent<CapsuleCollider>();
            cap.transform.SetParent(root, false);
            cap.transform.position = p + Vector3.up * poleH / 2f; cap.radius = 0.18f; cap.height = poleH;
        }
        // two lines between each pair of neighbouring poles (grindable), sagging a little
        for (int i = 0; i < poles.Count; i++)
        {
            Vector3 a = poles[i], b = poles[(i + 1) % poles.Count];
            if (Vector3.Distance(a, b) > step * 1.5f) continue;   // (not across the crossings, or under the railway)
            foreach (var off in new[] { -0.6f, 0.6f })
            {
                Vector3 outward = new Vector3(a.x + b.x, 0f, a.z + b.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * off;
                Rail("Power line", Catenary(a + side + Vector3.up * (poleH - 0.4f), b + side + Vector3.up * (poleH - 0.4f), 0.5f, 10), false);
            }
        }
        // from some poles, a line up to the roof edge of the nearest building that's taller than the pole
        foreach (var p in poles)
        {
            float best = 18f; int bi = -1;
            for (int i = 0; i < cityLots.Count; i++)
            {
                if (cityLotHeights[i] < poleH + 1f || cityLotHeights[i] > poleH + 9f) continue;
                Rect r = cityLots[i];
                Vector2 q = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.z, r.yMin, r.yMax));
                float dist = Vector2.Distance(q, new Vector2(p.x, p.z));
                if (dist < best && dist > 3f) { best = dist; bi = i; }
            }
            if (bi < 0) continue;
            Rect lot = cityLots[bi];
            Vector2 edge = new Vector2(Mathf.Clamp(p.x, lot.xMin + 0.8f, lot.xMax - 0.8f), Mathf.Clamp(p.z, lot.yMin + 0.8f, lot.yMax - 0.8f));
            Rail("Line to a roof", Catenary(p + Vector3.up * (poleH - 0.4f), new Vector3(edge.x, cityLotHeights[bi] + 0.9f, edge.y), 0.4f, 10), false);
        }
        Solid("Poles", root, k, toonMat, false);
    }

    // ---- traffic: cars and buses going round the ring road ------------------------------------------

    static GameObject Vehicle(Transform root, string name, float length, float width, float height, Color32 body, Color32 stripe, bool bus)
    {
        var k = new SkateMeshKit();
        float wheelR = 0.38f;
        if (bus)
        {
            k.Box(new Vector3(0f, wheelR + height / 2f, 0f), new Vector3(width, height, length), Quaternion.identity, body);
            k.Box(new Vector3(0f, wheelR + height * 0.45f, 0f), new Vector3(width + 0.02f, height * 0.3f, length - 1f), Quaternion.identity, CGlass);
            k.Box(new Vector3(0f, wheelR + 0.6f, 0f), new Vector3(width + 0.03f, 0.35f, length), Quaternion.identity, stripe);
            k.Box(new Vector3(0f, wheelR + height + 0.1f, 0f), new Vector3(width - 0.4f, 0.2f, length - 1f), Quaternion.identity, CTrim);
        }
        else
        {
            k.Box(new Vector3(0f, wheelR + 0.45f, 0f), new Vector3(width, 0.9f, length), Quaternion.identity, body);
            k.Box(new Vector3(0f, wheelR + 1.25f, -0.2f), new Vector3(width - 0.2f, 0.75f, length * 0.55f), Quaternion.identity, CGlass);
            k.Box(new Vector3(0f, wheelR + 1.65f, -0.2f), new Vector3(width - 0.2f, 0.08f, length * 0.55f), Quaternion.identity, body);
            k.Box(new Vector3(0f, wheelR + 0.55f, length / 2f + 0.01f), new Vector3(width * 0.8f, 0.2f, 0.02f), Quaternion.identity, CLit);
        }
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                k.Cylinder(new Vector3(sx * width / 2f - sx * 0.1f, wheelR, sz * (length / 2f - 0.9f)), new Vector3(sx * width / 2f + sx * 0.05f, wheelR, sz * (length / 2f - 0.9f)), wheelR, 10, new Color32(90, 86, 110, 255));
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody), typeof(Mover));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        var bc = go.GetComponent<BoxCollider>();
        float top = bus ? wheelR + height + 0.2f : wheelR + 1.7f;
        bc.center = new Vector3(0f, (wheelR * 0.6f + top) / 2f, 0f);
        bc.size = new Vector3(width, top - wheelR * 0.6f, length);
        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        return go;
    }

    static Vector3[] RingLane(float d, bool clockwise)
    {
        var p = new[] { new Vector3(d, 0f, -d), new Vector3(d, 0f, d), new Vector3(-d, 0f, d), new Vector3(-d, 0f, -d) };
        if (clockwise) System.Array.Reverse(p);
        return p;
    }

    static void Traffic(Transform city)
    {
        var root = new GameObject("Traffic").transform;
        root.SetParent(city, false);
        float inner = RingD - 3.5f, outer = RingD + 3.5f;
        float loopIn = inner * 8f, loopOut = outer * 8f;
        // inner lane: cars, anticlockwise
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            var car = Vehicle(root, "Car", R(3.6f, 4.4f), 1.75f, 1.5f, Pick(CCars), CTrim, false);
            var m = car.GetComponent<Mover>();
            m.path = RingLane(inner, false); m.loop = true; m.speed = 11f; m.startAt = loopIn * i / n;
        }
        // outer lane: buses and cars, clockwise (buses are what you ride)
        int o = 15;
        for (int i = 0; i < o; i++)
        {
            bool bus = i % 5 == 0;
            var v = bus ? Vehicle(root, "Bus", 11f, 2.5f, 2.6f, new Color32(250, 250, 255, 255), new Color32(124, 210, 152, 255), true)
                        : Vehicle(root, "Car", R(3.6f, 4.4f), 1.75f, 1.5f, Pick(CCars), CTrim, false);
            var m = v.GetComponent<Mover>();
            m.path = RingLane(outer, true); m.loop = true; m.speed = bus ? 8f : 8f; m.startAt = loopOut * i / o;
        }
        // traffic lights at the four crossings, cycling green, yellow, red
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            foreach (var sx in new[] { -8f, 8f })
            {
                Vector3 p = q * new Vector3(sx, Kerb, InnerEdge + 3f);
                var k = new SkateMeshKit();
                k.Cylinder(p, p + Vector3.up * 4.6f, 0.1f, 8, CPole);
                k.Box(p + Vector3.up * 4.6f, new Vector3(0.5f, 1.4f, 0.4f), q, new Color32(90, 86, 110, 255));
                Solid("Traffic light", root, k, toonMat, false);
                var lamps = new GameObject("Lights", typeof(Blinker));
                lamps.transform.SetParent(root, false);
                var bl = lamps.GetComponent<Blinker>();
                bl.durations = new[] { 6f, 1.5f, 6f };
                bl.offset = side * 1.7f;
                Color32[] cols = { new Color32(120, 240, 170, 255), new Color32(255, 222, 110, 255), new Color32(255, 100, 110, 255) };
                for (int c = 0; c < 3; c++)
                {
                    var lk = new SkateMeshKit();
                    lk.Ellipsoid(p + Vector3.up * (5.05f - c * 0.45f) + q * new Vector3(0f, 0f, -0.22f), new Vector3(0.16f, 0.16f, 0.06f), q, 10, 6, cols[c]);
                    var lamp = Solid("Lamp", lamps.transform, lk, toonMat, false);
                }
            }
        }
    }

    // ---- the train: back and forth along the railway, stopping at the station -----------------------

    static void Train(Transform city)
    {
        var root = new GameObject("Train").transform;
        root.SetParent(city, false);
        var k = new SkateMeshKit();
        float carLen = 18f, gap = 1f, w = 2.9f, h = 3.4f, floor = ViaductTop + 0.35f;
        Color32 body = new Color32(250, 250, 255, 255), stripe = new Color32(255, 118, 168, 255);
        var go = new GameObject("Train", typeof(Rigidbody), typeof(Mover));
        go.transform.SetParent(root, false);
        for (int i = -1; i <= 1; i++)
        {
            float cz = i * (carLen + gap);
            k.Box(new Vector3(0f, h / 2f + 0.4f, cz), new Vector3(w, h, carLen), Quaternion.identity, body);
            k.Box(new Vector3(0f, 1.6f + 0.4f, cz), new Vector3(w + 0.02f, 0.9f, carLen - 1f), Quaternion.identity, CGlass);
            k.Box(new Vector3(0f, 0.9f + 0.4f, cz), new Vector3(w + 0.03f, 0.25f, carLen), Quaternion.identity, stripe);
            k.Box(new Vector3(0f, h + 0.45f, cz), new Vector3(w - 0.6f, 0.1f, carLen - 2f), Quaternion.identity, new Color32(220, 220, 236, 255));
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, (h + 0.4f) / 2f + 0.2f, cz);
            bc.size = new Vector3(w, h + 0.1f, carLen);
        }
        var mesh = k.Build("Train");
        SaveFeatureMesh(mesh);
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        var m = go.GetComponent<Mover>();
        // the train runs along the x axis; the model is built along z, so the path turns it to face +x
        m.path = new[] { new Vector3(-230f, floor - 0.4f, 2.2f), new Vector3(0f, floor - 0.4f, 2.2f), new Vector3(230f, floor - 0.4f, 2.2f) };
        m.loop = false; m.speed = 15f; m.wait = 6f; m.easeIn = 40f; m.turn = false;
        m.stops = new[] { 230f };
        go.transform.rotation = Quaternion.LookRotation(Vector3.right);
    }

    // ---- motion and life: blossom petals, spinning rooftop fans, flickering signs ----------------------

    static void CityDressing(Transform city, Material shiny)
    {
        // cherry trees along the boulevard
        var k = new SkateMeshKit();
        for (float x = -84f; x < 84f; x += 12f)
            foreach (var z in new[] { -10f, 10f })
            {
                if (Mathf.Abs(x) < 34f && z > 0f) continue;   // the station stairs
                Vector3 c = new Vector3(x + 3f, Kerb, z);
                k.Cylinder(c, c + Vector3.up * 2.4f, 0.22f, 8, new Color32(170, 130, 150, 255));
                k.Ellipsoid(c + Vector3.up * 3.5f, new Vector3(2.2f, 1.6f, 2.2f), Quaternion.identity, 12, 8, new Color32(255, 200, 220, 255));
                k.Ellipsoid(c + new Vector3(1.2f, 3.1f, 0.4f), new Vector3(1.3f, 1f, 1.3f), Quaternion.identity, 10, 6, new Color32(255, 214, 230, 255));
                var cap = new GameObject("Tree").AddComponent<CapsuleCollider>();
                cap.transform.SetParent(city, false);
                cap.transform.position = c + Vector3.up * 1.2f; cap.radius = 0.28f; cap.height = 2.4f;
            }
        // guard rails along the inside pavement, either side of each crossing (grindable)
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            foreach (var span in new[] { new Vector2(-InnerEdge, -10f), new Vector2(10f, InnerEdge) })
            {
                Vector3 a = q * new Vector3(span.x, Kerb + 0.8f, InnerEdge + 3.6f), b = q * new Vector3(span.y, Kerb + 0.8f, InnerEdge + 3.6f);
                Rail("Guard rail", new[] { a, b });
            }
        }
        // bus stops on the outside pavement: a shelter with a roof you can get on
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            Vector3 c = q * new Vector3(40f, Kerb, OuterEdge - 1.6f);
            SolidBox(k, city, c + Vector3.up * 2.6f, q * new Vector3(5f, 0.15f, 2f), new Color32(250, 214, 226, 255), q);
            foreach (var sx in new[] { -2.3f, 2.3f })
                SolidBox(k, city, c + q * new Vector3(sx, 1.3f, 0.8f), new Vector3(0.12f, 2.6f, 0.12f), CTrim, q);
            k.Box(c + q * new Vector3(0f, 1.4f, 0.9f), new Vector3(4.5f, 1.8f, 0.04f), q, CGlass);
            SolidBox(k, city, c + q * new Vector3(0f, 0.25f, 0.5f), new Vector3(3f, 0.5f, 0.5f), CTrim, q);
            VendingMachine(k, city, c + q * new Vector3(4f, 0f, 0.6f), q);
        }
        Solid("Street things", city, k, toonMat, false);

        // spinning fans on some roofs, and flickering signs
        for (int i = 0; i < cityLots.Count; i++)
        {
            if (crng.NextDouble() > 0.35) continue;
            Rect lot = cityLots[i];
            float h = cityLotHeights[i];
            var fk = new SkateMeshKit();
            for (int b = 0; b < 3; b++)
                fk.Box(Vector3.zero, new Vector3(0.18f, 0.05f, 1.4f), Quaternion.Euler(0f, b * 60f, 0f), new Color32(220, 220, 236, 255));
            var fan = Solid("Fan", city, fk, toonMat, false);
            fan.transform.position = new Vector3(R(lot.xMin + 1.5f, lot.xMax - 1.5f), h + 1.2f, R(lot.yMin + 1.5f, lot.yMax - 1.5f));
            var sp = fan.AddComponent<Spin>(); sp.degreesPerSecond = R(200f, 500f); sp.bob = 0f;
            if (h > 9f && crng.NextDouble() < 0.6)
            {
                // a neon sign on the roof edge, flickering between two colours
                var sign = new GameObject("Neon", typeof(Blinker));
                sign.transform.SetParent(city, false);
                var bl = sign.GetComponent<Blinker>();
                bl.durations = new[] { R(0.4f, 1.6f), R(0.1f, 0.5f) };
                bl.offset = R(0f, 3f);
                Vector3 at = new Vector3(lot.center.x, h + 1.4f, lot.yMin + 0.3f);
                foreach (var col in new[] { Pick(CSigns), Pick(CSigns) })
                {
                    var nk = new SkateMeshKit();
                    nk.Box(at, new Vector3(Mathf.Min(6f, lot.width - 2f), 1.6f, 0.2f), Quaternion.identity, col);
                    nk.Box(at + Vector3.back * 0.11f, new Vector3(Mathf.Min(4.6f, lot.width - 3f), 0.8f, 0.02f), Quaternion.identity, CTrim);
                    Solid("Neon", sign.transform, nk, shiny, false);
                }
            }
        }

        // blossom petals drifting over the whole city
        var go = new GameObject("Petals", typeof(ParticleSystem));
        go.transform.SetParent(city, false);
        go.transform.position = new Vector3(0f, 18f, 0f);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.9f), new Color(1f, 0.94f, 0.97f));
        main.gravityModifier = 0.03f;
        main.maxParticles = 2500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 220f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(220f, 30f, 220f);
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.6f); vel.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = Mat("Sparks", "Sprites/Default");
    }

    // ---- review shots ---------------------------------------------------------------------------------

    public static void CityShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) { m.Init(); for (int i = 0; i < 40; i++) m.Step(0.05f); }
        Physics.SyncTransforms();
        Shot(cam, new Vector3(-72f, 1.6f, -7f), new Vector3(0f, 6f, 0f), "c01-start.png");
        Shot(cam, new Vector3(-150f, 70f, -150f), new Vector3(10f, 0f, 10f), "c02-overview.png");
        Shot(cam, new Vector3(20f, 3f, -11f), new Vector3(51f, 32f, 51f), "c03-goal-tower.png");
        Shot(cam, new Vector3(60f, 2f, 2f), new Vector3(76f, 10f, 25f), "c04-garage.png");
        Shot(cam, new Vector3(-60f, 2f, -96f), new Vector3(-10f, 3f, -100f), "c05-ring-road.png");
        Shot(cam, new Vector3(-10f, 12f, -14f), new Vector3(20f, 10f, 4f), "c06-station.png");
        Shot(cam, new Vector3(-62f, 3f, 14f), new Vector3(-38f, 1f, 36f), "c07-skate-plaza.png");
        Shot(cam, new Vector3(63f, 24f, 30f), new Vector3(63f, 18f, 60f), "c08-alley-and-billboard.png");
    }
}
