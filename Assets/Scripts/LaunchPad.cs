using UnityEngine;

// A launch pad: ride onto it and it throws you along an arc that lands you on `target` (the glowing dots
// over it show the way). In the air you can still steer; let go and the arc takes you there.
public class LaunchPad : MonoBehaviour
{
    [Tooltip("The speed it throws you at, worked out by the level builder to land you on the target.")]
    public Vector3 velocity;
    public Vector3 target;
}
