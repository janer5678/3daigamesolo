using UnityEngine;

// A glowing arrow on the ground: roll over it and you're shot off in its direction, at least this fast.
public class BoostPad : MonoBehaviour
{
    public Vector3 direction = Vector3.forward;
    public float speed = 26f;
}
