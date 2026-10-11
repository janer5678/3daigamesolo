using UnityEngine;

// Reach it and it becomes where you're put back when you fall (or press R).
public class Checkpoint : MonoBehaviour
{
    public float radius = 4f;
    public string label = "Checkpoint";

    SlideController player;

    void Start() { player = Object.FindFirstObjectByType<SlideController>(); }

    void Update()
    {
        if (player == null || player.checkpoint == transform) return;
        if (Vector3.Distance(player.transform.position, transform.position) > radius) return;
        player.checkpoint = transform;
        SlideHUD.Flash(label);
    }
}
