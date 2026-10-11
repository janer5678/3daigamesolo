using UnityEngine;

// Marks a collider as a riding surface (the curved face of a kicker or quarterpipe). The board
// rides onto it through the ground probe, so the body's wall check ignores it - otherwise the
// steep top of a quarterpipe would count as a wall you slammed into.
public class SkateRamp : MonoBehaviour { }
