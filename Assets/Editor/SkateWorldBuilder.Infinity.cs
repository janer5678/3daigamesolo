using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Infinity: after a fly-through of a 3D fractal, and it goes on for ever. The level itself is built as you
// play by InfinityWorld (an endless lattice of fractal towers, layer under layer, bridges from every tower's
// corners down to the towers diagonally below); this sets up the scene round it: the glowing, always-moving
// fractal material (SkateFractal.shader), the void, you on the first tower.
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

        var start = new GameObject("Start");
        start.transform.SetPositionAndRotation(new Vector3(-4f, 0.2f, -4f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SlideHUD), typeof(SkateAutoShot), typeof(LevelSwitcher));
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
        camGO.transform.SetPositionAndRotation(new Vector3(-6f, 2.5f, -6f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
        player.board = BuildBoard(new Vector3(-3f, 0f, -3f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));

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
        world.GenerateAround(Vector3.zero, true);
        Shot(cam, new Vector3(-6f, 2f, -6f), new Vector3(30f, -8f, 30f), "i01-start.png");
        Shot(cam, new Vector3(-40f, 30f, -60f), new Vector3(30f, -20f, 30f), "i02-overview.png");
        Shot(cam, new Vector3(20f, -6f, 20f), new Vector3(60f, -30f, 60f), "i03-down-the-layers.png");
        Shot(cam, new Vector3(28f, -14f, 0f), new Vector3(28f, 10f, 28f), "i04-looking-up.png");
        Vector3 deep = world.Centre(4, -3, 60);
        world.GenerateAround(deep, true);
        Shot(cam, deep + new Vector3(-6f, 3f, -6f), deep + new Vector3(30f, -12f, 30f), "i05-sixty-layers-down.png");
        Shot(cam, deep + new Vector3(0f, -20f, 40f), deep + new Vector3(60f, -40f, -60f), "i06-into-the-dark.png");
        world.GenerateAround(Vector3.zero, true);
    }
}
