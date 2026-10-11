using UnityEngine;

// Poses the little skater from what the board is doing. No animation clips - everything is a
// couple of rotations and a squash, so it reacts on the frame you press something.
public class SkaterRig : MonoBehaviour
{
    public SkateController skater;
    public Transform visual;      // the whole rider + board; tumbles when you bail
    public Transform board;       // the deck: flips, pitches into manuals
    public Transform body;        // stands sideways on the deck
    public Transform legs;        // squashes when crouching
    public Transform hips;        // everything above the legs
    public Transform head;
    public Transform armLeft, armRight;

    const float HipHeight = 0.74f;
    float pushPhase, boardLift, manualPitch, tumble;

    void LateUpdate()
    {
        if (skater == null) return;
        float dt = Time.deltaTime;
        bool air = skater.mode == SkateController.Mode.Air;
        bool grind = skater.mode == SkateController.Mode.Grind;
        bool bail = skater.mode == SkateController.Mode.Bail;

        // crouch: Space held, grabbing, or tucked in the air
        float crouch = Mathf.Max(skater.Crouch, air ? 0.45f : 0f);
        if (skater.Pushing) { pushPhase += dt * 9f; crouch = Mathf.Max(crouch, 0.25f + 0.2f * Mathf.Sin(pushPhase)); }
        float hipY = Mathf.Lerp(HipHeight, 0.44f, crouch);
        hips.localPosition = new Vector3(0f, hipY, 0f);
        legs.localScale = new Vector3(1f, hipY / HipHeight, 1f);

        // lean into turns (and into the balance on a rail)
        float lean = grind || skater.Manual ? skater.Balance * 25f : skater.Lean * Mathf.Clamp01(skater.Speed / 8f) * 16f;
        body.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-lean, 0f, 0f);

        // board: flips, the manual pitch, and popping up to the feet in the air
        boardLift = Mathf.MoveTowards(boardLift, air ? 0.12f : 0f, dt * 2f);
        float wantPitch = skater.Manual ? -14f + skater.Balance * 6f : 0f;
        manualPitch = Mathf.MoveTowards(manualPitch, wantPitch, dt * 120f);
        board.localPosition = new Vector3(0f, 0.09f + boardLift, 0f);
        board.localRotation = skater.BoardSpin * Quaternion.Euler(manualPitch, 0f, 0f);

        // look where you're going
        float look = skater.Fakie ? 75f : -75f;
        head.localRotation = Quaternion.Euler(0f, look, 0f);

        // arms: out for balance, down to the board for a grab
        float spread = grind || skater.Manual ? 70f + skater.Balance * 25f : air ? 45f : 18f;
        armLeft.localRotation = Quaternion.Euler(0f, 0f, -spread);
        armRight.localRotation = skater.Grabbing ? Quaternion.Euler(-35f, 0f, 10f) : Quaternion.Euler(0f, 0f, spread);

        // bail: over you go
        tumble = bail ? tumble + dt : 0f;
        visual.localRotation = bail ? Quaternion.Euler(tumble * 420f, 0f, tumble * 160f) : Quaternion.identity;
    }
}
