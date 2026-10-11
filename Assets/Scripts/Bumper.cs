using UnityEngine;

// A pinball bumper (a glossy orb, a giant app icon): touch it from any side and it throws you off it, hard,
// and a bit upward.
public class Bumper : MonoBehaviour
{
    [Tooltip("How hard it throws you away from it.")]
    public float strength = 20f;
    [Tooltip("At least this much upward speed when it throws you.")]
    public float lift = 10f;
}
