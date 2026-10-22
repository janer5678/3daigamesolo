using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sketchbook: after a pencil drawing of a courtyard washed in lime-green watercolour. Two huge pillars frame the
// way in; inside, wavy black-and-white Portuguese paving, a tall palm in a round planter in the middle (and
// more round the sides), a little old car parked with a plank up onto it, and an ornate old building with an
// arched door up a flight of steps. An arcade runs down the left side, a bank up the right. It's all pencil on
// paper (SkateSketch.shader) until you skate on it: the floor gets washed green a patch at a time as you ride
// over it, and anything you ride on or grind (the car, the planters, the steps, the ledges...) all at once
// (SketchLevel). Colour in the whole page.
public static partial class SkateWorldBuilder
{
    public const string SketchPath = "Assets/Scenes/Sketchbook.unity";

    public const float SkX0 = -60f, SkX1 = 60f, SkZ0 = -72f, SkZ1 = 48f, SkCell = 8f;
    public static readonly Vector3 SkCar = new Vector3(-28f, 0f, -6f);   // where the plank up onto the car starts
    public static readonly Vector3 SkPalmAt = new Vector3(6f, 0f, 10f);
    public const float SkPlanterR = 3.2f;

    static readonly Color32 SkWhite = new Color32(250, 248, 240, 255), SkStone = new Color32(214, 208, 196, 255), SkStoneDark = new Color32(150, 144, 134, 255);
    static readonly Color32 SkWindow = new Color32(110, 140, 190, 255), SkDoor = new Color32(70, 66, 62, 255), SkWood = new Color32(186, 150, 104, 255);
    static readonly Color32 SkLeaf = new Color32(120, 170, 70, 255), SkTrunk = new Color32(150, 120, 90, 255), SkCarBody = new Color32(236, 236, 230, 255);
    static readonly Color32 SkTyre = new Color32(40, 40, 42, 255), SkLamp = new Color32(240, 150, 60, 255), SkGrass = new Color32(150, 200, 90, 255);

    static Transform skRoot;
    static readonly List<Transform> skThings = new List<Transform>();

