using UnityEngine;

// The Infinity level's look over the whole frame, after the fractal fly-through video: colour mapped onto a
// palette that keeps cycling (cyan and green, red, white, orange...), blacks crushed, everything crunchy and
// embossed, streaks pulling out from the middle like a zoom through a kaleidoscope, and torn, glitching lines
// now and then. The faster you go (and the faster you fall), the harder it pulls and the more it glitches.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class InfinityFX : MonoBehaviour
{
    public Material material;
    public SlideController player;
    [Tooltip("How much of the frame is mapped onto the cycling palette (0: the colours as they are).")]
    [Range(0f, 1f)] public float paletteMix = 0.7f;
    [Tooltip("Palette turns per second.")]
    public float cycle = 0.035f;
    [Range(0f, 3f)] public float emboss = 1.4f;
    [Tooltip("Zoom streaks standing still, and at full speed.")]
    public float streak = 0.015f, streakAtSpeed = 0.11f;
    [Tooltip("Colour fringing standing still, and at full speed.")]
    public float fringe = 0.003f, fringeAtSpeed = 0.012f;
    [Tooltip("Glitch lines standing still, and at full speed.")]
    public float glitch = 0.6f, glitchAtSpeed = 3f;
    [Tooltip("Speed (m/s) that counts as full speed.")]
    public float fullSpeed = 40f;

    float pull;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (material == null) { Graphics.Blit(src, dst); return; }
        float speed = player != null && Application.isPlaying ? player.Speed : 0f;
        pull = Mathf.MoveTowards(pull, Mathf.Clamp01(speed / fullSpeed), Time.unscaledDeltaTime * 2f);
        material.SetFloat("_Mix", paletteMix);
        material.SetFloat("_Cycle", cycle);
        material.SetFloat("_Emboss", emboss);
        material.SetFloat("_Streak", Mathf.Lerp(streak, streakAtSpeed, pull));
        material.SetFloat("_Fringe", Mathf.Lerp(fringe, fringeAtSpeed, pull));
        material.SetFloat("_Glitch", Mathf.Lerp(glitch, glitchAtSpeed, pull));
        Graphics.Blit(src, dst, material);
    }
}
