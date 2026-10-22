using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Surf Mesa: after the CS:S map surf_mesa (the world record run). Four stages, each its own canyon or cave three
// kilometres from the next, surfed Counter-Strike style (no board: SlideController.surf, Source gravity, air
// strafing):
//   1 the canyon: red-brown rock shaded blue-teal under an open sky, three long ramps
//   2 the blue hollow: a cave of dark blue rock hung with stalactites, the ramps curving
//   3 the sand pit: a wide canyon floored with orange sand, twin ramps side by side, a long rising one
//   4 the magma chamber: orange rock over a floor of lava, ending on an octagon platform ringed in cyan
// Each stage starts on a white tiled start zone edged in cyan, high up at the back; walk off and drop onto the first
// ramp. The ramps are pale grey-blue metal prisms with a glowing cyan strip along the bottom of each face; the sand
// (and the lava) under them sends you back to the start of the stage; a round tunnel in the end wall, ringed with
// cyan lights, takes you to the next (SurfLevel).
public static partial class SkateWorldBuilder
{
    public const string SurfPath = "Assets/Scenes/SurfMesa.unity";
    const string SurfTexDir = "Assets/Surf/Textures", SurfMatDir = "Assets/Surf/Materials";
    public const float SurfGap = 3000f;
    public const float SurfRampAngle = 54f;   // the faces, from flat: steeper than you can stand on (cos 54 = 0.59 < 0.7)
    public const float SurfZ0 = -30f;         // where each stage's back wall is
    public static Vector3 SurfO(int i) { return new Vector3(i * SurfGap, 0f, 0f); }

    public class SurfRampDef
    {
        public Vector3 a, ctrl, b;   // the ridge along the top, from a to b (bending towards ctrl if it's curved)
        public bool curved;
        public float drop;           // how far the faces run down from the ridge
        public float Width { get { return drop / Mathf.Tan(SurfRampAngle * Mathf.Deg2Rad); } }
    }

    public class SurfStageDef
    {
        public string name, rock;
        public bool roof, lava;
        public float halfW, height, length, startY;
        public SurfRampDef[] ramps;
        public Vector3 tunnel;       // the tunnel's middle, in the end wall (none if it ends on the finish platform)
        public Vector3 finish;       // the finish platform's top middle (zero if there's a tunnel)
        public Color fog, fogSun, sky, ground, background;
        public float fogDensity;
    }

    static SurfRampDef SR(Vector3 a, Vector3 b, float drop) { return new SurfRampDef { a = a, b = b, ctrl = (a + b) * 0.5f, drop = drop }; }
    static SurfRampDef SRC(Vector3 a, Vector3 ctrl, Vector3 b, float drop) { return new SurfRampDef { a = a, b = b, ctrl = ctrl, curved = true, drop = drop }; }

    public const float SurfTunnelR = 9f, SurfFinishR = 16f;

