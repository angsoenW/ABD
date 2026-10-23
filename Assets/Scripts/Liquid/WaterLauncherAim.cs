
using UnityEngine;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Attach to a visible aiming handle.
// Drag backward to adjust angle and pressure.
// Release to tell PressureWaterLauncher to spray.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(LineRenderer))]
public class WaterLauncherAim : MonoBehaviour
{
    [Header("Setup")]
    public Transform anchor;
    public float maxDragDistance = 2.5f;
    public float launchPower = 8f;
    public float clickRadius = 0.75f;

    [Header("Trajectory Preview")]
    public int previewPoints = 30;
    public float previewTimeStep = 0.05f;

    [Header("Optional UI")]
    public TMP_Text shotsText;

    public event System.Action<Vector2> OnShot;

    Rigidbody2D rb;
    LineRenderer line;
    Camera cam;

    bool isDragging;
    int shotsUsed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        line = GetComponent<LineRenderer>();
        cam = Camera.main;

        rb.bodyType = RigidbodyType2D.Kinematic;

        line.useWorldSpace = true;
        line.positionCount = 0;
        line.startWidth = 0.08f;
        line.endWidth = 0.02f;

        if (line.sharedMaterial == null)
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            );

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
                line.material = new Material(shader);
        }
    }

    void Start()
    {
        if (anchor == null || cam == null)
        {
            Debug.LogError("WaterLauncherAim: Missing anchor or camera!");
            enabled = false;
            return;
        }

        ResetHandle();
        UpdateShotsText();
    }

    void Update()
    {
        Vector2 mouse = MouseWorld();

        if (MouseDown() &&
            Vector2.Distance(mouse, rb.position) <= clickRadius)
        {
            isDragging = true;
        }

        if (!isDragging) return;

        Vector2 offset = Vector2.ClampMagnitude(
            mouse - (Vector2)anchor.position,
            maxDragDistance
        );

        rb.position = (Vector2)anchor.position + offset;
        transform.position = rb.position;

        Vector2 velocity = LaunchVelocity();

        DrawTrajectory(rb.position, velocity);

        if (MouseUp())
        {
            isDragging = false;
            line.positionCount = 0;

            // Don't fire if the player barely dragged.
            if (offset.magnitude > 0.1f)
            {
                shotsUsed++;
                UpdateShotsText();

                OnShot?.Invoke(velocity);
            }

            ResetHandle();
        }
    }

    Vector2 LaunchVelocity()
    {
        return (
            (Vector2)anchor.position - rb.position
        ) * launchPower;
    }

    void DrawTrajectory(Vector2 start, Vector2 velocity)
    {
        // Use the same gravity as the water droplets.
        Vector2 gravity = Physics2D.gravity;

        line.positionCount = previewPoints;

        for (int i = 0; i < previewPoints; i++)
        {
            float t = i * previewTimeStep;

            Vector2 position =
                (Vector2)anchor.position +
                velocity * t +
                0.5f * gravity * t * t;

            line.SetPosition(i, position);
        }
    }

    void ResetHandle()
    {
        rb.position = anchor.position;
        transform.position = anchor.position;
        line.positionCount = 0;
    }

    void UpdateShotsText()
    {
        if (shotsText != null)
            shotsText.text = $"Shots: {shotsUsed}";
    }

    // Mouse controls (old and new Unity input systems)

    Vector2 MouseWorld()
    {
#if ENABLE_INPUT_SYSTEM
        Vector3 screen = Mouse.current != null
            ? (Vector3)Mouse.current.position.ReadValue()
            : Vector3.zero;
#else
        Vector3 screen = Input.mousePosition;
#endif

        screen.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(screen);
    }

    bool MouseDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
               Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool MouseUp()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
               Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }
}
