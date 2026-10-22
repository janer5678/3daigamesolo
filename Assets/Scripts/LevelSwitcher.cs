using UnityEngine;
using UnityEngine.SceneManagement;

// Tab: on to the next level in the build, round to the first again after the last. Started with "-level <scene name>"
// (e.g. -level EldenRuins), the game goes straight to that level (for checking a build, with -autoshot).
public class LevelSwitcher : MonoBehaviour
{
    static bool checkedArgs;

    void Awake()
    {
        if (checkedArgs) return;
        checkedArgs = true;
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-level");
        if (i < 0 || i + 1 >= args.Length || SceneManager.GetActiveScene().name == args[i + 1]) return;
        SceneManager.LoadScene(args[i + 1]);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;
        if (LevelDrawing.Open || LevelSketch.Editing) return;   // (not while you're drawing or editing)
        int count = SceneManager.sceneCountInBuildSettings;
        if (count < 2) return;
        SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex + 1) % count);
    }
}
