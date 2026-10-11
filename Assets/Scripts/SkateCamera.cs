using UnityEngine;

// Third person, behind the board. The mouse looks around; leave it alone for a moment and the
// camera swings back behind you, following where you're actually going (so it turns round with
// you after a fakie). Pulls back and widens a little with speed.
public class SkateCamera : MonoBehaviour
{
    public SkateController target;
    public float mouseSensitivity = 2.2f;
    public float distance = 5.2f;
    public float distanceAtSpeed = 6.6f;
    public float height = 1.5f;
    public float defaultPitch = 14f;
    public float recenterDelay = 0.7f;
    public float recenterRate = 4f;
    public float fov = 62f;
    public float fovAtSpeed = 74f;

    float yaw, pitch, idle;
    Vector3 focus;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        pitch = defaultPitch;
        if (target != null)
        {
            Vector3 f = target.transform.forward; f.y = 0f;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            focus = target.transform.position;
        }
        LockCursor(true);
    }

    static void LockCursor(bool on)
    {
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) LockCursor(true);

        float mx = Input.GetAxisRaw("Mouse X"), my = Input.GetAxisRaw("Mouse Y");
        if (Cursor.lockState != CursorLockMode.Locked) { mx = 0f; my = 0f; }
        if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.01f) idle = 0f; else idle += Time.deltaTime;
        yaw += mx * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - my * mouseSensitivity, -15f, 70f);

        Vector3 v = target.Velocity; v.y = 0f;
        bool steady = target.mode != SkateController.Mode.Bail && !target.VertAir;
        if (idle > recenterDelay && steady && v.magnitude > 1.5f)
        {
            float want = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, want, 1f - Mathf.Exp(-recenterRate * Time.deltaTime));
            pitch = Mathf.Lerp(pitch, defaultPitch, 1f - Mathf.Exp(-2f * Time.deltaTime));
        }

        // follow tightly sideways, a bit softer up and down so bumps don't shake the picture
        Vector3 p = target.transform.position;
        focus.x = p.x; focus.z = p.z;
        focus.y = Mathf.Lerp(focus.y, p.y, 1f - Mathf.Exp(-12f * Time.deltaTime));
        if (Mathf.Abs(focus.y - p.y) > 4f) focus.y = p.y;

        float speed01 = Mathf.Clamp01(target.Speed / 22f);
        float dist = Mathf.Lerp(distance, distanceAtSpeed, speed01);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 look = focus + Vector3.up * height;
        Vector3 want2 = look - rot * Vector3.forward * dist;

        RaycastHit hit;
        Vector3 dir = want2 - look;
        if (Physics.SphereCast(look, 0.3f, dir.normalized, out hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            want2 = look + dir.normalized * Mathf.Max(0.8f, hit.distance);

        transform.SetPositionAndRotation(want2, rot);
        if (cam != null) cam.fieldOfView = Mathf.Lerp(fov, fovAtSpeed, speed01);
    }
}