    public static void SketchFromCommandLine()
    {
        try { BuildSketch(); SlideTests.RunSketch(); SketchShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildSketch()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/SketchFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear(); skThings.Clear();
        floorAt = (x, z) => 0f;
        RampTop = SkWhite; RampSide = SkStone; RampSide2 = SkStone;
        Metal = SkStoneDark; RailCol = SkStoneDark; Concrete = SkStone; ConcreteDark = SkStoneDark;

        toonMat = Mat("Sketch", "3daigamesolo/World/Sketch");
        var paving = Mat("SketchPaving", "3daigamesolo/World/Sketch");
        paving.SetFloat("_Waves", 0.55f);
        outlineMat = Mat("SketchOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.18f, 0.18f, 0.2f));
        outlineMat.SetFloat("_OutlineWidth", 0.9f);
        outlineMat.SetColor("_Haze", new Color(0.97f, 0.96f, 0.92f)); outlineMat.SetColor("_HazeHigh", new Color(0.97f, 0.96f, 0.92f));
        outlineMat.SetFloat("_FogDensity", 0.004f);
        farOutlineMat = outlineMat;
        var post = Mat("SketchPost", "3daigamesolo/Post/Sketch");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;
        skRoot = new GameObject("Sketchbook").transform;

        var floor = SkFloor(paving, out var patches, out int cells, out int rows);
        SkBuildingNorth();
        SkArcade();
        SkEastSide();
        SkPillars();
        SkPalms();
        SkCarAndPlank();
        SkFeatures();

        // you, out in the street, looking in between the pillars at the palm and the door
        Vector3 startAt = new Vector3(0f, 0.2f, -62f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt, Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(SketchLevel));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 2000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.97f, 0.96f, 0.92f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "Sketchbook";
        camGO.transform.SetPositionAndRotation(startAt + new Vector3(0f, 2.5f, -4f), Quaternion.LookRotation(Vector3.forward));
        player.board = BuildBoard(startAt + Vector3.forward * 2f, Quaternion.LookRotation(Vector3.forward));

        // everything that can be coloured in: each feature, and each group of the courtyard's things
        foreach (Transform f in featureRoot) skThings.Add(f);
        var things = new List<Paintable>();
        foreach (var t in skThings) if (t.GetComponent<Paintable>() == null) things.Add(t.gameObject.AddComponent<Paintable>());
        var level = camGO.GetComponent<SketchLevel>();
        level.player = player; level.sketch = toonMat; level.post = post;
        level.floor = floor; level.patches = patches; level.cells = cells; level.rows = rows;
        level.corner = new Vector2(SkX0, SkZ0); level.cell = SkCell;
        level.things = things.ToArray();

        EditorSceneManager.SaveScene(scene, SketchPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_SKETCH_BUILT features=" + featureCount + " things=" + things.Count + " patches=" + patches.Length);
    }

    // a group of the courtyard's things that's coloured in all at once
    static Transform SkGroup(string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(skRoot, false);
        skThings.Add(g);
        return g;
    }

    static GameObject SkSolid(string name, Transform parent, SkateMeshKit k, Material mat = null)
    {
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        return MeshObject(name, parent, mesh, mat != null ? mat : toonMat, outlineMat);
    }

    // ---- the floor: patches of paving, each coloured in on its own ------------------------------------------

    static Collider SkFloor(Material paving, out Renderer[] patches, out int cells, out int rows)
    {
        cells = Mathf.RoundToInt((SkX1 - SkX0) / SkCell); rows = Mathf.RoundToInt((SkZ1 - SkZ0) / SkCell);
        var list = new List<Renderer>();
        var parent = new GameObject("Paving").transform;
        parent.SetParent(skRoot, false);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cells; c++)
            {
                float x0 = SkX0 + c * SkCell, z0 = SkZ0 + r * SkCell;
                var k = new SkateMeshKit();
                // a few quads each way, so the outline shader has something to hold on to at the patch's edge
                int sub = 2;
                for (int a = 0; a < sub; a++)
                    for (int b = 0; b < sub; b++)
                    {
                        float s = SkCell / sub, xa = x0 + a * s, za = z0 + b * s;
                        k.Quad(new Vector3(xa, 0f, za), new Vector3(xa, 0f, za + s), new Vector3(xa + s, 0f, za + s), new Vector3(xa + s, 0f, za), Vector3.up, SkWhite);
                    }
                var mesh = k.Build("Paving " + c + "," + r);
                SaveFeatureMesh(mesh);
                var go = MeshObject("Paving " + c + "," + r, parent, mesh, paving, null);
                list.Add(go.GetComponent<Renderer>());
            }
        patches = list.ToArray();
        var col = AddBox(skRoot, "Floor", new Vector3((SkX0 + SkX1) * 0.5f, -0.5f, (SkZ0 + SkZ1) * 0.5f), new Vector3(SkX1 - SkX0, 1f, SkZ1 - SkZ0));
        // walls all round (the buildings, and a low wall across the street)
        AddBox(skRoot, "Edge", new Vector3(0f, 10f, SkZ0 - 1f), new Vector3(SkX1 - SkX0 + 4f, 30f, 2f));
        var wall = new SkateMeshKit();
        wall.Box(new Vector3(0f, 0.4f, SkZ0 + 0.5f), new Vector3(SkX1 - SkX0, 0.8f, 1f), Quaternion.identity, SkStone);
        var sw = SkGroup("Street wall");
        SkSolid("Street wall", sw, wall);
        AddBox(sw, "Street wall", new Vector3(0f, 0.4f, SkZ0 + 0.5f), new Vector3(SkX1 - SkX0, 0.8f, 1f));
        return col;
    }

    // ---- the old building across the back, with its arched door up a flight of steps --------------------------

    static void SkBuildingNorth()
    {
        var g = SkGroup("Old building");
        var k = new SkateMeshKit();
        float front = SkZ1, w = SkX1 - SkX0, top = 17f;
        k.Box(new Vector3(0f, top * 0.5f, front + 7f), new Vector3(w, top, 14f), Quaternion.identity, SkWhite);
        AddBox(g, "Building", new Vector3(0f, top * 0.5f, front + 7f), new Vector3(w, top, 14f));
        // cornices along each floor, pilasters between the bays, windows with shutters
        foreach (float y in new[] { 5.6f, 10.8f, 16.2f })
            k.Box(new Vector3(0f, y, front - 0.3f), new Vector3(w, 0.5f, 0.6f), Quaternion.identity, SkStone);
        for (float x = SkX0 + 6f; x < SkX1; x += 12f)
            k.Box(new Vector3(x, top * 0.5f, front - 0.2f), new Vector3(1f, top, 0.4f), Quaternion.identity, SkStone);
        for (float x = SkX0 + 12f; x < SkX1 - 5f; x += 12f)
            foreach (float y in new[] { 2.8f, 8f, 13.3f })
            {
                if (Mathf.Abs(x) < 7f && y < 9f) continue;   // (the door)
                k.Box(new Vector3(x, y, front - 0.05f), new Vector3(2.4f, 3.2f, 0.12f), Quaternion.identity, SkWindow);
                k.Box(new Vector3(x, y + 1.8f, front - 0.25f), new Vector3(3.2f, 0.35f, 0.5f), Quaternion.identity, SkStone);
                foreach (float s in new[] { -1f, 1f })
                    k.Box(new Vector3(x + s * 1.65f, y, front - 0.1f), new Vector3(0.8f, 3.2f, 0.16f), Quaternion.identity, SkStoneDark);
            }
        // the arched door: tall and dark, the arch round its top, carved scrolls either side
        k.Box(new Vector3(0f, 3.6f, front - 0.05f), new Vector3(5f, 4.2f, 0.12f), Quaternion.identity, SkDoor);
        k.Cylinder(new Vector3(0f, 5.7f, front - 0.06f), new Vector3(0f, 5.7f, front + 0.06f), 2.5f, 20, SkDoor);
        k.Cylinder(new Vector3(0f, 5.7f, front - 0.4f), new Vector3(0f, 5.7f, front - 0.1f), 3.1f, 20, SkStone);
        foreach (float s in new[] { -1f, 1f })
        {
            k.Box(new Vector3(s * 3.4f, 4f, front - 0.4f), new Vector3(0.9f, 8f, 0.8f), Quaternion.identity, SkStone);
            k.Ellipsoid(new Vector3(s * 5.2f, 5.5f, front - 0.2f), new Vector3(1.1f, 1.6f, 0.3f), Quaternion.identity, 12, 8, SkStoneDark);
        }
        // a balcony over the door
        k.Box(new Vector3(0f, 10.4f, front - 1f), new Vector3(10f, 0.35f, 2f), Quaternion.identity, SkStone);
        for (float x = -4.8f; x <= 4.81f; x += 0.6f) k.Box(new Vector3(x, 11.1f, front - 1.9f), new Vector3(0.1f, 1f, 0.1f), Quaternion.identity, SkStoneDark);
        k.Box(new Vector3(0f, 11.6f, front - 1.9f), new Vector3(10f, 0.12f, 0.16f), Quaternion.identity, SkStoneDark);
        SkSolid("Old building", g, k);

        // the portico and the steps up to it, with handrails
        var steps = SkGroup("Steps");
        var s2 = new SkateMeshKit();
        float landing = 1.5f, z0 = front - 7f, run = 0.6f;
        s2.Box(new Vector3(0f, landing * 0.5f, z0 + 3.5f), new Vector3(24f, landing, 7f), Quaternion.identity, SkStone);
        AddBox(steps, "Portico", new Vector3(0f, landing * 0.5f, z0 + 3.5f), new Vector3(24f, landing, 7f));
        for (int i = 0; i < 5; i++)
        {
            float h = 0.3f * (i + 1), zs = z0 - (5 - i) * run;
            s2.Box(new Vector3(0f, h * 0.5f, zs + (z0 - zs) * 0.5f), new Vector3(16f, h, z0 - zs), Quaternion.identity, i % 2 == 0 ? SkWhite : SkStone);
            AddBox(steps, "Step", new Vector3(0f, h * 0.5f, zs + (z0 - zs) * 0.5f), new Vector3(16f, h, z0 - zs));
        }
        SkSolid("Steps", steps, s2);
        foreach (float x in new[] { -7.6f, -2.5f, 2.5f, 7.6f })
            Rail("Handrail", new[] { new Vector3(x, 0.95f, z0 - 5f * run - 0.4f), new Vector3(x, landing + 0.95f, z0 + 0.3f) });
    }

    // ---- the arcade down the left: a raised walk under arches, its edge to grind --------------------------------

    static void SkArcade()
    {
        var g = SkGroup("Arcade");
        var k = new SkateMeshKit();
        float x0 = SkX0, x1 = SkX0 + 8f, h = 0.8f;
        k.Box(new Vector3((x0 + x1) * 0.5f, h * 0.5f, -16f), new Vector3(8f, h, 112f), Quaternion.identity, SkStone);
        AddBox(g, "Walk", new Vector3((x0 + x1) * 0.5f, h * 0.5f, -16f), new Vector3(8f, h, 112f));
        k.Box(new Vector3((x0 + x1) * 0.5f, 7.5f, -16f), new Vector3(8f, 1.2f, 112f), Quaternion.identity, SkWhite);
        k.Box(new Vector3(x0 - 6f, 9f, -16f), new Vector3(12f, 18f, 112f), Quaternion.identity, SkWhite);
        AddBox(g, "Building", new Vector3(x0 - 6f, 9f, -16f), new Vector3(12f, 18f, 112f));
        for (float z = -70f; z <= 38f; z += 6f)
        {
            k.Cylinder(new Vector3(x0 + 5.5f, h, z), new Vector3(x0 + 5.5f, 6.9f, z), 0.35f, 10, SkWhite);
            AddBox(g, "Column", new Vector3(x0 + 5.5f, 3.9f, z), new Vector3(0.7f, 6.2f, 0.7f));
            k.Cylinder(new Vector3(x0 + 5.5f, 5.5f, z + 0.4f), new Vector3(x0 + 5.5f, 5.5f, z + 5.6f), 0.25f, 6, SkStone, false);   // the arches' line
            k.Box(new Vector3(x0 - 0.05f, 3.2f, z + 3f), new Vector3(0.12f, 3.6f, 2.2f), Quaternion.identity, SkWindow);
        }
        SkSolid("Arcade", g, k);
        Rail("Arcade edge", new[] { new Vector3(x1 - 0.06f, h + 0.03f, -70f), new Vector3(x1 - 0.06f, h + 0.03f, 38f) }, false);
    }

    // ---- the right side: a bank up the wall of the building there ----------------------------------------------

    static void SkEastSide()
    {
        var g = SkGroup("East building");
        var k = new SkateMeshKit();
        k.Box(new Vector3(SkX1 + 6f, 9f, -16f), new Vector3(12f, 18f, 128f), Quaternion.identity, SkWhite);
        AddBox(g, "Building", new Vector3(SkX1 + 6f, 9f, -16f), new Vector3(12f, 18f, 128f));
        for (float z = -66f; z < 44f; z += 7f)
            foreach (float y in new[] { 7f, 12.5f })
                k.Box(new Vector3(SkX1 + 0.05f, y, z), new Vector3(0.12f, 3f, 2.2f), Quaternion.identity, SkWindow);
        SkSolid("East building", g, k);
        RampTop = SkWhite; RampSide = SkStone;
        QuarterPipe("Bank", Matrix4x4.TRS(new Vector3(SkX1 - 7.5f, 0f, -16f), Quaternion.LookRotation(Vector3.right), Vector3.one), 100f, 4f, 3.4f);
    }

    // ---- the two huge pillars framing the way in ------------------------------------------------------------------

    static void SkPillars()
    {
        var g = SkGroup("Pillars");
        var k = new SkateMeshKit();
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 c = new Vector3(s * 24f, 12f, -40f);
            k.Box(c, new Vector3(3.6f, 24f, 3.6f), Quaternion.identity, SkLeaf);
            k.Box(new Vector3(c.x, 0.5f, c.z), new Vector3(4.6f, 1f, 4.6f), Quaternion.identity, SkStone);
            AddBox(g, "Pillar", c, new Vector3(3.6f, 24f, 3.6f));
            AddBox(g, "Pillar foot", new Vector3(c.x, 0.5f, c.z), new Vector3(4.6f, 1f, 4.6f));
        }
        k.Box(new Vector3(0f, 22.5f, -40f), new Vector3(52f, 3f, 3f), Quaternion.identity, SkLeaf);
        SkSolid("Pillars", g, k);
    }

    // ---- palms in round planters, their rims to grind -------------------------------------------------------------

    static void SkPalm(Vector3 at, float height, float lean, int seed)
    {
        var g = SkGroup("Palm");
        var k = new SkateMeshKit();
        k.Cylinder(at, at + Vector3.up * 0.45f, SkPlanterR, 24, SkStone);
        k.Cylinder(at + Vector3.up * 0.45f, at + Vector3.up * 0.47f, SkPlanterR - 0.25f, 24, SkGrass, false);
        AddBox(g, "Planter", at + Vector3.up * 0.225f, new Vector3(SkPlanterR * 1.6f, 0.45f, SkPlanterR * 1.6f));
        // the trunk: rings, curving as it goes up
        var rng = new System.Random(seed);
        Vector3 p = at + Vector3.up * 0.45f, dir = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f).normalized * lean;
        int segs = 12;
        for (int i = 0; i < segs; i++)
        {
            Vector3 q = p + (Vector3.up + dir * (i / (float)segs)).normalized * (height / segs);
            k.Cylinder(p, q, Mathf.Lerp(0.45f, 0.3f, i / (float)segs), 8, i % 2 == 0 ? SkTrunk : SkStoneDark, false);
            p = q;
        }
        AddBox(g, "Trunk", at + Vector3.up * 2f, new Vector3(0.9f, 4f, 0.9f));
        // fronds: drooping leaves all round the top
        for (int f = 0; f < 11; f++)
        {
            float a = f * 2.4f + (float)rng.NextDouble() * 0.4f, len = 4.5f + (float)rng.NextDouble() * 2f;
            Vector3 out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = Vector3.Cross(Vector3.up, out_);
            Vector3 prev = p;
            for (int s = 1; s <= 5; s++)
            {
                float t = s / 5f;
                Vector3 next = p + out_ * (len * t) + Vector3.up * (1.2f * t - 2.6f * t * t);
                float w0 = Mathf.Lerp(0.9f, 0.2f, (s - 1) / 5f), w1 = Mathf.Lerp(0.9f, 0.2f, t);
                k.Quad(prev - side * w0, next - side * w1, next + side * w1, prev + side * w0, Vector3.up, SkLeaf);
                k.Quad(prev - side * w0, next - side * w1, next + side * w1, prev + side * w0, Vector3.down, SkLeaf);
                prev = next;
            }
        }
        SkSolid("Palm", g, k);
        var rim = new List<Vector3>();
        for (int i = 0; i <= 32; i++) { float a = i / 32f * Mathf.PI * 2f; rim.Add(at + new Vector3(Mathf.Cos(a) * (SkPlanterR - 0.12f), 0.48f, Mathf.Sin(a) * (SkPlanterR - 0.12f))); }
        Rail("Planter rim", rim.ToArray(), false);
    }

