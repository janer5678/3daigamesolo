using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Akiba Sky: Akiba again, but this time you climb it, laid out from the user's drawing
// (Layouts/Drawings/Akiba 2026-10-13 2218: a map from above and a side view). The street's the same, the
// buildings round it go up two or three times as high, and there are no little launch pads, trampolines or
// bouncy capsules.
//
// Floating decks hang over the street at every height from 20m to 120m, and the way up is by grinding and
// jumping: ramps up onto the walkways from the pavement; speed rails (cyan in the drawing) that grab you as you
// ride into them and swoop you up onto the next deck; rails (yellow) you jump onto, one running from the tall
// bus stop by the start all the way up to 50m; a few giant jump pads (pink); vents. It goes round and round:
// the south end climbs to the summit at 135m (the top rail at that end of the drawing), the plaza and the north
// end climb to the north sky road at 120m, and a speed rail the length of the street joins that to the summit.
// A rail runs back down to the street at the north end. Very slow, very tall buses (blue) crawl up and down the
// street between stops twelve metres up. Every way up is a link with a test ride (SlideTests.RunAkibaSky).
public static partial class SkateWorldBuilder
{
    public const string AkibaSkyPath = "Assets/Scenes/AkibaSky.unity";
    static bool akSky;
    static readonly int[,] akSkyWalk = new int[2, 2];   // [south, north][west, east]

