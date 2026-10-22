using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// What the two levels after real games (Elden Ruins, Surf Mesa) share: textured, sunlit materials (World/Lit) and
// light/air ones (World/Glow), meshes collected per material (and per patch of ground, so far-off ones get culled),
// boxes and quads textured in world space so neighbouring pieces line up, the sun, the sky and the camera.
public static partial class SkateWorldBuilder
{
    static readonly Dictionary<string, Material> rMats = new Dictionary<string, Material>();
    static readonly Dictionary<string, PSXKit> rKits = new Dictionary<string, PSXKit>();
    static readonly HashSet<string> rNoShadow = new HashSet<string>(), rNoReceive = new HashSet<string>();
    static string rTexDir, rMatDir;
    const float RChunk = 90f;

    static void RealBegin(string texDir, string matDir)
    {
        rTexDir = texDir; rMatDir = matDir;
        rMats.Clear(); rKits.Clear(); rNoShadow.Clear(); rNoReceive.Clear();
        Directory.CreateDirectory(matDir);
        AssetDatabase.Refresh();
        // import settings: mipmapped, anisotropic; cut-out cards clamp and keep their coverage when far away
        foreach (var file in Directory.GetFiles(texDir, "*.png"))
        {
            string p = file.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            bool card = n.StartsWith("grass_b") || n.StartsWith("grass_g") || n.StartsWith("leaves") || n.StartsWith("erd") || n == "message" || n.StartsWith("glow") || n == "beam" || n == "banner";
            bool cut = n.StartsWith("grass_b") || n.StartsWith("grass_g") || n.StartsWith("leaves") || n.StartsWith("erd");
            var wrap = card ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti.wrapMode == wrap && ti.mipmapEnabled && ti.anisoLevel == 8 && ti.mipMapsPreserveCoverage == cut && ti.alphaIsTransparency == card) continue;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.filterMode = FilterMode.Trilinear;
            ti.alphaIsTransparency = card;
            ti.mipMapsPreserveCoverage = cut;
            ti.alphaTestReferenceValue = 0.45f;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }

    static Texture2D RTex(string tex) { return AssetDatabase.LoadAssetAtPath<Texture2D>(rTexDir + "/" + tex + ".png"); }

    static Material RMatAsset(string name, string shader)
    {
        string path = rMatDir + "/" + name + ".mat";
        var sh = Shader.Find(shader);
        if (sh == null) throw new System.Exception("Shader not found: " + shader);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        EditorUtility.SetDirty(m);
        return m;
    }

    // a sunlit textured material (World/Lit)
    static Material RLit(string name, string tex, Color? tint = null, float emission = 0f, float cutoff = 0f, float wind = 0f, float gloss = 0f,
                         float wrap = 0f, bool twoSided = false, bool shadows = true, Vector2? scroll = null, float fogAmount = 1f)
    {
        var m = RMatAsset(name, "3daigamesolo/World/Lit");
        m.SetTexture("_MainTex", RTex(tex));
        m.SetColor("_Color", tint ?? Color.white);
        m.SetFloat("_Emission", emission);
        m.SetFloat("_Cutoff", cutoff);
        m.SetFloat("_Wind", wind);
        m.SetFloat("_Gloss", gloss);
        m.SetFloat("_Wrap", wrap);
        m.SetFloat("_Cull", twoSided ? 0f : 2f);
        Vector2 s = scroll ?? Vector2.zero;
        m.SetVector("_Scroll", new Vector4(s.x, s.y, 0f, 0f));
        m.SetFloat("_FogAmount", fogAmount);
        m.renderQueue = cutoff > 0f ? 2450 : -1;
        rMats[name] = m;
        if (!shadows) rNoShadow.Add(name); else rNoShadow.Remove(name);
        if (!shadows && cutoff > 0f) rNoReceive.Add(name);   // (grass: cut-out cards that cast no shadow pick up the wrong one from the screen)
        return m;
    }

