using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Infinity: after a fly-through of a 3D fractal, and it goes on for ever. The level itself is built as you
// play by InfinityWorld (fractal towers all over the place joined by hard ways across, and under it the same
// again, layer under layer: fall off and you fall till you land on it lower down, a bit further back); this sets
// up the scene round it: the glowing, always-moving fractal material (SkateFractal.shader), the void, the
// warped, colour-cycling look over the whole frame (InfinityFX), you on the first tower facing the way on.
public static partial class SkateWorldBuilder
{
    public const string InfinityPath = "Assets/Scenes/Infinity.unity";

    public static void InfinityFromCommandLine()
    {
        try { BuildInfinity(); SlideTests.RunInfinity(); InfinityShots(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    public static void BuildInfinity()
    {
        Directory.CreateDirectory(Folder);
        featureCount = 0;
        featureFile = null;
        featureAsset = Folder + "/InfinityFeatures.asset";
        if (AssetDatabase.LoadMainAssetAtPath(featureAsset) != null) AssetDatabase.DeleteAsset(featureAsset);
        keepClear.Clear();
        floorAt = (x, z) => 0f;

        toonMat = Mat("InfinityFractal", "3daigamesolo/World/Fractal");
        outlineMat = Mat("InfinityOutline", "3daigamesolo/World/Outline");
        outlineMat.SetColor("_OutlineColor", Color.black);
        outlineMat.SetFloat("_OutlineWidth", 1.2f);
        outlineMat.SetColor("_Haze", Color.black); outlineMat.SetColor("_HazeHigh", Color.black);
        outlineMat.SetFloat("_FogDensity", 0.0065f); outlineMat.SetFloat("_FogHeight", 100000f); outlineMat.SetFloat("_FogMax", 1f);
        farOutlineMat = outlineMat;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;
        featureRoot = new GameObject("Skate features").transform;

        // the void, with a few far stars (it follows you down)
        var spaceMat = Mat("InfinitySpace", "3daigamesolo/World/Space");
        spaceMat.SetColor("_Deep", Color.black); spaceMat.SetColor("_Mid", new Color(0.01f, 0.01f, 0.02f));
        spaceMat.SetColor("_NebulaA", new Color(0.02f, 0.03f, 0.06f)); spaceMat.SetColor("_NebulaB", new Color(0.05f, 0.02f, 0.06f));
        spaceMat.SetFloat("_Stars", 0.25f); spaceMat.SetFloat("_BigStars", 0.1f);

        Vector3 on = new Vector3(428.1f, 0f, 612.3f).normalized;   // (InfinityWorld.Forward)
        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(-on * 3f + Vector3.up * 0.2f, Quaternion.LookRotation(on));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher), typeof(InfinityFX));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 4000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sky.name = "Void";
        Object.DestroyImmediate(sky.GetComponent<Collider>());
        sky.transform.SetParent(camGO.transform, false);
        sky.transform.localScale = Vector3.one * 3000f;
        sky.GetComponent<Renderer>().sharedMaterial = spaceMat;
        var player = BuildPlayer(start.transform, camGO.transform);
        player.fallLimit = -80f;
        camGO.GetComponent<SlideHUD>().player = player;
        camGO.GetComponent<SlideHUD>().levelName = "Infinity";
        camGO.transform.SetPositionAndRotation(-on * 5f + Vector3.up * 2.5f, Quaternion.LookRotation(on));
        player.board = BuildBoard(-on * 1.5f, Quaternion.LookRotation(on), false);   // (a hoverboard: no wheels)
        var fx = camGO.GetComponent<InfinityFX>();
        fx.material = Mat("InfinityPost", "3daigamesolo/Post/Infinity");
        fx.player = player;

        // the endless fractal, built as you go
        var world = new GameObject("Infinity", typeof(InfinityWorld)).GetComponent<InfinityWorld>();
        world.fractal = toonMat;
        world.player = player;

        EditorSceneManager.SaveScene(scene, InfinityPath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("SKATE_INFINITY_BUILT");
    }

    public static void InfinityShots()
    {
        var world = Object.FindFirstObjectByType<InfinityWorld>();
        var cam = Object.FindFirstObjectByType<Camera>();
        Vector3 on = world.Forward;
        world.GenerateAround(Vector3.zero, true);
        Shot(cam, -on * 5f + Vector3.up * 2.2f, on * 40f + Vector3.down * 2f, "i01-start.png");
        Shot(cam, new Vector3(-50f, 30f, -70f), new Vector3(30f, -20f, 30f), "i02-overview.png");
        Shot(cam, new Vector3(-20f, 14f, 50f), new Vector3(25f, -40f, 30f), "i03-the-layers-below.png");
        Shot(cam, new Vector3(4f, 2.2f, 2f), world.Centre(1, 0), "i04-a-way-across.png");
        Vector3 root, d; float len, ws;
        world.Petal(0, 0, 1, 1, out root, out d, out len, out ws);
        Vector3 tip = world.PetalAt(root, d, len, 1f);
        Shot(cam, tip - d * 2f + Vector3.up * 2.2f, tip + d * 14f + Vector3.down * 30f, "i05-looking-down-off-a-petal.png");
        Vector3 deep = world.ToWorld(world.Centre(20, 21), 60);
        world.GenerateAround(deep, true);
        Shot(cam, deep - on * 5f + Vector3.up * 3f, deep + on * 40f + Vector3.down * 6f, "i06-sixty-layers-down.png");
        world.GenerateAround(Vector3.zero, true);
    }
}
