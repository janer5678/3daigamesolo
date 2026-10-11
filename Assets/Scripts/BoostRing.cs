using UnityEngine;

// A glowing ring in the air: go through it and you're shot on the way it faces (its forward), at least
// this fast, flying clean (no air drag) till you land.
public class BoostRing : MonoBehaviour
{
    public float radius = 2.5f;
    public float speed = 26f;

    // are you going through it? (within the hole, and within half a metre of its plane)
    public bool Through(Vector3 p)
    {
        Vector3 l = transform.InverseTransformPoint(p);
        return Mathf.Abs(l.z) < 0.6f && new Vector2(l.x, l.y).magnitude < radius;
    }
}
