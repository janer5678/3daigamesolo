using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Circulation: after the Renai Circulation video. A white stage that goes on and on, a peach stripe round the
// horizon, tall pink poles, and circles drawn on the floor in pastel colours, two of them sunk into bowls. Carve a
// full lap round a circle and it lights up and speeds you on (CirculationLevel); light them all. A great ring of
// rail runs round the middle to grind round and round, pink "LOVE?" cards lean up as banks, and hiragana float
// about. Flat pastel shading (SkatePastel.shader) with the video's doubled pale-blue and pink silhouettes over it
// all (PastelPost.shader).
public static partial class SkateWorldBuilder
{
    public const string CirculationPath = "Assets/Scenes/Circulation.unity";
    const string CircTex = "Assets/Circulation/Textures";

    public struct CircDef { public Vector3 c; public float r; public bool bowl; public float depth; public Color32 col; }
    public const float CircArena = 140f, CircRingR = 32f, CircRingY = 0.9f, CircFlatR = 8f, CircFlatBand = 2.6f;
    public static readonly CircDef[] CircDefs = MakeCircDefs();

    static CircDef[] MakeCircDefs()
    {
        var l = new List<CircDef>
        {
            new CircDef { c = Vector3.zero, r = 12f, bowl = true, depth = 2.6f, col = new Color32(246, 170, 196, 255) },
            new CircDef { c = new Vector3(78f, 0f, 46f), r = 10f, bowl = true, depth = 2.2f, col = new Color32(170, 200, 246, 255) },
        };
        Color32[] cols = { new Color32(246, 214, 120, 255), new Color32(250, 170, 190, 255), new Color32(160, 196, 250, 255), new Color32(206, 170, 240, 255), new Color32(160, 226, 196, 255), new Color32(250, 196, 150, 255) };
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 15f) * Mathf.Deg2Rad;
            l.Add(new CircDef { c = new Vector3(Mathf.Cos(a) * 62f, 0f, Mathf.Sin(a) * 62f), r = CircFlatR, bowl = false, col = cols[i] });
        }
        return l.ToArray();
    }

    // the floor's height: flat, but sunk where the bowls are (a flat bottom, then a wall curving up to upright)
    public static float CircH(float x, float z)
    {
        float h = 0f;
        foreach (var d in CircDefs)
        {
            if (!d.bowl) continue;
            float r = new Vector2(x - d.c.x, z - d.c.z).magnitude;
            if (r >= d.r) continue;
            float flat = d.r * 0.4f, t = Mathf.Clamp01((r - flat) / (d.r - flat));
            h = Mathf.Min(h, -d.depth * Mathf.Sqrt(1f - t * t));
        }
        return h;
    }

    static Transform cRoot;
    static Material cCard, cKana, cGlow;

    public static void CirculationFromCommandLine()
    {
        try { BuildCirculation(); SlideTests.RunCirculation(); CirculationShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static Material CMat(string name, string tex, float cutoff, float glow)
    {
        var m = Mat(name, "3daigamesolo/World/Pastel");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(CircTex + "/" + tex + ".png"));
        m.SetFloat("_Cutoff", cutoff); m.SetFloat("_Glow", glow);
        return m;
    }

    public static void BuildCirculation()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/CirculationFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        floorAt = CircH;
        RampTop = new Color32(252, 248, 246, 255); RampSide = new Color32(246, 196, 210, 255); RampSide2 = new Color32(200, 216, 250, 255);
        Metal = new Color32(240, 200, 210, 255); RailCol = new Color32(240, 140, 170, 255); Concrete = new Color32(250, 246, 244, 255); ConcreteDark = new Color32(240, 214, 222, 255);

        toonMat = CMat("CircPastel", "white", 0f, 0f);
        cCard = CMat("CircCard", "love", 0f, 0f);
        cKana = CMat("CircKana", "kana", 0.4f, 1f);
        cGlow = CMat("CircGlow", "white", 0f, 1f);
        outlineMat = Mat("CircOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.62f, 0.42f, 0.5f)); outlineMat.SetFloat("_OutlineWidth", 0.6f);
        outlineMat.SetColor("_Haze", new Color(0.99f, 0.98f, 0.97f)); outlineMat.SetColor("_HazeHigh", new Color(0.99f, 0.98f, 0.97f)); outlineMat.SetFloat("_FogDensity", 0.004f);
        farOutlineMat = outlineMat;
        var post = Mat("CircPost", "3daigamesolo/Post/Pastel");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;
        cRoot = new GameObject("Circulation").transform;

        CircFloor();
        var circles = CircCircles();
        CircRing();
        CircCards();
        CircPoles();
        CircHorizon();
        CircKanaAll();
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(-20f, 0f, -55f), Quaternion.identity, Vector3.one), 4f, 26f, 4f, RampSide);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(40f, 0f, -20f), Quaternion.LookRotation(Vector3.right), Vector3.one), 4f, 26f, 4f, RampSide2);
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(-45f, 0f, 10f), Quaternion.LookRotation(Vector3.forward), Vector3.one), 6f);

        // you, out to the south, the big bowl and the ring rail in front of you
        Vector3 startAt = new Vector3(0f, 0.2f, -48f);
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(startAt, Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(CirculationLevel));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.99f, 0.98f, 0.97f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -30f;
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = "Circulation";
        camGO.transform.SetPositionAndRotation(startAt + new Vector3(0f, 2.5f, -4f), Quaternion.LookRotation(Vector3.forward));
        player.board = BuildBoard(startAt + Vector3.forward * 2f, Quaternion.LookRotation(Vector3.forward));
        var level = camGO.GetComponent<CirculationLevel>();
        level.player = player; level.circles = circles; level.post = post;

        EditorSceneManager.SaveScene(scene, CirculationPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_CIRCULATION_BUILT features=" + featureCount + " circles=" + circles.Length);
    }

    static GameObject CircSolid(string name, SkateMeshKit k, Material m, bool collide, bool outline = true)
    {
        var mesh = k.Build(name);
        SaveFeatureMesh(mesh);
        var go = MeshObject(name, cRoot, mesh, m, outline ? outlineMat : null);
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // ---- the floor: white, the bowls sunk into it --------------------------------------------------------------

    static void CircFloor()
    {
        float a = CircArena, cell = 1f;
        int n = Mathf.RoundToInt(2f * a / cell);
        var verts = new Vector3[(n + 1) * (n + 1)];
        var cols = new Color32[verts.Length];
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -a + i * cell, z = -a + j * cell;
                float h = CircH(x, z);
                verts[j * (n + 1) + i] = new Vector3(x, h, z);
                Color32 col = new Color32(252, 250, 248, 255);
                // in a bowl: washed with its colour, more so the deeper it gets
                foreach (var d in CircDefs)
                    if (d.bowl && h < 0f && new Vector2(x - d.c.x, z - d.c.z).magnitude < d.r)
                        col = Color32.Lerp(col, d.col, 0.25f + 0.45f * (-h / d.depth));
                cols[j * (n + 1) + i] = col;
            }
        var tris = new List<int>();
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int p = j * (n + 1) + i;
                tris.Add(p); tris.Add(p + n + 1); tris.Add(p + 1);
                tris.Add(p + 1); tris.Add(p + n + 1); tris.Add(p + n + 2);
            }
        var mesh = new Mesh { name = "Floor", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.colors32 = cols; mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveFeatureMesh(mesh);
        var go = MeshObject("Floor", cRoot, mesh, toonMat, null);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        var far = new SkateMeshKit();
        far.Quad(new Vector3(-2500f, -0.02f, -2500f), new Vector3(-2500f, -0.02f, 2500f), new Vector3(2500f, -0.02f, 2500f), new Vector3(2500f, -0.02f, -2500f), Vector3.up, new Color32(252, 250, 248, 255));
        CircSolid("Far floor", far, toonMat, false, false);
        foreach (var s in new[] { -1f, 1f })
        {
            AddBox(cRoot, "Edge", new Vector3(s * (a + 0.5f), 10f, 0f), new Vector3(1f, 30f, 2f * a));
            AddBox(cRoot, "Edge", new Vector3(0f, 10f, s * (a + 0.5f)), new Vector3(2f * a, 30f, 1f));
        }
    }

    // ---- the circles: a line on the floor, and a filled disc that shows when it's lit -----------------------------

    static CirculationLevel.Circle[] CircCircles()
    {
        var list = new List<CirculationLevel.Circle>();
        foreach (var d in CircDefs)
        {
            var line = new SkateMeshKit();
            float ringR = d.bowl ? d.r - 0.15f : d.r;
            CircRingOnFloor(line, d.c, ringR - 0.14f, ringR + 0.14f, d.col, 0.03f);
            if (!d.bowl) CircRingOnFloor(line, d.c, d.r - CircFlatBand, d.r - CircFlatBand + 0.08f, d.col, 0.03f);
            CircSolid("Circle line", line, cGlow, false, false);
            // lit: the ring band filled with its colour (a bowl: its whole inside)
            var disc = new SkateMeshKit();
            CircRingOnFloor(disc, d.c, d.bowl ? 0f : d.r - CircFlatBand, d.bowl ? d.r : d.r + CircFlatBand, d.col, 0.02f);
            var lit = CircSolid("Circle lit", disc, cGlow, false, false);
            lit.SetActive(false);
            list.Add(new CirculationLevel.Circle { centre = d.c, radius = d.bowl ? d.r * 0.72f : d.r, band = d.bowl ? d.r * 0.32f : CircFlatBand, litDisc = lit });
        }
        return list.ToArray();
    }

    // a flat ring (or disc) lying on the floor, following it down into a bowl
    static void CircRingOnFloor(SkateMeshKit k, Vector3 c, float r0, float r1, Color32 col, float lift)
    {
        int segs = 72, rings = Mathf.Max(1, Mathf.CeilToInt((r1 - r0) / 1f));
        for (int s = 0; s < segs; s++)
        {
            float a0 = s * Mathf.PI * 2f / segs, a1 = (s + 1) * Mathf.PI * 2f / segs;
            for (int q = 0; q < rings; q++)
            {
                float ra = Mathf.Lerp(r0, r1, q / (float)rings), rb = Mathf.Lerp(r0, r1, (q + 1f) / rings);
                System.Func<float, float, Vector3> P = (r, a) => { float x = c.x + Mathf.Cos(a) * r, z = c.z + Mathf.Sin(a) * r; return new Vector3(x, CircH(x, z) + lift, z); };
                k.Quad(P(ra, a0), P(rb, a0), P(rb, a1), P(ra, a1), Vector3.up, col);
            }
        }
    }

    // ---- the great ring rail round the middle ---------------------------------------------------------------------

    static void CircRing()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i <= 96; i++)
        {
            float a = i / 96f * Mathf.PI * 2f;
            pts.Add(new Vector3(Mathf.Cos(a) * CircRingR, CircRingY, Mathf.Sin(a) * CircRingR));
        }
        Rail("Ring rail", pts.ToArray());
    }

    // ---- LOVE? cards: pink banks leaning up off the floor ------------------------------------------------------------

    static void CircCards()
    {
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 at = new Vector3(Mathf.Cos(a) * 44f, 0f, Mathf.Sin(a) * 44f), dir = -at.normalized;   // rising towards the middle
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float len = 6f, hgt = 2.4f, w = 8f;
            Vector3 top = at + dir * len + Vector3.up * hgt;
            var card = new PSXKit();
            card.Quad(at - side * w * 0.5f, top - side * w * 0.5f, top + side * w * 0.5f, at + side * w * 0.5f, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.Cross(top - at, side).y > 0 ? Vector3.Cross(top - at, side) : -Vector3.Cross(top - at, side));
            var mesh = card.Build("LOVE card");
            SaveFeatureMesh(mesh);
            MeshObject("LOVE card", cRoot, mesh, cCard, outlineMat);
            var body = new SkateMeshKit();
            Vector3 back = at + dir * len;
            body.Quad(back - side * w * 0.5f, back - side * w * 0.5f + Vector3.up * hgt, back + side * w * 0.5f + Vector3.up * hgt, back + side * w * 0.5f, dir, new Color32(246, 196, 210, 255));
            foreach (var s in new[] { -1f, 1f })
                body.Tri(at + side * s * w * 0.5f, top + side * s * w * 0.5f, back + side * s * w * 0.5f, side * s, new Color32(240, 170, 190, 255));
            CircSolid("LOVE card body", body, toonMat, false);
            float slope = Mathf.Atan2(hgt, len) * Mathf.Rad2Deg, l2 = Mathf.Sqrt(hgt * hgt + len * len);
            Quaternion q = Quaternion.LookRotation(dir) * Quaternion.Euler(-slope, 0f, 0f);
            AddBox(cRoot, "LOVE card", (at + top) / 2f + q * Vector3.down * 0.15f, new Vector3(w, 0.3f, l2), q);
            AddBox(cRoot, "LOVE card back", back + Vector3.up * hgt / 2f - dir * 0.15f, new Vector3(w, hgt, 0.3f), Quaternion.LookRotation(dir));
        }
    }

    // ---- tall pink poles, the peach stripe round the horizon, floating hiragana -----------------------------------------

    static void CircPoles()
    {
        var k = new SkateMeshKit();
        var rng = new System.Random(5);
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f * Mathf.Deg2Rad + (float)rng.NextDouble() * 0.2f, r = i % 2 == 0 ? 100f : 22f + (float)rng.NextDouble() * 6f;
            if (i % 2 == 1 && i % 4 != 1) r = 92f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (CircH(p.x, p.z) < -0.01f) continue;
            k.Cylinder(p, p + Vector3.up * 20f, 0.35f, 10, new Color32(246, 176, 196, 255));
            AddBox(cRoot, "Pole", p + Vector3.up * 10f, new Vector3(0.7f, 20f, 0.7f));
        }
        CircSolid("Poles", k, toonMat, false);
    }

    static void CircHorizon()
    {
        var k = new SkateMeshKit();
        int segs = 96; float r = 700f;
        for (int s = 0; s < segs; s++)
        {
            float a0 = s * Mathf.PI * 2f / segs, a1 = (s + 1) * Mathf.PI * 2f / segs;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            k.Quad(p0 + Vector3.up * 14f, p1 + Vector3.up * 14f, p1 + Vector3.up * 24f, p0 + Vector3.up * 24f, -(p0 + p1).normalized, new Color32(244, 206, 140, 255));
        }
        var go = CircSolid("Horizon stripe", k, cGlow, false, false);
        go.GetComponent<Renderer>().sharedMaterial = Mat("CircStripe", "3daigamesolo/World/Pastel");
        var m = go.GetComponent<Renderer>().sharedMaterial;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(CircTex + "/white.png")); m.SetFloat("_Glow", 1f); m.SetFloat("_Fade", 100000f); m.SetFloat("_Cutoff", 0f);
    }

    static void CircKanaAll()
    {
        var rng = new System.Random(11);
        for (int i = 0; i < 46; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 16f + (float)rng.NextDouble() * 110f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 3f + (float)rng.NextDouble() * 14f, Mathf.Sin(a) * r);
            int cellI = rng.Next(16);
            float s = 1.2f + (float)rng.NextDouble() * 1.6f;
            var k = new PSXKit();
            Rect uv = new Rect((cellI % 4) * 0.25f, 0.75f - (cellI / 4) * 0.25f, 0.25f, 0.25f);
            k.Panel(Vector3.zero, Vector3.right * s, Vector3.up * s, null, false, uv);   // (drawn from both sides by the shader)
            var mesh = k.Build("Kana");
            SaveFeatureMesh(mesh);
            var go = new GameObject("Kana", typeof(MeshFilter), typeof(MeshRenderer), typeof(Billboard), typeof(Spin));
            go.transform.SetParent(cRoot, false);
            go.transform.position = p;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = cKana;
            var spin = go.GetComponent<Spin>(); spin.degreesPerSecond = 0f; spin.bob = 0.8f; spin.bobSpeed = 0.5f + (float)rng.NextDouble() * 0.6f;
        }
    }

    public static void CirculationShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2.2f, -52f), new Vector3(0f, 1f, 0f), "c01-start.png");
        Shot(cam, new Vector3(0f, 50f, -110f), new Vector3(0f, 0f, 10f), "c02-overview.png");
        Shot(cam, new Vector3(10f, 2f, -16f), new Vector3(0f, -2f, 0f), "c03-bowl.png");
        var d = CircDefs[3];
        Shot(cam, d.c + new Vector3(-14f, 3f, -10f), d.c, "c04-circle.png");
    }
}
