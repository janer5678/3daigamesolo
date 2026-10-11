using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the whole game scene from nothing, every time it is run (menu: Skate > Build world).
//
// A long pink dirt run winding down a valley - the composition of the reference at 0:11, teal
// pines either side, a cyan river below on the right, pastel ranges going on into the haze -
// with kickers, rails and ledges along it, ending in a flat skate plaza with a halfpipe, a
// funbox, rails, a manual pad and a big quarterpipe wall to turn round on.
//
// Run from the command line it also test-rides the level (SlideTests) and writes review
// screenshots to Review/.
public static partial class SkateWorldBuilder
{
    public const string ScenePath = "Assets/Scenes/SkatePark.unity";
    const string Folder = "Assets/World";
    const string ReviewOut = "Review/";

    // ---- the shape of the land ------------------------------------------------------------

    public const float PathHalfWidth = 10f;
    public const float PlazaX0 = 650f, PlazaX1 = 870f, PlazaZ0 = -32f, PlazaZ1 = 42f;
    const float RiverHalf = 7f;

    static readonly Rect Near = Rect.MinMaxRect(-160f, -170f, 1000f, 170f);
    static readonly Rect Far  = Rect.MinMaxRect(-2600f, -2600f, 3600f, 2600f);
    const float NearCell = 2f;
    const float FarCell  = 24f;

    static readonly Vector4[] Peaks =
    {
        new Vector4(  300f,  1000f, 430f, 520f),
        new Vector4( 1100f,  1150f, 500f, 600f),
        new Vector4( 1900f,   950f, 450f, 520f),
        new Vector4(  200f, -1050f, 400f, 480f),
        new Vector4(  950f, -1100f, 500f, 560f),
        new Vector4( 1750f, -1000f, 540f, 600f),
        new Vector4( 2700f,   450f, 580f, 650f),
        new Vector4( 2750f,  -550f, 550f, 650f),
        new Vector4( -900f,   300f, 420f, 520f),
        new Vector4( -950f,  -400f, 390f, 500f),
    };

    public static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Sq(float x) { return x * x; }

    public static float PathZ(float x)
    {
        float wig = 22f * Mathf.Sin(x * 0.009f) + 9f * Mathf.Sin(x * 0.023f + 1f);
        return wig * (1f - Smooth(480f, 630f, x));
    }

    public static float RiverZ(float x) { return PathZ(x) - 52f - 6f * Mathf.Sin(x * 0.017f); }

    // flat ground everywhere you can skate, for testing movement; false brings back the valley run, river and hills
    public const bool FlatGround = true;

    // the long fall of the valley floor: steady down the run, flat through the plaza, up behind it
    static float Grade(float x)
    {
        if (FlatGround) return 0f;
        if (x < -10f) return 0.07f + (-10f - x) * 0.004f;
        if (x > 880f) return -(x - 880f) * 0.006f;
        return 0.07f * (1f - Smooth(480f, 640f, x));
    }

    const float TableMin = -3000f, TableMax = 4000f;
    static float[] baseTable;

    public static float Base(float x)
    {
        if (baseTable == null)
        {
            int count = (int)(TableMax - TableMin) + 1;
            baseTable = new float[count];
            int i0 = (int)(-TableMin);
            baseTable[i0] = 50f;
            for (int i = i0 + 1; i < count; i++) baseTable[i] = baseTable[i - 1] - Mathf.Clamp(Grade(TableMin + i - 0.5f), -0.5f, 0.6f);
            for (int i = i0 - 1; i >= 0; i--) baseTable[i] = baseTable[i + 1] + Mathf.Clamp(Grade(TableMin + i + 0.5f), -0.5f, 0.6f);
        }
        float f = Mathf.Clamp(x - TableMin, 0f, TableMax - TableMin - 1.001f);
        int k = (int)f;
        return Mathf.Lerp(baseTable[k], baseTable[k + 1], f - k);
    }

    public static float RiverLevel(float x) { return Base(x) - 1.8f; }

