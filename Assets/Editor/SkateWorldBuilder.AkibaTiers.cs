using System.Collections.Generic;
using UnityEngine;

// Akiba's stacked city: decks out over the pavements at four heights (four storeys), up the building
// fronts on both sides of the main street. Stairs (with handrails to grind and a boost arrow at the foot)
// take you between storeys, gaps to jump between decks, quarter pipes where a run of decks ends, and
// glowing tube rails sweeping over the street from deck to deck and from the top decks up to the roofs.
// Ways up from the street everywhere: ramps and vents off the pavement, and a vent at the road's edge by
// every deck that blows you up to it.
public static partial class SkateWorldBuilder
{
    static readonly float[] Tiers = { 9f, 20f, 33f, 48f };
    const float DeckIn = Road + 0.6f;   // a deck's street edge (x = +-12.6); its back is against the buildings at +-Front
    const float TierThick = 1.2f;

    // the plan, made before the buildings so their signs and balconies keep out of the way
    static readonly List<(float side, float z0, float z1, float y)> akDecks = new List<(float, float, float, float)>();
    static readonly List<(float side, float z0, float z1, float ya, float yb)> akStairs = new List<(float, float, float, float, float)>();
    // ways up from the pavement: 0 a ramp, 1 a vent; z0..z1 is the stretch of pavement it uses, y the deck it reaches
    static readonly List<(float side, float z0, float z1, int kind, float y)> akEntries = new List<(float, float, float, int, float)>();
    static readonly List<(int first, int last)> akChains = new List<(int, int)>();   // runs of decks (indices into akDecks)

    static readonly Color32[] Neon = { new Color32(60, 240, 255, 255), new Color32(255, 70, 190, 255), new Color32(255, 214, 40, 255), new Color32(150, 255, 120, 255) };

    // is anything of the stacked city on this side, over this stretch, at these heights?
    static bool TierClash(float side, float z0, float z1, float y0, float y1)
    {
        foreach (var d in akDecks)
            if (d.side == side && z1 > d.z0 - 1f && z0 < d.z1 + 1f && y1 > d.y - TierThick - 1f && y0 < d.y + 3f) return true;
        foreach (var s in akStairs)
            if (s.side == side && z1 > s.z0 - 1f && z0 < s.z1 + 1f && y1 > Mathf.Min(s.ya, s.yb) - 2f && y0 < Mathf.Max(s.ya, s.yb) + 3f) return true;
        foreach (var e in akEntries)
            if (e.side == side && z1 > e.z0 && z0 < e.z1 && y0 < e.y + 1f) return true;
        return false;
    }

    // any of it on this side over this stretch, at any height
    static bool TierOver(float side, float z0, float z1) { return TierClash(side, z0, z1, -10f, 999f); }

    static void AkPlanTiers()
    {
        akDecks.Clear(); akStairs.Clear(); akEntries.Clear(); akChains.Clear();
        var rnd = new System.Random(4242);
        foreach (var side in new[] { -1f, 1f })
        {
            // where there's room: not by the railway, the start, the first skyscraper (the flying car climbs it),
            // or in front of the other skyscrapers (their spiral rails and vents)
            var blocked = new List<Vector2> { new Vector2(-RailHalf - 10f, RailHalf + 10f), new Vector2(-999f, -AveLen + 10f), new Vector2(AveLen - 15f, 999f) };
            blocked.Add(side > 0 ? new Vector2(-999f, -290f) : new Vector2(-330f, -186f));
            foreach (var lot in new[] { E2Lot, W3Lot, E4Lot, W5Lot, E6Lot })
            {
                if (Mathf.Sign(lot.center.x) != side) continue;
                float r = Mathf.Max(lot.width, lot.height) * 0.5f + 4f, dx = Mathf.Abs(lot.center.x) - (Road + 3f);
                float dz = Mathf.Sqrt(Mathf.Max(0f, r * r - dx * dx));
                blocked.Add(new Vector2(lot.center.y - dz - 8f, lot.center.y + dz + 8f));
            }
            blocked.Sort((p, q) => p.x.CompareTo(q.x));
            float at = -AveLen;
            foreach (var b in blocked)
            {
                if (b.x - at > 45f)
                {
                    // two layers over the same stretch: the lower storeys, and above them the upper ones
                    PlanChain(side, at + 1f, b.x - 1f, rnd, 0, 1, true);
                    PlanChain(side, at + 1f + (float)rnd.NextDouble() * 20f, b.x - 1f, rnd, 2, 3, false);
                }
                at = Mathf.Max(at, b.y);
            }
        }
    }

