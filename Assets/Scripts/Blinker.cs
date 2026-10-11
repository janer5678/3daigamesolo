using UnityEngine;

// Cycles through its children, showing one at a time: traffic lights changing, neon signs flickering.
public class Blinker : MonoBehaviour
{
    [Tooltip("Seconds each child stays on.")]
    public float[] durations = { 1f };
    public float offset;

    Transform[] lamps;

    void Start()
    {
        lamps = new Transform[transform.childCount];
        for (int i = 0; i < lamps.Length; i++) lamps[i] = transform.GetChild(i);
    }

    void Update()
    {
        if (lamps == null || lamps.Length == 0) return;
        float cycle = 0f;
        for (int i = 0; i < lamps.Length; i++) cycle += durations[Mathf.Min(i, durations.Length - 1)];
        float t = Mathf.Repeat(Time.time + offset, cycle);
        int on = 0;
        for (; on < lamps.Length - 1; on++)
        {
            float d = durations[Mathf.Min(on, durations.Length - 1)];
            if (t < d) break;
            t -= d;
        }
        for (int i = 0; i < lamps.Length; i++) lamps[i].gameObject.SetActive(i == on);
    }
}
