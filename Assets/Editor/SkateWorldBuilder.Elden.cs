using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Elden Ruins: after the Elden Ring gameplay preview, as a parkour course (no board: SlideController.parkour, so you
// vault low walls, grab and climb ledges, run along walls and kick off them, slide under things; no double jump).
// Golden late-afternoon light through haze; a meadow of long grass and golden autumn trees, broken ashlar walls and
// columns; a ruined aqueduct striding across a gorge full of mist; a grey castle with round towers, slate roofs, red
// banners and torches, its wall clad in wooden scaffolding; and away in the sky, the Erdtree, glowing gold.
//
// The course (Sites of Grace are the checkpoints; EldenLevel):
//   the Ruined Meadow: vault two low walls, climb a ruin, hop a row of broken columns, climb onto the aqueduct
//   the Broken Aqueduct: along the top over the gorge, jumping the gaps where it's fallen, and where a whole span's
//     gone, running along a standing wall beside it
//   Castle Hollowmere: up the wooden scaffolding on the curtain wall, along the wall-walk, sliding under a fallen
//     block, then running along the side of a building where the wall-walk's collapsed
//   the Keep: up the scaffolding round the outside, up a narrow chimney by kicking from wall to wall, onto the roof
public static partial class SkateWorldBuilder
{
    public const string EldenPath = "Assets/Scenes/EldenRuins.unity";
    const string EldenTexDir = "Assets/Elden/Textures", EldenMatDir = "Assets/Elden/Materials";

    // the solid pieces of the course, as boxes (min, max) and what they're made of; public so the tests know them
    public struct EBox { public string name, mat; public Vector3 min, max; }
    static EBox EB(string name, string mat, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        return new EBox { name = name, mat = mat, min = new Vector3(x0, y0, z0), max = new Vector3(x1, y1, z1) };
    }

    // the broken columns to hop along: (x, top, z), all 0.9 m round
    public static readonly Vector3[] EPillars = { new Vector3(1f, 3.4f, 51.5f), new Vector3(-1f, 3.8f, 55.6f), new Vector3(1.5f, 4.2f, 59.7f), new Vector3(-0.5f, 4.6f, 63.8f), new Vector3(1f, 5f, 67.9f) };
    public const float EPillarR = 0.9f;
    public const float EDeckY = 10f, EWallY = 26f, EKeepY = 46f;
    // where the aqueduct's deck is missing (z from, to)
    public static readonly Vector2[] EAqueductGaps = { new Vector2(112f, 115.5f), new Vector2(134f, 137.5f), new Vector2(150f, 160f), new Vector2(182f, 185.8f) };

    public static readonly Vector3[] EGraces = { new Vector3(0f, 0f, 0f), new Vector3(0f, EDeckY, 81f), new Vector3(0f, 10f, 217f), new Vector3(28f, EWallY, 266f), new Vector3(19f, EKeepY, 279f) };
    static readonly string[] EGraceNames = { "Ruined Meadow", "Aqueduct's Edge", "Castle Hollowmere Gate", "Rampart Watch", "Keep's Crown" };

    public static List<EBox> EldenCourse()
    {
        var b = new List<EBox>();
        // the meadow: two low walls to vault, a ruin to climb, the columns (separately), a block, another, the aqueduct
        b.Add(EB("Low wall", "stone_block", -4f, 4f, -0.5f, 0.9f, 13.65f, 14.35f));
        b.Add(EB("Low wall", "stone_block", -5f, 5f, -0.5f, 1.1f, 21.65f, 22.35f));
        b.Add(EB("Ruin", "stone_block", -6f, 6f, -0.5f, 3f, 37.25f, 47.75f));
        b.Add(EB("Fallen block", "stone_block", -4f, 4f, -0.5f, 6.2f, 70f, 78f));
        b.Add(EB("Fallen block", "stone_block", -1.5f, 1.5f, 6f, 8.2f, 75f, 78f));
        // the aqueduct's deck, in pieces between the gaps (wider where the standing wall is)
        float z = 78f;
        foreach (var g in EAqueductGaps)
        {
            b.Add(EB("Aqueduct", "stone_wall", -1.6f, g.x > 145f && g.x < 155f ? 2f : 1.6f, EDeckY - 1.4f, EDeckY, z, g.x));
            z = g.y;
        }
        b.Add(EB("Aqueduct", "stone_wall", -1.6f, 1.6f, EDeckY - 1.4f, EDeckY, 160f, 182f));
        b[b.Count - 1] = EB("Aqueduct", "stone_wall", -1.6f, 2f, EDeckY - 1.4f, EDeckY, 160f, 182f);
        b.Add(EB("Aqueduct", "stone_wall", -1.6f, 1.6f, EDeckY - 1.4f, EDeckY, 185.8f, 212f));
        // the wall still standing beside the fallen span: run along it
        b.Add(EB("Standing wall", "stone_wall", 2f, 2.8f, 4f, 14.5f, 146f, 164f));

        // the castle: the curtain wall, and the scaffolding up its face, step by step
        b.Add(EB("Curtain wall", "stone_castle", -30f, 30f, 8f, EWallY, 232f, 236f));
        for (int i = 0; i < 6; i++)
        {
            float x0 = -14f + i * 5f, top = 12.6f + i * 2.4f;
            b.Add(EB("Scaffolding", "wood", x0, x0 + 4f, top - 2.2f < 12f ? 9f : top - 2.2f, top, 229f, 232f));
        }
        // the wall-walk east, then north along the east wall, collapsed for 9 m beside a tall building
        b.Add(EB("East wall", "stone_castle", 26f, 30f, 8f, EWallY, 236f, 250f));
        b.Add(EB("East wall", "stone_castle", 25.6f, 30f, EWallY - 3f, EWallY, 240f, 250f));
        b.Add(EB("East wall", "stone_castle", 25.6f, 30f, 8f, EWallY, 259f, 270f));
        b.Add(EB("Building", "stone_castle", 18f, 25.6f, 8f, 34f, 244f, 263f));
        b.Add(EB("Fallen block", "stone_block", 22f, 24.5f, EWallY + 1f, EWallY + 3.5f, 232.8f, 235.2f));
        // the keep, and the scaffolding up its east side, along its north side, the chimney and its buttress, its west side
        b.Add(EB("Keep", "stone_castle", 12f, 26f, 8f, EKeepY, 272f, 286f));
        b.Add(EB("Keep scaffold", "wood", 26f, 29.5f, 25.6f, 27.6f, 271.5f, 275.5f));
        b.Add(EB("Keep scaffold", "wood", 26f, 29.5f, 28f, 30f, 277.5f, 281.5f));
        b.Add(EB("Keep scaffold", "wood", 26f, 29.5f, 30.4f, 32.4f, 283.5f, 287.5f));
        b.Add(EB("Keep scaffold", "wood", 20.5f, 24.5f, 32.8f, 34.8f, 286f, 289.5f));
        b.Add(EB("Keep scaffold", "wood", 13f, 19f, 32.8f, 34.8f, 286f, 288.2f));
        b.Add(EB("Buttress", "stone_castle", 8.5f, 18f, 8f, 41f, 288.2f, 289.4f));
        b.Add(EB("Keep scaffold", "wood", 8.5f, 12f, 41.2f, 43.2f, 282f, 286f));
        b.Add(EB("Keep scaffold", "wood", 8.5f, 12f, 43.2f, 45.2f, 276f, 280f));
        return b;
    }

    public static Bounds EldenGoal { get { return new Bounds(new Vector3(19f, EKeepY + 3f, 279f), new Vector3(14f, 6f, 14f)); } }

    // ---- the land ------------------------------------------------------------------------------------------------

    // the meadow at 0 (z < ~98), the gorge (down to -90) up to ~212, the castle's plateau at 10 beyond; hills round
    // the sides, flat where the course is
    public static float EldenH(float x, float z)
    {
        float hills = Smooth(25f, 140f, Mathf.Abs(x + 0.15f * z)) * (6f + 34f * Mathf.PerlinNoise(x * 0.008f + 40f, z * 0.008f + 12f))
                    + (Mathf.PerlinNoise(x * 0.03f + 3f, z * 0.03f + 5f) - 0.5f) * 2.5f * Smooth(12f, 30f, Mathf.Abs(x));
        float back = Smooth(330f, 520f, z) * (20f + 80f * Mathf.PerlinNoise(x * 0.006f + 7f, z * 0.006f + 3f));   // mountains behind the castle
        float front = (1f - Smooth(-160f, -60f, z)) * (30f + 60f * Mathf.PerlinNoise(x * 0.006f, 9f));
        float meadow = hills + back + front;
        float plateau = 10f + hills * (1f - Smooth(-40f, 0f, 45f - Mathf.Abs(x - 5f)) * (1f - Smooth(300f, 320f, z))) + back;
        float lerp = Smooth(206f, 214f, z);
        float h = Mathf.Lerp(meadow, plateau, lerp);
        // the gorge
        float rim = (Mathf.PerlinNoise(x * 0.05f + 1f, 2f) - 0.5f) * 6f;
        float gorge = Smooth(96f + rim, 108f + rim, z) * (1f - Smooth(200f - rim, 211f - rim, z));
        h = Mathf.Lerp(h, -90f + 10f * Mathf.PerlinNoise(x * 0.02f, z * 0.02f), gorge);
        return h;
    }

    static readonly List<Rect> eFoot = new List<Rect>();   // where solid things stand (no grass, no trees)
    static bool EFree(float x, float z, float pad)
    {
        foreach (var r in eFoot) if (x > r.xMin - pad && x < r.xMax + pad && z > r.yMin - pad && z < r.yMax + pad) return false;
        return true;
    }

    public static void EldenFromCommandLine()
    {
        try { BuildElden(); SlideTests.RunElden(); EldenShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildElden()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/EldenFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        eFoot.Clear();
        RealBegin(EldenTexDir, EldenMatDir);
        toonMat = Mat("Toon", "3daigamesolo/World/Toon");
        outlineMat = Mat("Outline", "3daigamesolo/World/Outline");

        RLit("grass_ground", "grass_ground", new Color(0.92f, 0.95f, 0.85f));
        RLit("rock", "rock", null, 0f, 0f, 0f, 0.05f);
        RLit("dirt", "dirt");
        RLit("stone_block", "stone_block", null, 0f, 0f, 0f, 0.05f);
        RLit("stone_wall", "stone_wall", null, 0f, 0f, 0f, 0.05f);
        RLit("stone_castle", "stone_castle", null, 0f, 0f, 0f, 0.05f);
        RLit("flagstone", "flagstone");
        RLit("wood", "wood", null, 0f, 0f, 0f, 0.05f);
        RLit("slate", "slate", null, 0f, 0f, 0f, 0.08f);
        RLit("bark", "bark");
        RLit("grass_blades", "grass_blades", null, 0f, 0.45f, 0.12f, 0f, 0.55f, true, false);
        RLit("grass_golden", "grass_golden", null, 0f, 0.45f, 0.12f, 0f, 0.55f, true, false);
        RLit("leaves_gold", "leaves_gold", null, 0f, 0.45f, 0.18f, 0f, 0.6f, true, true);
        RLit("leaves_green", "leaves_green", null, 0f, 0.45f, 0.15f, 0f, 0.6f, true, true);
        RLit("banner", "banner", null, 0f, 0f, 0.25f, 0f, 0.4f, true, true);
        RLit("erd_leaves", "erd_leaves", new Color(1f, 0.84f, 0.5f), 1.15f, 0.4f, 0f, 0f, 0f, true, false, null, 0.42f);
        RLit("erd_bark", "erd_leaves", new Color(1f, 0.8f, 0.45f), 1.5f, 0f, 0f, 0f, 0f, false, false, null, 0.35f);
        RLit("far_rock", "rock", new Color(0.55f, 0.6f, 0.7f), 0f, 0f, 0f, 0f, 0f, false, false, null, 0.85f);
        RLit("armour", "white", null, 0f, 0f, 0f, 0.5f);
        RLit("flame", "white", new Color(1f, 0.62f, 0.25f), 3f, 0f, 0f, 0f, 0f, false, false);
        RGlow("grace", "glow", new Color(1f, 0.82f, 0.4f, 0.75f), true, null, 1.5f, new Vector2(2.2f, 0.25f));
        RGlow("grace_beam", "beam", new Color(1f, 0.85f, 0.5f, 0.22f), true, null, 2f, new Vector2(1.3f, 0.2f));
        RGlow("torch_glow", "glow", new Color(1f, 0.55f, 0.2f, 0.7f), true, null, 1f, new Vector2(9f, 0.12f));
        RGlow("message", "message", new Color(1f, 0.55f, 0.3f, 0.9f), true, null, 0.5f, new Vector2(1.5f, 0.2f));
        RGlow("mist", "mist", new Color(0.82f, 0.84f, 0.84f, 0.55f), false, new Vector4(0.004f, 0.002f, -0.003f, 0.0015f), 20f, null, 3100);
        RGlow("erd_halo", "glow_soft", new Color(1f, 0.8f, 0.45f, 0.22f), true, null, 50f);
        RGlow("goal_beam", "beam", new Color(1f, 0.85f, 0.45f, 0.6f), true, null, 3f, new Vector2(1.1f, 0.15f));

        var skyMat = RMatAsset("EldenSky", "3daigamesolo/World/RealSky");
        skyMat.SetColor("_Zenith", new Color(0.3f, 0.4f, 0.54f)); skyMat.SetColor("_Horizon", new Color(0.72f, 0.72f, 0.66f));
        skyMat.SetColor("_Below", new Color(0.6f, 0.6f, 0.55f)); skyMat.SetColor("_SunGlow", new Color(1f, 0.8f, 0.5f));
        skyMat.SetColor("_GlowColor", new Color(1f, 0.78f, 0.35f, 0.3f)); skyMat.SetVector("_GlowDir", new Vector4(-0.38f, 0.36f, 0.86f, 0f));
        skyMat.SetTexture("_Clouds", RTex("clouds")); skyMat.SetFloat("_CloudCover", 0.62f); skyMat.SetFloat("_CloudScale", 0.45f);
        skyMat.SetColor("_CloudColor", new Color(1f, 0.96f, 0.9f)); skyMat.SetColor("_CloudShade", new Color(0.55f, 0.58f, 0.64f));
        var post = RMatAsset("EldenPost", "3daigamesolo/Post/Real");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RealScene();
        featureRoot = new GameObject("Skate features").transform;
        RealSun(new Vector3(-0.62f, 0.42f, -0.4f), new Color(1f, 0.85f, 0.62f), 1.05f, 0.85f);
        var root = new GameObject("Elden").transform;
        var solids = new GameObject("Solids").transform;
        solids.SetParent(root, false);

        // ---- the course
        foreach (var b in EldenCourse())
        {
            Vector3 c = (b.min + b.max) * 0.5f, size = b.max - b.min;
            WBox(RK(b.mat), c, size, Quaternion.identity, b.mat == "wood" ? 3f : 4f, null, true, solids);
            eFoot.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
        }
        foreach (var p in EPillars)
        {
            Vector3 top = new Vector3(p.x, p.y, p.z);
            WCyl(RK("stone_wall"), new Vector3(p.x, -0.5f, p.z), top, EPillarR + 0.08f, EPillarR, 14, 4f);
            Cap(RK("stone_wall"), top, EPillarR, 14);
            AddBox(solids, "Column", new Vector3(p.x, (top.y - 0.5f) * 0.5f, p.z), new Vector3(EPillarR * 1.8f, top.y + 0.5f, EPillarR * 1.8f));
            eFoot.Add(Rect.MinMaxRect(p.x - 1f, p.z - 1f, p.x + 1f, p.z + 1f));
        }
        EAqueduct(solids);
        ECastle(solids);
        EGround(root);
        EDecor(root);
        EErdtree(root);
        var graces = EGraceObjects(root);
        var messages = EMessages();
        RFlush(root);
        RealSky(skyMat, new Vector3(0f, 0f, 200f), 4200f);

        // ---- you
        var camGO = RealCamera(typeof(EldenLevel), 5000f, new Color(0.75f, 0.76f, 0.7f));
        var look = camGO.GetComponent<RealLook>();
        look.post = post;
        look.fog = new Color(0.7f, 0.71f, 0.64f); look.fogSun = new Color(1f, 0.84f, 0.56f); look.fogDensity = 0.0042f; look.fogStart = 25f; look.fogHeight = 70f; look.fogMax = 0.97f; look.fogFalloff = 0.0016f;
        look.sky = new Color(0.44f, 0.48f, 0.52f); look.ground = new Color(0.3f, 0.29f, 0.21f);
        look.bloomThreshold = 1.1f; look.bloom = 0.45f; look.exposure = 1.05f; look.contrast = 1.16f; look.saturation = 0.9f;
        look.shadowTint = new Color(0.42f, 0.5f, 0.5f, 0.35f); look.highTint = new Color(0.6f, 0.52f, 0.4f, 0.3f); look.vignette = 0.45f; look.grain = 0.02f;
        var startT = graces[0].spot;
        var player = BuildPlayer(startT, camGO.transform);
        EldenPlayer(player);
        Object.DestroyImmediate(player.rider.gameObject);
        player.rider = BuildKnight(player.transform);
        var hud = camGO.GetComponent<SlideHUD>();
        hud.player = player; hud.levelName = ""; hud.noSpeed = true;
        hud.hint = "WASD   SHIFT sprint   SPACE jump (on a wall: kick off it)   run at low walls to vault   jump at a ledge + hold W to climb   C slide   V camera   R last grace   TAB level";
        var level = camGO.GetComponent<EldenLevel>();
        level.player = player; level.graces = graces; level.messages = messages;
        level.goal = EldenGoal;
        level.areas = new[]
        {
            new EldenLevel.Area { name = "RUINED MEADOW", bounds = new Bounds(new Vector3(0f, 0f, 30f), new Vector3(80f, 40f, 80f)) },
            new EldenLevel.Area { name = "THE BROKEN AQUEDUCT", bounds = new Bounds(new Vector3(0f, 10f, 100f), new Vector3(20f, 20f, 30f)) },
            new EldenLevel.Area { name = "CASTLE HOLLOWMERE", bounds = new Bounds(new Vector3(0f, 15f, 222f), new Vector3(80f, 30f, 14f)) },
            new EldenLevel.Area { name = "THE KEEP", bounds = new Bounds(new Vector3(27f, 30f, 274f), new Vector3(8f, 12f, 8f)) },
        };
        camGO.transform.SetPositionAndRotation(startT.position + new Vector3(0f, 2.5f, -4.5f), Quaternion.LookRotation(Vector3.forward));

        EditorSceneManager.SaveScene(scene, EldenPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("SKATE_ELDEN_BUILT features=" + featureCount);
    }

    // on foot, parkour on, no double jump, the camera behind you (V for first person)
    public static void EldenPlayer(SlideController p)
    {
        p.alwaysBoard = false;
        p.board = null;
        p.parkour = true;
        p.doubleJumpOn = false;
        p.fallLimit = -400f;
        p.thirdPerson = true;
        p.camDistance = 4.6f;
        p.camPivotHeight = 1.75f;
        p.fov = 62f;
        p.fovAtSpeed = 70f;
    }

    static void Cap(PSXKit k, Vector3 top, float r, int sides)
    {
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, p1 = top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
            k.Quad(top, p0, p1, p1, WUV(top, Vector3.up, 4f), WUV(p0, Vector3.up, 4f), WUV(p1, Vector3.up, 4f), WUV(p1, Vector3.up, 4f), Vector3.up);
        }
    }

    // ---- the aqueduct: piers down into the gorge, arches between them, the deck on top (the course's boxes) ----------

    static void EAqueduct(Transform solids)
    {
        var k = RK("stone_wall");
        float pier = 2.4f, span = 5.2f, period = pier + span, spring = 4.6f;
        for (float z = 78f + pier * 0.5f; z < 212f; z += period)
        {
            float g = EldenH(0f, z);
            float pierTop = EDeckY - 1.4f;
            WBox(k, new Vector3(0f, (g - 4f + pierTop) * 0.5f, z), new Vector3(3.4f, pierTop - g + 4f, pier), Quaternion.identity, 4f);
            AddBox(solids, "Pier", new Vector3(0f, (g - 4f + pierTop) * 0.5f, z), new Vector3(3.4f, pierTop - g + 4f, pier));
            // the arch to the next pier, unless it's fallen with the deck
            float a = z + pier * 0.5f, b = a + span;
            bool broken = false;
            foreach (var gap in EAqueductGaps) if (b > gap.x + 0.5f && a < gap.y - 0.5f) broken = true;
            if (broken || b > 212f) continue;
            float r = span * 0.5f, mid = (a + b) * 0.5f;
            int n = 10;
            for (int i = 0; i < n; i++)
            {
                float t0 = Mathf.PI * i / n, t1 = Mathf.PI * (i + 1) / n;
                float za = mid - Mathf.Cos(t0) * r, zb = mid - Mathf.Cos(t1) * r;
                float ya = spring + Mathf.Sin(t0) * r, yb = spring + Mathf.Sin(t1) * r;
                // the underside of the arch, and the wall above it each side
                WQuad(k, new Vector3(-1.6f, ya, za), new Vector3(1.6f, ya, za), new Vector3(1.6f, yb, zb), new Vector3(-1.6f, yb, zb), Vector3.down, 4f);
                foreach (float sx in new[] { -1.6f, 1.6f })
                    WQuad(k, new Vector3(sx, ya, za), new Vector3(sx, EDeckY - 1.4f, za), new Vector3(sx, EDeckY - 1.4f, zb), new Vector3(sx, yb, zb), new Vector3(Mathf.Sign(sx), 0f, 0f), 4f);
            }
        }
        // broken stubs of arch either side of the gaps, and rubble
        var rng = new System.Random(77);
        foreach (var gap in EAqueductGaps)
            for (int i = 0; i < 5; i++)
            {
                float z = Mathf.Lerp(gap.x, gap.y, (float)rng.NextDouble());
                float y = EldenH(0f, z);
                if (y < -20f) continue;
                WBox(RK("stone_wall"), new Vector3(((float)rng.NextDouble() - 0.5f) * 8f, y + 0.4f, z), new Vector3(1.4f, 1f, 1f), Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 20f), 4f);
            }
    }

    // ---- the castle -----------------------------------------------------------------------------------------------

    static void ECastle(Transform solids)
    {
        var st = RK("stone_castle");
        // crenellations along the wall-walks (not where the scaffolding comes up, or beside the building)
        for (float x = -29.5f; x < 30f; x += 2f)
        {
            if (!(x > 10f && x < 16f)) MerlonBox(st, solids, new Vector3(x, EWallY + 0.6f, 232.4f), new Vector3(1.1f, 1.2f, 0.8f));
            if (x < 25f) MerlonBox(st, solids, new Vector3(x, EWallY + 0.6f, 235.6f), new Vector3(1.1f, 1.2f, 0.8f));
        }
        for (float z = 238f; z < 270f; z += 2f)
        {
            if (z > 249f && z < 260f) continue;
            MerlonBox(st, solids, new Vector3(29.6f, EWallY + 0.6f, z), new Vector3(0.8f, 1.2f, 1.1f));
        }
        // the keep's parapet (open where the last scaffold meets it)
        for (float x = 12.5f; x < 26f; x += 2f) { MerlonBox(st, solids, new Vector3(x, EKeepY + 0.6f, 272.4f), new Vector3(1.1f, 1.2f, 0.8f)); MerlonBox(st, solids, new Vector3(x, EKeepY + 0.6f, 285.6f), new Vector3(1.1f, 1.2f, 0.8f)); }
        for (float z = 273.5f; z < 285f; z += 2f)
        {
            MerlonBox(st, solids, new Vector3(25.6f, EKeepY + 0.6f, z), new Vector3(0.8f, 1.2f, 1.1f));
            if (z < 275.5f || z > 280.5f) MerlonBox(st, solids, new Vector3(12.4f, EKeepY + 0.6f, z), new Vector3(0.8f, 1.2f, 1.1f));
        }
        // a little turret on the keep roof, with a slate spire
        WCyl(st, new Vector3(23f, EKeepY, 283f), new Vector3(23f, EKeepY + 7f, 283f), 1.8f, 1.8f, 12, 4f);
        WCyl(RK("slate"), new Vector3(23f, EKeepY + 7f, 283f), new Vector3(23f, EKeepY + 12f, 283f), 2.3f, 0.05f, 12, 3f);
        AddBox(solids, "Turret", new Vector3(23f, EKeepY + 3.5f, 283f), new Vector3(3.2f, 7f, 3.2f));

        // the gate in the curtain wall: its portcullis
        WBox(RK("wood"), new Vector3(-22f, 14f, 231.8f), new Vector3(5f, 8f, 0.4f), Quaternion.identity, 2f);
        // round towers at the corners and either side of the gate, with slate spires
        foreach (var t in new[] { new Vector3(-35f, 0f, 230f), new Vector3(35f, 0f, 230f), new Vector3(-26.5f, 0f, 230.5f), new Vector3(-17.5f, 0f, 230.5f), new Vector3(36f, 0f, 268f) })
        {
            float r = Mathf.Abs(t.x) > 30f ? 5f : 3f, top = Mathf.Abs(t.x) > 30f ? 36f : 31f;
            WCyl(st, new Vector3(t.x, 8f, t.z), new Vector3(t.x, top, t.z), r, r, 18, 4f);
            Cap(st, new Vector3(t.x, top, t.z), r, 18);
            WCyl(RK("slate"), new Vector3(t.x, top, t.z), new Vector3(t.x, top + r * 2.6f, t.z), r + 0.8f, 0.05f, 18, 3f);
            AddBox(solids, "Tower", new Vector3(t.x, (8f + top) * 0.5f, t.z), new Vector3(r * 1.6f, top - 8f, r * 1.6f));
            eFoot.Add(Rect.MinMaxRect(t.x - r, t.z - r, t.x + r, t.z + r));
        }
        // banners down the outside of the curtain wall
        foreach (float x in new[] { 17.5f, 21.5f, 25.5f })
        {
            var k = RK("banner");
            Vector3 top = new Vector3(x, EWallY - 0.5f, 231.85f);
            int segs = 6;
            for (int i = 0; i < segs; i++)
            {
                float v0 = 1f - i / (float)segs, v1 = 1f - (i + 1) / (float)segs;
                Vector3 a = top + Vector3.down * 7f * i / segs, b = top + Vector3.down * 7f * (i + 1) / segs;
                byte sa = (byte)(255 * (1f - 0.8f * i / segs)), sb = (byte)(255 * (1f - 0.8f * (i + 1) / segs));
                int vi = k.v.Count;
                k.v.Add(a + Vector3.left * 1.4f); k.v.Add(a + Vector3.right * 1.4f); k.v.Add(b + Vector3.right * 1.4f); k.v.Add(b + Vector3.left * 1.4f);
                for (int j = 0; j < 4; j++) k.n.Add(Vector3.back);
                k.c.Add(new Color32(255, 255, 255, sa)); k.c.Add(new Color32(255, 255, 255, sa)); k.c.Add(new Color32(255, 255, 255, sb)); k.c.Add(new Color32(255, 255, 255, sb));
                k.uv.Add(new Vector2(0f, v0)); k.uv.Add(new Vector2(1f, v0)); k.uv.Add(new Vector2(1f, v1)); k.uv.Add(new Vector2(0f, v1));
                k.t.Add(vi); k.t.Add(vi + 1); k.t.Add(vi + 2); k.t.Add(vi); k.t.Add(vi + 2); k.t.Add(vi + 3);
            }
        }
        // torches on the wall and the keep
        foreach (var t in new[] { new Vector3(-16f, 13.5f, 231.7f), new Vector3(16.5f, 13.5f, 231.7f), new Vector3(28f, EWallY + 1.6f, 262f), new Vector3(26.3f, 29f, 276.5f), new Vector3(16f, 37f, 286.3f), new Vector3(-3f, 1.6f, 36.5f), new Vector3(3f, 11.6f, 80f) })
            ETorch(t);

        // the scaffolding's posts and braces, down to whatever's below
        foreach (var b in EldenCourse())
        {
            if (b.mat != "wood") continue;
            var w = RK("wood");
            foreach (var c in new[] { new Vector2(b.min.x + 0.2f, b.min.z + 0.2f), new Vector2(b.max.x - 0.2f, b.min.z + 0.2f), new Vector2(b.min.x + 0.2f, b.max.z - 0.2f), new Vector2(b.max.x - 0.2f, b.max.z - 0.2f) })
            {
                float ground = b.min.y > 20f ? Mathf.Max(8f, b.min.y - 9f) : 9.5f;
                WCyl(w, new Vector3(c.x, b.min.y, c.y), new Vector3(c.x, ground, c.y), 0.13f, 0.13f, 6, 2f);
            }
            WCyl(w, new Vector3(b.min.x + 0.2f, b.min.y, b.min.z + 0.2f), new Vector3(b.max.x - 0.2f, Mathf.Max(8f, b.min.y - 4f), b.min.z + 0.2f), 0.09f, 0.09f, 5, 2f);
        }
        // the courtyard floor inside the walls
        var flag = RK("flagstone");
        flag.Ground(-30f, 236f, 26f, 300f, 10.03f, 4f, 8f);
    }

    static void MerlonBox(PSXKit k, Transform solids, Vector3 c, Vector3 size)
    {
        WBox(k, c, size, Quaternion.identity, 4f, null, false, solids);
    }

    static void ETorch(Vector3 at)
    {
        WCyl(RK("wood"), at + new Vector3(0f, -0.7f, 0f), at, 0.06f, 0.09f, 6, 1f);
        WBox(RK("flame"), at + Vector3.up * 0.15f, new Vector3(0.16f, 0.3f, 0.16f), Quaternion.Euler(0f, 45f, 0f), 1f);
        var g = RK("torch_glow");
        foreach (var f in new[] { Vector3.forward, Vector3.right, (Vector3.forward + Vector3.right).normalized, (Vector3.forward - Vector3.right).normalized })
            FlatCard(g, at + Vector3.up * 0.2f, Vector3.Cross(Vector3.up, f) * 1.1f, Vector3.up * 1.1f, new Color32(255, 255, 255, 255), f);
    }

    // ---- the ground: grass on the flat, rock on the steep; the gorge's mist --------------------------------------

    // the far ranges: a ring of mountains 1.5 to 3 km out
    static Vector3 FarRange(float ang, float r)
    {
        float h = r > 1500f && r < 3000f ? (200f + 520f * Mathf.PerlinNoise(ang * 3f + 2f, r * 0.0005f)) * Mathf.Sin((r - 1500f) / 1500f * Mathf.PI) : 0f;
        return new Vector3(Mathf.Sin(ang) * r, h - 40f, 200f + Mathf.Cos(ang) * r);
    }

    static void EGround(Transform root)
    {
        float cell = 5f;
        for (float x = -320f; x < 320f; x += cell)
            for (float z = -260f; z < 620f; z += cell)
            {
                float s = cell;
                Vector3 a = new Vector3(x, EldenH(x, z), z), b = new Vector3(x, EldenH(x, z + s), z + s), c = new Vector3(x + s, EldenH(x + s, z + s), z + s), d = new Vector3(x + s, EldenH(x + s, z), z);
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                if (n.y < 0f) n = -n;
                Vector3 mid = (a + c) * 0.5f;
                bool steep = n.y < 0.72f;
                var k = RK(steep ? "rock" : "grass_ground", true, mid);
                // the green of the meadow goes olive and straw-coloured in patches, darker up the hills
                float tone = Mathf.PerlinNoise(x * 0.02f + 5f, z * 0.02f + 9f);
                float high = Mathf.Clamp01((mid.y - 12f) / 120f);
                Color c0 = Color.Lerp(new Color(0.86f, 0.92f, 0.78f), new Color(1f, 0.92f, 0.72f), tone) * (1f - 0.35f * high);
                Color32 col = steep ? new Color32(220, 222, 226, 255) : (Color32)new Color(c0.r, c0.g, c0.b, 1f);
                k.Quad(a, b, c, d, WUV(a, n, steep ? 14f : 7f), WUV(b, n, steep ? 14f : 7f), WUV(c, n, steep ? 14f : 7f), WUV(d, n, steep ? 14f : 7f), n, col);
            }
        // far ranges all round, blue-grey in the haze
        var far = RK("far_rock");
        int seg = 72;
        float[] rs = { 1100f, 1700f, 2050f, 2400f, 2800f, 3200f };
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            for (int j = 0; j < rs.Length - 1; j++)
            {
                Vector3 p00 = FarRange(a0, rs[j]), p01 = FarRange(a1, rs[j]), p10 = FarRange(a0, rs[j + 1]), p11 = FarRange(a1, rs[j + 1]);
                Vector3 n = Vector3.Cross(p10 - p00, p01 - p00).normalized;
                if (n.y < 0f) n = -n;
                far.Quad(p00, p10, p11, p01, new Vector2(p00.x, p00.y) / 80f, new Vector2(p10.x, p10.y) / 80f, new Vector2(p11.x, p11.y) / 80f, new Vector2(p01.x, p01.y) / 80f, n);
            }
        }
        // the mist filling the gorge, in layers
        var mist = RK("mist");
        foreach (float y in new[] { -18f, -28f, -40f })
            for (float x = -320f; x < 320f; x += 80f)
                for (float z = 96f; z < 216f; z += 40f)
                {
                    int i = mist.v.Count;
                    float s = 1f / 90f;
                    Vector3 a = new Vector3(x, y, z), b = new Vector3(x, y, z + 40f), c = new Vector3(x + 80f, y, z + 40f), d = new Vector3(x + 80f, y, z);
                    mist.Quad(a, b, c, d, new Vector2(a.x, a.z + y) * s, new Vector2(b.x, b.z + y) * s, new Vector2(c.x, c.z + y) * s, new Vector2(d.x, d.z + y) * s, Vector3.up);
                }
        // grass: thick round the course, thinner further off
        var rng = new System.Random(5150);
        int clumps = 0;
        for (int i = 0; i < 90000 && clumps < 26000; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * 130f, z = -50f + (float)rng.NextDouble() * 400f;
            float near = Mathf.Abs(x) < 30f ? 1f : 0.45f;
            if (rng.NextDouble() > near) continue;
            float y = EldenH(x, z);
            if (y < -1f || Mathf.Abs(y - EldenH(x + 1.5f, z)) > 0.9f || Mathf.Abs(y - EldenH(x, z + 1.5f)) > 0.9f) continue;   // not on the cliffs
            if (!EFree(x, z, 0.4f)) continue;
            if (z > 236f && z < 300f && x > -30f && x < 26f) continue;   // (the courtyard's paved)
            bool golden = Mathf.PerlinNoise(x * 0.03f + 20f, z * 0.03f) > 0.58f || rng.NextDouble() < 0.12;
            var k = RK(golden ? "grass_golden" : "grass_blades", false, new Vector3(x, 0f, z));
            float w = 1.1f + (float)rng.NextDouble() * 0.9f, h = 0.6f + (float)rng.NextDouble() * 0.7f;
            float turn = (float)rng.NextDouble() * Mathf.PI;
            Vector3 f1 = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)), f2 = new Vector3(-f1.z, 0f, f1.x);
            float tone = 0.72f + (float)rng.NextDouble() * 0.28f;
            var col = new Color32((byte)(255 * tone), (byte)(255 * tone), (byte)(240 * tone), 255);
            Card(k, new Vector3(x, y - 0.05f, z), f1, w, h, col, 0f, 0.1f, Vector3.up);
            Card(k, new Vector3(x, y - 0.05f, z), f2, w, h, col, 0f, -0.1f, Vector3.up);
            clumps++;
        }
    }

    // ---- trees, ruins about the meadow, rocks ----------------------------------------------------------------------

    static void EDecor(Transform root)
    {
        var rng = new System.Random(2022);
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        // golden autumn trees, the odd dark green one
        int trees = 0;
        for (int i = 0; i < 600 && trees < 95; i++)
        {
            float x = R(-140f, 140f), z = R(-60f, 360f);
            if (Mathf.Abs(x) < 9f && z < 215f) continue;           // off the line of the course
            if (z > 222f && z < 300f && x > -36f && x < 36f) continue;   // not in the castle
            float y = EldenH(x, z);
            if (y < -1f || Mathf.Abs(EldenH(x + 3f, z) - y) > 1.5f) continue;
            if (!EFree(x, z, 4f)) continue;
            bool green = rng.NextDouble() < 0.2;
            ETree(new Vector3(x, y, z), R(0.8f, 1.35f), green, rng);
            eFoot.Add(Rect.MinMaxRect(x - 1f, z - 1f, x + 1f, z + 1f));
            trees++;
        }
        // ruins scattered about the meadow: broken walls, columns, an arch or two
        var stone = RK("stone_block");
        for (int i = 0; i < 40; i++)
        {
            float x = R(-90f, 90f), z = R(-40f, 95f);
            if (Mathf.Abs(x) < 10f) continue;
            float y = EldenH(x, z);
            if (!EFree(x, z, 3f)) continue;
            float roll = (float)rng.NextDouble();
            if (roll < 0.45f)
            {
                float len = R(3f, 10f), h = R(0.8f, 4f);
                WBox(stone, new Vector3(x, y + h * 0.5f - 0.3f, z), new Vector3(len, h, R(0.7f, 1.2f)), Quaternion.Euler(0f, R(0f, 180f), 0f), 4f);
            }
            else if (roll < 0.8f)
            {
                float h = R(1.5f, 7f);
                WCyl(RK("stone_wall"), new Vector3(x, y - 0.3f, z), new Vector3(x, y + h, z), 0.75f, 0.7f, 12, 4f);
                Cap(RK("stone_wall"), new Vector3(x, y + h, z), 0.7f, 12);
            }
            else WBox(stone, new Vector3(x, y + 0.3f, z), new Vector3(R(1f, 2.5f), R(0.6f, 1.4f), R(1f, 2.5f)), Quaternion.Euler(R(-15f, 15f), R(0f, 180f), R(-15f, 15f)), 4f);
            eFoot.Add(Rect.MinMaxRect(x - 2f, z - 2f, x + 2f, z + 2f));
        }
        // a line of ruined arches off to the west, like the aqueduct in the distance
        for (int i = 0; i < 9; i++)
        {
            float z = -20f + i * 14f, x = -110f + i * 3f;
            float y = EldenH(x, z);
            if (i == 4 || i == 7) continue;   // fallen
            WBox(RK("stone_wall"), new Vector3(x, y + 9f, z), new Vector3(3f, 18f, 3f), Quaternion.identity, 4f);
            if (i % 3 != 2) WBox(RK("stone_wall"), new Vector3(x + 1.5f, y + 19f, z + 7f), new Vector3(3f, 2.5f, 14f), Quaternion.Euler(0f, -12f, 0f), 4f);
        }
        // boulders along the gorge's rims
        for (int i = 0; i < 40; i++)
        {
            float x = R(-150f, 150f), z = rng.NextDouble() < 0.5 ? R(88f, 98f) : R(208f, 220f);
            if (Mathf.Abs(x) < 8f) continue;
            float y = EldenH(x, z);
            WBox(RK("rock"), new Vector3(x, y + 0.5f, z), new Vector3(R(1.5f, 5f), R(1f, 3f), R(1.5f, 4f)), Quaternion.Euler(R(-20f, 20f), R(0f, 180f), R(-20f, 20f)), 6f);
        }
        // the castle's far side: more towers and a great hall against the mountains
        var st = RK("stone_castle");
        WBox(st, new Vector3(-10f, 25f, 310f), new Vector3(40f, 30f, 18f), Quaternion.identity, 4f);
        foreach (var t in new[] { new Vector3(-34f, 0f, 300f), new Vector3(14f, 0f, 318f), new Vector3(-20f, 0f, 330f) })
        {
            WCyl(st, new Vector3(t.x, 9f, t.z), new Vector3(t.x, 52f, t.z), 6f, 5.5f, 18, 4f);
            WCyl(RK("slate"), new Vector3(t.x, 52f, t.z), new Vector3(t.x, 70f, t.z), 6.6f, 0.05f, 18, 3f);
        }
        WBox(RK("slate"), new Vector3(-10f, 43f, 310f), new Vector3(40f, 6f, 12f), Quaternion.Euler(0f, 0f, 0f), 3f);
    }

    static void ETree(Vector3 at, float size, bool green, System.Random rng)
    {
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        float h = 7f * size;
        var bark = RK("bark", false, at);
        Vector3 top = at + new Vector3(R(-0.6f, 0.6f), h, R(-0.6f, 0.6f));
        WCyl(bark, at - Vector3.up * 0.5f, top, 0.38f * size, 0.18f * size, 8, 2f);
        var crowns = new List<Vector3> { top + Vector3.up * 1.2f * size };
        for (int b = 0; b < 4; b++)
        {
            float a = R(0f, Mathf.PI * 2f);
            Vector3 from = at + Vector3.up * h * R(0.45f, 0.8f);
            Vector3 to = from + new Vector3(Mathf.Cos(a), R(0.5f, 0.9f), Mathf.Sin(a)) * 3f * size;
            WCyl(bark, from, to, 0.15f * size, 0.07f * size, 6, 2f);
            crowns.Add(to);
        }
        var leaves = RK(green ? "leaves_green" : "leaves_gold", false, at);
        foreach (var c in crowns)
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = c + new Vector3(R(-1.8f, 1.8f), R(-1f, 1.4f), R(-1.8f, 1.8f)) * size;
                float s = R(1.2f, 2f) * size;
                Quaternion q = Quaternion.Euler(R(-40f, 40f), R(0f, 360f), R(-30f, 30f));
                byte tone = (byte)R(200f, 255f);
                FlatCard(leaves, p, q * Vector3.right * s, q * Vector3.up * s, new Color32(tone, tone, tone, 200), (p - c).normalized);
            }
    }

    // ---- the Erdtree, far off in the sky -------------------------------------------------------------------------

    static void EErdtree(Transform root)
    {
        Vector3 at = new Vector3(-900f, -60f, 2100f);
        var bark = RK("erd_bark");
        // a trunk of twisting strands
        var rng = new System.Random(1);
        for (int s = 0; s < 7; s++)
        {
            float a = s * Mathf.PI * 2f / 7f;
            Vector3 prev = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 70f;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                float aa = a + t * 1.6f;
                Vector3 next = at + new Vector3(Mathf.Cos(aa) * (70f - 40f * t), t * 700f, Mathf.Sin(aa) * (70f - 40f * t));
                WCyl(bark, prev, next, 30f - 14f * t, 30f - 14f * (t + 0.125f), 8, 60f);
                prev = next;
            }
        }
        // the canopy: a vast golden crown of leaves, glowing
        var leaves = RK("erd_leaves");
        Vector3 crown = at + Vector3.up * 860f;
        for (int i = 0; i < 900; i++)
        {
            // a broad dome, spreading wide and drooping at its edges
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rng.NextDouble());
            float spread = rr * 640f;
            Vector3 d = new Vector3(Mathf.Cos(ang) * spread, 160f * (1f - rr * rr) - 90f * rr * rr + ((float)rng.NextDouble() - 0.5f) * 120f, Mathf.Sin(ang) * spread * 0.85f);
            float s = 60f + (float)rng.NextDouble() * 80f;
            Quaternion q = Quaternion.Euler((float)rng.NextDouble() * 60f - 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f - 20f);
            FlatCard(leaves, crown + d, q * Vector3.right * s, q * Vector3.up * s, new Color32(255, 255, 255, 255), (crown + d - new Vector3(0f, 0f, 150f)).normalized);
        }
        // a halo round it
        var halo = RK("erd_halo");
        Vector3 toUs = (new Vector3(0f, 20f, 150f) - crown).normalized;
        FlatCard(halo, crown + Vector3.up * 40f, Vector3.Cross(Vector3.up, toUs).normalized * 1300f, Vector3.up * 700f, new Color32(255, 255, 255, 255), toUs);
    }

    // ---- Sites of Grace and messages ---------------------------------------------------------------------------------

    static EldenLevel.Grace[] EGraceObjects(Transform root)
    {
        var list = new List<EldenLevel.Grace>();
        for (int i = 0; i < EGraces.Length; i++)
        {
            Vector3 p = EGraces[i];
            var t = new GameObject("Grace " + EGraceNames[i]).transform;
            t.SetParent(root, false);
            Vector3 next = i + 1 < EGraces.Length ? EGraces[i + 1] : p + Vector3.forward;
            Vector3 dir = next - p; dir.y = 0f;
            // you come back beside the light, not in it (so the camera behind you isn't looking through its glare)
            Vector3 beside = Vector3.Cross(Vector3.up, dir.normalized) * 1.2f;   // (room for that on the aqueduct too)
            t.SetPositionAndRotation(p + Vector3.up * 0.05f + dir.normalized * 0.6f + beside, Quaternion.LookRotation(dir.normalized));
            // the light: a mote of gold above the ground, a soft column above it
            var g = RK("grace");
            Vector3 c = p + Vector3.up * 0.9f;
            for (int a = 0; a < 3; a++)
            {
                Vector3 f = Quaternion.Euler(0f, a * 60f, 0f) * Vector3.forward;
                FlatCard(g, c, Vector3.Cross(Vector3.up, f) * 0.9f, Vector3.up * 0.9f, new Color32(255, 255, 255, 255), f);
                FlatCard(g, c, Vector3.Cross(Vector3.up, f) * 2.4f, Vector3.up * 2.4f, new Color32(255, 230, 180, 90), f);
            }
            var beam = RK("grace_beam");
            for (int a = 0; a < 2; a++)
            {
                Vector3 f = Quaternion.Euler(0f, a * 90f, 0f) * Vector3.forward;
                FlatCard(beam, p + Vector3.up * 4f, Vector3.Cross(Vector3.up, f) * 0.6f, Vector3.up * 4f, new Color32(255, 255, 255, 255), f);
            }
            // guidance: thin golden rays leaning off towards the next grace
            if (i + 1 < EGraces.Length)
            {
                Vector3 d = dir.normalized;
                for (int r = -1; r <= 1; r++)
                {
                    Vector3 lean = (Quaternion.Euler(0f, r * 8f, 0f) * d * 0.8f + Vector3.up * 0.6f).normalized;
                    Vector3 side = Vector3.Cross(lean, Vector3.up).normalized;
                    FlatCard(beam, c + lean * 3f, side * 0.12f, lean * 3f, new Color32(255, 255, 255, 200), Vector3.Cross(side, lean));
                }
            }
            list.Add(new EldenLevel.Grace { name = EGraceNames[i], spot = t, radius = 3f });
        }
        return list.ToArray();
    }

    static EldenLevel.Message[] EMessages()
    {
        var m = new List<EldenLevel.Message>
        {
            new EldenLevel.Message { at = new Vector3(0f, 0f, 7f), text = "Low wall ahead, therefore run at it  (you vault over)" },
            new EldenLevel.Message { at = new Vector3(0f, 0f, 32f), text = "Ledge ahead, therefore jump at it and hold forward  (climb)" },
            new EldenLevel.Message { at = new Vector3(0f, 3f, 46f), text = "Try jumping  (hold SPACE to jump higher)" },
            new EldenLevel.Message { at = new Vector3(0f, EDeckY, 85f), text = "Gap ahead, therefore hold SHIFT and jump" },
            new EldenLevel.Message { at = new Vector3(0f, EDeckY, 141f), text = "Wall ahead, therefore jump at it and run along, holding W" },
            new EldenLevel.Message { at = new Vector3(-12f, 10f, 225f), text = "Climb ahead, therefore scale the scaffolding" },
            new EldenLevel.Message { at = new Vector3(18f, EWallY, 234f), text = "Low passage ahead, therefore sprint and slide  (C)" },
            new EldenLevel.Message { at = new Vector3(28f, EWallY, 244f), text = "Wall ahead, therefore run off beside it, holding W" },
            new EldenLevel.Message { at = new Vector3(19.5f, 34.8f, 287.5f), text = "Narrow walls ahead, therefore jump from one to the other  (SPACE on a wall)" },
        };
        var k = RK("message");
        foreach (var msg in m)
        {
            float y = msg.at.y;
            FlatCard(k, new Vector3(msg.at.x, y + 0.04f, msg.at.z), Vector3.right * 0.8f, Vector3.forward * 0.8f, new Color32(255, 255, 255, 255), Vector3.up);
            msg.at = new Vector3(msg.at.x, y + 0.4f, msg.at.z);
        }
        return m.ToArray();
    }

    // ---- the knight, seen in third person: dark iron, a tattered cloak, a rounded helm -----------------------------

    static Transform BuildKnight(Transform parent)
    {
        var mat = rMats["armour"];
        Color32 iron = new Color32(78, 80, 86, 255), dark = new Color32(46, 46, 50, 255), cloak = new Color32(70, 62, 48, 255), leather = new Color32(84, 62, 44, 255), gold = new Color32(150, 120, 70, 255);
        var root = new GameObject("Rider").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, -0.35f, 0f);

        var body = new SkateMeshKit();
        body.Ellipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.22f, 0.34f, 0.15f), Quaternion.identity, 12, 8, iron);           // breastplate
        body.Ellipsoid(new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.12f, 0.14f), Quaternion.identity, 12, 6, leather);         // belt and hips
        body.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.06f, 0.3f), Quaternion.identity, dark);
        body.Ellipsoid(new Vector3(0f, 0.82f, 0f), new Vector3(0.13f, 0.15f, 0.14f), Quaternion.identity, 12, 8, iron);          // helm
        body.Box(new Vector3(0f, 0.82f, 0.125f), new Vector3(0.16f, 0.025f, 0.03f), Quaternion.identity, new Color32(10, 10, 12, 255));   // eye slit
        body.Box(new Vector3(0f, 0.92f, 0f), new Vector3(0.025f, 0.12f, 0.26f), Quaternion.identity, gold);                       // crest
        foreach (float sx in new[] { -1f, 1f })
        {
            body.Ellipsoid(new Vector3(sx * 0.24f, 0.56f, 0f), new Vector3(0.12f, 0.09f, 0.13f), Quaternion.identity, 10, 6, iron);   // pauldron
            body.Cylinder(new Vector3(sx * 0.25f, 0.5f, 0f), new Vector3(sx * 0.3f, 0.08f, 0.04f), 0.055f, 8, dark);
            body.Ellipsoid(new Vector3(sx * 0.3f, 0.05f, 0.04f), new Vector3(0.055f, 0.06f, 0.055f), Quaternion.identity, 8, 5, leather);
        }
        // the cloak: hangs from the shoulders down the back, ragged at the hem
        for (int i = 0; i < 6; i++)
        {
            float x0 = -0.26f + i * 0.0867f, x1 = x0 + 0.0867f;
            float len = 0.95f + ((i * 37) % 5) * 0.04f;
            body.Quad(new Vector3(x0, 0.62f, -0.17f), new Vector3(x1, 0.62f, -0.17f), new Vector3(x1 * 1.25f, 0.62f - len, -0.3f), new Vector3(x0 * 1.25f, 0.62f - len, -0.3f), Vector3.back, cloak);
            body.Quad(new Vector3(x0, 0.62f, -0.165f), new Vector3(x0 * 1.25f, 0.62f - len, -0.295f), new Vector3(x1 * 1.25f, 0.62f - len, -0.295f), new Vector3(x1, 0.62f, -0.165f), Vector3.forward, cloak);
        }
        body.Ellipsoid(new Vector3(0f, 0.64f, -0.06f), new Vector3(0.26f, 0.06f, 0.16f), Quaternion.identity, 12, 5, cloak);    // the cloak's collar
        // a sword on the hip
        body.Box(new Vector3(-0.25f, -0.15f, -0.12f), new Vector3(0.03f, 0.8f, 0.06f), Quaternion.Euler(-20f, 0f, 10f), new Color32(170, 172, 178, 255));
        KnightPart("Body", root, body, mat).localPosition = new Vector3(0f, 0.9f, 0f);

        foreach (float sx in new[] { -1f, 1f })
        {
            var leg = new SkateMeshKit();
            leg.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.45f, 0f), 0.085f, 8, dark);
            leg.Ellipsoid(new Vector3(0f, -0.45f, 0.03f), new Vector3(0.08f, 0.07f, 0.08f), Quaternion.identity, 8, 5, iron);   // knee
            leg.Cylinder(new Vector3(0f, -0.45f, 0f), new Vector3(0f, -0.82f, 0f), 0.075f, 8, iron);
            leg.Box(new Vector3(0f, -0.86f, 0.05f), new Vector3(0.13f, 0.09f, 0.28f), Quaternion.identity, leather);
            KnightPart(sx < 0f ? "Leg L" : "Leg R", root, leg, mat).localPosition = new Vector3(sx * 0.11f, 0.9f, 0f);
        }
        root.gameObject.SetActive(false);
        return root;
    }

    static Transform KnightPart(string name, Transform root, SkateMeshKit kit, Material mat)
    {
        var mesh = kit.Build("Knight " + name);
        // (SkateMeshKit has no uvs; the material's texture is plain white, so any will do)
        SaveFeatureMesh(mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    public static void EldenShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        Shot(cam, new Vector3(0f, 2.2f, -6f), new Vector3(0f, 1.5f, 20f), "r01-start.png");
        Shot(cam, new Vector3(-14f, 8f, 20f), new Vector3(0f, 3f, 55f), "r02-meadow-ruins.png");
        Shot(cam, new Vector3(-30f, 18f, 70f), new Vector3(0f, 6f, 150f), "r03-aqueduct.png");
        Shot(cam, new Vector3(0f, 11.7f, 140f), new Vector3(2f, 11f, 165f), "r04-wallrun.png");
        Shot(cam, new Vector3(-10f, 16f, 200f), new Vector3(0f, 22f, 240f), "r05-castle.png");
        Shot(cam, new Vector3(40f, 34f, 250f), new Vector3(18f, 36f, 282f), "r06-keep.png");
        Shot(cam, new Vector3(19f, 48f, 279f), new Vector3(-200f, 200f, 900f), "r07-erdtree-from-the-keep.png");
        Shot(cam, new Vector3(6f, 1.7f, 2f), new Vector3(-60f, 60f, 400f), "r08-erdtree-from-the-start.png");
        Shot(cam, new Vector3(-700f, 380f, 1000f), new Vector3(-900f, 750f, 2100f), "r09-erdtree.png");
        // the knight, as seen in third person
        var player = Object.FindFirstObjectByType<SlideController>();
        var rider = player.rider;
        Vector3 at = new Vector3(0f, 0f, 10f);
        rider.gameObject.SetActive(true);
        rider.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.forward));
        Shot(cam, at + new Vector3(-1.6f, 1.8f, -3.2f), at + Vector3.up * 1f, "r10-knight.png");
        rider.gameObject.SetActive(false);
        rider.localPosition = new Vector3(0f, -0.35f, 0f);
        rider.localRotation = Quaternion.identity;
    }
}
