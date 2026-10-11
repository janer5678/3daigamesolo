using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The combo: every trick done without touching the ground between (grinds, manuals and airs
// all count as not touching it) adds its points, and the whole lot is multiplied by how many
// tricks are in it. Repeating the same trick in one combo is worth less each time, so the big
// scores come from variety - that, and perfect landings (+1 multiplier each), is the skill
// ceiling. Plain C# so it can be tested without a scene.
public class SkateCombo
{
    public class Entry { public string name; public float points; public float factor; }

    public readonly List<Entry> tricks = new List<Entry>();
    readonly Dictionary<string, int> uses = new Dictionary<string, int>();

    public float basePoints;
    public int bonusMultiplier;
    public float totalScore;
    public float bestCombo;

    public bool Active { get { return tricks.Count > 0; } }
    public int Multiplier { get { return tricks.Count + bonusMultiplier; } }
    public float Value { get { return basePoints * Multiplier; } }

    // the last thing that happened to a combo, for the HUD
    public string lastResult = "";
    public float lastResultValue;
    public bool lastResultLost;
    public float lastResultTime = -99f;

    float Factor(string name)
    {
        int n;
        uses.TryGetValue(name, out n);
        return Mathf.Max(0.125f, Mathf.Pow(0.5f, n));
    }

    // a trick worth a fixed amount
    public Entry Add(string name, float points)
    {
        var e = new Entry { name = name, factor = Factor(name) };
        e.points = points * e.factor;
        uses[name] = (uses.ContainsKey(name) ? uses[name] : 0) + 1;
        tricks.Add(e);
        basePoints += e.points;
        return e;
    }

    // more points for a trick already in the combo (a grind or manual still going)
    public void Accrue(Entry e, float points)
    {
        if (e == null) return;
        float p = points * e.factor;
        e.points += p;
        basePoints += p;
    }

    // "Kickflip" then "Kickflip" again in the same air becomes one "Double Kickflip"
    public void Upgrade(Entry e, string newName, float extraPoints)
    {
        e.name = newName;
        Accrue(e, extraPoints);
    }

    public Entry Last { get { return tricks.Count > 0 ? tricks[tricks.Count - 1] : null; } }

    public void Bank(float time)
    {
        if (!Active) return;
        float v = Value;
        totalScore += v;
        if (v > bestCombo) bestCombo = v;
        lastResult = Describe(4);
        lastResultValue = v;
        lastResultLost = false;
        lastResultTime = time;
        Clear();
    }

    public void Lose(string why, float time)
    {
        if (!Active) return;
        lastResult = why;
        lastResultValue = Value;
        lastResultLost = true;
        lastResultTime = time;
        Clear();
    }

    public void Clear()
    {
        tricks.Clear();
        uses.Clear();
        basePoints = 0f;
        bonusMultiplier = 0;
    }

    // the newest few tricks, oldest first
    public string Describe(int max)
    {
        var sb = new StringBuilder();
        int start = Mathf.Max(0, tricks.Count - max);
        if (start > 0) sb.Append("... + ");
        for (int i = start; i < tricks.Count; i++)
        {
            if (i > start) sb.Append(" + ");
            sb.Append(tricks[i].name);
        }
        return sb.ToString();
    }
}