    // light or air (World/Glow): additive, or laid over (mist)
    static Material RGlow(string name, string tex, Color col, bool additive = true, Vector4? scroll = null, float nearFade = 2f, Vector2? pulse = null, int queue = 3000)
    {
        var m = RMatAsset(name, "3daigamesolo/World/Glow");
        m.SetTexture("_MainTex", RTex(tex));
        m.SetColor("_Color", col);
        m.SetVector("_Scroll", scroll ?? Vector4.zero);
        m.SetFloat("_NearFade", nearFade);
        Vector2 p = pulse ?? Vector2.zero;
        m.SetVector("_Pulse", new Vector4(p.x, p.y, 0f, 0f));
        m.SetFloat("_Src", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_Dst", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.renderQueue = queue;
        rMats[name] = m;
        rNoShadow.Add(name);
        return m;
    }

    // the mesh being collected for a material (`solid`: it gets a collider too); `at` puts it in a patch of
    // ground of its own, so patches out of view aren't drawn
    static PSXKit RK(string mat, bool solid = false, Vector3? at = null)
    {
        string chunk = at.HasValue ? Mathf.FloorToInt(at.Value.x / RChunk) + "_" + Mathf.FloorToInt(at.Value.z / RChunk) : "";
        string key = mat + "|" + (solid ? "s" : "") + "|" + chunk;
        if (!rKits.TryGetValue(key, out var k)) { k = new PSXKit(); rKits[key] = k; }
        return k;
    }

    // everything collected so far, made into objects under `parent`
    static List<GameObject> RFlush(Transform parent)
    {
        var made = new List<GameObject>();
        foreach (var kv in rKits)
        {
            if (kv.Value.Empty) continue;
            string[] parts = kv.Key.Split('|');
            var mesh = kv.Value.Build(parent.name + " " + parts[0] + " " + parts[2]);
            SaveFeatureMesh(mesh);
            var go = new GameObject(parts[0] + (parts[2].Length > 0 ? " " + parts[2] : ""), typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = rMats[parts[0]];
            mr.shadowCastingMode = rNoShadow.Contains(parts[0]) ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = !rNoReceive.Contains(parts[0]);
            if (parts[1] == "s") go.AddComponent<MeshCollider>().sharedMesh = mesh;
            made.Add(go);
        }
        rKits.Clear();
        return made;
    }

    // ---- shapes textured in world space -----------------------------------------------------------------

    static Vector2 WUV(Vector3 p, Vector3 n, float tile)
    {
        Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
        if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z) / tile;
        if (a.x >= a.z) return new Vector2(p.z * Mathf.Sign(n.x), p.y) / tile;
        return new Vector2(-p.x * Mathf.Sign(n.z), p.y) / tile;
    }

    // a quad a-b-c-d facing n, its texture laid on in world space (tile metres a repeat)
    static void WQuad(PSXKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float tile, Color32? col = null)
    {
        k.Quad(a, b, c, d, WUV(a, n, tile), WUV(b, n, tile), WUV(c, n, tile), WUV(d, n, tile), n, col);
    }

    // a box (centre, size, turned by rot), textured in world space; a collider too if `solid` is given
    static void WBox(PSXKit k, Vector3 c, Vector3 size, Quaternion rot, float tile, Color32? col = null, bool bottom = false, Transform solid = null)
    {
        Vector3 e = size * 0.5f;
        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
        foreach (var f in faces)
        {
            if (f == Vector3.down && !bottom) continue;
            Vector3 up = Mathf.Abs(f.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(f, up);
            Vector3 mid = Vector3.Scale(f, e);
            Vector3 ru = right * Mathf.Abs(Vector3.Dot(e, right)), uu = up * Mathf.Abs(Vector3.Dot(e, up));
            WQuad(k, c + rot * (mid - ru - uu), c + rot * (mid - ru + uu), c + rot * (mid + ru + uu), c + rot * (mid + ru - uu), rot * f, tile, col);
        }
        if (solid != null) AddBox(solid, "Solid", c, size, rot);
    }

    // a round post or trunk from a to b
    static void WCyl(PSXKit k, Vector3 a, Vector3 b, float ra, float rb, int sides, float tile, Color32? col = null)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 s1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 s2 = Vector3.Cross(axis, s1);
        float len = Vector3.Distance(a, b);
        float circ = 2f * Mathf.PI * Mathf.Max(ra, rb);
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = s1 * Mathf.Cos(a0) + s2 * Mathf.Sin(a0), d1 = s1 * Mathf.Cos(a1) + s2 * Mathf.Sin(a1);
            float u0 = circ * s / sides / tile, u1 = circ * (s + 1) / sides / tile;
            k.Quad(a + d0 * ra, b + d0 * rb, b + d1 * rb, a + d1 * ra, new Vector2(u0, 0f), new Vector2(u0, len / tile), new Vector2(u1, len / tile), new Vector2(u1, 0f), d0 + d1, col);
        }
    }

