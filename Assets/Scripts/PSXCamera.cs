using UnityEngine;

// The PlayStation look for a level: renders the frame at a low resolution and blows it back up with
// hard pixels, and sets the fog, moonlight, vertex wobble and texture warp the PSX shader reads.
[RequireComponent(typeof(Camera))]
public class PSXCamera : MonoBehaviour
{
    [Tooltip("Vertical resolution the frame is drawn at (PS1 games were about 240).")]
    public int height = 270;
    public Color fogColor = new Color(0.18f, 0.10f, 0.26f);
    public float fogStart = 60f, fogEnd = 420f;
    public Vector3 moonDirection = new Vector3(-0.4f, 0.8f, 0.3f);
    public float moonStrength = 0.45f;
    public Color ambient = new Color(0.42f, 0.38f, 0.55f);
    [Tooltip("The grid vertices snap to: lower is wobblier.")]
    public Vector2 snapGrid = new Vector2(320f, 180f);
    [Range(0f, 1f)] public float affine = 0.35f;

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    public void Apply()
    {
        Shader.SetGlobalColor("_PSXFogColor", fogColor);
        Shader.SetGlobalVector("_PSXFog", new Vector4(fogStart, fogEnd, 0f, 0f));
        Vector3 m = moonDirection.normalized;
        Shader.SetGlobalVector("_PSXSun", new Vector4(m.x, m.y, m.z, moonStrength));
        Shader.SetGlobalColor("_PSXAmbient", ambient);
        Shader.SetGlobalVector("_PSXSnap", new Vector4(snapGrid.x, snapGrid.y, affine, 0f));
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        int h = Mathf.Max(120, height);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)src.width / src.height));
        var low = RenderTexture.GetTemporary(w, h, 0, src.format);
        low.filterMode = FilterMode.Point;
        Graphics.Blit(src, low);
        Graphics.Blit(low, dst);
        RenderTexture.ReleaseTemporary(low);
    }
}