    static bool NearBridge(float z0, float z1, float m)
    {
        foreach (var bz in BridgeZ) if (z1 > bz - m && z0 < bz + m) return true;
        return false;
    }

    // one run of decks from z = a to b, stepping up and down the storeys
    static void PlanChain(float side, float a, float b, System.Random rnd, int lowest, int highest, bool fromStreet)
    {
        System.Func<float, float, float> R = (lo, hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        float z = a;
        int tier = lowest + (rnd.NextDouble() < 0.4 ? 1 : 0);
        // the way up at the start: a long ramp off the pavement (with a boost arrow), or a vent
        bool pavement = fromStreet && !Reserved(z + 14f, 16f);
        if (fromStreet) tier = 0;
        if (pavement && rnd.NextDouble() < 0.55)
        {
            akEntries.Add((side, z, z + 28f, 0, Tiers[0]));
            z += 28f;
        }
        else if (pavement)
        {
            tier = rnd.NextDouble() < 0.5 ? 0 : 1;
            akEntries.Add((side, z, z + 6f, 1, Tiers[tier]));
            z += 6f;
        }
        int first = akDecks.Count;
        while (b - z > 16f)
        {
            float len = Mathf.Min(R(20f, 36f), b - z);
            // the lowest decks keep clear of the footbridges (they're 6 m up): jump the gap over them
            if (tier == 0)
                foreach (var bz in BridgeZ)
                    if (z + len > bz - 4f && z < bz + 4f)
                    {
                        if (bz - 4f - z >= 14f) len = bz - 4f - z;
                        else { len = 0f; z = bz + 4f; }
                        break;
                    }
            if (len < 14f) { if (len == 0f) continue; break; }
            akDecks.Add((side, z, z + len, Tiers[tier]));
            z += len;
            if (b - z < 22f) break;
            double r = rnd.NextDouble();
            int next = r < 0.6 && tier < highest ? tier + 1 : r < 0.85 && tier > lowest ? tier - 1 : -1;
            if (next >= 0)
            {
                float ya = Tiers[tier], yb = Tiers[next], run = Mathf.Abs(yb - ya) * 1.6f;
                bool low = Mathf.Min(ya, yb) < 10f;
                if (z + run + 14f > b || (low && NearBridge(z, z + run, 4f))) { z += R(5f, 8f); continue; }
                akStairs.Add((side, z, z + run, ya, yb));
                z += run;
                tier = next;
            }
            else
            {
                // a gap to jump, sometimes dropping a storey
                z += R(5f, 9f);
                if (tier > lowest && rnd.NextDouble() < 0.5) tier--;
            }
        }
        if (akDecks.Count > first) akChains.Add((first, akDecks.Count - 1));
    }

    // ---- building it ------------------------------------------------------------------------------------

    static bool roadVentMarked;

    static void AkTiers()
    {
        roadVentMarked = false;
        foreach (var e in akEntries)
        {
            float s = e.side;
            if (e.kind == 0)
            {
                // a long ramp up off the pavement onto the first deck, with handrails and a boost arrow
                Vector3 foot = new Vector3(s * 16f, Kerb, e.z0 + 6f);
                Wedge("concrete", foot, Vector3.forward, e.z1 - e.z0 - 6f, e.y - Kerb, 4f);
                BoostArrow(new Vector3(s * 16f, Kerb, e.z0 + 2.2f), Vector3.forward, 27f);
                foreach (var o in new[] { -1.9f, 1.9f })
                    Rail("Ramp rail", new[] { foot + new Vector3(o, 1f, 0f), new Vector3(s * 16f + o, e.y + 1f, e.z1) });
            }
            else Vent(new Vector3(s * 16f, Kerb, e.z1 - 3.2f), e.y - Kerb + 3f, 2f);
        }
        for (int i = 0; i < akDecks.Count; i++) Deck(i);
        foreach (var s in akStairs) Stairs(s.side, s.z0, s.z1, s.ya, s.yb);
        Physics.SyncTransforms();
        foreach (var d in akDecks) Pillars(d.side, d.z0, d.z1, d.y - TierThick);
        foreach (var s in akStairs) Pillars(s.side, s.z0 + 2f, s.z1 - 2f, Mathf.Min(s.ya, s.yb) - 1f);

        // where a run of decks ends: a quarter pipe to turn round on; and a checkpoint on its highest deck
        int n = 0;
        foreach (var (first, last) in akChains)
        {
            var d = akDecks[last];
            float xc = d.side * (DeckIn + Front) / 2f;
            QuarterPipe("Deck quarter pipe", Matrix4x4.TRS(new Vector3(xc, d.y, d.z1 - 4.5f), Quaternion.identity, Vector3.one), Front - DeckIn - 0.4f, 3.2f, 1f);
            int top = first;
            for (int i = first; i <= last; i++) if (akDecks[i].y > akDecks[top].y) top = i;
            var t = akDecks[top];
            TowerCheckpoint(akRoot, new Vector3(t.side * (Front - 1.2f), t.y, (t.z0 + t.z1) / 2f), "Deck " + (++n));
        }
        AkTierLinks();
        // markers for the test runs
        foreach (var s in akStairs)
            if (s.yb > s.ya)
            {
                Marker("Stair test " + s.yb.ToString("F0"), new Vector3(s.side * (Front - 3.2f), s.ya + 0.6f, s.z0 - 12f));
                break;
            }
        var perTier = new int[4];
        foreach (var d in akDecks) perTier[System.Array.IndexOf(Tiers, d.y)]++;
        Debug.Log("AKIBA_TIERS per storey " + string.Join("/", perTier) + " decks=" + akDecks.Count + " stairs=" + akStairs.Count + " entries=" + akEntries.Count + " runs=" + akChains.Count);
    }

    static void Marker(string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(akRoot, false);
        go.transform.position = at;
    }

    static void Deck(int index)
    {
        var (s, z0, z1, y) = akDecks[index];
        float len = z1 - z0, zc = (z0 + z1) / 2f, w = Front - DeckIn;
        Vector3 c = new Vector3(s * (DeckIn + Front) / 2f, y - TierThick / 2f, zc);
        Vector3 size = new Vector3(w, TierThick, len);
        AK("roof").Box(c, size, Quaternion.identity, 4f, 4f, null, true, true);
        AkBox(c, size);
        Vector3 outward = new Vector3(-s, 0f, 0f);
        // an LED ticker along the street face, a neon strip along the edge, a railing to grind
        AK("led_" + AI(0, 3), "s").Panel(new Vector3(s * (DeckIn - 0.03f), y - TierThick / 2f, zc), Rt(outward) * (len / 2f), Vector3.up * (TierThick / 2f - 0.1f), null, false, new Rect(0f, 0f, len / 6.4f, 1f));
        Color32 neon = Neon[index % Neon.Length];
        AK("white", "e").Box(new Vector3(s * (DeckIn + 0.12f), y + 0.03f, zc), new Vector3(0.12f, 0.06f, len), Quaternion.identity, 1f, 1f, neon);
        AK("white", "e").Box(new Vector3(s * (DeckIn + 0.05f), y - TierThick - 0.04f, zc), new Vector3(0.1f, 0.08f, len), Quaternion.identity, 1f, 1f, neon);
        for (float z = z0 + 0.4f; z < z1; z += 2.5f)
            AK("metal").Box(new Vector3(s * (DeckIn + 0.25f), y + 0.5f, z), new Vector3(0.07f, 1f, 0.07f), Quaternion.identity, 1f, 1f);
        Rail("Deck railing", new[] { new Vector3(s * (DeckIn + 0.25f), y + 1.02f, z0 + 0.4f), new Vector3(s * (DeckIn + 0.25f), y + 1.02f, z1 - 0.4f) }, false);
        // a neon sign on a post at the edge
        if (ark.NextDouble() < 0.6)
        {
            float sz = AR(z0 + 4f, z1 - 4f), sh = AR(2.5f, 4f);
            Vector3 sc = new Vector3(s * (DeckIn + 0.4f), y + 1.4f + sh / 2f, sz);
            string tex = "sign_v_" + AI(0, 8), st = ark.NextDouble() < 0.35 ? "f" : "e";
            AK(tex, st).Panel(sc + Vector3.forward * 0.09f, Rt(Vector3.forward) * 0.6f, Vector3.up * sh / 2f);
            AK(tex, st).Panel(sc + Vector3.back * 0.09f, Rt(Vector3.back) * 0.6f, Vector3.up * sh / 2f);
            AK("metal").Box(sc, new Vector3(1.24f, sh + 0.12f, 0.1f), Quaternion.identity, 1f, 1f);
            AK("metal").Box(new Vector3(sc.x, y + 0.7f, sz), new Vector3(0.12f, 1.4f, 0.12f), Quaternion.identity, 1f, 1f);
        }
        // something to skate on the longer decks: a funbox or a ledge
        if (len > 32f)
        {
            float mz = AR(z0 + 8f, z1 - 16f);
            if (ark.NextDouble() < 0.5) Funbox("Deck funbox", Matrix4x4.TRS(new Vector3(s * 16.8f, y, mz), Quaternion.identity, Vector3.one), 3f);
            else
            {
                Vector3 lc = new Vector3(s * 17.6f, y + 0.3f, mz + 4f);
                AK("concrete").Box(lc, new Vector3(0.8f, 0.6f, 8f), Quaternion.identity, 2f, 2f);
                AkBox(lc, new Vector3(0.8f, 0.6f, 8f));
                Rail("Ledge", new[] { lc + new Vector3(-s * 0.4f, 0.32f, -3.8f), lc + new Vector3(-s * 0.4f, 0.32f, 3.8f) }, false);
            }
        }
        // and a vent at the road's edge beside it, to get up here from the street
        float vz = AR(z0 + 6f, z1 - 6f);
        if (!NearBridge(vz, vz, 5f))
        {
            Vent(new Vector3(s * (Road - 1.4f), 0f, vz), y + 3f, 1.7f);
            if (!roadVentMarked && y < 10f)
            {
                Marker("Road vent test " + y.ToString("F0"), new Vector3(s * (Road - 1.4f), 0.6f, vz));
                roadVentMarked = true;
            }
        }
    }

    // stairs between two storeys: steps to look at, a smooth slope to ride, handrails, a boost arrow at the foot
    static void Stairs(float s, float z0, float z1, float ya, float yb)
    {
        float x = s * (Front - 3.2f), w = 5.2f, rise = Mathf.Abs(yb - ya), run = z1 - z0;
        bool up = yb > ya;
        float zLow = up ? z0 : z1, yLow = Mathf.Min(ya, yb), dir = up ? 1f : -1f;
        int n = Mathf.CeilToInt(rise / 0.32f);
        var k = AK("concrete");
        for (int i = 0; i < n; i++)
        {
            float top = yLow + rise * (i + 1) / n;
            float za = zLow + dir * run * i / n, zb = zLow + dir * run * (i + 1) / n;
            k.Box(new Vector3(x, top - 0.4f, (za + zb) / 2f), new Vector3(w, 0.8f, Mathf.Abs(zb - za) + 0.02f), Quaternion.identity, 2f, 1f);
        }
        Vector3 lo = new Vector3(x, yLow, zLow), hi = new Vector3(x, yLow + rise, zLow + dir * run);
        float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(rise * rise + run * run);
        Quaternion q = Quaternion.LookRotation(new Vector3(0f, 0f, dir)) * Quaternion.Euler(-ang, 0f, 0f);
        AkBox((lo + hi) / 2f + q * Vector3.down * 0.1f, new Vector3(w, 0.3f, len), q);
        // the underside
        AK("concrete").Box((lo + hi) / 2f + q * Vector3.down * 0.7f, new Vector3(w, 0.4f, len), q, 2f, 4f, null, false, true);
        foreach (var o in new[] { -w / 2f + 0.15f, w / 2f - 0.15f })
        {
            Vector3 a = lo + new Vector3(o, 1f, 0f), b = hi + new Vector3(o, 1f, 0f);
            Rail("Stair rail", new[] { a, b }, false);
            for (int i = 0; i <= 4; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / 4f);
                AK("metal").Box(p + Vector3.down * 0.5f, new Vector3(0.07f, 1f, 0.07f), Quaternion.identity, 1f, 1f);
            }
        }
        BoostArrow(new Vector3(x, yLow, zLow - dir * 3.5f), new Vector3(0f, 0f, dir), 27f);
    }

