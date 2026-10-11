using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The Pink Palace: a Y2K pink palace on a floating island out in deep space. Glossy pink tiles with
// halftone dots and glitter, white marble with pink trim, hearts, bows and giant strawberries, a
// ringed pink planet in the sky. Skate-wise: a grand staircase with handrails up to a terrace, banks
// either side, a fountain with a curved rail round it, a funbox, kickers, a halfpipe, a quarterpipe
// wall, ledges and rails. The edge of the island has railings with gaps; ride off and you fall into
// space and start again.
public static partial class SkateWorldBuilder
{
    public const string PalacePath = "Assets/Scenes/PinkPalace.unity";

    // the island: a rounded rectangle, floor at y = 0
    const float IslandHX = 90f, IslandHZ = 75f, IslandCorner = 26f;

    static void PalacePalette()
    {
        RampTop = new Color32(255, 206, 226, 255);
        RampSide = new Color32(255, 238, 246, 255);
        RampSide2 = new Color32(250, 186, 214, 255);
        Metal = new Color32(255, 246, 252, 255);
        RailCol = new Color32(255, 116, 178, 255);
        Concrete = new Color32(255, 242, 248, 255);
        ConcreteDark = new Color32(246, 196, 220, 255);
    }

    static readonly Color32 PPink = new Color32(250, 168, 202, 255), PPinkDeep = new Color32(236, 104, 160, 255);
    static readonly Color32 PPinkPale = new Color32(255, 214, 230, 255), PWhite = new Color32(255, 246, 250, 255);
    static readonly Color32 PLilac = new Color32(222, 196, 246, 255), PGlass = new Color32(206, 226, 255, 255);
    static readonly Color32 PRed = new Color32(244, 86, 128, 255), PLeaf = new Color32(140, 220, 170, 255), PSeed = new Color32(255, 236, 170, 255);
    static readonly Color32 PTileA = new Color32(255, 204, 224, 255), PTileB = new Color32(250, 186, 212, 255);

    public static void BuildPalace()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Scenes");
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/PalaceFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        PalacePalette();
        floorAt = (x, z) => 0f;

        // materials: the same toon look, shaded mauve, with a faint violet haze instead of the valley's pink-white
        Color haze = new Color(0.16f, 0.10f, 0.32f), hazeHigh = new Color(0.10f, 0.08f, 0.26f);
        toonMat = Mat("PalaceToon", "3daigamesolo/World/Toon");
        toonMat.SetColor("_ShadeTint", new Color(0.82f, 0.64f, 0.90f));
        SpaceHaze(toonMat, haze, hazeHigh);
        var floorMat = Mat("PalaceFloor", "3daigamesolo/World/Toon");
        floorMat.SetColor("_ShadeTint", new Color(0.82f, 0.64f, 0.90f));
        floorMat.SetColor("_DotColor", new Color(1f, 0.93f, 0.97f, 0.6f));
        floorMat.SetFloat("_DotScale", 1.2f);
        floorMat.SetFloat("_Glitter", 0.7f);
        SpaceHaze(floorMat, haze, hazeHigh);
        var shinyMat = Mat("PalaceShiny", "3daigamesolo/World/Toon");   // glitter on the palace walls and decorations
        shinyMat.SetColor("_ShadeTint", new Color(0.84f, 0.66f, 0.92f));
        shinyMat.SetFloat("_Glitter", 0.35f);
        SpaceHaze(shinyMat, haze, hazeHigh);
        outlineMat = Mat("PalaceOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", new Color(0.40f, 0.10f, 0.32f));
        SpaceHaze(outlineMat, haze, hazeHigh);
        farOutlineMat = Mat("PalaceOutlineFar", "3daigamesolo/World/Outline");
        farOutlineMat.SetColor("_OutlineColor", new Color(0.40f, 0.10f, 0.32f));
        farOutlineMat.SetFloat("_OutlineWidth", 1.1f);
        SpaceHaze(farOutlineMat, haze, hazeHigh);
        var spaceMat = Mat("Space", "3daigamesolo/World/Space");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        featureRoot = new GameObject("Skate features").transform;
        var deco = new GameObject("Palace").transform;

        Island(deco, floorMat);
        Balustrade(deco);
        PalaceBuilding(deco, shinyMat);
        Terrace(deco);
        PalaceFeatures();
        Fountain(deco, shinyMat);
        Strawberries(deco, shinyMat);
        FloatingThings(deco, shinyMat);
        Planet(deco);
        SpaceCourse(deco, shinyMat);
        Sparkles(deco);

        // sky
        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.name = "Space";
        Object.DestroyImmediate(sky.GetComponent<Collider>());
        sky.transform.localScale = Vector3.one * 8000f;
        sky.GetComponent<Renderer>().sharedMaterial = spaceMat;

