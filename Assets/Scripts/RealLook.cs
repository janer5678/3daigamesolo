using UnityEngine;

// On the camera in the realistic levels (Elden Ruins, Surf Mesa): sets the haze and ambient colours the World/Lit
// shader reads, and draws the frame through Post/Real (bloom, tone curve, colour grade, vignette). Levels can change
// the haze as you move between areas (Surf Mesa's stages) by setting the fields.
[ExecuteAlways]   // (so the review shots get the look too)
public class RealLook : MonoBehaviour
{
    [Header("Haze")]
    public Color fog = new Color(0.7f, 0.72f, 0.7f);
    [Tooltip("The haze looking towards the sun.")]
    public Color fogSun = new Color(1f, 0.85f, 0.6f);
    public float fogDensity = 0.004f;
    public float fogStart = 10f;
    [Tooltip("Above this the haze thins out.")]
    public float fogHeight = 40f;
    [Range(0f, 1f)] public float fogMax = 0.95f;
    [Tooltip("How quickly the haze thins out above fogHeight (per metre).")]
    public float fogFalloff = 0.004f;

    [Header("Ambient light")]
    public Color sky = new Color(0.45f, 0.5f, 0.58f);
    public Color ground = new Color(0.3f, 0.28f, 0.22f);

    [Header("Frame")]
    public Material post;
    public float bloomThreshold = 1f;
    public float bloom = 0.5f;
    public float exposure = 1.1f;
    public float contrast = 1.05f;
    public float saturation = 1f;
    [Tooltip("Pushed into the shadows (alpha: how much).")]
    public Color shadowTint = new Color(0.45f, 0.5f, 0.55f, 0.3f);
    [Tooltip("Pushed into the highlights (alpha: how much).")]
    public Color highTint = new Color(0.55f, 0.5f, 0.42f, 0.3f);
    public float vignette = 0.35f;
    public float grain = 0.02f;

    void OnEnable() { Apply(); }
    void Update() { Apply(); }

    public void Apply()
    {
        Shader.SetGlobalColor("_LitFog", fog);
        Shader.SetGlobalColor("_LitFogSun", fogSun);
        Shader.SetGlobalFloat("_LitFogDensity", fogDensity);
        Shader.SetGlobalFloat("_LitFogStart", fogStart);
        Shader.SetGlobalFloat("_LitFogHeight", fogHeight);
        Shader.SetGlobalFloat("_LitFogMax", fogMax);
        Shader.SetGlobalFloat("_LitFogFalloff", fogFalloff);
        Shader.SetGlobalColor("_LitSky", sky);
        Shader.SetGlobalColor("_LitGround", ground);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Apply();
        if (post == null) { Graphics.Blit(src, dst); return; }
        post.SetFloat("_Threshold", bloomThreshold);
        post.SetFloat("_BloomAmount", bloom);
        post.SetFloat("_Exposure", exposure);
        post.SetFloat("_Contrast", contrast);
        post.SetFloat("_Saturation", saturation);
        post.SetColor("_ShadowTint", shadowTint);
        post.SetColor("_HighTint", highTint);
        post.SetFloat("_Vignette", vignette);
        post.SetFloat("_Grain", grain);

        // bloom: a chain of ever smaller blurred copies of the bright parts, added back up
        const int levels = 6;
        var chain = new RenderTexture[levels];
        int w = src.width / 2, h = src.height / 2;
        var fmt = RenderTextureFormat.DefaultHDR;
        chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
        Graphics.Blit(src, chain[0], post, 0);
        int n = 1;
        for (; n < levels; n++)
        {
            w /= 2; h /= 2;
            if (w < 4 || h < 4) break;
            chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(chain[n - 1], chain[n], post, 1);
        }
        for (int i = n - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], post, 2);
        post.SetTexture("_Bloom", chain[0]);
        Graphics.Blit(src, dst, post, 3);
        for (int i = 0; i < n; i++) RenderTexture.ReleaseTemporary(chain[i]);
    }
}