    // the four stages, each in its own space (add SurfO(i)); every one runs along +z from its back wall at SurfZ0
    public static readonly SurfStageDef[] SurfStages =
    {
        new SurfStageDef
        {
            name = "Canyon", rock = "rock_red", halfW = 38f, height = 112f, length = 430f, startY = 86f,
            ramps = new[]
            {
                SR(new Vector3(-5f, 78f, -4f), new Vector3(-5f, 68f, 118f), 16f),
                SR(new Vector3(9f, 61f, 136f), new Vector3(9f, 52f, 250f), 15f),
                SR(new Vector3(-9f, 45f, 268f), new Vector3(-9f, 47f, 380f), 15f),
            },
            tunnel = new Vector3(-2f, 37f, 400f),
            fog = new Color(0.78f, 0.72f, 0.64f), fogSun = new Color(1f, 0.86f, 0.66f), sky = new Color(0.46f, 0.5f, 0.58f), ground = new Color(0.42f, 0.3f, 0.22f),
            background = new Color(0.62f, 0.76f, 0.92f), fogDensity = 0.0022f,
        },
        new SurfStageDef
        {
            name = "Blue Hollow", rock = "rock_blue", roof = true, halfW = 36f, height = 102f, length = 430f, startY = 80f,
            ramps = new[]
            {
                SRC(new Vector3(-5f, 72f, -4f), new Vector3(1f, 67f, 57f), new Vector3(-8f, 62f, 118f), 15f),
                SRC(new Vector3(10f, 55f, 134f), new Vector3(3f, 51f, 190f), new Vector3(10f, 47f, 246f), 15f),
                SR(new Vector3(-10f, 40f, 262f), new Vector3(-10f, 37f, 304f), 13f),
                SR(new Vector3(10f, 31f, 320f), new Vector3(10f, 33f, 380f), 13f),
            },
            tunnel = new Vector3(2f, 26f, 400f),
            fog = new Color(0.07f, 0.15f, 0.2f), fogSun = new Color(0.1f, 0.22f, 0.28f), sky = new Color(0.4f, 0.6f, 0.7f), ground = new Color(0.16f, 0.26f, 0.3f),
            background = new Color(0.03f, 0.07f, 0.09f), fogDensity = 0.006f,
        },
        new SurfStageDef
        {
            name = "Sand Pit", rock = "rock_red", halfW = 44f, height = 95f, length = 430f, startY = 76f,
            ramps = new[]
            {
                SR(new Vector3(-5f, 68f, -4f), new Vector3(-5f, 60f, 104f), 14f),
                SR(new Vector3(8f, 53f, 120f), new Vector3(8f, 46f, 204f), 13f),
                SR(new Vector3(28f, 53f, 120f), new Vector3(28f, 46f, 204f), 13f),
                SRC(new Vector3(-8f, 39f, 220f), new Vector3(-2f, 38f, 300f), new Vector3(-8f, 41f, 382f), 14f),
            },
            tunnel = new Vector3(0f, 31f, 400f),
            fog = new Color(0.84f, 0.74f, 0.6f), fogSun = new Color(1f, 0.88f, 0.66f), sky = new Color(0.5f, 0.52f, 0.58f), ground = new Color(0.5f, 0.37f, 0.25f),
            background = new Color(0.66f, 0.78f, 0.92f), fogDensity = 0.0024f,
        },
        new SurfStageDef
        {
            name = "Magma Chamber", rock = "rock_lava", roof = true, lava = true, halfW = 36f, height = 98f, length = 430f, startY = 78f,
            ramps = new[]
            {
                SR(new Vector3(-5f, 70f, -4f), new Vector3(-5f, 62f, 100f), 15f),
                SRC(new Vector3(9f, 55f, 116f), new Vector3(2f, 51f, 170f), new Vector3(9f, 47f, 224f), 14f),
                SR(new Vector3(-9f, 40f, 240f), new Vector3(-9f, 43f, 322f), 13f),
            },
            finish = new Vector3(0f, 14f, 372f),
            fog = new Color(0.32f, 0.1f, 0.03f), fogSun = new Color(0.5f, 0.18f, 0.05f), sky = new Color(0.45f, 0.26f, 0.16f), ground = new Color(1f, 0.46f, 0.16f),
            background = new Color(0.12f, 0.03f, 0.01f), fogDensity = 0.005f,
        },
    };