        // you, at the front of the island looking up at the palace
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(new Vector3(0f, 0.2f, -62f), Quaternion.LookRotation(Vector3.forward));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 9000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.012f, 0.045f);
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -40f;
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Pink Palace";
        camGO.transform.SetPositionAndRotation(new Vector3(0f, 2.5f, -67f), Quaternion.LookRotation(Vector3.forward));

        EditorSceneManager.SaveScene(scene, PalacePath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_PALACE_BUILT features=" + featureCount);
    }

    // the pink palace opens first; Tab goes on to the valley
    static void SetBuildScenes()
    {
        var list = new List<EditorBuildSettingsScene>();
        if (File.Exists(PalacePath)) list.Add(new EditorBuildSettingsScene(PalacePath, true));
        if (File.Exists(ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void SpaceHaze(Material m, Color low, Color high)
    {
        m.SetColor("_Haze", low);
        m.SetColor("_HazeHigh", high);
        m.SetFloat("_FogDensity", 0.0016f);
        m.SetFloat("_FogHeight", 600f);
        m.SetFloat("_FogMax", 0.55f);
        EditorUtility.SetDirty(m);
    }

    static GameObject Solid(string name, Transform parent, SkateMeshKit kit, Material mat, bool collide)
    {
        var mesh = kit.Build(name);
        SaveFeatureMesh(mesh);
        var go = MeshObject(name, parent, mesh, mat, outlineMat);
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // ---- the island -------------------------------------------------------------------------

    // nearest point inside the rounded rectangle
    static Vector2 InIsland(Vector2 p, float shrink = 0f)
    {
        float hx = IslandHX - shrink, hz = IslandHZ - shrink, r = Mathf.Max(1f, IslandCorner - shrink);
        Vector2 q = new Vector2(Mathf.Clamp(p.x, -hx + r, hx - r), Mathf.Clamp(p.y, -hz + r, hz - r));
        Vector2 d = p - q;
        return d.magnitude > r ? q + d.normalized * r : p;
    }

    static List<Vector2> IslandOutline(float shrink, int perCorner = 12)
    {
        float hx = IslandHX - shrink, hz = IslandHZ - shrink, r = Mathf.Max(1f, IslandCorner - shrink);
        var pts = new List<Vector2>();
        Vector2[] centres = { new Vector2(hx - r, hz - r), new Vector2(-hx + r, hz - r), new Vector2(-hx + r, -hz + r), new Vector2(hx - r, -hz + r) };
        for (int c = 0; c < 4; c++)
            for (int i = 0; i <= perCorner; i++)
            {
                float a = (c * 90f + 90f * i / perCorner) * Mathf.Deg2Rad;
                pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        return pts;
    }

    static void Island(Transform parent, Material floorMat)
    {
        // the floor: big glossy tiles in two pinks, the grid pulled in to follow the rounded edge
        var top = new SkateMeshKit();
        float tile = 4f;
        for (float x = -IslandHX; x < IslandHX; x += tile)
            for (float z = -IslandHZ; z < IslandHZ; z += tile)
            {
                Vector2 c = new Vector2(x + tile / 2f, z + tile / 2f);
                if ((InIsland(c) - c).magnitude > tile * 0.75f) continue;
                Vector2 a = InIsland(new Vector2(x, z)), b = InIsland(new Vector2(x + tile, z));
                Vector2 cc = InIsland(new Vector2(x + tile, z + tile)), d = InIsland(new Vector2(x, z + tile));
                bool alt = ((int)((x + 1000f) / tile) + (int)((z + 1000f) / tile)) % 2 == 0;
                top.Quad(new Vector3(a.x, 0f, a.y), new Vector3(b.x, 0f, b.y), new Vector3(cc.x, 0f, cc.y), new Vector3(d.x, 0f, d.y), Vector3.up, alt ? PTileA : PTileB);
            }
        var floorMesh = top.Build("Palace floor");
        SaveFeatureMesh(floorMesh);
        var floor = MeshObject("Floor", parent, floorMesh, floorMat, outlineMat);
        floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;

        // the sides and the underneath: a white band with pink trim, then tiers narrowing down like
        // an upside-down cake to a crystal point
        var under = new SkateMeshKit();
        var rings = new[] {
            (shrink: 0f, y: 0f, col: PWhite), (shrink: 0f, y: -1.2f, col: PPinkDeep), (shrink: 0f, y: -3f, col: PPinkPale),
            (shrink: 10f, y: -7f, col: PPink), (shrink: 26f, y: -13f, col: PLilac), (shrink: 44f, y: -21f, col: PPinkPale) };
        List<Vector2> prev = null; float prevY = 0f; Color32 prevC = PWhite;
        foreach (var r in rings)
        {
            var o = IslandOutline(r.shrink);
            if (prev != null)
                for (int i = 0; i < o.Count; i++)
                {
                    int j = (i + 1) % o.Count;
                    Vector3 p0 = new Vector3(prev[i].x, prevY, prev[i].y), p1 = new Vector3(prev[j].x, prevY, prev[j].y);
                    Vector3 q0 = new Vector3(o[i].x, r.y, o[i].y), q1 = new Vector3(o[j].x, r.y, o[j].y);
                    Vector3 outward = new Vector3((prev[i] + prev[j]).x, 0f, (prev[i] + prev[j]).y).normalized;
                    under.Quad(p0, p1, q1, q0, outward + Vector3.down * 0.3f, prevC);
                }
            prev = o; prevY = r.y; prevC = r.col;
        }
        // the point underneath
        for (int i = 0; i < prev.Count; i++)
        {
            int j = (i + 1) % prev.Count;
            Vector3 p0 = new Vector3(prev[i].x, prevY, prev[i].y), p1 = new Vector3(prev[j].x, prevY, prev[j].y);
            under.Tri(p0, p1, new Vector3(0f, -48f, 0f), Vector3.down + new Vector3(p0.x + p1.x, 0f, p0.z + p1.z).normalized, PLilac);
        }
        Solid("Island underside", parent, under, toonMat, true);
    }

    // a low marble railing round the edge, with openings you can ride out of (into space)
    static void Balustrade(Transform parent)
    {
        var o = IslandOutline(1.2f, 16);
        var kit = new SkateMeshKit();
        var root = new GameObject("Railing").transform;
        root.SetParent(parent, false);
        float run = 0f;
        for (int i = 0; i < o.Count; i++)
        {
            int j = (i + 1) % o.Count;
            Vector3 a = new Vector3(o[i].x, 0f, o[i].y), b = new Vector3(o[j].x, 0f, o[j].y);
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            run += len;
            // gaps: the middle of each side, so there's always a way off
            bool gap = Mathf.Abs(mid.x) < 7f || Mathf.Abs(mid.z) < 7f;
            if (gap || len < 0.1f) continue;
            Quaternion rot = Quaternion.LookRotation(b - a);
            kit.Box(mid + Vector3.up * 1.0f, new Vector3(0.45f, 0.18f, len + 0.02f), rot, PWhite);      // handrail
            kit.Box(mid + Vector3.up * 0.08f, new Vector3(0.55f, 0.16f, len + 0.02f), rot, PPinkDeep);  // plinth
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.9f));
            for (int k = 0; k < n; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, (k + 0.5f) / n);
                kit.Ellipsoid(p + Vector3.up * 0.55f, new Vector3(0.14f, 0.38f, 0.14f), Quaternion.identity, 8, 5, PPinkPale);
            }
            var col = new GameObject("Railing collider").AddComponent<BoxCollider>();
            col.transform.SetParent(root, false);
            col.transform.SetPositionAndRotation(mid + Vector3.up * 0.55f, rot);
            col.size = new Vector3(0.5f, 1.1f, len);
        }
        var mesh = kit.Build("Railing");
        SaveFeatureMesh(mesh);
        MeshObject("Railing", root, mesh, toonMat, outlineMat);
    }

    // ---- the palace ---------------------------------------------------------------------------

    static void Heart(SkateMeshKit k, Vector3 c, float s, Quaternion rot, Color32 col)
    {
        k.Ellipsoid(c + rot * new Vector3(-0.3f * s, 0.22f * s, 0f), new Vector3(0.36f * s, 0.36f * s, 0.2f * s), rot, 12, 8, col);
        k.Ellipsoid(c + rot * new Vector3(0.3f * s, 0.22f * s, 0f), new Vector3(0.36f * s, 0.36f * s, 0.2f * s), rot, 12, 8, col);
        k.Box(c + rot * new Vector3(0f, -0.08f * s, 0f), new Vector3(0.6f * s, 0.6f * s, 0.34f * s), rot * Quaternion.Euler(0f, 0f, 45f), col);
    }

    static void Star(SkateMeshKit k, Vector3 c, float s, Quaternion rot, Color32 col)
    {
        // a chunky four-point sparkle star
        k.Box(c, new Vector3(0.22f * s, 1.0f * s, 0.12f * s), rot, col);
        k.Box(c, new Vector3(1.0f * s, 0.22f * s, 0.12f * s), rot, col);
        k.Box(c, new Vector3(0.36f * s, 0.36f * s, 0.14f * s), rot * Quaternion.Euler(0f, 0f, 45f), col);
    }

    static void Tower(SkateMeshKit k, Vector3 at, float r, float h, float roof, Color32 wall)
    {
        k.Cylinder(at, at + Vector3.up * h, r, 16, wall);
        for (float y = 4f; y < h; y += 7f)
            k.Cylinder(at + Vector3.up * y, at + Vector3.up * (y + 0.6f), r + 0.25f, 16, PWhite);
        k.Cylinder(at + Vector3.up * h, at + Vector3.up * (h + 1.2f), r + 0.6f, 16, PWhite);
        k.Cone(at + Vector3.up * (h + 1.2f), r + 0.9f, 0f, roof, 16, PPinkDeep, 0f);
        // arched windows round the top
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 w = at + dir * (r + 0.02f) + Vector3.up * (h - 4f);
            Quaternion face = Quaternion.LookRotation(dir);
            k.Box(w, new Vector3(1.2f, 2.4f, 0.1f), face, PGlass);
            k.Ellipsoid(w + Vector3.up * 1.2f, new Vector3(0.6f, 0.6f, 0.06f), face, 10, 6, PGlass);
        }
    }

    static void PalaceBuilding(Transform parent, Material shiny)
    {
        var k = new SkateMeshKit();
        float front = 48f, depth = 18f, cz = front + depth / 2f;
        // the main hall, white trim top and bottom
        k.Box(new Vector3(0f, 9f, cz), new Vector3(46f, 18f, depth), Quaternion.identity, PPink);
        k.Box(new Vector3(0f, 18.4f, cz), new Vector3(48f, 1.2f, depth + 2f), Quaternion.identity, PWhite);
        k.Box(new Vector3(0f, 0.6f, cz), new Vector3(47f, 1.2f, depth + 1f), Quaternion.identity, PWhite);
        // scalloped trim under the roof line
        for (float x = -22.5f; x <= 22.5f; x += 1.5f)
            k.Ellipsoid(new Vector3(x, 17.6f, front - 0.1f), new Vector3(0.75f, 0.5f, 0.15f), Quaternion.identity, 8, 4, PWhite);
        // the doorway: a tall arch
        k.Box(new Vector3(0f, 7.6f, front - 0.05f), new Vector3(9f, 10f, 0.3f), Quaternion.identity, PPinkDeep);
        k.Ellipsoid(new Vector3(0f, 12.6f, front - 0.05f), new Vector3(4.5f, 4.5f, 0.15f), Quaternion.identity, 16, 8, PPinkDeep);
        Heart(k, new Vector3(0f, 14.2f, front - 0.4f), 2.2f, Quaternion.identity, PWhite);
        // arched windows either side, two floors
        foreach (float y in new[] { 5f, 12f })
            foreach (float x in new[] { -18f, -12f, 12f, 18f })
            {
                k.Box(new Vector3(x, y, front - 0.05f), new Vector3(2.4f, 3.6f, 0.2f), Quaternion.identity, PGlass);
                k.Ellipsoid(new Vector3(x, y + 1.8f, front - 0.05f), new Vector3(1.2f, 1.2f, 0.1f), Quaternion.identity, 12, 6, PGlass);
                k.Box(new Vector3(x, y - 2f, front - 0.3f), new Vector3(3.2f, 0.4f, 0.6f), Quaternion.identity, PWhite);
            }
        // columns along the front
        foreach (float x in new[] { -21f, -15f, -9f, 9f, 15f, 21f })
        {
            k.Cylinder(new Vector3(x, 1.2f, front - 1.2f), new Vector3(x, 16.6f, front - 1.2f), 0.7f, 12, PWhite);
            k.Box(new Vector3(x, 16.9f, front - 1.2f), new Vector3(2f, 0.6f, 2f), Quaternion.identity, PPinkPale);
            k.Box(new Vector3(x, 1.5f, front - 1.2f), new Vector3(1.8f, 0.6f, 1.8f), Quaternion.identity, PPinkPale);
        }
        // the central dome with a heart on top, and a giant bow over the door
        k.Cylinder(new Vector3(0f, 19f, cz), new Vector3(0f, 22f, cz), 9f, 24, PWhite);
        k.Ellipsoid(new Vector3(0f, 22f, cz), new Vector3(9f, 9f, 9f), Quaternion.identity, 24, 12, PPinkPale);
        Heart(k, new Vector3(0f, 33f, cz), 4f, Quaternion.identity, PPinkDeep);
        k.Ellipsoid(new Vector3(-3.2f, 20.4f, front - 0.8f), new Vector3(3f, 1.6f, 0.8f), Quaternion.Euler(0f, 0f, 18f), 12, 8, PRed);
        k.Ellipsoid(new Vector3(3.2f, 20.4f, front - 0.8f), new Vector3(3f, 1.6f, 0.8f), Quaternion.Euler(0f, 0f, -18f), 12, 8, PRed);
        k.Ellipsoid(new Vector3(0f, 20.4f, front - 1f), new Vector3(1f, 1f, 0.9f), Quaternion.identity, 10, 6, PRed);
        k.Box(new Vector3(-1.4f, 18.6f, front - 0.9f), new Vector3(0.8f, 3f, 0.3f), Quaternion.Euler(0f, 0f, -20f), PRed);
        k.Box(new Vector3(1.4f, 18.6f, front - 0.9f), new Vector3(0.8f, 3f, 0.3f), Quaternion.Euler(0f, 0f, 20f), PRed);
        // towers: two big ones either side, two slim spires in front of them
        Tower(k, new Vector3(-28f, 0f, cz), 6f, 30f, 15f, PPinkPale);
        Tower(k, new Vector3(28f, 0f, cz), 6f, 30f, 15f, PPinkPale);
        Tower(k, new Vector3(-36f, 0f, cz - 6f), 2.4f, 22f, 9f, PPink);
        Tower(k, new Vector3(36f, 0f, cz - 6f), 2.4f, 22f, 9f, PPink);
        Heart(k, new Vector3(-28f, 48.5f, cz), 3f, Quaternion.identity, PPinkDeep);
        Heart(k, new Vector3(28f, 48.5f, cz), 3f, Quaternion.identity, PPinkDeep);
        Star(k, new Vector3(-36f, 33.5f, cz - 6f), 2.2f, Quaternion.identity, PWhite);
        Star(k, new Vector3(36f, 33.5f, cz - 6f), 2.2f, Quaternion.identity, PWhite);
        Solid("Palace", parent, k, shiny, false);

        // colliders: the hall and the towers, so you can't ride into them
        AddBox(parent, "Palace hall collider", new Vector3(0f, 9f, cz), new Vector3(48f, 18f, depth + 2f));
        foreach (float x in new[] { -28f, 28f })
        {
            var cap = new GameObject("Tower collider").AddComponent<CapsuleCollider>();
            cap.transform.SetParent(parent, false);
            cap.transform.position = new Vector3(x, 15f, cz);
            cap.radius = 6.6f; cap.height = 30f;
        }
        foreach (float x in new[] { -36f, 36f })
        {
            var cap = new GameObject("Spire collider").AddComponent<CapsuleCollider>();
            cap.transform.SetParent(parent, false);
            cap.transform.position = new Vector3(x, 11f, cz - 6f);
            cap.radius = 2.8f; cap.height = 22f;
        }
        foreach (float x in new[] { -21f, -15f, -9f, 9f, 15f, 21f })
        {
            var cap = new GameObject("Column collider").AddComponent<CapsuleCollider>();
            cap.transform.SetParent(parent, false);
            cap.transform.position = new Vector3(x, 9f, front - 1.2f);
            cap.radius = 0.8f; cap.height = 16f;
        }
    }

    static BoxCollider AddBox(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion? rot = null)
    {
        var b = new GameObject(name).AddComponent<BoxCollider>();
        b.transform.SetParent(parent, false);
        b.transform.SetPositionAndRotation(centre, rot ?? Quaternion.identity);
        b.size = size;
        return b;
    }

    // ---- the terrace and grand staircase in front of the palace -------------------------------

    const float TerraceH = 2.4f, TerraceZ0 = 30f, TerraceZ1 = 47f, TerraceHX = 30f, StairsHX = 6f, StairsRun = 8f;

    static void Terrace(Transform parent)
    {
        var k = new SkateMeshKit();
        float cz = (TerraceZ0 + TerraceZ1) / 2f, dz = TerraceZ1 - TerraceZ0;
        // the deck, in two halves either side of the stairs' top, with a pink lip
        k.Box(new Vector3(0f, TerraceH / 2f - 0.2f, cz), new Vector3(TerraceHX * 2f, TerraceH + 0.4f, dz), Quaternion.identity, Concrete);
        k.Box(new Vector3(0f, TerraceH - 0.03f, TerraceZ0 + 0.25f), new Vector3(TerraceHX * 2f + 0.1f, 0.12f, 0.6f), Quaternion.identity, PPinkDeep);
        // the stairs: 12 steps down to the floor
        int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float h = TerraceH * (steps - i) / steps;
            float z0 = TerraceZ0 - StairsRun * (i + 1) / steps, z1 = TerraceZ0 - StairsRun * i / steps;
            k.Box(new Vector3(0f, h / 2f, (z0 + z1) / 2f), new Vector3(StairsHX * 2f, h, z1 - z0), Quaternion.identity, i % 2 == 0 ? PWhite : PPinkPale);
        }
        // stair walls either side
        foreach (float sx in new[] { -1f, 1f })
            k.Box(new Vector3(sx * (StairsHX + 0.3f), TerraceH * 0.5f - 0.2f, TerraceZ0 - StairsRun / 2f), new Vector3(0.6f, 0.4f, StairsRun), Quaternion.identity, PPinkDeep);
        Solid("Terrace", parent, k, toonMat, false);

        // colliders: the deck, and a smooth slope over the stairs so you run down (or up) them without catching on the steps
        AddBox(parent, "Terrace collider", new Vector3(0f, TerraceH / 2f - 0.2f, cz), new Vector3(TerraceHX * 2f, TerraceH + 0.4f, dz));
        float slope = Mathf.Atan2(TerraceH, StairsRun) * Mathf.Rad2Deg, len = Mathf.Sqrt(TerraceH * TerraceH + StairsRun * StairsRun);
        AddBox(parent, "Stairs slope", new Vector3(0f, TerraceH / 2f - 0.15f, TerraceZ0 - StairsRun / 2f), new Vector3(StairsHX * 2f, 0.3f, len),
               Quaternion.Euler(-slope, 0f, 0f));

        // handrails down both sides of the stairs and one down the middle
        foreach (float x in new[] { -StairsHX - 0.3f, 0f, StairsHX + 0.3f })
            Rail("Stair handrail", new[] { new Vector3(x, 0.85f, TerraceZ0 - StairsRun - 1.5f), new Vector3(x, 0.85f, TerraceZ0 - StairsRun),
                                           new Vector3(x, TerraceH + 0.85f, TerraceZ0), new Vector3(x, TerraceH + 0.85f, TerraceZ0 + 2f) });
        // banks up to the terrace either side of the stairs, and grindable coping along the drop between
        foreach (float sx in new[] { -1f, 1f })
        {
            var p = new List<Vector2> { new Vector2(-0.25f, -0.06f) };
            for (int i = 0; i <= 8; i++) { float u = i / 8f; p.Add(new Vector2(u * StairsRun, TerraceH * Mathf.SmoothStep(0f, 1f, u * 0.4f + u * 0.6f))); }
            p.Add(new Vector2(StairsRun + 0.6f, TerraceH));
            ProfileRamp("Bank", Matrix4x4.TRS(new Vector3(sx * 21f, 0f, TerraceZ0 - StairsRun), Quaternion.identity, Vector3.one), p, 16f, RampTop, RampSide);
            Rail("Terrace coping", new[] { new Vector3(sx * (StairsHX + 0.9f), TerraceH + 0.03f, TerraceZ0 + 0.05f), new Vector3(sx * 12.8f, TerraceH + 0.03f, TerraceZ0 + 0.05f) }, false);
        }
    }

    // ---- ramps, rails and ledges in the courtyard ----------------------------------------------

    static void PalaceFeatures()
    {
        Quaternion east = Quaternion.LookRotation(Vector3.right), west = Quaternion.LookRotation(Vector3.left);
        Quaternion north = Quaternion.LookRotation(Vector3.forward), south = Quaternion.LookRotation(Vector3.back);

        // left: a halfpipe running front to back
        QuarterPipe("Halfpipe west wall", Matrix4x4.TRS(new Vector3(-58f, 0f, -18f), west, Vector3.one), 30f, 3f, 2.5f);
        QuarterPipe("Halfpipe east wall", Matrix4x4.TRS(new Vector3(-44f, 0f, -18f), east, Vector3.one), 30f, 3f, 2.5f);
        // right: a big quarterpipe wall to turn round on
        QuarterPipe("Quarterpipe wall", Matrix4x4.TRS(new Vector3(76f, 0f, -32f), east, Vector3.one), 36f, 3.5f, 3f);   // south of the east gap, which is where the space course starts

        // the middle: funbox with rails, kickers, ledges, rails
        Funbox("Funbox", Matrix4x4.TRS(new Vector3(32f, 0f, -26f), north, Vector3.one), 8f);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(-14f, 0f, -48f), north, Vector3.one), 4f, 26f, 3.5f, RampSide2);
        Kicker("Kicker", Matrix4x4.TRS(new Vector3(14f, 0f, -48f), north, Vector3.one), 4f, 26f, 3.5f, RampSide2);
        Kicker("Big kicker", Matrix4x4.TRS(new Vector3(52f, 0f, 22f), west, Vector3.one), 5.5f, 30f, 4.5f, RampSide2);
        Kicker("Kicker to the halfpipe", Matrix4x4.TRS(new Vector3(-24f, 0f, 8f), west, Vector3.one), 4f, 24f, 3.2f, RampSide2);
        Ledge("Marble ledge", new Vector3(-20f, 0f, -30f), north, 14f, 0.5f, 1.2f, Concrete);
        Ledge("Marble ledge", new Vector3(56f, 0f, -50f), east, 16f, 0.45f, 1.2f, Concrete);
        Ledge("Manual pad", new Vector3(20f, 0f, 14f), east, 10f, 0.22f, 2f, Concrete);
        Rail("Flat rail", new[] { new Vector3(-6f, 0.5f, -36f), new Vector3(10f, 0.5f, -36f) });
        Rail("Long rail", new[] { new Vector3(-30f, 0.55f, -62f), new Vector3(-30f, 0.55f, -40f) });
        Rail("Kinked rail", new[] { new Vector3(30f, 0.5f, -62f), new Vector3(40f, 0.5f, -62f), new Vector3(50f, 1.4f, -62f), new Vector3(60f, 1.4f, -62f) });
        Rail("Low rail", new[] { new Vector3(40f, 0.4f, 0f), new Vector3(40f, 0.4f, 16f) });
    }

    // ---- decorations -------------------------------------------------------------------------

    static void Fountain(Transform parent, Material shiny)
    {
        var k = new SkateMeshKit();
        Vector3 c = new Vector3(0f, 0f, -6f);
        k.Cylinder(c, c + Vector3.up * 0.75f, 6f, 40, PWhite);
        k.Cylinder(c + Vector3.up * 0.74f, c + Vector3.up * 0.8f, 5.4f, 40, new Color32(255, 150, 196, 255));   // pink water
        k.Cylinder(c, c + Vector3.up * 3.2f, 1.1f, 16, PPinkPale);
        k.Ellipsoid(c + Vector3.up * 3.4f, new Vector3(2.6f, 0.7f, 2.6f), Quaternion.identity, 20, 8, PWhite);
        k.Cylinder(c + Vector3.up * 3.6f, c + Vector3.up * 5f, 0.5f, 12, PPinkPale);
        Heart(k, c + Vector3.up * 6.2f, 2.4f, Quaternion.identity, PPinkDeep);
        Solid("Fountain", parent, k, shiny, false);
        var basin = new GameObject("Fountain collider").AddComponent<CapsuleCollider>();
        basin.transform.SetParent(parent, false);
        basin.transform.position = c + Vector3.up * 0.4f;
        basin.radius = 6f; basin.height = 0.8f;   // a capsule this flat behaves as a squat cylinder rounded at the edge
        basin.direction = 1;
        var column = new GameObject("Fountain column collider").AddComponent<CapsuleCollider>();
        column.transform.SetParent(parent, false);
        column.transform.position = c + Vector3.up * 3f;
        column.radius = 1.2f; column.height = 6f;

        // a rail most of the way round the fountain's rim: grind it in a curve
        var arc = new List<Vector3>();
        for (int i = 0; i <= 40; i++)
        {
            float a = (-60f + 300f * i / 40f) * Mathf.Deg2Rad;
            arc.Add(c + new Vector3(Mathf.Cos(a) * 6.8f, 0.55f, Mathf.Sin(a) * 6.8f));
        }
        Rail("Fountain rail", arc.ToArray());
    }

    static void Strawberry(SkateMeshKit k, Vector3 at, float s, float turn)
    {
        Vector3 c = at + Vector3.up * 0.9f * s;
        k.Ellipsoid(c, new Vector3(0.8f * s, 1f * s, 0.8f * s), Quaternion.Euler(0f, turn, 0f), 14, 10, PRed);
        // seeds
        var rng = new System.Random((int)(at.x * 13 + at.z * 7));
        for (int i = 0; i < 40; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, y = Mathf.Lerp(-0.75f, 0.6f, (float)rng.NextDouble());
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)) * 0.8f * s;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, y * s, Mathf.Sin(a) * r);
            k.Ellipsoid(p, Vector3.one * 0.05f * s, Quaternion.identity, 4, 3, PSeed);
        }
        // leafy crown
        for (int i = 0; i < 6; i++)
        {
            Quaternion q = Quaternion.Euler(0f, turn + i * 60f, 0f);
            k.Ellipsoid(c + Vector3.up * 0.95f * s + q * Vector3.forward * 0.35f * s, new Vector3(0.18f * s, 0.06f * s, 0.42f * s), q * Quaternion.Euler(-20f, 0f, 0f), 8, 4, PLeaf);
        }
        k.Cylinder(c + Vector3.up * 0.95f * s, c + Vector3.up * 1.3f * s, 0.06f * s, 6, PLeaf);
    }

    static void Strawberries(Transform parent, Material shiny)
    {
        var k = new SkateMeshKit();
        var spots = new[] { new Vector4(-72f, 0f, -52f, 3.2f), new Vector4(-64f, 0f, -60f, 2f), new Vector4(70f, 0f, 46f, 3.6f),
                            new Vector4(-70f, 0f, 44f, 2.6f), new Vector4(74f, 0f, 30f, 2.4f) };
        foreach (var s in spots)
        {
            Strawberry(k, new Vector3(s.x, s.y, s.z), s.w, s.x * 3f);
            var cap = new GameObject("Strawberry collider").AddComponent<SphereCollider>();
            cap.transform.SetParent(parent, false);
            cap.transform.position = new Vector3(s.x, s.y + 0.9f * s.w, s.z);
            cap.radius = 0.85f * s.w;
        }
        Solid("Giant strawberries", parent, k, shiny, false);
    }

    // hearts and stars floating round the island, turning slowly
    static void FloatingThings(Transform parent, Material shiny)
    {
        var rng = new System.Random(7);
        var root = new GameObject("Floating hearts and stars").transform;
        root.SetParent(parent, false);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = 95f + (float)rng.NextDouble() * 60f;
            Vector3 at = new Vector3(Mathf.Cos(a) * d * 1.1f, -10f + (float)rng.NextDouble() * 50f, Mathf.Sin(a) * d);
            float s = 2f + (float)rng.NextDouble() * 4f;
            var k = new SkateMeshKit();
            Color32 col = i % 3 == 0 ? PWhite : i % 3 == 1 ? PPinkDeep : PPinkPale;
            if (i % 2 == 0) Heart(k, Vector3.zero, s, Quaternion.identity, col);
            else Star(k, Vector3.zero, s, Quaternion.identity, col);
            var go = Solid(i % 2 == 0 ? "Heart" : "Star", root, k, shiny, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var spin = go.AddComponent<Spin>();
            spin.degreesPerSecond = 8f + (float)rng.NextDouble() * 20f;
            spin.bob = 0.5f + (float)rng.NextDouble();
        }
    }

    // a big pink ringed planet hanging in the sky behind the palace
    static void Planet(Transform parent)
    {
        var k = new SkateMeshKit();
        Vector3 c = new Vector3(-700f, 380f, 1500f);
        k.Ellipsoid(c, Vector3.one * 260f, Quaternion.identity, 40, 24, PPink);
        k.Ellipsoid(c + new Vector3(0f, 60f, 0f), new Vector3(262f, 40f, 262f), Quaternion.identity, 40, 6, PPinkPale);   // a paler band
        Quaternion tilt = Quaternion.Euler(18f, 0f, -14f);
        int n = 64;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            foreach (var band in new[] { (r0: 340f, r1: 420f, col: PLilac), (r0: 430f, r1: 470f, col: PWhite) })
            {
                Vector3 p0 = c + tilt * new Vector3(Mathf.Cos(a0) * band.r0, 0f, Mathf.Sin(a0) * band.r0);
                Vector3 p1 = c + tilt * new Vector3(Mathf.Cos(a1) * band.r0, 0f, Mathf.Sin(a1) * band.r0);
                Vector3 q0 = c + tilt * new Vector3(Mathf.Cos(a0) * band.r1, 0f, Mathf.Sin(a0) * band.r1);
                Vector3 q1 = c + tilt * new Vector3(Mathf.Cos(a1) * band.r1, 0f, Mathf.Sin(a1) * band.r1);
                k.Quad(p0, p1, q1, q0, tilt * Vector3.up, band.col);
                k.Quad(p0, q0, q1, p1, tilt * Vector3.down, band.col);
            }
        }
        var mesh = k.Build("Planet");
        SaveFeatureMesh(mesh);
        MeshObject("Ringed planet", parent, mesh, toonMat, farOutlineMat);
    }

    // glittery specks drifting in the air over the island
    static void Sparkles(Transform parent)
    {
        var go = new GameObject("Sparkles", typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 14f, 0f);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f), new Color(1f, 0.7f, 0.86f));
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 160f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(IslandHX * 2f, 26f, IslandHZ * 2f);
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = Mat("Sparks", "Sprites/Default");
    }

    // ---- the space course: a parkour run of floating platforms off the east side of the island ----

    static readonly Color32[] CourseCols = { new Color32(255, 196, 222, 255), new Color32(255, 240, 248, 255), new Color32(236, 206, 252, 255) };

    static void CoursePlatform(Transform parent, string name, Vector3 top, float sx, float sz, int colour, bool checkpoint = false, string label = "Checkpoint")
    {
        var k = new SkateMeshKit();
        Color32 col = CourseCols[colour % CourseCols.Length];
        k.Box(top + Vector3.down * 0.6f, new Vector3(sx, 1.2f, sz), Quaternion.identity, col);
        k.Box(top + Vector3.down * 0.2f, new Vector3(sx + 0.15f, 0.16f, sz + 0.15f), Quaternion.identity, PPinkDeep);   // pink band just under the top edge
        k.Cone(top + Vector3.down * 1.2f, Mathf.Min(sx, sz) * 0.5f, 0f, -Mathf.Min(sx, sz) * 0.9f, 6, PLilac, 0.3f);    // a crystal hanging underneath
        var go = Solid(name, parent, k, toonMat, true);
        if (checkpoint) CourseCheckpoint(parent, top + new Vector3(sx * 0.5f - 0.6f, 0f, sz * 0.5f - 0.6f), top, label);
    }

    static void CourseStone(Transform parent, Vector3 top, float r, int colour)
    {
        var k = new SkateMeshKit();
        k.Cylinder(top + Vector3.down * 1f, top, r, 18, CourseCols[colour % CourseCols.Length]);
        k.Cylinder(top + Vector3.down * 0.28f, top + Vector3.down * 0.12f, r + 0.08f, 18, PPinkDeep);
        k.Cone(top + Vector3.down * 1f, r, 0f, -r * 1.6f, 6, PLilac, 0.2f);
        Solid("Stepping stone", parent, k, toonMat, true);
    }

    // a heart on a pole at the corner of the platform; reach the middle of the platform to save there
    static void CourseCheckpoint(Transform parent, Vector3 pole, Vector3 centre, string label)
    {
        var k = new SkateMeshKit();
        k.Cylinder(pole, pole + Vector3.up * 3.2f, 0.09f, 8, PWhite);
        Heart(k, pole + Vector3.up * 3.6f, 1.1f, Quaternion.identity, PPinkDeep);
        var flag = Solid("Checkpoint heart", parent, k, toonMat, false);
        var cp = new GameObject(label);
        cp.transform.SetParent(parent, false);
        cp.transform.SetPositionAndRotation(centre + Vector3.up * 0.4f, Quaternion.LookRotation(Vector3.right));
        var c = cp.AddComponent<Checkpoint>();
        c.label = label;
        c.radius = 4f;
    }

    static void SpaceCourse(Transform parent, Material shiny)
    {
        var root = new GameObject("Space course").transform;
        root.SetParent(parent, false);
        Quaternion east = Quaternion.LookRotation(Vector3.right);

        // out of the gap in the railing, over the edge
        CoursePlatform(root, "Course start", new Vector3(97f, 0f, 0f), 8f, 10f, 1, true, "Space course");
        CoursePlatform(root, "Step", new Vector3(108f, 0.6f, -1f), 6f, 6f, 0);
        CoursePlatform(root, "Step", new Vector3(118f, 1.2f, 2f), 5f, 5f, 2);
        // a rail across the first big gap
        Rail("Space rail", new[] { new Vector3(121f, 2.0f, 2f), new Vector3(136f, 2.0f, 2f) }, false);
        CoursePlatform(root, "Landing", new Vector3(142f, 1.2f, 2f), 10f, 8f, 1, true);
        // a kicker over the next gap
        Kicker("Space kicker", Matrix4x4.TRS(new Vector3(143f, 1.2f, 2f), east, Vector3.one), 4f, 26f, 3.5f, RampSide2);
        CoursePlatform(root, "Landing", new Vector3(153f, 1.6f, 2f), 8f, 8f, 0);
        // stepping stones, climbing and weaving
        CourseStone(root, new Vector3(162f, 2.2f, 2f), 2f, 2);
        CourseStone(root, new Vector3(168f, 2.8f, -2f), 2f, 1);
        CourseStone(root, new Vector3(174f, 3.4f, 1f), 2f, 0);
        CourseStone(root, new Vector3(180f, 4.0f, 3f), 2f, 2);
        CoursePlatform(root, "High platform", new Vector3(190f, 4.0f, 2f), 10f, 8f, 1, true);
        // a long rail down, kinked twice
        Rail("Space rail", new[] { new Vector3(195.5f, 4.8f, 2f), new Vector3(206f, 3.8f, 2f), new Vector3(218f, 2.8f, 2f) }, false);
        CoursePlatform(root, "Low platform", new Vector3(226f, 2.0f, 2f), 12f, 8f, 0, true);
        // a steep kicker up, then pillars climbing to the goal
        Kicker("Space kicker", Matrix4x4.TRS(new Vector3(229f, 2.0f, 2f), east, Vector3.one), 4.5f, 35f, 3.5f, RampSide2);
        CoursePlatform(root, "Landing", new Vector3(238f, 2.8f, 2f), 8f, 8f, 2);
        CoursePlatform(root, "Pillar", new Vector3(246f, 3.6f, 2f), 4f, 4f, 1);
        CoursePlatform(root, "Pillar", new Vector3(252f, 4.4f, -1f), 4f, 4f, 0);
        CoursePlatform(root, "Pillar", new Vector3(258f, 5.2f, 2f), 4f, 4f, 2);
        CoursePlatform(root, "Goal", new Vector3(271f, 5.2f, 2f), 14f, 14f, 1, true, "Course complete!");

        // a giant spinning star over the goal
        var k = new SkateMeshKit();
        Star(k, Vector3.zero, 7f, Quaternion.identity, PWhite);
        var star = Solid("Goal star", root, k, shiny, false);
        star.transform.position = new Vector3(271f, 13f, 2f);
        var spin = star.AddComponent<Spin>();
        spin.degreesPerSecond = 40f;
        spin.bob = 0.8f;
    }
    // ---- review shots -------------------------------------------------------------------------

    public static void PalaceShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 1.5f, -62f), new Vector3(0f, 8f, 40f), "p01-start.png");
        Shot(cam, new Vector3(-95f, 45f, -110f), new Vector3(0f, 0f, 0f), "p02-overview.png");
        Shot(cam, new Vector3(10f, 4f, 10f), new Vector3(0f, 2f, 30f), "p03-stairs.png");
        Shot(cam, new Vector3(-30f, 6f, -40f), new Vector3(-51f, 1f, -18f), "p04-halfpipe.png");
        Shot(cam, new Vector3(0f, 3f, -20f), new Vector3(-400f, 450f, 1500f), "p05-sky.png");
        Shot(cam, new Vector3(60f, 20f, -120f), new Vector3(0f, -20f, 0f), "p06-underside.png");
        Shot(cam, new Vector3(70f, 9f, -18f), new Vector3(180f, 2f, 2f), "p07-space-course.png");
        Shot(cam, new Vector3(150f, 30f, -70f), new Vector3(200f, 2f, 2f), "p08-space-course-above.png");
    }
}