    // -executeMethod SkateWorldBuilder.AkibaSkyFromCommandLine: just this level, its test runs and screenshots
    public static void AkibaSkyFromCommandLine()
    {
        try { BuildAkibaSky(); SlideTests.RunAkibaSky(); AkibaSkyShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildAkibaSky() { BuildAkibaLevel(true); }

    // the buildings in the sky: towers half as high again and more, everything else a good deal taller
    static float AkSkyHeight(float h, int kind) { return kind == 1 ? h * 1.5f + 20f : h * 1.4f + 30f; }

    // ---- the plan --------------------------------------------------------------------------------------

    const float SkyStopY = 12f, TallBus = 12f;

    // the decks, from the drawing (its side view squeezed to fit the street): name, height, x from-to, z from-to
    static readonly (string name, float y, float x0, float x1, float z0, float z1)[] AkDecks = {
        ("South deck", 21f, -2f, 10f, -118f, -94f),          // 0
        ("Middle deck", 20f, -6f, 6f, -41f, -25f),           // 1
        ("North long deck", 25f, -12f, 2f, 36f, 94f),        // 2
        ("South mid deck", 43f, -20f, -8f, -80f, -54f),      // 3
        ("Plaza deck", 38f, 6f, 18f, -4f, 16f),              // 4
        ("Rail top", 50f, -10f, 2f, -22f, -8f),              // 5
        ("Tower ledge", 60f, 17f, 27f, -36f, -22f),          // 6
        ("Plaza high deck", 64f, -22f, -10f, 4f, 20f),       // 7
        ("North mid deck", 63f, 10f, 22f, 52f, 68f),         // 8
        ("North deck", 50f, 8f, 20f, 72f, 106f),             // 9
        ("South high deck", 75f, -22f, -10f, -115f, -96f),   // 10
        ("Sky road", 91f, 0f, 12f, -60f, 6f),                // 11
        ("Plaza top deck", 95f, -16f, -4f, 22f, 37f),        // 12
        ("North high deck", 93f, -10f, 2f, 98f, 114f),       // 13
        ("South top deck", 110f, -8f, 6f, -93f, -66f),       // 14
        ("North sky road", 120f, -6f, 6f, 46f, 96f),         // 15
        ("South end deck", 125f, -10f, 6f, -119f, -106f),    // 16
        ("Summit", 135f, -12f, 12f, -84f, -64f) };           // 17
    static readonly int[] akDeck = new int[AkDecks.Length];

    // ramps up from the pavement onto each walkway (x from-to, z at the bottom, z at the top), with a deck at the top
    static readonly (int side, int end, float x0, float x1, float zLow, float zTop)[] AkWalkRamps = {
        (1, 0, 15.5f, 21.5f, -100f, -84f),    // by the start, up onto the south-east walkway
        (1, 1, 16f, 22f, 62f, 78f),
        (0, 0, -21.5f, -15.5f, -62f, -78f),
        (0, 1, -21.5f, -15.5f, 60f, 76f) };

    static readonly Dictionary<int, List<(int edge, float a, float b)>> akOpenings = new Dictionary<int, List<(int, float, float)>>();
    static int akGiantPads;

    // the stops beside the bus lanes, level with a tall bus's roof
    static readonly Rect[] AkSkyStopRects = {
        Rect.MinMaxRect(-11f, -24f, -7.4f, -12f),   // 0 tall stop W, by the plaza
        Rect.MinMaxRect(-11f, -92f, -7.4f, -80f),   // 1 tall stop W, south
        Rect.MinMaxRect(7.4f, 12f, 11f, 24f),       // 2 tall stop E, by the plaza
        Rect.MinMaxRect(7.4f, 80f, 11f, 92f) };     // 3 tall stop E, north
    static readonly string[] AkSkyStopNames = { "Tall stop W plaza", "Tall stop W south", "Tall stop E plaza", "Tall stop E north" };
    static readonly int[] akSkyStop = new int[4];

    // (bottom, top, where it goes): up to the walkways and balconies as in Akiba, and up to every stop
    static readonly (Vector3, Vector3, string)[] AkSkyEscalatorDefs = {
        (new Vector3(23.5f, Kerb, -45.9f), new Vector3(23.5f, AkWalkY, -58f), "south e"),
        (new Vector3(-23.5f, Kerb, -45.9f), new Vector3(-23.5f, AkWalkY, -58f), "south w"),
        (new Vector3(23.5f, Kerb, 104.1f), new Vector3(23.5f, AkWalkY, 92f), "north e"),
        (new Vector3(-23.5f, Kerb, 44f), new Vector3(-23.5f, AkWalkY, 56f), "north w"),
        (new Vector3(26.2f, Kerb, 24f), new Vector3(AkPlazaFront - 6f, AkBalconyY, 24f), "balcony e"),
        (new Vector3(-26.2f, Kerb, -24f), new Vector3(-(AkPlazaFront - 6f), AkBalconyY, -24f), "balcony w"),
        (new Vector3(-9.2f, Kerb, 12f), new Vector3(-9.2f, SkyStopY, -12f), "0"),
        (new Vector3(-9.2f, Kerb, -56f), new Vector3(-9.2f, SkyStopY, -80f), "1"),
        (new Vector3(9.2f, Kerb, -12f), new Vector3(9.2f, SkyStopY, 12f), "2"),
        (new Vector3(9.2f, Kerb, 56f), new Vector3(9.2f, SkyStopY, 80f), "3") };

    // where everything goes, so the buildings keep their signs and the stalls out of the way
    static void AkSkyPlan()
    {
        akOpenings.Clear(); akGiantPads = 0;
        foreach (var d in AkDecks) akKeepOut.Add(Box3((d.x0 + d.x1) / 2f, d.y, (d.z0 + d.z1) / 2f, d.x1 - d.x0 + 8f, 14f, d.z1 - d.z0 + 8f));
        foreach (var r in AkWalkRamps) akKeepOut.Add(Box3((r.x0 + r.x1) / 2f, 5f, (r.zLow + r.zTop) / 2f + Mathf.Sign(r.zTop - r.zLow) * 6f, r.x1 - r.x0 + 3f, 10f, 34f));
        foreach (var r in AkSkyStopRects) akKeepOut.Add(Box3(r.center.x, 7f, r.center.y, r.width + 2f, 14f, r.height + 4f));
    }

    // ---- openings in the low walls round the edges -----------------------------------------------------

    // an opening in the wall round n, hw either side of p, on whichever edge p's by (both, at a corner)
    static void AkOpen(int n, Vector3 p, float hw)
    {
        if (!akOpenings.TryGetValue(n, out var list)) return;
        Rect r = akNodes[n].areas[0];
        float[] d = { Mathf.Abs(p.z - r.yMin), Mathf.Abs(p.z - r.yMax), Mathf.Abs(p.x - r.xMin), Mathf.Abs(p.x - r.xMax) };
        float best = Mathf.Min(Mathf.Min(d[0], d[1]), Mathf.Min(d[2], d[3]));
        for (int e = 0; e < 4; e++)
            if (d[e] < best + 1.5f) list.Add((e, (e < 2 ? p.x : p.z) - hw, (e < 2 ? p.x : p.z) + hw));
    }

    // openings wherever a path goes over n's edge
    static void AkOpenCrossings(int n, IList<Vector3> pts, float hw)
    {
        if (!akOpenings.ContainsKey(n)) return;
        Rect r = akNodes[n].areas[0];
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(XZ(a), XZ(b)) / 0.25f));
            bool was = r.Contains(XZ(a));
            for (int k = 1; k <= steps; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / (float)steps);
                bool now = r.Contains(XZ(p));
                if (now != was) AkOpen(n, p, hw);
                was = now;
            }
        }
    }

    // a low wall round n's edges (bits: 1 south, 2 north, 4 west, 8 east), leaving the openings
    static void AkRim(int n, int edges, float h = 0.7f)
    {
        Rect r = akNodes[n].areas[0];
        float y = akNodes[n].top;
        var open = akOpenings[n];
        Color32 glow = new Color32(255, 70, 190, 255);
        for (int e = 0; e < 4; e++)
        {
            if ((edges & (1 << e)) == 0) continue;
            float a0 = e < 2 ? r.xMin : r.yMin, a1 = e < 2 ? r.xMax : r.yMax;
            var cuts = new List<(float a, float b)>();
            foreach (var o in open) if (o.edge == e) cuts.Add((o.a, o.b));
            cuts.Sort((p, q) => p.a.CompareTo(q.a));
            float at = a0;
            var spans = new List<(float, float)>();
            foreach (var c in cuts)
            {
                if (c.a > at) spans.Add((at, Mathf.Min(c.a, a1)));
                at = Mathf.Max(at, c.b);
            }
            if (at < a1) spans.Add((at, a1));
            foreach (var (s0, s1) in spans)
            {
                if (s1 - s0 < 0.6f) continue;
                float mid = (s0 + s1) / 2f, len = s1 - s0;
                Vector3 c = e == 0 ? new Vector3(mid, y + h / 2f, r.yMin + 0.15f) : e == 1 ? new Vector3(mid, y + h / 2f, r.yMax - 0.15f)
                          : e == 2 ? new Vector3(r.xMin + 0.15f, y + h / 2f, mid) : new Vector3(r.xMax - 0.15f, y + h / 2f, mid);
                Vector3 size = e < 2 ? new Vector3(len, h, 0.3f) : new Vector3(0.3f, h, len);
                AK("concrete").Box(c, size, Quaternion.identity, 3f, 3f);
                AK("white", "e").Box(c + Vector3.up * (h / 2f + 0.02f), new Vector3(e < 2 ? len : 0.32f, 0.04f, e < 2 ? 0.32f : len), Quaternion.identity, 1f, 1f, glow);
                AkBox(c, size);
            }
        }
    }

    // ---- the stops --------------------------------------------------------------------------------------

    static void AkSkyStops()
    {
        for (int i = 0; i < AkSkyStopRects.Length; i++)
        {
            Rect r = AkSkyStopRects[i];
            float busSide = -Mathf.Sign(r.center.x);   // the bus pulls up on the side towards the middle of the street
            float h = SkyStopY - Kerb;
            Vector3 c = new Vector3(r.center.x, Kerb + h / 2f, r.center.y);
            AK("concrete").Box(c, new Vector3(r.width, h, r.height), Quaternion.identity, 3f, 3f);
            AkBox(c, new Vector3(r.width, h, r.height));
            // posters all the way up the sides, a lit band round the top, the tactile strip along the bus side
            foreach (var sx in new[] { -1f, 1f })
                AK("posters", "e").Panel(new Vector3(sx > 0 ? r.xMax + 0.02f : r.xMin - 0.02f, c.y, r.center.y), Rt(new Vector3(sx, 0f, 0f)) * (r.height / 2f - 0.2f), Vector3.up * h / 2f * 0.94f, null, false, new Rect(0f, 0f, r.height / 6f, h / 2.5f));
            Color32 band = i < 4 ? new Color32(60, 230, 120, 255) : new Color32(255, 70, 190, 255);
            AK("white", "e").Box(new Vector3(r.center.x, SkyStopY - 0.25f, r.center.y), new Vector3(r.width + 0.08f, 0.3f, r.height + 0.08f), Quaternion.identity, 1f, 1f, band);
            float ex = busSide > 0 ? r.xMax : r.xMin;
            AK("tactile").Ground(Mathf.Min(ex, ex - busSide * 0.6f), r.yMin, Mathf.Max(ex, ex - busSide * 0.6f), r.yMax, SkyStopY + 0.01f, 0.6f, 20f);
            akSkyStop[i] = AkAddNode(AkSkyStopNames[i], NStop, SkyStopY, r);
        }
    }

    static void AkSkyEscalators()
    {
        foreach (var (a, b, to) in AkSkyEscalatorDefs)
        {
            int node = to == "south w" ? akSkyWalk[0, 0] : to == "south e" ? akSkyWalk[0, 1] : to == "north w" ? akSkyWalk[1, 0] : to == "north e" ? akSkyWalk[1, 1]
                     : to == "balcony e" ? akBalconyE : to == "balcony w" ? akBalconyW : akSkyStop[int.Parse(to)];
            Escalator(a, b, 2.2f, false);
            Vector3 dir = FlatDir(b - a);
            Landing(b + dir * 2.5f);
            var l = AkAddLink("escalator", 0, node);
            l.start = a - dir * 3f; l.yaw = YawOf(dir);
        }
    }

    // more signs all the way up the tall buildings, so the higher you get there's still neon going past
    static void AkSkySigns(Rect lot, float h, float side, float fx, float w, float zc)
    {
        Vector3 n = new Vector3(-side, 0f, 0f);
        for (float y = 34f; y < h - 6f; y += AR(8f, 14f))
        {
            if (ark.NextDouble() < 0.55)
            {
                float bw = Mathf.Min(w - 1f, AR(5f, 10f)), bz = zc + AR(-(w - bw) / 2f, (w - bw) / 2f);
                if (Kept(Box3(fx, y, bz, 1f, bw / 2.7f + 0.4f, bw))) continue;
                AK("sign_h_" + AI(0, 6), ark.NextDouble() < 0.3 ? "f" : "e").Panel(new Vector3(fx - side * 0.06f, y, bz), Rt(n) * (bw / 2f), Vector3.up * (bw / 5.4f));
            }
            else
            {
                float sh = AR(4f, 8f), z = zc + AR(-w / 2f + 1.5f, w / 2f - 1.5f);
                Vector3 sc = new Vector3(fx - side * 0.85f, y, z);
                if (Kept(Box3(sc.x, y, z, 2.2f, sh + 1f, 2f))) continue;
                string st = ark.NextDouble() < 0.35 ? "f" : "e", tex = "sign_v_" + AI(0, 8);
                AK(tex, st).Panel(sc + Vector3.forward * 0.09f, Rt(Vector3.forward) * 0.65f, Vector3.up * sh / 2f);
                AK(tex, st).Panel(sc + Vector3.back * 0.09f, Rt(Vector3.back) * 0.65f, Vector3.up * sh / 2f);
                AK("metal").Box(sc, new Vector3(1.34f, sh + 0.12f, 0.1f), Quaternion.identity, 1f, 1f);
                AkBox(sc, new Vector3(1.4f, sh + 0.12f, 0.2f));
            }
        }
    }

    // ---- the climb ----------------------------------------------------------------------------------------

    static void AkSkyClimb()
    {
        Physics.SyncTransforms();
        for (int i = 0; i < AkDecks.Length; i++)
        {
            var d = AkDecks[i];
            Rect r = Rect.MinMaxRect(d.x0, d.z0, d.x1, d.z1);
            akDeck[i] = AkSkyDeck(d.name + " (" + d.y + "m)", r, d.y, r.height > 40f);
            TowerCheckpoint(akRoot, new Vector3(r.center.x, d.y, r.center.y), d.name);
        }
        int south = akDeck[0], mid = akDeck[1], northLong = akDeck[2], southMid = akDeck[3], plaza = akDeck[4], railTop = akDeck[5], ledge = akDeck[6],
            plazaHigh = akDeck[7], northMid = akDeck[8], north = akDeck[9], southHigh = akDeck[10], skyRoad = akDeck[11], plazaTop = akDeck[12],
            northHigh = akDeck[13], southTop = akDeck[14], northSky = akDeck[15], southEnd = akDeck[16], summit = akDeck[17];
        LuckyCat(new Vector3(8f, 135f, -74f), 0.8f);
        Vector3 N = Vector3.forward, S = Vector3.back, E = Vector3.right, W = Vector3.left;

        // up off the street: a ramp up onto each walkway (orange in the drawing; the first one's right by the start)
        foreach (var rp in AkWalkRamps) AkWalkRamp(rp.side, rp.end, rp.x0, rp.x1, rp.zLow, rp.zTop);

        // onto the first decks: speed rails off both south walkways onto the south deck, off the north-east walkway
        // and the north tall stop onto the north long deck, off both balconies (cyan); vents off the tall stops by the
        // plaza; and the rail from the tall stop by the start all the way up to 50m (the long yellow one)
        AkCurve(akSkyWalk[0, 0], south, new Vector3(-25f, 0f, -64f), S, new Vector3(-1f, 0f, -104f), E, true);
        AkCurve(akSkyWalk[0, 1], south, new Vector3(25f, 0f, -63f), S, new Vector3(9f, 0f, -100f), W, true);
        AkCurve(akSkyWalk[1, 1], northLong, new Vector3(25f, 0f, 84f), S, new Vector3(0f, 0f, 62f), S, true);
        AkCurve(akSkyStop[3], northLong, new Vector3(9.2f, 0f, 85f), N, new Vector3(0f, 0f, 86f), S, true, 3f);
        AkCurve(akBalconyW, mid, new Vector3(-43f, 0f, -16f), S, new Vector3(-5f, 0f, -33f), E, true);
        AkCurve(akBalconyE, plaza, new Vector3(43f, 0f, -16f), N, new Vector3(12f, 0f, 0f), N, true);
        AkSkyVent(akSkyStop[0], mid, new Vector3(-9.2f, SkyStopY, -21.5f), new Vector3(-3f, 20f, -28f));
        AkSkyVent(akSkyStop[2], plaza, new Vector3(9.2f, SkyStopY, 21.5f), new Vector3(10f, 38f, 12f), 9f, 7f);
        AkCurve(akSkyStop[1], railTop, new Vector3(-9.2f, 0f, -86f), N, new Vector3(-4f, 0f, -20f), N, false);
        AkSkyVent(akSkyWalk[1, 0], akRailway, new Vector3(-25f, AkWalkY, 90.4f), new Vector3(-24f, AkViaductY, 97.5f), 9f, 7f);

        // the middle: up from the middle deck onto the plaza deck, from the rail top over to the tower ledge (rails),
        // down off the rail top onto the middle deck; across the plaza and up to the plaza high deck; the north end
        AkCurve(mid, plaza, new Vector3(2f, 0f, -29f), new Vector3(0.5f, 0f, 1f), new Vector3(11f, 0f, 2f), N, false);
        AkCurve(railTop, ledge, new Vector3(0f, 0f, -11f), E, new Vector3(22f, 0f, -24f), S, false);
        AkDrop(railTop, mid, new Vector3(-4f, 50f, -10f), S, false);
        AkCurve(plaza, plazaHigh, new Vector3(16f, 0f, 0f), N, new Vector3(-13f, 0f, 12f), W, true);
        AkDrop(northMid, north, new Vector3(19f, 63f, 58f), N, true);
        AkCurve(southMid, southHigh, new Vector3(-14f, 0f, -62f), S, new Vector3(-16f, 0f, -100f), S, true);

        // higher: the tower ledge up onto the sky road, the plaza high deck's vent up to the plaza top deck, the north
        // deck up to the north high deck, the south high deck's vent up to the south top deck
        AkCurve(ledge, skyRoad, new Vector3(25f, 0f, -30f), N, new Vector3(6f, 0f, -20f), S, true, 1.6f);
        AkSkyVent(plazaHigh, plazaTop, new Vector3(-11.5f, 64f, 18.5f), new Vector3(-10f, 95f, 26f));
        AkCurve(north, northHigh, new Vector3(10f, 0f, 84f), N, new Vector3(-1f, 0f, 106f), W, true, 1.4f);
        AkSkyVent(southHigh, southTop, new Vector3(-11.5f, 75f, -97.5f), new Vector3(-5f, 110f, -89f), 7f, 5f);

        // the top: up to the north sky road from the plaza top deck and the north high deck; the south top deck up to
        // the south end deck, and the rail from there up onto the summit (the top yellow one); and a speed rail the
        // length of the street from the north sky road to the summit
        AkCurve(plazaTop, northSky, new Vector3(-13f, 0f, 28f), N, new Vector3(-2f, 0f, 52f), N, true);
        AkCurve(northHigh, northSky, new Vector3(-4f, 0f, 108f), S, new Vector3(2f, 0f, 90f), S, true, 2f);
        AkCurve(southTop, southEnd, new Vector3(-1f, 0f, -80f), S, new Vector3(-2f, 0f, -108f), S, false);
        AkCurve(southEnd, summit, new Vector3(3f, 0f, -110f), N, new Vector3(-4f, 0f, -78f), N, false);
        AkCurve(northSky, summit, new Vector3(3f, 0f, 80f), S, new Vector3(2f, 0f, -70f), S, true);

        // and back down: the rail off the plaza high deck all the way down to the street at the north end
        AkCurve(plazaHigh, 0, new Vector3(-20f, 0f, 12f), N, new Vector3(-13f, 0f, 76f), N, false);

        // the tall buses (blue), very slow, between the stops by the plaza and the ones near the ends (they don't
        // turn round at the ends, just go back the other way: a bus that long turning on the spot would sweep you
        // off its roof)
        Color32 pink = new Color32(255, 70, 190, 255), green = new Color32(60, 230, 120, 255);
        FlyingBusLine("Tall bus W", new[] { akSkyStop[0], akSkyStop[1] }, new[] { Dock(akSkyStop[0], 1f, -18f), Dock(akSkyStop[1], 1f, -86f) }, pink, TallBus, 3f, 120f, "tall bus").turn = false;
        FlyingBusLine("Tall bus E", new[] { akSkyStop[2], akSkyStop[3] }, new[] { Dock(akSkyStop[2], -1f, 18f), Dock(akSkyStop[3], -1f, 86f) }, green, TallBus, 3f, 120f, "tall bus").turn = false;

        // the giant jump pads (pink): off the south deck, the plaza, the north long deck and the sky road
        RefreshRails();
        Physics.SyncTransforms();
        AkGiantPad(south, southMid, new Vector3(8f, 21f, -112f), new Vector3(-13f, 43f, -58f));
        AkGiantPad(0, plazaHigh, new Vector3(-17f, Kerb, -20f), new Vector3(-18f, 64f, 8f));
        AkGiantPad(northLong, northMid, new Vector3(-6f, 25f, 48f), new Vector3(16f, 63f, 60f));
        AkGiantPad(skyRoad, southTop, new Vector3(6f, 91f, -50f), new Vector3(-1f, 110f, -75f));

        // low walls round every deck (bar where things come and go), so you don't slide off at speed
        foreach (int n in akDeck) AkRim(n, 15);
    }

    // a floating deck: a thick slab of rooftop (or a long one, a road), lit round the sides
    static int AkSkyDeck(string name, Rect r, float y, bool road)
    {
        Vector3 c = new Vector3(r.center.x, y - 0.8f, r.center.y);
        AK(road ? "asphalt" : "roof").Box(c, new Vector3(r.width, 1.6f, r.height), Quaternion.identity, road ? 8f : 4f, road ? 8f : 4f, null, true, true);
        if (road)
            for (float z = r.yMin + 3f; z < r.yMax - 3f; z += 8f)
                AK("white", "e").Box(new Vector3(r.center.x, y + 0.02f, z), new Vector3(0.16f, 0.02f, 4f), Quaternion.identity, 1f, 1f);
        foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            float half = (Mathf.Abs(n.x) > 0.5f ? r.height : r.width) / 2f;
            float off = (Mathf.Abs(n.x) > 0.5f ? r.width : r.height) / 2f + 0.03f;
            AK("led_" + AI(0, 3), "s").Panel(c + n * off, Rt(n) * half, Vector3.up * 0.75f, null, false, new Rect(0f, 0f, half / 3.2f, 1f));
        }
        AkBox(c, new Vector3(r.width, 1.6f, r.height));
        // a sign hanging off the bottom, an air conditioner
        AK("sign_h_" + AI(0, 6), "e").Panel(new Vector3(r.center.x, y - 2.6f, r.yMin - 0.05f), Rt(Vector3.back) * Mathf.Min(4f, r.width / 2f - 0.5f), Vector3.up * 0.9f);
        AK("metal").Box(c + Vector3.down * 1.3f, new Vector3(1.4f, 1f, 1f), Quaternion.identity, 1f, 1f, new Color32(210, 210, 220, 255));
        int n0 = AkAddNode(name, NIsland, y, r);
        akOpenings[n0] = new List<(int, float, float)>();
        return n0;
    }

    // a ramp up from the pavement onto a walkway (side 0 west, 1 east; end 0 south, 1 north), with a deck at the top
    // joining onto it
    static void AkWalkRamp(int side, int end, float x0, float x1, float zLow, float zTop)
    {
        int walk = akSkyWalk[end, side];
        float s = Mathf.Sign(zTop - zLow), len = Mathf.Abs(zTop - zLow), w = x1 - x0, xc = (x0 + x1) / 2f, deckLen = 14f;
        Vector3 dir = new Vector3(0f, 0f, s);
        Wedge("roof", new Vector3(xc, Kerb, zLow), dir, len, AkWalkY - Kerb, w);
        for (int i = 0; i < 3; i++)
            AK("white", "e").Box(new Vector3(xc, Kerb, zLow) + dir * (i * 0.8f - 2.6f) + Vector3.up * 0.03f, new Vector3(w * 0.8f, 0.04f, 0.3f), Quaternion.LookRotation(dir), 1f, 1f, new Color32(255, 120, 20, 255));
        Rect r = Rect.MinMaxRect(x0, Mathf.Min(zTop, zTop + s * deckLen), x1, Mathf.Max(zTop, zTop + s * deckLen));
        Vector3 c = new Vector3(xc, AkWalkY - 0.4f, r.center.y);
        AK("pavement").Box(c, new Vector3(w, 0.8f, deckLen), Quaternion.identity, 4f, 4f, null, true, true);
        AkBox(c, new Vector3(w, 0.8f, deckLen));
        Vector3 post = new Vector3(xc, (AkWalkY - 0.8f + Kerb) / 2f, zTop + s * (deckLen - 1f)), postSize = new Vector3(0.5f, AkWalkY - 0.8f - Kerb, 0.5f);
        AK("metal").Box(post, postSize, Quaternion.identity, 1f, 2f, new Color32(150, 150, 170, 255));
        AkBox(post, postSize);
        akNodes[walk].areas.Add(r);
        // low walls across its far end and along the street side
        float streetX = Mathf.Sign(xc) > 0 ? x0 : x1;
        foreach (var (wc, ws) in new[] {
            (new Vector3(xc, AkWalkY + 0.35f, zTop + s * (deckLen - 0.15f)), new Vector3(w, 0.7f, 0.3f)),
            (new Vector3(streetX + Mathf.Sign(xc) * 0.15f, AkWalkY + 0.35f, r.center.y), new Vector3(0.3f, 0.7f, deckLen)) })
        {
            AK("concrete").Box(wc, ws, Quaternion.identity, 3f, 3f);
            AK("white", "e").Box(wc + Vector3.up * 0.37f, new Vector3(ws.x + 0.02f, 0.04f, ws.z + 0.02f), Quaternion.identity, 1f, 1f, new Color32(255, 120, 20, 255));
            AkBox(wc, ws);
        }
        var l = AkAddLink("ramp", 0, walk);
        l.start = new Vector3(xc, Kerb, zLow - s * 9f); l.yaw = YawOf(dir); l.sprint = true; l.timeout = 8f;
    }

    // a rail from `from` on a (setting off along fromDir) curving round to `to` over b (coming in along toDir),
    // rising or falling smoothly between, kept up off the deck it comes in over. A speed rail grabs you as you ride
    // into it and rushes you along; an ordinary one you jump onto, and it keeps you moving. `bend` swings it wider
    static void AkCurve(int a, int b, Vector3 from, Vector3 fromDir, Vector3 to, Vector3 toDir, bool speed, float bend = 1f)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        float y0 = A.top + (speed ? 0.6f : 0.9f), y1 = B.top + 1.2f;
        fromDir = FlatDir(fromDir); toDir = FlatDir(toDir);
        Vector3 f0 = new Vector3(from.x, y0, from.z), t0 = new Vector3(to.x, y1, to.z);
        float L = Vector2.Distance(XZ(from), XZ(to)) * 0.45f * bend;
        Vector3 c1 = f0 + fromDir * L, c2 = t0 - toDir * L;
        var pts = new List<Vector3>();
        const int n = 48;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            Vector3 q = u * u * u * f0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * t0;
            q.y = Mathf.Lerp(y0, y1, t * t * (3f - 2f * t));
            pts.Add(q);
        }
        pts.Add(t0 + toDir * 4f);
        // over b (and just short of it) it's always a little above b's top
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            if (B.kind != NGround && B.Contains(XZ(p), -2.5f)) p.y = Mathf.Max(p.y, B.top + (B.Contains(XZ(p), 0.5f) ? 1.2f : 1.8f));
            pts[i] = p;
        }
        if (speed) AkSkyRail(a, b, pts.ToArray());
        else AkGrindRail(a, b, pts.ToArray());
    }

    // a rail (yellow) to jump onto: it keeps you moving along it, up or down, and throws you off the end
    static void AkGrindRail(int a, int b, Vector3[] pts)
    {
        AkNode A = akNodes[a];
        Rail("Grind rail", pts, false);
        RefreshRails();
        var rail = akRails[akRails.Length - 1];
        rail.catchRadius = 0.9f; rail.forceSpeed = 9f;
        AK("white", "e").Tube(pts, 0.09f, 6, 2f, new Color32(255, 214, 40, 255));
        Vector3 dir = FlatDir(pts[2] - pts[0]);
        Vector3 foot = new Vector3(pts[0].x, A.top, pts[0].z);
        float run = Mathf.Min(RunRoom(A, foot, dir), 6f);
        if (run < 3f) Debug.Log("AKIBA_SKY short run-up (" + run + "m) for the rail " + A.name + " -> " + akNodes[b].name);
        var l = AkAddLink("rail", a, b);
        l.start = foot - dir * run; l.yaw = YawOf(dir); l.jumpAt = run - 1.3f; l.timeout = 30f;
        RailClear(pts, A.name + " -> " + akNodes[b].name);
        AkOpenCrossings(a, pts, 2.5f);
        AkOpenCrossings(b, pts, 2.5f);
    }

    // is it clear where you ride (a body's height over it)? (not counting where it starts and ends)
    static void RailClear(Vector3[] pts, string what)
    {
        float len = 0f;
        for (int i = 1; i < pts.Length; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
        float along = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            along += seg;
            if (along < 3f || along > len - 2.5f) continue;
            Vector3 p0 = pts[i - 1] + Vector3.up * 0.75f, p1 = pts[i] + Vector3.up * 0.75f;
            if (Physics.SphereCast(p0, 0.38f, (p1 - p0).normalized, out RaycastHit hit, seg, ~0, QueryTriggerInteraction.Ignore))
                Debug.Log("AKIBA_SKY_RAIL_BLOCKED " + what + " by " + hit.collider.name + " at " + hit.point.ToString("F1"));
        }
    }

    // ride off the edge of a, down onto b
    static void AkDrop(int a, int b, Vector3 start, Vector3 dir, bool sprint)
    {
        var l = AkAddLink("drop", a, b);
        l.start = start; l.yaw = YawOf(dir); l.sprint = sprint;
        Vector3 p = start;
        for (int guard = 0; akNodes[a].Contains(XZ(p)) && guard < 400; guard++) p += dir * 0.25f;
        AkOpen(a, p, 3f);
    }

    // a speed rail: ride into its start (it grabs you from a metre and a half) and it rushes you along it, up
    // or down, and throws you off the end
    static void AkSkyRail(int a, int b, Vector3[] ctrl, float speed = 14f)
    {
        AkNode A = akNodes[a];
        var pts = Spline(ctrl, 5);
        Rail("Speed rail", pts, false);
        RefreshRails();
        var rail = akRails[akRails.Length - 1];
        rail.catchRadius = 1.6f; rail.forceSpeed = speed;
        Color32 tube = new Color32(60, 240, 255, 255);
        AK("white", "e").Tube(pts, 0.11f, 6, 2f, tube);
        // glowing chevrons along it, pointing the way it goes
        float acc = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            if (acc < 3f) continue;
            acc = 0f;
            Vector3 d = (pts[i] - pts[i - 1]).normalized;
            AK("white", "e").Box(pts[i] + Vector3.down * 0.22f, new Vector3(0.7f, 0.06f, 0.18f), Quaternion.LookRotation(d), 1f, 1f, new Color32(255, 70, 190, 255));
        }
        // is it clear where you ride (a body's height over it)? (not counting where it starts and ends)
        float len = 0f;
        for (int i = 1; i < pts.Length; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
        float along = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            along += seg;
            if (along < 3f || along > len - 2.5f) continue;
            Vector3 p0 = pts[i - 1] + Vector3.up * 0.75f, p1 = pts[i] + Vector3.up * 0.75f;
            if (Physics.SphereCast(p0, 0.38f, (p1 - p0).normalized, out RaycastHit hit, seg, ~0, QueryTriggerInteraction.Ignore))
                Debug.Log("AKIBA_SKY_RAIL_BLOCKED " + A.name + " -> " + akNodes[b].name + " by " + hit.collider.name + " at " + hit.point.ToString("F1"));
        }
        Vector3 d0 = FlatDir(ctrl[1] - ctrl[0]);
        Vector3 foot = new Vector3(ctrl[0].x, A.top, ctrl[0].z);
        var l = AkAddLink("speed rail", a, b);
        l.start = foot - d0 * Mathf.Min(5f, Mathf.Max(2.5f, RunRoom(A, foot, d0))); l.yaw = YawOf(d0); l.timeout = 20f;
        AkOpenCrossings(a, pts, 3f);
        AkOpenCrossings(b, pts, 3f);
        Landing(new Vector3(ctrl[ctrl.Length - 1].x, akNodes[b].top, ctrl[ctrl.Length - 1].z));
    }

    // a vent on a that blows you up to `over` above b's top, and you steer off onto it at `land` (once you're
    // `steer` above it: higher to clear a wall with a rail along it, which would grab you)
    static void AkSkyVent(int a, int b, Vector3 at, Vector3 land, float over = 3.2f, float steer = 1.2f)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        Vent(at, B.top + over - A.top, 1.6f);
        var l = AkAddLink("vent", a, b);
        l.start = at; l.yaw = YawOf(land - at); l.forward = false;
        l.steerAbove = B.top + steer; l.steerTo = land;
        AkOpenCrossings(b, new[] { at, land }, 3f);
    }

    // ---- the giant jump pads ------------------------------------------------------------------------------

    // a giant jump pad at `pad` on a, throwing you onto b at `land`; tries higher arcs until one's clear
    static bool AkGiantPad(int a, int b, Vector3 pad, Vector3 land)
    {
        AkNode A = akNodes[a], B = akNodes[b];
        land.y = B.top;
        Vector3 toward = FlatDir(land - pad);
        Vector3 p0 = pad + Vector3.up * 0.45f, p1 = land + Vector3.up * 0.35f;
        foreach (var over in new[] { 4f, 7f, 10f, 14f, 19f, 25f })
        {
            if (!Ballistic(p0, p1, over, out var vel, out var path)) continue;
            if (!PathClear(path, 4f, 3f, 0.7f)) continue;
            // a run-up onto it from whichever side has room
            Vector3 start = Vector3.zero;
            bool found = false;
            foreach (var d in new[] { toward, Rt(toward), -Rt(toward), -toward })
            {
                Vector3 st = pad - d * 9f;
                st.y = A.kind == NGround ? floorAt(st.x, st.z) : A.top;
                if (A.kind == NGround ? !GroundOk(st) : !A.Contains(XZ(st), 0.8f)) continue;
                if (!RunClear(st, pad)) continue;
                start = st; found = true; break;
            }
            if (!found) { Debug.Log("AKIBA_SKY_PAD no run-up to the pad on " + A.name); return false; }
            GiantPadMesh(pad, vel, path);
            var l = AkAddLink("giant pad", a, b);
            l.start = start; l.yaw = YawOf(pad - start); l.letGo = true; l.timeout = 20f;
            AkOpenCrossings(a, path, 4f);
            AkOpenCrossings(b, path, 4f);
            akGiantPads++;
            return true;
        }
        Debug.Log("AKIBA_SKY_PAD every arc from " + A.name + " to " + B.name + " is blocked");
        return false;
    }

    // seven metres across, rings of light, big arrows, four beacons with beams of light going straight up,
    // and glowing balls all the way along the arc it throws you on
    static void GiantPadMesh(Vector3 pad, Vector3 vel, List<Vector3> path)
    {
        Vector3 dir = FlatDir(vel), side = Rt(dir);
        Color32 rim = new Color32(255, 70, 190, 255), glow = new Color32(255, 160, 230, 255), cyan = new Color32(60, 240, 255, 255), dark = new Color32(40, 20, 60, 255), gold = new Color32(255, 214, 40, 255);
        AK("metal").Cylinder(pad, pad + Vector3.up * 0.12f, 3.8f, 24, 2f, new Color32(60, 50, 80, 255));
        var k = AK("white", "e");
        // a lit disc: rings of pink and cyan on dark
        Ring(k, pad + Vector3.up * 0.12f, 0f, 3.8f, dark);
        Ring(k, pad + Vector3.up * 0.13f, 3.0f, 3.6f, rim);
        Ring(k, pad + Vector3.up * 0.13f, 2.0f, 2.5f, cyan);
        Ring(k, pad + Vector3.up * 0.13f, 0f, 0.8f, rim);
        for (int i = 0; i < 3; i++)
        {
            Vector3 c = pad + Vector3.up * 0.15f + dir * (i * 1.1f - 1.1f);
            foreach (var s in new[] { -1f, 1f })
                k.Box(c + side * s * 0.55f, new Vector3(0.36f, 0.04f, 1.5f), Quaternion.LookRotation(dir + side * s * 0.9f), 1f, 1f, glow);
        }
        for (int i = 0; i < 4; i++)
        {
            float ang = (i + 0.5f) * Mathf.PI / 2f;
            Vector3 p = pad + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 4.6f;
            AK("metal").Cylinder(p, p + Vector3.up * 3.2f, 0.16f, 6, 2f, new Color32(150, 150, 170, 255));
            k.Ellipsoid(p + Vector3.up * 3.45f, Vector3.one * 0.4f, 8, 5, gold);
            k.Box(p + Vector3.up * 18.4f, new Vector3(0.16f, 30f, 0.16f), Quaternion.identity, 1f, 1f, i % 2 == 0 ? rim : cyan);
        }
        // the arc: a glowing ball every few metres, all the way there
        float acc = 0f, total = 0f;
        for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        float run = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            acc += seg; run += seg;
            if (acc < 4.5f || run > total - 4f) continue;
            acc = 0f;
            k.Ellipsoid(path[i], Vector3.one * 0.32f, 6, 4, (int)(run / 4.5f) % 2 == 0 ? glow : cyan);
        }
        var col = AddBox(akCol, "Giant jump pad", pad + Vector3.up * 0.06f, new Vector3(4f, 0.12f, 4f), Quaternion.LookRotation(dir));
        var lp = col.gameObject.AddComponent<LaunchPad>();
        lp.velocity = vel; lp.target = path[path.Count - 1];
        // the base, solid, so you ride up onto it
        AkBox(pad + Vector3.up * 0.03f, new Vector3(7.2f, 0.06f, 7.2f));
    }

    // a flat ring (or with r0 = 0 a disc) lying at c, facing up
    static void Ring(PSXKit k, Vector3 c, float r0, float r1, Color32 col, int seg = 24)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            k.Quad(c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.up, col);
        }
    }

    // ---- flying cars ---------------------------------------------------------------------------------------

    static void AkSkyCars()
    {
        Color32[] cols = { new Color32(255, 230, 60, 255), new Color32(255, 80, 200, 255), new Color32(60, 230, 255, 255) };
        var spots = new[] { (new Vector3(20f, Kerb, -24f), 0f), (new Vector3(-20f, Kerb, 24f), 180f), (new Vector3(17f, 50f, 100f), 180f) };
        int i = 0;
        foreach (var (at, yaw) in spots)
        {
            var go = new GameObject("Flying car", typeof(Rigidbody), typeof(HoverCar), typeof(MeshCollider), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(akRoot, false);
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<Rigidbody>().isKinematic = true;
            var mesh = HoverMesh("Flying car", cols[i++ % cols.Length]);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { AkMat("white", "e"), outlineMat };
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            var hc = go.GetComponent<HoverCar>();
            hc.climb = 60f; hc.fuse = 5f;
            hc.seat = new Vector3(0f, 1.3f, 0f);
            hc.fire = Burst(go.transform, "Explosion", new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.4f), 1.2f, 3.5f, 14f, 26f, 0.7f, 120);
            hc.smoke = Burst(go.transform, "Smoke", new Color(0.25f, 0.2f, 0.3f, 0.8f), new Color(0.5f, 0.45f, 0.55f, 0.6f), 2.5f, 5f, 3f, 8f, 2.2f, 40);
        }
    }

    // ---- review shots ----------------------------------------------------------------------------------------

    public static void AkibaSkyShots()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        var psx = cam.GetComponent<PSXCamera>();
        if (psx != null) psx.Apply();
        foreach (var m in Object.FindObjectsByType<Mover>(FindObjectsSortMode.None)) { m.Init(); for (int i = 0; i < 30; i++) m.Step(0.05f); }
        Physics.SyncTransforms();
        Shot(cam, new Vector3(16f, 1.8f, -114f), new Vector3(18f, 6f, -80f), "s01-start-ramp.png");
        Shot(cam, new Vector3(-20f, 175f, -150f), new Vector3(0f, 60f, -10f), "s02-overview.png");
        Shot(cam, new Vector3(30f, 90f, 0f), new Vector3(-30f, 60f, 0f), "s03-side.png");
        Shot(cam, new Vector3(-9.2f, 14f, -95f), new Vector3(-4f, 40f, -30f), "s04-long-rail.png");
        Shot(cam, new Vector3(-30f, 3f, -34f), new Vector3(-16f, 30f, -10f), "s05-plaza-pad.png");
        Shot(cam, new Vector3(-6f, 64f, -24f), new Vector3(6f, 95f, -50f), "s06-sky-road.png");
        Shot(cam, new Vector3(-14f, 141f, -50f), new Vector3(0f, 135f, -74f), "s07-summit.png");
        Shot(cam, new Vector3(0f, 140f, -60f), new Vector3(0f, 110f, 60f), "s08-from-the-summit.png");
        Shot(cam, new Vector3(20f, 40f, 30f), new Vector3(0f, 60f, 80f), "s09-north.png");
        Shot(cam, new Vector3(-2f, 2f, -40f), new Vector3(-6f, 6f, -10f), "s10-tall-bus.png");
    }
}