    static void SkPalms()
    {
        SkPalm(SkPalmAt, 15f, 0.25f, 1);
        SkPalm(new Vector3(-36f, 0f, -22f), 12f, 0.35f, 2);
        SkPalm(new Vector3(34f, 0f, -24f), 13f, 0.3f, 3);
        SkPalm(new Vector3(-34f, 0f, 26f), 11f, 0.4f, 4);
        SkPalm(new Vector3(36f, 0f, 24f), 14f, 0.3f, 5);
    }

    // ---- the little old car, and the plank up onto it ------------------------------------------------------------

    // the car's top, front to back (along +z from SkCar): bonnet, windscreen, roof, back window, boot
    public static readonly Vector2[] SkCarTop = { new Vector2(3.4f, 0.95f), new Vector2(4.7f, 1.05f), new Vector2(5.6f, 1.6f), new Vector2(7.4f, 1.62f), new Vector2(8.0f, 1.3f), new Vector2(8.6f, 1.22f) };

    static void SkCarAndPlank()
    {
        Matrix4x4 frame = Matrix4x4.TRS(SkCar, Quaternion.identity, Vector3.one);
        var plank = new List<Vector2> { new Vector2(-0.1f, -0.02f), new Vector2(0f, 0.02f), new Vector2(3.5f, 1.0f) };
        ProfileRamp("Plank", frame, plank, 1.4f, SkWood, SkWood, -0.2f);
        var top = new List<Vector2>(SkCarTop);
        ProfileRamp("Car", frame, top, 1.9f, SkCarBody, SkCarBody, 0.3f);
        // its wheels, windows and lamps, coloured in with it
        var car = featureRoot.GetChild(featureRoot.childCount - 1);
        var k = new SkateMeshKit();
        foreach (float z in new[] { 4.3f, 7.7f })
            foreach (float s in new[] { -1f, 1f })
                k.Cylinder(SkCar + new Vector3(s * 0.85f, 0.36f, z), SkCar + new Vector3(s * 1.0f, 0.36f, z), 0.36f, 14, SkTyre);
        foreach (float s in new[] { -1f, 1f })
        {
            k.Quad(SkCar + new Vector3(s * 0.96f, 1.1f, 4.9f), SkCar + new Vector3(s * 0.96f, 1.5f, 5.6f), SkCar + new Vector3(s * 0.96f, 1.5f, 7.3f), SkCar + new Vector3(s * 0.96f, 1.15f, 7.9f), new Vector3(s, 0f, 0f), SkWindow);
            k.Box(SkCar + new Vector3(s * 0.65f, 0.75f, 3.38f), new Vector3(0.4f, 0.22f, 0.06f), Quaternion.identity, SkLamp);
        }
        k.Quad(SkCar + new Vector3(-0.85f, 1.08f, 4.75f), SkCar + new Vector3(0.85f, 1.08f, 4.75f), SkCar + new Vector3(0.85f, 1.55f, 5.55f), SkCar + new Vector3(-0.85f, 1.55f, 5.55f), new Vector3(0f, 0.8f, -0.6f), SkWindow);
        var go = SkSolid("Car bits", car, k);
        go.transform.SetParent(car, true);
        AddBox(car, "Car", SkCar + new Vector3(0f, 0.6f, 6f), new Vector3(1.9f, 0.6f, 5.2f));
    }

