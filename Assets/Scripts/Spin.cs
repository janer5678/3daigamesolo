using UnityEngine;

// Slowly turns and bobs a decoration (the floating hearts and stars round the pink palace).
public class Spin : MonoBehaviour
{
    public float degreesPerSecond = 20f;
    public float bob = 0.6f;
    public float bobSpeed = 0.7f;

    Vector3 home;
    float phase;

    void Start()
    {
        home = transform.position;
        phase = (home.x * 0.37f + home.z * 0.11f) % 6.28f;
    }

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        transform.position = home + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bob;
    }
}
