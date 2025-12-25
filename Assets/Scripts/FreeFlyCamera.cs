using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Look")]
    public float mouseSensitivity = 2f;
    public bool lockCursor = true;

    [Header("Move")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 3f;
    public float climbSpeed = 5f; // up/down (E/Q)

    [Header("Smoothing (optional)")]
    public bool smooth = true;
    public float smoothTime = 0.08f;

    float yaw;
    float pitch;

    Vector3 currentVelocity;
    Vector3 targetMove;

    void OnEnable()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        ApplyCursorState(lockCursor);
    }

    void Update()
    {
        // Toggle cursor lock with Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            lockCursor = !lockCursor;
            ApplyCursorState(lockCursor);
        }

        if (lockCursor)
            Look();

        Move();
    }

    void Look()
    {
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        float up = 0f;
        if (Input.GetKey(KeyCode.E)) up += 1f;
        if (Input.GetKey(KeyCode.Q)) up -= 1f;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        Vector3 move =
            (transform.right * h + transform.forward * v) * speed +
            (Vector3.up * up) * climbSpeed;

        if (smooth)
        {
            targetMove = move;
            Vector3 smoothed = Vector3.SmoothDamp(Vector3.zero, targetMove, ref currentVelocity, smoothTime);
            transform.position += smoothed * Time.deltaTime;
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }
    }

    void ApplyCursorState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