    // ---- the skate stuff ----------------------------------------------------------------------------------------

    static void SkFeatures()
    {
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(0f, 0f, -24f), Quaternion.identity, Vector3.one), 4f, 26f, 3.6f, SkStone);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(30f, 0f, 0f), Quaternion.LookRotation(Vector3.right), Vector3.one), 3.5f, 24f, 3.2f, SkStone);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(-12f, 0f, -12f), Quaternion.identity, Vector3.one), 6f);
        Ledge("Bench", new Vector3(20f, 0f, -12f), Quaternion.identity, 7f, 0.45f, 0.9f, SkWood);
        Ledge("Bench", new Vector3(-16f, 0f, 26f), Quaternion.LookRotation(Vector3.right), 7f, 0.45f, 0.9f, SkWood);
        Ledge("Manual pad", new Vector3(14f, 0f, 32f), Quaternion.LookRotation(Vector3.right), 9f, 0.2f, 1.8f, SkStone);
        Rail("Flat rail", new[] { new Vector3(-6f, 0.5f, 36f), new Vector3(-20f, 0.5f, 36f) });
        Rail("Flat rail", new[] { new Vector3(16f, 0.5f, -36f), new Vector3(16f, 0.5f, -50f) });
    }

    public static void SketchShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2.2f, -66f), new Vector3(0f, 6f, 20f), "s01-start.png");
        Shot(cam, new Vector3(-10f, 2f, -40f), SkCar + new Vector3(0f, 1f, 5f), "s02-car.png");
        Shot(cam, new Vector3(-30f, 30f, -70f), new Vector3(10f, 0f, 20f), "s03-overview.png");
        Shot(cam, new Vector3(8f, 3f, 20f), new Vector3(0f, 4f, SkZ1), "s04-door.png");
    }
}