    // the ridge of a ramp as a line of points (a straight one is a single piece, so it's perfectly smooth to surf)
    public static Vector3[] SurfRidge(SurfRampDef r)
    {
        if (!r.curved) return new[] { r.a, r.b };
        int n = 24;
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            pts[i] = (1 - t) * (1 - t) * r.a + 2 * (1 - t) * t * r.ctrl + t * t * r.b;
        }
        return pts;
    }

    public static void SurfFromCommandLine()
    {
        try { BuildSurf(); SlideTests.RunSurf(); SurfShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildSurf()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/SurfFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        RealBegin(SurfTexDir, SurfMatDir);
        toonMat = Mat("Toon", "3daigamesolo/World/Toon");          // (the runner, seen in third person)
        outlineMat = Mat("Outline", "3daigamesolo/World/Outline");

        RLit("rock_red", "rock_red", null, 0f, 0f, 0f, 0.05f);
        RLit("rock_blue", "rock_blue", null, 0f, 0f, 0f, 0.1f);
        RLit("rock_lava", "rock_lava", new Color(0.85f, 0.7f, 0.6f), 0.35f);
        RLit("sand", "sand");
        RLit("lava", "lava", new Color(0.9f, 0.75f, 0.6f), 1.25f, 0f, 0f, 0f, 0f, false, false, new Vector2(0.01f, 0.006f));
        RLit("ramp", "ramp", new Color(0.78f, 0.8f, 0.84f), 0f, 0f, 0f, 0.25f);
        RLit("tile", "tile", new Color(0.82f, 0.82f, 0.82f), 0f, 0f, 0f, 0.15f);
        RLit("cyan", "white", new Color(0.15f, 0.95f, 1f), 3.2f, 0f, 0f, 0f, 0f, false, false);
        RLit("tunnel_end", "white", new Color(0.6f, 0.85f, 0.95f), 1.6f, 0f, 0f, 0f, 0f, false, false);
        RLit("stalactite", "stalactite", null, 0f, 0f, 0f, 0.4f);
        RLit("dark", "white", new Color(0.06f, 0.07f, 0.08f), 0f, 0f, 0f, 0.6f);
        RGlow("lamp", "glow", new Color(0.3f, 0.95f, 1f, 0.6f));
        var skyMat = RMatAsset("SurfSky", "3daigamesolo/World/RealSky");
        skyMat.SetColor("_Zenith", new Color(0.3f, 0.52f, 0.82f)); skyMat.SetColor("_Horizon", new Color(0.78f, 0.86f, 0.94f));
        skyMat.SetColor("_Below", new Color(0.7f, 0.6f, 0.5f)); skyMat.SetColor("_SunGlow", new Color(1f, 0.9f, 0.7f));
        skyMat.SetTexture("_Clouds", RTex("clouds")); skyMat.SetFloat("_CloudCover", 0.55f); skyMat.SetFloat("_CloudScale", 0.5f);
        skyMat.SetColor("_CloudColor", Color.white); skyMat.SetColor("_CloudShade", new Color(0.7f, 0.74f, 0.82f));
        var post = RMatAsset("SurfPost", "3daigamesolo/Post/Real");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RealScene();
        featureRoot = new GameObject("Skate features").transform;
        RealSun(new Vector3(0.35f, 0.85f, -0.4f), new Color(1f, 0.95f, 0.86f), 0.85f, 0.75f);

        var stages = new List<SurfLevel.Stage>();
        for (int i = 0; i < SurfStages.Length; i++)
        {
            stages.Add(SurfStage(i, SurfStages[i]));
            if (!SurfStages[i].roof) RealSky(skyMat, SurfO(i) + new Vector3(0f, 0f, 200f), 2600f);
        }

        var camGO = RealCamera(typeof(SurfLevel), 6000f, SurfStages[0].background);
        var look = camGO.GetComponent<RealLook>();
        look.post = post; look.bloomThreshold = 1.25f; look.bloom = 0.35f; look.exposure = 1.0f; look.contrast = 1.06f; look.saturation = 1.08f;
        look.shadowTint = new Color(0.45f, 0.5f, 0.56f, 0.2f); look.highTint = new Color(0.52f, 0.5f, 0.47f, 0.1f); look.vignette = 0.25f; look.grain = 0.012f;
        look.fogStart = 20f; look.fogHeight = 120f; look.fogMax = 0.9f;
        var player = BuildPlayer(stages[0].start, camGO.transform);
        SurfPlayer(player);
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "surf_mesa"; hud.noSpeed = true;
        hud.hint = "MOUSE + A/D: hold A or D into the ramp, steer with the mouse (no W on a ramp)    SPACE jump    R stage start    T restart run    TAB level";
        var level = camGO.GetComponent<SurfLevel>();
        level.player = player; level.stages = stages.ToArray(); level.look = look;
        level.ApplyLook(0);
        camGO.transform.SetPositionAndRotation(stages[0].start.position + new Vector3(0f, 1.5f, 0f), stages[0].start.rotation);

        EditorSceneManager.SaveScene(scene, SurfPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_SURF_BUILT features=" + featureCount + " stages=" + stages.Count);
    }

    // Source movement: no board, 250 u/s on the ground, Source gravity and jump, air strafing, no speed cap to speak of
    public static void SurfPlayer(SlideController p)
    {
        p.alwaysBoard = false;
        p.board = null;
        p.surf = true;
        p.doubleJumpOn = false;
        p.gravityScale = 20.3f / 9.81f;
        p.fallBoost = 1f;
        p.jumpSpeed = 7.4f;
        p.jumpHoldMax = 0f;
        p.walkSpeed = 6.35f;
        p.sprintSpeed = 6.35f;
        p.runAccel = 45f;
        p.stopDecel = 40f;
        p.hopBoost = 0f;
        p.maxSpeed = 90f;
        p.fallLimit = -500f;
        p.fov = 74f;
        p.fovAtSpeed = 84f;
    }

    static SurfLevel.Stage SurfStage(int i, SurfStageDef d)
    {
        Vector3 O = SurfO(i);
        var g = new GameObject("Stage " + (i + 1) + " " + d.name).transform;
        float z0 = SurfZ0, z1 = SurfZ0 + d.length;

        // the floor: sand (or lava), which sends you back
        var floor = new PSXKit();
        floor.Ground(O.x - d.halfW - 20f, O.z + z0 - 5f, O.x + d.halfW + 20f, O.z + z1 + 40f, 0f, d.lava ? 18f : 10f, 10f);
        var fgo = SurfObject(g, d.lava ? "Lava" : "Sand", floor, d.lava ? "lava" : "sand", true);
        fgo.AddComponent<SurfSand>();

        // the rock round it
        SurfRock(g, O, d, z0, z1);

        // the start zone
        Vector3 sc = O + new Vector3(0f, d.startY, -8f);
        var tile = RK("tile", true); var side = RK("ramp", true); var cyan = RK("cyan");
        WBox(tile, sc - Vector3.up * 1f, new Vector3(16f, 2f, 16f), Quaternion.identity, 4f);
        foreach (var e in new[] { new Vector3(0f, 0f, 7.85f), new Vector3(0f, 0f, -7.85f), new Vector3(7.85f, 0f, 0f), new Vector3(-7.85f, 0f, 0f) })
        {
            Vector3 size = Mathf.Abs(e.x) > 0.1f ? new Vector3(0.3f, 0.08f, 16f) : new Vector3(16f, 0.08f, 0.3f);
            WBox(cyan, sc + e + Vector3.up * 0.03f, size, Quaternion.identity, 4f);
        }
        WBox(side, sc - Vector3.up * 4f, new Vector3(3f, 6f, 3f), Quaternion.identity, 4f);   // a stem under it
        var start = new GameObject("Stage " + (i + 1) + " start").transform;
        start.SetParent(g, false);
        start.SetPositionAndRotation(sc + new Vector3(0f, 0.05f, -4f), Quaternion.LookRotation(Vector3.forward));

        // the ramps
        foreach (var r in d.ramps) SurfRamp(O, r);

        // the end: a tunnel in the end wall, or the finish platform
        Bounds end;
        if (d.finish == Vector3.zero)
        {
            Vector3 t = O + d.tunnel;
            SurfTunnel(t, z1, d);
            end = new Bounds(new Vector3(t.x, t.y, O.z + z1 + 14f), new Vector3(SurfTunnelR * 2f, SurfTunnelR * 2f, 28f));
        }
        else
        {
            Vector3 f = O + d.finish;
            SurfFinish(f);
            // the whole end of the chamber counts: get over the platform (or past it) and you're done
            end = new Bounds(new Vector3(f.x, f.y + 30f, O.z + (z1 + f.z - SurfFinishR - 10f) * 0.5f), new Vector3(d.halfW * 2f, 62f, z1 - (f.z - SurfFinishR - 10f)));
        }

        // lamps along the walls
        var lamp = RK("lamp");
        for (float z = 20f; z < d.length - 50f; z += 45f)
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 at = O + new Vector3(sx * (d.halfW - 5f), d.roof ? 30f : 20f, z);
                FlatCard(lamp, at, Vector3.forward * 2.5f, Vector3.up * 2.5f, new Color32(255, 255, 255, 255), Vector3.right * -sx);
                WBox(cyan, at, new Vector3(0.4f, 0.4f, 1.2f), Quaternion.identity, 2f);
            }

        RFlush(g);
        return new SurfLevel.Stage
        {
            name = d.name, start = start,
            startZone = new Bounds(sc + Vector3.up * 2.5f, new Vector3(16.5f, 6f, 16.5f)),
            end = end,
            fog = d.fog, fogSun = d.fogSun, sky = d.sky, ground = d.ground, background = d.background, fogDensity = d.fogDensity,
        };
    }

    static GameObject SurfObject(Transform parent, string name, PSXKit k, string mat, bool solid)
    {
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = rMats[mat];
        if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // A ramp: a long prism with its ridge along the top and a steep face down each side, pale grey-blue metal in panels,
    // a cyan strip glowing along the bottom of each face. Solid all round.
    static void SurfRamp(Vector3 O, SurfRampDef r)
    {
        Vector3[] ridge = SurfRidge(r);
        for (int i = 0; i < ridge.Length; i++) ridge[i] += O;
        int n = ridge.Length;
        float w = r.Width, drop = r.drop;
        float faceLen = Mathf.Sqrt(w * w + drop * drop);
        var side = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 t = ridge[Mathf.Min(i + 1, n - 1)] - ridge[Mathf.Max(i - 1, 0)];
            t.y = 0f;
            side[i] = Vector3.Cross(Vector3.up, t.normalized).normalized;
        }
        var metal = RK("ramp", true);
        var glow = RK("cyan");
        float along = 0f;
        for (int i = 0; i < n - 1; i++)
        {
            float seg = Vector3.Distance(ridge[i], ridge[i + 1]);
            float u0 = r.curved ? i : along / 9f, u1 = r.curved ? i + 1 : (along + seg) / 9f;
            along += seg;
            foreach (float f in new[] { 1f, -1f })
            {
                Vector3 a0 = ridge[i], a1 = ridge[i + 1];
                Vector3 b0 = a0 + side[i] * f * w - Vector3.up * drop, b1 = a1 + side[i + 1] * f * w - Vector3.up * drop;
                Vector3 nrm = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                if (Vector3.Dot(nrm, side[i] * f) < 0f) nrm = -nrm;
                metal.Quad(a0, a1, b1, b0, new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, faceLen / 6f), new Vector2(u0, faceLen / 6f), nrm);
                // the cyan strip: just up the face from its bottom edge
                Vector3 up0 = (a0 - b0).normalized * 0.45f, up1 = (a1 - b1).normalized * 0.45f;
                glow.Quad(b0 + nrm * 0.03f, b1 + nrm * 0.03f, b1 + up1 + nrm * 0.03f, b0 + up0 + nrm * 0.03f, Vector2.zero, Vector2.zero, Vector2.one, Vector2.one, nrm);
            }
            // underneath
            Vector3 l0 = ridge[i] - side[i] * w - Vector3.up * drop, l1 = ridge[i + 1] - side[i + 1] * w - Vector3.up * drop;
            Vector3 r0 = ridge[i] + side[i] * w - Vector3.up * drop, r1 = ridge[i + 1] + side[i + 1] * w - Vector3.up * drop;
            metal.Quad(l0, l1, r1, r0, Vector2.zero, new Vector2(1f, 0f), Vector2.one, new Vector2(0f, 1f), Vector3.down);
        }
        // the two ends
        for (int e = 0; e < 2; e++)
        {
            int i = e == 0 ? 0 : n - 1;
            Vector3 t = (ridge[Mathf.Min(i + 1, n - 1)] - ridge[Mathf.Max(i - 1, 0)]).normalized * (e == 0 ? -1f : 1f);
            Vector3 top = ridge[i], l = top - side[i] * w - Vector3.up * drop, rr = top + side[i] * w - Vector3.up * drop;
            metal.Quad(top, rr, l, l, new Vector2(0.5f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), t);
            glow.Quad(l + t * 0.03f, rr + t * 0.03f, rr + t * 0.03f + Vector3.up * 0.45f, l + t * 0.03f + Vector3.up * 0.45f, Vector2.zero, Vector2.zero, Vector2.one, Vector2.one, t);
        }
    }

    // the canyon walls (and in a cave, the roof), rough rock, along the stage
    static void SurfRock(Transform g, Vector3 O, SurfStageDef d, float z0, float z1)
    {
        float H = d.height, hw = d.halfW;
        var profiles = new List<List<Vector2>>();
        if (d.roof)
        {
            var p = new List<Vector2>();
            p.Add(new Vector2(hw, -3f));
            for (float y = 4f; y < H * 0.45f; y += 4f) p.Add(new Vector2(hw, y));
            for (int a = 0; a <= 24; a++)
            {
                float th = a * Mathf.PI / 24f;
                p.Add(new Vector2(hw * Mathf.Cos(th), H * 0.45f + H * 0.55f * Mathf.Sin(th)));
            }
            for (float y = H * 0.45f - 4f; y > 0f; y -= 4f) p.Add(new Vector2(-hw, y));
            p.Add(new Vector2(-hw, -3f));
            profiles.Add(p);
        }
        else
        {
            foreach (float s in new[] { 1f, -1f })
            {
                var p = new List<Vector2>();
                p.Add(new Vector2(s * hw, -3f));
                for (float y = 4f; y <= H; y += 4f) p.Add(new Vector2(s * (hw + 9f * (y / H) * (y / H)), y));
                p.Add(new Vector2(s * (hw + 20f), H + 3f));
                p.Add(new Vector2(s * (hw + 90f), H + 8f));
                if (s < 0f) p.Reverse();
                profiles.Add(p);
            }
        }
        foreach (var prof in profiles) RockSheet(g, O, prof, z0, z1 + 40f, d.rock, 5.5f);
        // the back wall behind the start, and the end wall (with the tunnel through it)
        RockCap(g, O, d, z0, false);
        RockCap(g, O, d, z1, d.finish == Vector3.zero);

        // stalactites from the roof
        if (d.roof)
        {
            var rng = new System.Random(4000 + (int)O.x);
            var k = RK("stalactite");
            for (int s = 0; s < 70; s++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * hw * 0.85f, z = z0 + 10f + (float)rng.NextDouble() * (z1 - z0 - 20f);
                float roofY = H * 0.45f + H * 0.55f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / hw) * (x / hw)));
                float len = 4f + (float)rng.NextDouble() * 12f, rad = 0.6f + (float)rng.NextDouble() * 1.4f;
                Vector3 top = O + new Vector3(x, roofY + 2f, z);
                WCyl(k, top, top - Vector3.up * len, rad, 0.05f, 7, 4f);
            }
        }
    }

    static float RockNoise(float a, float b)
    {
        return (Mathf.PerlinNoise(a * 0.045f + 3.1f, b * 0.045f + 7.7f) - 0.5f) * 2f
             + (Mathf.PerlinNoise(a * 0.13f + 11.3f, b * 0.13f + 2.9f) - 0.5f) * 0.9f
             + (Mathf.PerlinNoise(a * 0.4f + 5.5f, b * 0.4f + 9.1f) - 0.5f) * 0.3f;
    }

    // a sheet of rock swept along z from a cross-section, pushed in and out by noise (less near the floor), smooth-shaded
    static void RockSheet(Transform g, Vector3 O, List<Vector2> prof, float z0, float z1, string mat, float amp)
    {
        int nz = Mathf.CeilToInt((z1 - z0) / 3f) + 1, np = prof.Count;
        // arc length along the profile, for the texture and the noise
        var arc = new float[np];
        for (int j = 1; j < np; j++) arc[j] = arc[j - 1] + Vector2.Distance(prof[j - 1], prof[j]);
        var verts = new Vector3[nz * np];
        var uvs = new Vector2[nz * np];
        Vector3 inside = new Vector3(O.x, O.y + 30f, 0f);
        for (int i = 0; i < nz; i++)
        {
            float z = Mathf.Lerp(z0, z1, i / (float)(nz - 1));
            for (int j = 0; j < np; j++)
            {
                Vector2 a = prof[Mathf.Max(j - 1, 0)], b = prof[Mathf.Min(j + 1, np - 1)];
                Vector2 tang = (b - a).normalized;
                Vector2 nrm = new Vector2(-tang.y, tang.x);
                Vector2 toMid = new Vector2(0f, 30f) - prof[j];
                if (Vector2.Dot(nrm, toMid) < 0f) nrm = -nrm;   // in towards the middle
                float k = Mathf.Clamp01((prof[j].y - 2f) / 10f);
                float push = RockNoise(z + O.x * 0.01f, arc[j]) * amp * (0.35f + 0.65f * k);
                Vector2 p = prof[j] + nrm * push;
                verts[i * np + j] = O + new Vector3(p.x, p.y, z);
                uvs[i * np + j] = new Vector2(z / 26f, arc[j] / 26f);
            }
        }
        var tris = new List<int>();
        for (int i = 0; i < nz - 1; i++)
            for (int j = 0; j < np - 1; j++)
            {
                int a = i * np + j, b = a + 1, c = a + np, dd = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(dd);
            }
        var mesh = new Mesh { name = "Rock " + mat, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.uv = uvs;
        var cols = new Color32[verts.Length];
        for (int v = 0; v < cols.Length; v++) cols[v] = new Color32(255, 255, 255, 255);
        mesh.colors32 = cols;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        // face the inside of the canyon
        var nm = mesh.normals;
        int mid = (nz / 2) * np + np / 2;
        Vector3 toward = new Vector3(O.x, O.y + 30f, verts[mid].z) - verts[mid];
        if (Vector3.Dot(nm[mid], toward) < 0f)
        {
            for (int t = 0; t < tris.Count; t += 3) { int s = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = s; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        SaveFeatureMesh(mesh);
        var go = new GameObject("Rock", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(g, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = rMats[mat];
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // the wall across the end of the stage (or the back), rough rock, with a round hole for the tunnel if it has one
    static void RockCap(Transform g, Vector3 O, SurfStageDef d, float z, bool hole)
    {
        float hw = d.halfW + 30f, H = d.height + 10f;
        int nx = Mathf.CeilToInt(hw * 2f / 2f) + 1, ny = Mathf.CeilToInt((H + 3f) / 2f) + 1;
        Vector3 hc = O + d.tunnel;
        var k = RK(d.rock, true);
        float dir = z > 0f ? 1f : -1f;   // which way is out of the stage
        System.Func<int, int, Vector3> at = (i, j) =>
        {
            float x = -hw + i * 2f, y = -3f + j * 2f;
            Vector3 p = O + new Vector3(x, y, z);
            if (hole)
            {
                Vector2 off = new Vector2(p.x - hc.x, p.y - hc.y);
                if (off.magnitude < SurfTunnelR) { off = off.normalized * SurfTunnelR; p.x = hc.x + off.x; p.y = hc.y + off.y; return p; }
                float edge = Mathf.Clamp01((off.magnitude - SurfTunnelR) / 6f);
                p.z += RockNoise(x + 40f, y) * 4f * edge;
                return p;
            }
            p.z += RockNoise(x + 40f, y) * 4f;
            // up top it slopes back like a hillside, with a ragged skyline
            float over = Mathf.Max(0f, y - d.height * 0.7f);
            p.z += dir * over * 1.4f;
            p.y += over * 0.3f * RockNoise(x * 0.5f, 90f);
            return p;
        };
        for (int i = 0; i < nx - 1; i++)
            for (int j = 0; j < ny - 1; j++)
            {
                Vector3 a = at(i, j), b = at(i, j + 1), c = at(i + 1, j + 1), e = at(i + 1, j);
                if (hole)
                {
                    float m = new Vector2((a.x + c.x) * 0.5f - hc.x, (a.y + c.y) * 0.5f - hc.y).magnitude;
                    if (m < SurfTunnelR + 0.2f) continue;
                }
                k.Quad(a, b, c, e, new Vector2(a.x, a.y) / 26f, new Vector2(b.x, b.y) / 26f, new Vector2(c.x, c.y) / 26f, new Vector2(e.x, e.y) / 26f, new Vector3(0f, 0f, -dir));
            }
    }

    // the tunnel through the end wall: a round bore lined with rock, cyan lights round its mouth, white light inside
    static void SurfTunnel(Vector3 c, float z1, SurfStageDef d)
    {
        var rock = RK(d.rock, true);
        var cyan = RK("cyan");
        int sides = 24;
        float len = 34f, R = SurfTunnelR;
        Vector3 mouth = new Vector3(c.x, c.y, c.z - (d.tunnel.z - z1));
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
            rock.Quad(mouth + d0 * R, mouth + d0 * R + Vector3.forward * len, mouth + d1 * R + Vector3.forward * len, mouth + d1 * R,
                      new Vector2(s / 3f, 0f), new Vector2(s / 3f, len / 14f), new Vector2((s + 1) / 3f, len / 14f), new Vector2((s + 1) / 3f, 0f), -(d0 + d1));
            // a ring of lights just inside the mouth, and another further in
            foreach (float zz in new[] { 0.6f, 12f })
                cyan.Quad(mouth + d0 * (R - 0.05f) + Vector3.forward * zz, mouth + d0 * (R - 0.05f) + Vector3.forward * (zz + 0.6f),
                          mouth + d1 * (R - 0.05f) + Vector3.forward * (zz + 0.6f), mouth + d1 * (R - 0.05f) + Vector3.forward * zz,
                          Vector2.zero, Vector2.zero, Vector2.one, Vector2.one, -(d0 + d1));
        }
        var end = RK("tunnel_end");
        end.Quad(mouth + new Vector3(-R, -R, len - 0.5f), mouth + new Vector3(-R, R, len - 0.5f), mouth + new Vector3(R, R, len - 0.5f), mouth + new Vector3(R, -R, len - 0.5f),
                 Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.back);
        var glow = RK("lamp");
        FlatCard(glow, mouth + Vector3.forward * (len - 2f), Vector3.right * R * 1.6f, Vector3.up * R * 1.6f, new Color32(255, 255, 255, 255), Vector3.back);
    }

    // the finish: an octagon platform, dark, ringed in cyan
    static void SurfFinish(Vector3 top)
    {
        var dark = RK("dark", true);
        var cyan = RK("cyan");
        float R = SurfFinishR, thick = 3f;
        for (int s = 0; s < 8; s++)
        {
            float a0 = (s + 0.5f) * Mathf.PI / 4f, a1 = (s + 1.5f) * Mathf.PI / 4f;
            Vector3 p0 = top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * R, p1 = top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * R;
            dark.Quad(top, p0, p1, p1, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.right, Vector2.right, Vector3.up);
            Vector3 outN = ((p0 + p1) * 0.5f - top).normalized;
            dark.Quad(p0, p1, p1 - Vector3.up * thick, p0 - Vector3.up * thick, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outN);
            dark.Quad(p0 - Vector3.up * thick, p1 - Vector3.up * thick, top - Vector3.up * thick, top - Vector3.up * thick, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.down);
            // the cyan ring, just inside the edge
            Vector3 i0 = top + (p0 - top) * 0.93f, i1 = top + (p1 - top) * 0.93f;
            cyan.Quad(i0 + Vector3.up * 0.03f, p0 + Vector3.up * 0.03f, p1 + Vector3.up * 0.03f, i1 + Vector3.up * 0.03f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.up);
        }
        // a stem down into the lava
        WBox(RK("dark", true), top - Vector3.up * 10f, new Vector3(5f, 14f, 5f), Quaternion.identity, 4f);
    }

    public static void SurfShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        var level = Object.FindFirstObjectByType<SurfLevel>();
        for (int i = 0; i < SurfStages.Length; i++)
        {
            level.ApplyLook(i);
            Vector3 O = SurfO(i);
            var d = SurfStages[i];
            Shot(cam, O + new Vector3(0f, d.startY + 1.6f, -10f), O + new Vector3(-4f, d.startY - 14f, 40f), "u" + (i + 1) + "1-start.png");
            var r = d.ramps[0];
            Vector3 mid = O + (r.a + r.b) * 0.5f;
            Shot(cam, mid + new Vector3(r.a.x < 0f ? 10f : -10f, -4f, -30f), mid + new Vector3(0f, -6f, 20f), "u" + (i + 1) + "2-ramp.png");
            Vector3 endAt = d.finish == Vector3.zero ? O + d.tunnel : O + d.finish;
            Shot(cam, endAt + new Vector3(6f, 6f, -60f), endAt, "u" + (i + 1) + "3-end.png");
        }
        level.ApplyLook(0);
    }
}