    // posts under a deck or stairs, down to whatever's below
    static void Pillars(float s, float z0, float z1, float yBottom)
    {
        if (z1 - z0 < 2f) return;
        int count = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / 12f));
        for (int i = 0; i <= count; i++)
        {
            float z = Mathf.Lerp(z0 + 1f, z1 - 1f, i / (float)count);
            foreach (var px in new[] { s * (DeckIn + 0.3f), s * (Front - 0.35f) })
            {
                Vector3 top = new Vector3(px, yBottom - 0.05f, z);
                // against a building there's no need
                if (Mathf.Abs(px) > Front - 1f && Physics.Raycast(top + Vector3.down * 0.5f, new Vector3(s, 0f, 0f), 1.5f)) continue;
                RaycastHit h;
                float floor = Physics.Raycast(top, Vector3.down, out h, 80f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
                float ht = top.y - floor;
                if (ht < 0.5f) continue;
                if (floor > Kerb + 1f && Mathf.Abs(px) < Front - 1f)
                {
                    // something below (a lower deck): a brace back to the building instead of a post
                    Vector3 wall = new Vector3(s * Front, top.y - 4f, z), mid = (top + wall) / 2f;
                    AK("metal").Box(mid, new Vector3(0.3f, 0.3f, Vector3.Distance(top, wall)), Quaternion.LookRotation(wall - top), 1f, 2f, new Color32(150, 150, 170, 255));
                    continue;
                }
                Vector3 pc = new Vector3(px, floor + ht / 2f, z);
                AK("metal").Box(pc, new Vector3(0.5f, ht, 0.5f), Quaternion.identity, 1f, ht / 2f, new Color32(150, 150, 170, 255));
                AkBox(pc, new Vector3(0.5f, ht, 0.5f));
            }
        }
    }

    // a glowing tube rail: a bright core in a dark housing, grabs you from a couple of metres and rushes you along
    static void NeonRail(string name, Vector3[] ctrl, Color32 col)
    {
        CurvyRail(name, ctrl, col, false);
        var pts = Spline(ctrl, 10);
        var below = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++) below[i] = pts[i] + Vector3.down * 0.22f;
        AK("metal").Tube(below, 0.2f, 6, 3f, new Color32(50, 50, 70, 255));
        AK("white", "e").Tube(pts, 0.14f, 6, 2f, col);
    }

    // the tube rails: over the street from deck to deck, from one run of decks on to the next, and up from
    // the top decks onto the roofs
    // the start's flying car climbs the first skyscraper through here: no rails in its way
    static bool FlyingCarZone(float za, float zb) { return Mathf.Max(za, zb) > -405f && Mathf.Min(za, zb) < -180f; }

    static void AkTierLinks()
    {
        int links = 0, roofs = 0;
        System.Func<(float side, float z0, float z1, float y), (float side, float z0, float z1, float y), int, bool> link = (a, b, ci) =>
        {
            float za = Mathf.Clamp(b.z0 > a.z1 ? a.z1 - 6f : a.z0 + 6f, a.z0 + 4f, a.z1 - 4f);
            float dir = b.z0 > a.z1 ? 1f : -1f;
            float zb = Mathf.Clamp(dir > 0 ? b.z0 + 6f : b.z1 - 6f, b.z0 + 4f, b.z1 - 4f);
            if (Mathf.Abs(zb - za) < 20f || FlyingCarZone(za, zb)) return false;
            Vector3 p0 = new Vector3(a.side * 14.2f, a.y + 0.6f, za - dir * 4f), p1 = new Vector3(a.side * 14.2f, a.y + 0.6f, za);
            Vector3 q1 = new Vector3(b.side * 14.2f, b.y + 0.6f, zb), q0 = new Vector3(b.side * 14.2f, b.y + 0.6f, zb + dir * 5f);
            float mid = Mathf.Max(a.y, b.y) + AR(3f, 9f);
            float mx = a.side == b.side ? a.side * AR(4f, 8f) : AR(-5f, 5f);
            NeonRail("Neon link", new[] { p0, p1, new Vector3(a.side * 11.5f, a.y + 2f, za + dir * 8f), new Vector3(mx, mid, (za + zb) / 2f),
                                          new Vector3(b.side * 11.5f, b.y + 2.5f, zb - dir * 8f), q1, q0 }, Neon[ci % Neon.Length]);
            return true;
        };
        // over the street
        for (int i = 0; i < akDecks.Count; i++)
        {
            var a = akDecks[i];
            if (ark.NextDouble() < 0.25) continue;
            int best = -1; float bestScore = 1e9f;
            for (int j = 0; j < akDecks.Count; j++)
            {
                var b = akDecks[j];
                if (b.side == a.side) continue;
                float dz = Mathf.Abs((b.z0 + b.z1) / 2f - (a.z0 + a.z1) / 2f), dy = b.y - a.y;
                if (dz < 25f || dz > 120f || dy < -14f || dy > 14f) continue;
                float score = Mathf.Abs(dz - 60f) - dy * 1.5f + AR(0f, 30f);
                if (score < bestScore) { bestScore = score; best = j; }
            }
            if (best >= 0 && link(a, akDecks[best], links)) links++;
        }
        // up from the lower storeys to the upper ones on the same side
        foreach (var hiDeck in akDecks.ToArray())
        {
            if (hiDeck.y < 30f) continue;
            int best = -1; float bestScore = 1e9f;
            for (int j = 0; j < akDecks.Count; j++)
            {
                var lo = akDecks[j];
                float dz = Mathf.Abs((lo.z0 + lo.z1) / 2f - (hiDeck.z0 + hiDeck.z1) / 2f);
                if (lo.side != hiDeck.side || lo.y > 21f || dz < 30f || dz > 90f) continue;
                float score = Mathf.Abs(dz - 50f) + AR(0f, 20f);
                if (score < bestScore) { bestScore = score; best = j; }
            }
            if (best >= 0 && link(akDecks[best], hiDeck, links)) links++;
        }
        // from the end of one run of decks on to the start of the next one along the same side
        for (int c = 0; c < akChains.Count - 1; c++)
        {
            var a = akDecks[akChains[c].last];
            var b = akDecks[akChains[c + 1].first];
            if (a.side != b.side || b.z0 - a.z1 > 140f) continue;
            if (link(a, b, links)) links++;
        }
        // from the high decks up onto the tall roofs nearby
        foreach (var d in akDecks)
        {
            if (d.y < 30f) continue;
            float zc = (d.z0 + d.z1) / 2f;
            (Rect, float, float) target = default; float bestDz = 1e9f;
            foreach (var l in akLots)
            {
                if (Mathf.Sign(l.Item1.center.x) != d.side || l.Item2 < d.y + 10f || l.Item2 > 120f || l.Item1.height < 10f) continue;
                float dz = Mathf.Abs(l.Item1.center.y - zc);
                if (dz < 30f || dz > 110f || dz > bestDz) continue;
                bestDz = dz; target = l;
            }
            if (bestDz > 1e8f || FlyingCarZone(zc, target.Item1.center.y)) continue;
            Rect lot = target.Item1; float h = target.Item2;
            float dir = lot.center.y > zc ? 1f : -1f;
            float za = dir > 0 ? d.z1 - 6f : d.z0 + 6f, zl = dir > 0 ? lot.yMin + 2.5f : lot.yMax - 2.5f;
            float s = d.side;
            NeonRail("Neon roof link", new[] { new Vector3(s * 14.2f, d.y + 0.6f, za - dir * 4f), new Vector3(s * 14.2f, d.y + 0.6f, za),
                                               new Vector3(s * 10f, d.y + 4f, za + dir * 12f), new Vector3(s * 9f, (d.y + h) / 2f + 6f, (za + zl) / 2f),
                                               new Vector3(s * 15f, h + 4f, zl - dir * 8f), new Vector3(s * (Front + 4f), h + 0.8f, zl), new Vector3(s * (Front + 10f), h + 0.8f, zl) },
                     Neon[(roofs + 2) % Neon.Length]);
            roofs++;
        }
        Debug.Log("AKIBA_TIER_LINKS street/run links=" + links + " roof links=" + roofs);
    }
}
