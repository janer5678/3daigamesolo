using UnityEngine;

// Something in the Sketchbook level that gets washed with watercolour when you ride on it or grind it
// (SketchLevel does the painting): all of the renderers under it at once.
public class Paintable : MonoBehaviour
{
    [System.NonSerialized] public float paint;
    Renderer[] renderers;
    public Renderer[] Renderers { get { if (renderers == null) renderers = GetComponentsInChildren<Renderer>(); return renderers; } }
}
