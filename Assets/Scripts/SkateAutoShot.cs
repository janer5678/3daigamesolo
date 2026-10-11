using System.Collections;
using UnityEngine;

// Only does anything when the game is started with "-autoshot <folder>": takes a few
// screenshots of the real game running (coasting from the start) and quits. Used to check a
// build without anyone at the keyboard.
public class SkateAutoShot : MonoBehaviour
{
    IEnumerator Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-autoshot");
        if (i < 0 || i + 1 >= args.Length) yield break;
        string folder = args[i + 1];
        Application.runInBackground = true;
        System.IO.Directory.CreateDirectory(folder);
        float[] at = { 2f, 6f, 11f, 16f };
        float t0 = Time.time;
        for (int k = 0; k < at.Length; k++)
        {
            while (Time.time - t0 < at[k]) yield return null;
            // the last one looks down at the board
            var player = FindFirstObjectByType<SlideController>();
            if (k == at.Length - 1 && player != null) { player.pitch = 70f; yield return null; }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "play-" + k + ".png"));
        }
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }
}