    static float Fbm(float x, float z, int octaves)
    {
        float s = 0f, a = 0.5f, f = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            s += a * Mathf.PerlinNoise(x * f + 31.7f * i, z * f + 17.3f * i);
            norm += a; a *= 0.5f; f *= 2.03f;
        }
        return s / norm;
    }

    static float Ridged(float x, float z, int octaves)
    {
        float s = 0f, a = 0.55f, f = 1f, w = 1f;
        for (int i = 0; i < octaves; i++)
        {
            float nn = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * f + 11.1f * i + 400f, z * f + 7.7f * i + 400f) - 1f);
            nn *= nn; nn *= w;
            w = Mathf.Clamp01(nn * 1.7f);
            s += nn * a;
            a *= 0.5f; f *= 2.1f;
        }
        return s;
    }

    public static float Plazaness(float x) { return Smooth(600f, 650f, x) * (1f - Smooth(900f, 960f, x)); }

    public static float Height(float x, float z)
    {
        float b = Base(x);
        float pz = PathZ(x);
        float d = z - pz, ad = Mathf.Abs(d);
        float W = PathHalfWidth;
        float plaza = Plazaness(x);
        if (FlatGround) return b + DistantRanges(x, z, pz);

        // across the run: a shallow dish you can carve, the hill rising on the left, the land
        // falling gently away to the river on the right
        float hRun = d >= 0f
            ? Sq(Mathf.Min(ad, W)) * 0.004f + Bank(ad - W)
            : Sq(Mathf.Min(ad, W)) * 0.004f - Mathf.Max(0f, ad - W) * 0.045f;
        float hPlaza = z > PlazaZ1 ? Bank(z - PlazaZ1) : z < PlazaZ0 ? -(PlazaZ0 - z) * 0.045f : 0f;
        float h = Mathf.Lerp(hRun, hPlaza, plaza);

        // the river and its far bank
        float rz = RiverZ(x);
        if (z < rz) h = -0.6f + Bank(rz - z - RiverHalf) * 0.8f;
        float rd = Mathf.Abs(z - rz);
        h = Mathf.Lerp(-2.8f, h, Smooth(RiverHalf * 0.5f, RiverHalf + 3f, rd));

        // hills where you can't skate anyway
        float wildA = z >= pz ? Mathf.Lerp(Smooth(W + 20f, W + 110f, ad), Smooth(PlazaZ1 + 20f, PlazaZ1 + 110f, z), plaza) : 0f;
        float wildB = Smooth(10f, 90f, rz - RiverHalf - z);
        float wildC = 1f - Smooth(-160f, -40f, x);
        float wild = Mathf.Max(wildA, Mathf.Max(wildB, wildC));
        h += wild * (2f + 16f * Fbm(x / 110f + 9f, z / 110f + 9f, 4));

        h += DistantRanges(x, z, pz);
        return b + h;
    }

    // two ranges behind: a nearer, lower one and the big snowy one far back
    static float DistantRanges(float x, float z, float pz)
    {
        float h = 0f;
        float dv = Mathf.Abs(z - (pz - 26f));
        float ridge = Ridged(x / 200f, z / 200f, 6);
        float low  = Mathf.Max(Smooth(230f, 520f, dv), Mathf.Max(1f - Smooth(-700f, -320f, x), Smooth(1400f, 1800f, x)));
        float high = Mathf.Max(Smooth(600f, 1200f, dv), Mathf.Max(1f - Smooth(-1200f, -700f, x), Smooth(1900f, 2400f, x)));
        h += low * (30f + 150f * ridge);
        h += high * (90f + 300f * Ridged(x / 330f + 3f, z / 330f + 3f, 6));

        float peakMask = Smooth(0.1f, 0.5f, high);
        if (peakMask > 0f)
        {
            foreach (var p in Peaks)
            {
                float dist = Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.y));
                float t = 1f - dist / p.w;
                if (t <= 0f) continue;
                h += peakMask * p.z * Mathf.Pow(t, 1.5f) * (0.7f + 0.6f * ridge);
            }
        }
        return h;
    }

    // the meadow rising off the side of the run: curving up, then a steady grassy slope
    static float Bank(float u)
    {
        if (u <= 0f) return 0f;
        return u < 15f ? Sq(u) * 0.012f : 2.7f + (u - 15f) * 0.36f;
    }

    public static Vector3 Normal(float x, float z, float e = 1f)
    {
        float l = Height(x - e, z), r = Height(x + e, z);
        float dn = Height(x, z - e), up = Height(x, z + e);
        return new Vector3(-(r - l) / (2f * e), 1f, -(up - dn) / (2f * e)).normalized;
    }

    public static Vector3 Ground(float x, float z) { return new Vector3(x, Height(x, z), z); }

    // a point on the run: `x` along it, `d` metres across (+ is away from the river)
    public static Vector3 OnPath(float x, float d) { return Ground(x, PathZ(x) + d); }

    // downhill along the run at x
    public static Vector3 PathDir(float x)
    {
        Vector3 a = OnPath(x - 1f, 0f), b = OnPath(x + 1f, 0f);
        return (b - a).normalized;
    }

    // ---- build ------------------------------------------------------------------------------

    static Material terrainMat, toonMat, outlineMat, farOutlineMat, skyMat, waterMat;
    static readonly List<Vector3> keepClear = new List<Vector3>();   // x, z, radius - no trees here
    static Transform featureRoot;
    static int featureCount;

    [MenuItem("Skate/Build world")]
    public static void BuildMenu()
    {
        Build();
        Shots();
    }

    // -executeMethod SkateWorldBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        try
        {
            Build();
            SlideTests.RunAll();
            Shots();
            BuildPalace();
            SlideTests.RunPalace();
            PalaceShots();
            BuildCity();
            SlideTests.RunCity();
            CityShots();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    // -executeMethod SkateWorldBuilder.BuildPlayer : a Windows build in Builds/Staging (copied to Builds/Windows once
    // it's built, so the copy being played isn't overwritten mid-game)
    public static void BuildPlayer()
    {
        var report = BuildPipeline.BuildPlayer(System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path), "Builds/Staging/3daigamesolo.exe",
                                               BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("SKATE_PLAYER_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    public static void Build()
    {
        baseTable = null;
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        keepClear.Clear();
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/Features.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        ValleyPalette();
        floorAt = Height;

        terrainMat = Mat("Terrain", "3daigamesolo/World/Terrain");
        toonMat = Mat("Toon", "3daigamesolo/World/Toon");
        outlineMat = Mat("Outline", "3daigamesolo/World/Outline");
        farOutlineMat = Mat("OutlineFar", "3daigamesolo/World/Outline");
        farOutlineMat.SetFloat("_OutlineWidth", 1.1f);
        skyMat = Mat("Sky", "3daigamesolo/World/Sky");
        skyMat.SetFloat("_CloudCover", 0.6f);
        waterMat = Mat("Water", "3daigamesolo/World/Water");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        // ---- features first, so the trees know where not to grow
        featureRoot = new GameObject("Skate features").transform;
        RunFeatures();
        PlazaFeatures();

        // ---- ground
        var land = new GameObject("Land").transform;
        Mesh nearMesh = SaveMesh(Grid("Near ground", Near, NearCell, new Rect(), 0f, true), "NearGround");
        var near = MeshObject("Near ground (skateable)", land, nearMesh, terrainMat, outlineMat);
        near.AddComponent<MeshCollider>().sharedMesh = nearMesh;

        Rect hole = Rect.MinMaxRect(Near.xMin + 20f, Near.yMin + 20f, Near.xMax - 20f, Near.yMax - 20f);
        Mesh farMesh = SaveMesh(Grid("Far ground", Far, FarCell, hole, 1.5f, false), "FarGround");
        var far = MeshObject("Far ranges (backdrop)", land, farMesh, terrainMat, farOutlineMat);
        far.AddComponent<MeshCollider>().sharedMesh = farMesh;   // solid too, so riding off the edge of the near ground doesn't drop you through the world

        Mesh riverMesh = SaveMesh(River(), "River");
        var river = MeshObject("River", land, riverMesh, waterMat, null);
        river.layer = 4;    // Water
        river.AddComponent<MeshCollider>().sharedMesh = riverMesh;

        // ---- trees
        var rng = new System.Random(20261011);
        var trunks = new GameObject("Tree trunks (colliders)");
        trunks.transform.SetParent(land, false);
        Mesh nearTrees = SaveMesh(Trees(rng, true, trunks), "NearTrees");
        MeshObject("Trees (near)", land, nearTrees, toonMat, outlineMat);
        Mesh farTrees = SaveMesh(Trees(rng, false, null), "FarTrees");
        MeshObject("Trees (far)", land, farTrees, toonMat, farOutlineMat);

        // ---- sky
        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.name = "Sky";
        Object.DestroyImmediate(sky.GetComponent<Collider>());
        sky.transform.position = new Vector3(500f, 0f, 0f);
        sky.transform.localScale = Vector3.one * 10000f;
        sky.GetComponent<Renderer>().sharedMaterial = skyMat;

        // ---- the skater
        Vector3 startAt = OnPath(4f, 0f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt + Vector3.up * 0.2f, Quaternion.LookRotation(Flat(PathDir(4f))));
        // ---- camera, HUD and you
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 9000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.98f, 0.88f, 0.94f);
        var player = BuildPlayer(start.transform, camGO.transform);
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Pururin Valley";
        player.board = BuildBoard(OnPath(12f, 2.5f), Quaternion.LookRotation(Flat(PathDir(12f))));   // lying on the run: run over it to ride
        camGO.transform.SetPositionAndRotation(startAt + Vector3.up * 2.5f - Flat(PathDir(4f)) * 5f, Quaternion.LookRotation(Flat(PathDir(4f))));

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_WORLD_BUILT nearVerts=" + nearMesh.vertexCount + " farVerts=" + farMesh.vertexCount
                  + " trees=" + nearTrees.vertexCount + "/" + farTrees.vertexCount + " features=" + featureCount
                  + " base(0)=" + Base(0f) + " base(650)=" + Base(650f));
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

    // ---- what's on the run ----------------------------------------------------------------------

    // the colours ramps, rails and ledges are built in; each level sets its own (ValleyPalette here)
    static Color32 RampTop, RampSide, RampSide2, Metal, RailCol, Concrete, ConcreteDark;
    // the floor height rail posts reach down to; the valley's ground, or a level's flat floor
    static System.Func<float, float, float> floorAt = Height;

    static void ValleyPalette()
    {
        RampTop = new Color32(250, 214, 226, 255);
        RampSide = new Color32(226, 214, 248, 255);
        RampSide2 = new Color32(196, 236, 242, 255);
        Metal = new Color32(240, 240, 250, 255);
        RailCol = new Color32(248, 150, 196, 255);
        Concrete = new Color32(236, 228, 246, 255);
        ConcreteDark = new Color32(206, 194, 234, 255);
    }

    static void RunFeatures()
    {
        Kicker("Kicker", PathFrame(60f, 0f), 4f, 24f, 3.2f, RampSide);
        PathRail("Rail", 110f, 140f, 4.5f, 0.5f);
        PathLedge("Ledge", 172f, 12f, -4f, 0.45f, 0.9f);
        Kicker("Big kicker", PathFrame(230f, 0f), 5f, 28f, 4f, RampSide2);
        Kicker("Twin kicker L", PathFrame(290f, -4.5f), 3f, 22f, 2.6f, RampSide);
        Kicker("Twin kicker R", PathFrame(290f, 4.5f), 3f, 22f, 2.6f, RampSide2);
        PathRail("Rail", 335f, 370f, -4.5f, 0.5f);
        PathLedge("Manual pad", 400f, 9f, 3f, 0.2f, 1.8f);
        Kicker("Huge kicker", PathFrame(445f, 0f), 6f, 30f, 4.5f, RampSide);
        PathRail("Long rail", 480f, 525f, 4.5f, 0.55f);
        PathRail("Long rail", 480f, 525f, -4.5f, 0.55f);
        Funbox("Funbox", PathFrame(565f, 0f), 6f);
        Kicker("Kicker", PathFrame(610f, -3f), 3.5f, 24f, 3f, RampSide2);
    }

    static void PlazaFeatures()
    {
        float y = Base(700f);
        Quaternion east = Quaternion.LookRotation(Vector3.right), west = Quaternion.LookRotation(Vector3.left);
        Quaternion north = Quaternion.LookRotation(Vector3.forward), south = Quaternion.LookRotation(Vector3.back);

        Ledge("Manual pad", new Vector3(680f, y, -12f), east, 8f, 0.2f, 1.8f, Concrete);
        Rail("Flat rail", new[] { new Vector3(684f, y + 0.5f, 18f), new Vector3(700f, y + 0.5f, 18f) });
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(700f, y, 0f), east, Vector3.one), 7f);
        Ledge("Ledge", new Vector3(745f, y, -18f), east, 14f, 0.5f, 1f, Concrete);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(740f, y, 26f), east, Vector3.one), 4f, 26f, 3.5f, RampSide2);
        Rail("Low rail", new[] { new Vector3(722f, y + 0.4f, -2f), new Vector3(736f, y + 0.4f, -2f) });
        Kicker("Big kicker", Matrix4x4.TRS(new Vector3(790f, y, -12f), east, Vector3.one), 5.5f, 30f, 4.5f, RampSide);
        Rail("Kinked rail", new[] { new Vector3(784f, y + 0.5f, 8f), new Vector3(794f, y + 0.5f, 8f), new Vector3(806f, y + 1.4f, 8f), new Vector3(814f, y + 1.4f, 8f) });

        // a halfpipe running east-west at the north side of the plaza
        float hpZ = 27f, hpX = 790f, hpLen = 26f;
        QuarterPipe("Halfpipe south wall", Matrix4x4.TRS(new Vector3(hpX, y, hpZ - 4f), south, Vector3.one), hpLen, 2.4f, 2.2f);
        QuarterPipe("Halfpipe north wall", Matrix4x4.TRS(new Vector3(hpX, y, hpZ + 4f), north, Vector3.one), hpLen, 2.4f, 2.2f);

        // the end wall: a big quarterpipe right across the plaza to turn round on
        QuarterPipe("End wall", Matrix4x4.TRS(new Vector3(852f, y, (PlazaZ0 + PlazaZ1) / 2f), east, Vector3.one), PlazaZ1 - PlazaZ0 + 8f, 3.5f, 3f);
        keepClear.Add(new Vector3(760f, 5f, 200f));
    }

    // origin on the ground at (x, d), local z along the run downhill, y up out of the slope
    static Matrix4x4 PathFrame(float x, float d)
    {
        Vector3 p = OnPath(x, d);
        Vector3 nrm = Normal(p.x, p.z);
        Vector3 f = Vector3.ProjectOnPlane(PathDir(x), nrm).normalized;
        return Matrix4x4.TRS(p, Quaternion.LookRotation(f, nrm), Vector3.one);
    }

    static Transform NewFeature(string name, Vector3 at)
    {
        var go = new GameObject(name + " " + (++featureCount));
        go.transform.SetParent(featureRoot, false);
        go.transform.position = at;
        return go.transform;
    }

    // A ramp from its side-on profile: (along, up) points over the top from front to back, swept
    // across `width`. The top is one object (the riding surface, a SkateRamp); the sides and
    // back are another (solid, so you can slam into them).
    static void ProfileRamp(string name, Matrix4x4 frame, List<Vector2> profile, float width, Color32 top, Color32 side, float bottom = -0.6f)
    {
        Vector3 origin = frame.MultiplyPoint3x4(Vector3.zero);
        Transform root = NewFeature(name, origin);
        var surf = new SkateMeshKit { frame = frame };
        var shell = new SkateMeshKit { frame = frame };
        float hw = width * 0.5f;

        // smooth normals along the profile so curves shade as curves
        int count = profile.Count;
        var normals = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 acc = Vector3.zero;
            if (i > 0) acc += SegNormal(profile[i - 1], profile[i]);
            if (i < count - 1) acc += SegNormal(profile[i], profile[i + 1]);
            normals[i] = acc.normalized;
        }
        for (int i = 0; i < count - 1; i++)
        {
            Vector2 a = profile[i], b = profile[i + 1];
            if ((b - a).sqrMagnitude < 1e-6f) continue;
            surf.SmoothQuad(new Vector3(-hw, a.y, a.x), new Vector3(-hw, b.y, b.x), new Vector3(hw, b.y, b.x), new Vector3(hw, a.y, a.x),
                            normals[i], normals[i + 1], normals[i + 1], normals[i], top);
            // the two sides, as vertical strips down to the bottom
            foreach (float sx in new[] { -hw, hw })
            {
                if (a.y <= bottom && b.y <= bottom) continue;
                shell.Quad(new Vector3(sx, a.y, a.x), new Vector3(sx, b.y, b.x), new Vector3(sx, bottom, b.x), new Vector3(sx, bottom, a.x),
                           new Vector3(Mathf.Sign(sx), 0f, 0f), side);
            }
        }
        Vector2 first = profile[0], last = profile[count - 1];
        if (last.y > bottom + 0.01f)
            shell.Quad(new Vector3(-hw, last.y, last.x), new Vector3(hw, last.y, last.x), new Vector3(hw, bottom, last.x), new Vector3(-hw, bottom, last.x), Vector3.forward, side);
        if (first.y > bottom + 0.01f)
            shell.Quad(new Vector3(-hw, first.y, first.x), new Vector3(hw, first.y, first.x), new Vector3(hw, bottom, first.x), new Vector3(-hw, bottom, first.x), Vector3.back, side);

        AddPart(root, "Surface", surf.Build(name + " surface"), true);
        AddPart(root, "Body", shell.Build(name + " body"), false);

        float reach = Mathf.Max(width, last.x - first.x) * 0.5f + 6f;
        keepClear.Add(new Vector3(origin.x, origin.z, reach + 4f));
    }

    static Vector3 SegNormal(Vector2 a, Vector2 b)
    {
        Vector2 d = (b - a).normalized;
        return new Vector3(0f, d.x, -d.y);     // (along,up) rotated a quarter turn, in (x=0, y, z)
    }

    static void AddPart(Transform root, string name, Mesh mesh, bool ramp)
    {
        // meshes are built in world space; the part sits at the origin
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(root, true);
        go.transform.position = Vector3.zero;
        SaveFeatureMesh(mesh);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    static void Kicker(string name, Matrix4x4 frame, float radius, float degrees, float width, Color32 side)
    {
        var p = new List<Vector2> { new Vector2(-0.25f, -0.08f) };
        int segs = 10;
        for (int i = 0; i <= segs; i++)
        {
            float a = degrees * Mathf.Deg2Rad * i / segs;
            p.Add(new Vector2(radius * Mathf.Sin(a), radius * (1f - Mathf.Cos(a))));
        }
        ProfileRamp(name, frame, p, width, RampTop, side);
    }

    static void QuarterPipe(string name, Matrix4x4 frame, float width, float radius, float deck)
    {
        var p = new List<Vector2> { new Vector2(-0.25f, -0.05f) };
        int segs = 16;
        for (int i = 0; i <= segs; i++)
        {
            float a = 90f * Mathf.Deg2Rad * i / segs;
            p.Add(new Vector2(radius * Mathf.Sin(a), radius * (1f - Mathf.Cos(a))));
        }
        float lip = radius + 0.3f;
        p.Add(new Vector2(radius, lip));
        p.Add(new Vector2(radius + deck, lip));
        ProfileRamp(name, frame, p, width, RampTop, RampSide);

        // the coping: a steel bar along the lip, grindable
        Vector3 a0 = frame.MultiplyPoint3x4(new Vector3(-width * 0.5f + 0.2f, lip + 0.03f, radius + 0.02f));
        Vector3 a1 = frame.MultiplyPoint3x4(new Vector3(width * 0.5f - 0.2f, lip + 0.03f, radius + 0.02f));
        Rail(name + " coping", new[] { a0, a1 }, false);
    }

    static void Funbox(string name, Matrix4x4 frame, float width)
    {
        var p = new List<Vector2> { new Vector2(-0.25f, -0.08f) };
        float rise = 1.0f, ramp = 2.8f, top = 3.2f;
        for (int i = 0; i <= 6; i++)
        {
            float u = i / 6f;
            p.Add(new Vector2(u * ramp, rise * Mathf.SmoothStep(0f, 1f, u * 0.5f + 0.5f * u * u)));
        }
        p.Add(new Vector2(ramp + top, rise));
        for (int i = 1; i <= 6; i++)
        {
            float u = i / 6f;
            p.Add(new Vector2(ramp + top + u * ramp, rise * (1f - u)));
        }
        p.Add(new Vector2(ramp * 2f + top + 0.25f, -0.08f));
        ProfileRamp(name, frame, p, width, RampTop, RampSide2);

        // rails down both edges of the flat top
        foreach (float sx in new[] { -1f, 1f })
        {
            Vector3 a = frame.MultiplyPoint3x4(new Vector3(sx * (width * 0.5f - 0.15f), rise + 0.05f, ramp - 0.2f));
            Vector3 b = frame.MultiplyPoint3x4(new Vector3(sx * (width * 0.5f - 0.15f), rise + 0.05f, ramp + top + 0.2f));
            Rail(name + " edge", new[] { a, b }, false);
        }
    }

    // a rail along the run, following the ground at `height`
    static void PathRail(string name, float x0, float x1, float d, float height)
    {
        var pts = new List<Vector3>();
        for (float x = x0; x <= x1 + 0.01f; x += 2.5f)
            pts.Add(OnPath(x, d) + Vector3.up * height);
        Rail(name, pts.ToArray());
    }

    static void Rail(string name, Vector3[] pts, bool posts = true)
    {
        Transform root = NewFeature(name, pts[0]);
        var kit = new SkateMeshKit();
        for (int i = 0; i < pts.Length - 1; i++)
            kit.Cylinder(pts[i], pts[i + 1], 0.05f, 8, RailCol, false);
        if (posts)
        {
            float acc = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                if (i > 0) acc += Vector3.Distance(pts[i - 1], pts[i]);
                if (i != 0 && i != pts.Length - 1 && acc < 3f) continue;
                acc = 0f;
                Vector3 g = new Vector3(pts[i].x, floorAt(pts[i].x, pts[i].z), pts[i].z);
                kit.Cylinder(pts[i], new Vector3(pts[i].x, Mathf.Min(g.y, pts[i].y) - 0.3f, pts[i].z), 0.04f, 6, Metal, false);
            }
        }
        var mesh = kit.Build(name);
        SaveFeatureMesh(mesh);
        var go = new GameObject("Rail mesh", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, true);
        go.transform.position = Vector3.zero;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        var rail = root.gameObject.AddComponent<SkateRail>();   // grindable on the board
        rail.points = pts;
        rail.radius = 0.05f;
        foreach (var p in pts) keepClear.Add(new Vector3(p.x, p.z, 6f));
    }

    static void PathLedge(string name, float x, float length, float d, float height, float width)
    {
        Vector3 a = OnPath(x - length * 0.5f, d), b = OnPath(x + length * 0.5f, d);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = (b - a).normalized;
        Ledge(name, mid, Quaternion.LookRotation(dir, Vector3.up), length, height, width, Concrete);
    }

    // a concrete box with grindable top edges; `at` is the middle of its base
    static void Ledge(string name, Vector3 at, Quaternion rot, float length, float height, float width, Color32 col)
    {
        Transform root = NewFeature(name, at);
        root.rotation = rot;
        float sink = 0.4f;
        var kit = new SkateMeshKit();
        kit.Box(new Vector3(0f, (height - sink) * 0.5f, 0f), new Vector3(width, height + sink, length), Quaternion.identity, col);
        kit.Box(new Vector3(0f, height - 0.02f, 0f), new Vector3(width + 0.04f, 0.06f, length + 0.04f), Quaternion.identity, ConcreteDark);
        var mesh = kit.Build(name);
        SaveFeatureMesh(mesh);
        var go = new GameObject("Box", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        var box = go.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, (height - sink) * 0.5f, 0f);
        box.size = new Vector3(width, height + sink, length);
        keepClear.Add(new Vector3(at.x, at.z, length * 0.5f + 6f));
    }

    // ---- ground meshes ------------------------------------------------------------------------

    static Mesh Grid(string name, Rect area, float cell, Rect hole, float holeDrop, bool masks)
    {
        int nx = Mathf.CeilToInt(area.width / cell) + 1;
        int nz = Mathf.CeilToInt(area.height / cell) + 1;
        var verts = new Vector3[nx * nz];
        var normals = new Vector3[nx * nz];
        var colors = new Color32[nx * nz];
        bool hasHole = hole.width > 0f;
        for (int j = 0; j < nz; j++)
        for (int i = 0; i < nx; i++)
        {
            float x = area.xMin + i * cell, z = area.yMin + j * cell;
            float y = Height(x, z);
            if (hasHole && hole.Contains(new Vector2(x, z))) y -= holeDrop;
            int k = j * nx + i;
            verts[k] = new Vector3(x, y, z);
            normals[k] = Normal(x, z, Mathf.Max(cell, 2f));
            if (masks)
            {
                float plaza = Plazaness(x);
                float ad = Mathf.Abs(z - PathZ(x));
                float path = (1f - Smooth(PathHalfWidth - 1.5f, PathHalfWidth + 0.5f, ad)) * (1f - Smooth(630f, 660f, x)) * Smooth(-30f, -10f, x);
                float inPlaza = Smooth(PlazaX0 - 6f, PlazaX0, x) * (1f - Smooth(PlazaX1 + 6f, PlazaX1 + 12f, x))
                              * Smooth(PlazaZ0 - 2f, PlazaZ0, z) * (1f - Smooth(PlazaZ1, PlazaZ1 + 2f, z));
                colors[k] = new Color(path, inPlaza > 0.5f && plaza > 0.5f ? 1f : 0f, 0f, 1f);
            }
            else colors[k] = new Color(0f, 0f, 0f, 1f);
        }

        Rect inner = hasHole ? Rect.MinMaxRect(hole.xMin + cell, hole.yMin + cell, hole.xMax - cell, hole.yMax - cell) : new Rect();
        var tris = new List<int>((nx - 1) * (nz - 1) * 6);
        for (int j = 0; j < nz - 1; j++)
        for (int i = 0; i < nx - 1; i++)
        {
            int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
            if (hasHole && inner.Contains(new Vector2(verts[a].x, verts[a].z)) && inner.Contains(new Vector2(verts[d].x, verts[d].z)))
                continue;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.colors32 = colors;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh River()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float w = RiverHalf + 5f;
        for (float x = Near.xMin; x <= Near.xMax; x += 2f)
        {
            float z = RiverZ(x), y = RiverLevel(x);
            verts.Add(new Vector3(x, y, z - w));
            verts.Add(new Vector3(x, y, z + w));
            int k = verts.Count;
            if (k >= 4)
            {
                tris.Add(k - 4); tris.Add(k - 3); tris.Add(k - 2);
                tris.Add(k - 3); tris.Add(k - 1); tris.Add(k - 2);
            }
        }
        var mesh = new Mesh { name = "River", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float Rand(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    // Pines, two stacked cones on a trunk, flat-shaded so the cel bands fall on the facets.
    // Mostly teal and cyan, with the odd pink one - as in the reference.
    static Mesh Trees(System.Random rng, bool near, GameObject colliders)
    {
        var kit = new SkateMeshKit();
        Rect area = near ? Rect.MinMaxRect(Near.xMin + 4f, Near.yMin + 4f, Near.xMax - 4f, Near.yMax - 4f) : Far;
        float spacing = near ? 4.4f : 21f;
        Color32[] foliage = {
            new Color32(76, 200, 212, 255), new Color32(112, 220, 228, 255),
            new Color32(64, 178, 204, 255), new Color32(248, 168, 204, 255),
        };
        Color32 trunk = new Color32(170, 110, 140, 255);

        for (float z = area.yMin; z < area.yMax; z += spacing)
        for (float x = area.xMin; x < area.xMax; x += spacing)
        {
            float px = x + Rand(rng, -0.45f, 0.45f) * spacing;
            float pz = z + Rand(rng, -0.45f, 0.45f) * spacing;
            float roll = (float)rng.NextDouble();
            float pick = (float)rng.NextDouble();
            float size = Rand(rng, 0.8f, 1.35f) * (near ? 1f : 2.4f);

            if (!near && Near.Contains(new Vector2(px, pz))) continue;
            if (Mathf.PerlinNoise(px / 70f + 50f, pz / 70f + 50f) < 0.45f) continue;   // in clumps
            if (roll > (near ? 0.82f : 0.7f)) continue;
            float y = Height(px, pz);
            if (y > Base(px) + 160f) continue;
            float ad = Mathf.Abs(pz - PathZ(px));
            if (px > -40f && px < 640f && ad < PathHalfWidth + 4f) continue;          // off the run
            if (px > PlazaX0 - 15f && px < PlazaX1 + 15f && pz > PlazaZ0 - 6f && pz < PlazaZ1 + 6f) continue;
            if (Mathf.Abs(pz - RiverZ(px)) < RiverHalf + 4f) continue;
            if (Normal(px, pz, 2f).y < 0.8f) continue;
            bool blocked = false;
            foreach (var k in keepClear)
                if (Sq(px - k.x) + Sq(pz - k.y) < k.z * k.z) { blocked = true; break; }
            if (blocked) continue;

            Color32 leaf = pick < 0.12f ? foliage[3] : foliage[(int)(pick * 31f) % 3];
            Vector3 at = new Vector3(px, y - 0.2f, pz);
            float turn = Rand(rng, 0f, Mathf.PI * 2f);
            kit.Cone(at, 0.22f * size, 0.22f * size, 1.8f * size, 4, trunk, turn);
            kit.Cone(at + Vector3.up * 1.2f * size, 1.9f * size, 0f, 4.3f * size, 6, leaf, turn);
            kit.Cone(at + Vector3.up * 3.3f * size, 1.35f * size, 0f, 3.9f * size, 6, leaf, turn + 0.5f);

            if (colliders != null && ad < 60f)
            {
                var cap = colliders.AddComponent<CapsuleCollider>();
                cap.center = at + Vector3.up * 1.5f * size;
                cap.radius = 0.45f * size;
                cap.height = 3f * size;
            }
        }
        return kit.Build(near ? "Trees near" : "Trees far");
    }

    // ---- the player --------------------------------------------------------------------------

    // a frictionless ball with the camera at eye height above it
    static SlideController BuildPlayer(Transform start, Transform cam)
    {
        var root = new GameObject("Player", typeof(Rigidbody), typeof(SphereCollider), typeof(SlideController));
        root.transform.position = start.position + Vector3.up * 0.35f;
        var ball = root.GetComponent<SphereCollider>();
        ball.radius = 0.35f;
        var slippy = new PhysicsMaterial("Slippery") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
        string path = Folder + "/Slippery.asset";
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);   // shared by every level, so keep the one there is
        if (existing != null) slippy = existing;
        else AssetDatabase.CreateAsset(slippy, path);
        ball.sharedMaterial = slippy;
        var rb = root.GetComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0f;   // no air drag: the controller does all the slowing down
        rb.angularDamping = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var ctrl = root.GetComponent<SlideController>();
        ctrl.startPoint = start;
        ctrl.cam = cam;
        ctrl.rider = BuildRider(root.transform);
        return ctrl;
    }

    // the skateboard lying on the floor: run over it and you ride it (no collider; it's just to look at).
    // Deliberately plain, like the runner: chunky shapes and flat colours, charcoal grip with a light
    // wood edge, one accent colour on the bottom, grey trucks and off-white wheels. A bit bigger than a
    // real board so it reads well. Pivot where the wheels touch the ground.
    const float DeckHalfLen = 0.56f, DeckHalfWidth = 0.21f, DeckBottom = 0.14f, DeckThick = 0.032f, KickStart = 0.37f;

    static float DeckHalfW(float z)
    {
        float e = Mathf.Abs(z) - (DeckHalfLen - DeckHalfWidth);   // rounded ends: a semicircle of the deck's half width
        return e <= 0f ? DeckHalfWidth : DeckHalfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - (e / DeckHalfWidth) * (e / DeckHalfWidth)));
    }

    static float DeckLift(float z)
    {
        float k = Mathf.Abs(z) - KickStart;                         // nose and tail kick up about 18 degrees
        return k <= 0f ? 0f : (k < 0.06f ? k * k / 0.12f * 0.32f : 0.06f * 0.16f + (k - 0.06f) * 0.32f);
    }

    static Transform BuildBoard(Vector3 at, Quaternion rot)
    {
        Color32 grip = new Color32(52, 50, 58, 255), wood = new Color32(232, 208, 168, 255);
        Color32 metal = new Color32(178, 180, 188, 255), wheel = new Color32(244, 240, 230, 255);
        var b = new SkateMeshKit();

        // the deck: rows across its length, top (grip), bottom (accent) and edges (wood)
        int rows = 96;
        var topL = new Vector3[rows + 1]; var topR = new Vector3[rows + 1];
        for (int i = 0; i <= rows; i++)
        {
            float z = Mathf.Lerp(-DeckHalfLen, DeckHalfLen, i / (float)rows);
            float w = Mathf.Max(DeckHalfW(z), 0.004f), y = DeckBottom + DeckThick + DeckLift(z);
            topL[i] = new Vector3(-w, y, z); topR[i] = new Vector3(w, y, z);
        }
        Vector3 down = Vector3.down * DeckThick;
        for (int i = 0; i < rows; i++)
        {
            Vector3 along = topL[i + 1] - topL[i];
            Vector3 up = Vector3.Cross(along, Vector3.right).normalized;
            if (up.y < 0f) up = -up;
            b.Quad(topL[i], topR[i], topR[i + 1], topL[i + 1], up, grip);
            b.Quad(topL[i] + down, topR[i] + down, topR[i + 1] + down, topL[i + 1] + down, -up, RunnerAccent);
            b.Quad(topL[i], topL[i + 1], topL[i + 1] + down, topL[i] + down, Vector3.left + 0.2f * Vector3.forward * Mathf.Sign(topL[i].z), wood);
            b.Quad(topR[i], topR[i + 1], topR[i + 1] + down, topR[i] + down, Vector3.right + 0.2f * Vector3.forward * Mathf.Sign(topR[i].z), wood);
        }
        // a wood line round the edge of the grip, so the shape reads from above
        for (int i = 0; i < rows; i++)
        {
            Vector3 lift = Vector3.up * 0.002f;
            float inset = 0.014f;
            Vector3 l0 = topL[i] + lift, l1 = topL[i + 1] + lift, r0 = topR[i] + lift, r1 = topR[i + 1] + lift;
            Vector3 il0 = l0 + Vector3.right * Mathf.Min(inset, -l0.x), il1 = l1 + Vector3.right * Mathf.Min(inset, -l1.x);
            Vector3 ir0 = r0 + Vector3.left * Mathf.Min(inset, r0.x), ir1 = r1 + Vector3.left * Mathf.Min(inset, r1.x);
            b.Quad(l0, il0, il1, l1, Vector3.up, wood);
            b.Quad(r0, ir0, ir1, r1, Vector3.up, wood);
        }

        foreach (float s in new[] { -1f, 1f })
        {
            float tz = s * 0.3f, axle = 0.058f;
            // truck: baseplate, kingpin, and a round hanger out to the wheels
            b.Box(new Vector3(0f, DeckBottom - 0.01f, tz), new Vector3(0.11f, 0.02f, 0.12f), Quaternion.identity, metal);
            b.Cylinder(new Vector3(0f, DeckBottom - 0.02f, tz), new Vector3(0f, axle + 0.01f, tz), 0.03f, 10, metal);
            b.Cylinder(new Vector3(-0.17f, axle, tz), new Vector3(0.17f, axle, tz), 0.022f, 10, metal);
            // chunky wheels
            foreach (float w in new[] { -1f, 1f })
                b.Cylinder(new Vector3(w * 0.15f, axle, tz), new Vector3(w * 0.205f, axle, tz), axle, 18, wheel);
        }
        var mesh = b.Build("Board");
        SaveFeatureMesh(mesh);
        var go = new GameObject("Board", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetPositionAndRotation(at, rot);
        go.transform.localScale = Vector3.one * 1.2f;   // about 1.35 m long
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        return go.transform;
    }

    static readonly Color32 RunnerAccent = new Color32(236, 112, 86, 255);

    // the runner, seen in third person: plain, flat colours so they fit any level. Pivot at the feet; the body
    // and each leg are separate parts pivoting at the hips so the controller can pose them.
    static Transform BuildRider(Transform parent)
    {
        Color32 top = new Color32(236, 232, 224, 255), pants = new Color32(62, 66, 84, 255);
        Color32 skin = new Color32(214, 172, 142, 255), shoe = new Color32(246, 246, 246, 255), sole = new Color32(70, 70, 78, 255);
        var root = new GameObject("Rider").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, -0.35f, 0f);

        var body = new SkateMeshKit();
        body.Ellipsoid(new Vector3(0f, 0.3f, 0f), new Vector3(0.2f, 0.34f, 0.13f), Quaternion.identity, 12, 8, top);          // hoodie
        body.Ellipsoid(new Vector3(0f, 0.04f, 0f), new Vector3(0.17f, 0.1f, 0.12f), Quaternion.identity, 12, 6, pants);       // hips
        body.Cylinder(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 0.7f, 0f), 0.05f, 8, skin);                                   // neck
        body.Ellipsoid(new Vector3(0f, 0.8f, 0.01f), new Vector3(0.12f, 0.14f, 0.13f), Quaternion.identity, 12, 8, skin);     // head
        body.Ellipsoid(new Vector3(0f, 0.86f, 0f), new Vector3(0.13f, 0.09f, 0.135f), Quaternion.identity, 12, 6, RunnerAccent); // cap
        body.Box(new Vector3(0f, 0.86f, 0.14f), new Vector3(0.18f, 0.02f, 0.12f), Quaternion.identity, RunnerAccent);            // brim
        foreach (float sx in new[] { -1f, 1f })
        {
            body.Cylinder(new Vector3(sx * 0.2f, 0.52f, 0f), new Vector3(sx * 0.27f, 0.08f, 0.02f), 0.05f, 8, top);
            body.Ellipsoid(new Vector3(sx * 0.275f, 0.04f, 0.02f), new Vector3(0.045f, 0.055f, 0.045f), Quaternion.identity, 8, 5, skin);
        }
        RiderPart("Body", root, body);

        foreach (float sx in new[] { -1f, 1f })
        {
            var leg = new SkateMeshKit();
            leg.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.8f, 0f), 0.075f, 8, pants);
            leg.Box(new Vector3(0f, -0.85f, 0.05f), new Vector3(0.12f, 0.08f, 0.27f), Quaternion.identity, shoe);
            leg.Box(new Vector3(0f, -0.885f, 0.05f), new Vector3(0.125f, 0.025f, 0.275f), Quaternion.identity, sole);
            RiderPart(sx < 0f ? "Leg L" : "Leg R", root, leg).localPosition = new Vector3(sx * 0.11f, 0.9f, 0f);
        }
        root.Find("Body").localPosition = new Vector3(0f, 0.9f, 0f);
        root.gameObject.SetActive(false);
        return root;
    }

    static Transform RiderPart(string name, Transform root, SkateMeshKit kit)
    {
        var mesh = kit.Build("Rider " + name);
        SaveFeatureMesh(mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        return go.transform;
    }

    static Transform Part(string name, Transform parent, Vector3 local, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        if (mesh != null)
        {
            SaveFeatureMesh(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { toonMat, outlineMat };
        }
        return go.transform;
    }

    // ---- assets -------------------------------------------------------------------------------

    static Material Mat(string name, string shader)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (sh == null) throw new System.Exception("Shader not found: " + shader);
        if (mat == null)
        {
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = sh;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Folder + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // all the little meshes (ramps, rails, the skater) go in one file, rebuilt each time
    static string featureFile, featureAsset;
    static void SaveFeatureMesh(Mesh mesh)
    {
        string path = featureAsset;
        if (featureFile == null || AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            featureFile = path;
        }
        else AssetDatabase.AddObjectToAsset(mesh, path);
    }

    static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material colour, Material line)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = line != null ? new[] { colour, line } : new[] { colour };
        return go;
    }

    // ---- review shots -------------------------------------------------------------------------

    public static void Shots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        var player = Object.FindFirstObjectByType<SlideController>();
        player.Respawn();
        Vector3 s = player.transform.position;
        Vector3 f = Flat(PathDir(4f));
        Shot(cam, s + Vector3.up * 1.15f, s + f * 30f, "01-start-first-person.png");
        Vector3 high = OnPath(40f, 6f) + Vector3.up * 26f;
        Shot(cam, high, OnPath(160f, 0f), "02-down-the-run-from-above.png");
        Shot(cam, OnPath(200f, 2f) + Vector3.up * 3f, OnPath(240f, 0f) + Vector3.up * 1f, "03-big-kicker.png");
        Shot(cam, OnPath(95f, -2f) + Vector3.up * 2f, OnPath(125f, 4.5f), "04-rail.png");
        Shot(cam, new Vector3(630f, Base(630f) + 18f, -10f), new Vector3(760f, Base(700f), 5f), "05-plaza.png");
        Shot(cam, new Vector3(770f, Base(700f) + 2.5f, 27f), new Vector3(800f, Base(700f) + 1f, 27f), "06-halfpipe.png");
        Shot(cam, OnPath(300f, 20f) + Vector3.up * 40f, OnPath(300f, -60f), "08-river.png");

        // the runner, as seen in third person
        var rider = player.rider;
        Vector3 bp = new Vector3(700f, Base(700f), 30f);
        rider.gameObject.SetActive(true);
        rider.SetPositionAndRotation(bp, Quaternion.LookRotation(Vector3.left));
        Shot(cam, bp + new Vector3(-1.2f, 2.0f, -3.4f), bp + Vector3.up * 0.9f, "11-runner.png");
        rider.gameObject.SetActive(false);
        rider.localPosition = new Vector3(0f, -0.35f, 0f);
        rider.localRotation = Quaternion.identity;
    }

    static void Shot(Camera cam, Vector3 from, Vector3 lookAt, string file)
    {
        Transform t = cam.transform;
        Vector3 p = t.position; Quaternion q = t.rotation;
        t.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from));
        cam.fieldOfView = 62f;
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        png.Apply();
        RenderTexture.active = active;
        cam.targetTexture = prev;
        rt.Release();
        t.SetPositionAndRotation(p, q);
        Directory.CreateDirectory(ReviewOut);
        File.WriteAllBytes(ReviewOut + file, png.EncodeToPNG());
        Debug.Log("SKATE_SHOT " + ReviewOut + file);
    }
}