    // a card showing its whole texture, standing up from its bottom middle `at`, facing `face` (both sides drawn
    // by a two-sided material); vertex alpha 1 at the bottom and `swayTop` at the top, so the top sways in the wind
    static void Card(PSXKit k, Vector3 at, Vector3 face, float w, float h, Color32 col, float swayTop = 1f, float lean = 0f, Vector3? normal = null)
    {
        Vector3 right = Vector3.Cross(Vector3.up, face).normalized * (w * 0.5f);
        Vector3 up = (Vector3.up + face.normalized * lean).normalized * h;
        Color32 top = col; top.a = (byte)(255 * swayTop);
        Color32 bot = col; bot.a = 255;
        int i = k.v.Count;
        Vector3 n = normal ?? face.normalized;
        k.v.Add(at - right); k.v.Add(at - right + up); k.v.Add(at + right + up); k.v.Add(at + right);
        k.n.Add(n); k.n.Add(n); k.n.Add(n); k.n.Add(n);
        k.c.Add(bot); k.c.Add(top); k.c.Add(top); k.c.Add(bot);
        k.uv.Add(new Vector2(0f, 0f)); k.uv.Add(new Vector2(0f, 1f)); k.uv.Add(new Vector2(1f, 1f)); k.uv.Add(new Vector2(1f, 0f));
        k.t.Add(i); k.t.Add(i + 1); k.t.Add(i + 2); k.t.Add(i); k.t.Add(i + 2); k.t.Add(i + 3);
    }

    // a flat card centred on `c` with its own up and right (leaf clusters, glows)
    static void FlatCard(PSXKit k, Vector3 c, Vector3 right, Vector3 up, Color32 col, Vector3? normal = null)
    {
        int i = k.v.Count;
        Vector3 n = normal ?? Vector3.Cross(up, right).normalized;
        k.v.Add(c - right - up); k.v.Add(c - right + up); k.v.Add(c + right + up); k.v.Add(c + right - up);
        for (int j = 0; j < 4; j++) { k.n.Add(n); k.c.Add(col); }
        k.uv.Add(new Vector2(0f, 0f)); k.uv.Add(new Vector2(0f, 1f)); k.uv.Add(new Vector2(1f, 1f)); k.uv.Add(new Vector2(1f, 0f));
        k.t.Add(i); k.t.Add(i + 1); k.t.Add(i + 2); k.t.Add(i); k.t.Add(i + 2); k.t.Add(i + 3);
    }

    // ---- sun, sky, camera ---------------------------------------------------------------------------------

    static Light RealSun(Vector3 towardsSun, Color col, float intensity, float shadowStrength)
    {
        var go = new GameObject("Sun", typeof(Light));
        go.transform.rotation = Quaternion.LookRotation(-towardsSun.normalized);
        var l = go.GetComponent<Light>();
        l.type = LightType.Directional;
        l.color = col;
        l.intensity = intensity;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = shadowStrength;
        l.shadowBias = 0.05f;
        l.shadowNormalBias = 0.4f;
        RenderSettings.sun = l;
        return l;
    }

    static void RealSky(Material m, Vector3 centre, float radius)
    {
        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.name = "Sky";
        Object.DestroyImmediate(sky.GetComponent<Collider>());
        sky.transform.position = centre;
        sky.transform.localScale = Vector3.one * radius * 2f;
        var mr = sky.GetComponent<Renderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static GameObject RealCamera(System.Type level, float far, Color background)
    {
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(RealLook), level);
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = far;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.allowHDR = true;
        cam.allowMSAA = true;
        return camGO;
    }

    static void RealScene()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
    }
}
