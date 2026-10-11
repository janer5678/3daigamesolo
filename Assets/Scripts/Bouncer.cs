using UnityEngine;

// Something springy (a giant capsule toy, a stretched shop awning): land on it and it throws you up.
public class Bouncer : MonoBehaviour
{
    [Tooltip("How fast it sends you up.")]
    public float speed = 20f;
}
