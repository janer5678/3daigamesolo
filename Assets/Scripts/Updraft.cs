using UnityEngine;

// A column of air blowing up out of a vent: get into it and it carries you up, to the top of the column.
// Steer while you rise to come off it onto a balcony or a roof.
public class Updraft : MonoBehaviour
{
    public float radius = 2.5f;
    public float height = 20f;
    [Tooltip("How fast it carries you up.")]
    public float lift = 14f;

    // is p inside the column?
    public bool Contains(Vector3 p)
    {
        Vector3 b = transform.position;
        float dx = p.x - b.x, dz = p.z - b.z;
        return dx * dx + dz * dz < radius * radius && p.y > b.y - 1f && p.y < b.y + height;
    }

    public float Top { get { return transform.position.y + height; } }
}
