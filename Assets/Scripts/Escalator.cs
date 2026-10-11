using UnityEngine;

// A moving staircase, or a flat moving walkway: stand on it and it carries you along (you can still
// ride or run along it, faster). Goes on the collider you stand on.
public class Escalator : MonoBehaviour
{
    [Tooltip("Which way it carries you, in world space (up the slope, for an escalator).")]
    public Vector3 direction = Vector3.forward;
    public float speed = 5f;

    public Vector3 Velocity { get { return direction.normalized * speed; } }
}
