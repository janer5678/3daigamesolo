using UnityEngine;
using UnityEngine.SceneManagement;

// Tab: on to the next level in the build, round to the first again after the last.
public class LevelSwitcher : MonoBehaviour
{
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;
        int count = SceneManager.sceneCountInBuildSettings;
        if (count < 2) return;
        SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex + 1) % count);
    }
}
