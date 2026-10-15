using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Attach to the projectile (needs Rigidbody2D + Collider2D + LineRenderer).
// Drag the projectile back from the anchor and release to launch.
// The projectile comes back to the slingshot automatically when it stops,
// leaves the screen, or flies too long. Press R to bring it back manually.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(LineRenderer))]
public class Slingshot : MonoBehaviour
{
    [Header("Setup")]
    public Transform anchor;            // empty GameObject where the slingshot is
    public float maxDragDistance = 2.5f;
    public float launchPower = 8f;

    [Header("Trajectory Preview")]
    public int previewPoints = 30;
    public float previewTimeStep = 0.05f;

    [Header("Auto Return")]
    public float stopSpeed = 0.2f;              // slower than this counts as "stopped"
    public float stoppedTimeToReturn = 1f;      // seconds stopped before it comes back
    public float maxFlightTime = 6f;            // comes back after this long no matter what

    [Header("Shots")]
    public int maxShots = 0;                    // 0 = unlimited
    public TMP_Text shotsText;                  // optional UI text, e.g. "Shots: 2/5"

    public int ShotsUsed { get; private set; }
    public bool OutOfShots => maxShots > 0 && ShotsUsed >= maxShots;

    Rigidbody2D rb;
    LineRenderer line;
    Camera cam;
    bool isDragging;
    bool launched;
    float flightTime;
    float stoppedTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        line = GetComponent<LineRenderer>();
        cam = Camera.main;

        line.useWorldSpace = true;
        line.positionCount = 0;
        line.startWidth = 0.08f;
        line.endWidth = 0.02f;
        if (line.sharedMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null) line.material = new Material(s);
        }
    }

    void Start()
    {
        ResetProjectile();
        UpdateShotsText();
    }

    void Update()
    {
        if (ResetPressed()) ResetProjectile();

        if (launched)
        {
            TrackFlight();
            return;
        }

        if (OutOfShots) return;

        Vector2 mouse = MouseWorld();

        if (MouseDown() && Vector2.Distance(mouse, rb.position) < 0.75f)
            isDragging = true;

        if (isDragging)
        {
            Vector2 offset = Vector2.ClampMagnitude(mouse - (Vector2)anchor.position, maxDragDistance);
            rb.position = (Vector2)anchor.position + offset;
            transform.position = rb.position;
            DrawTrajectory(rb.position, LaunchVelocity());

            if (MouseUp()) Launch();
        }
    }

    Vector2 LaunchVelocity()
    {
        return ((Vector2)anchor.position - rb.position) * launchPower;
    }

    void Launch()
    {
        isDragging = false;
        launched = true;
        flightTime = 0f;
        stoppedTime = 0f;
        line.positionCount = 0;

        ShotsUsed++;
        UpdateShotsText();

        rb.bodyType = RigidbodyType2D.Dynamic;
        SetVelocity(LaunchVelocity());
    }

    // Brings the projectile back once it has stopped, left the screen, or flown too long.
    void TrackFlight()
    {
        flightTime += Time.deltaTime;

        if (GetVelocity().magnitude < stopSpeed) stoppedTime += Time.deltaTime;
        else stoppedTime = 0f;

        if (stoppedTime >= stoppedTimeToReturn || flightTime >= maxFlightTime || IsOffScreen())
            ResetProjectile();
    }

    // Only checks left, right and bottom, so a high arc above the screen doesn't end the shot.
    bool IsOffScreen()
    {
        Vector3 vp = cam.WorldToViewportPoint(rb.position);
        return vp.x < -0.2f || vp.x > 1.2f || vp.y < -0.2f;
    }

    void ResetProjectile()
    {
        isDragging = false;
        launched = false;
        line.positionCount = 0;
        rb.bodyType = RigidbodyType2D.Kinematic;
        SetVelocity(Vector2.zero);
        rb.angularVelocity = 0f;
        rb.position = anchor.position;
        transform.position = anchor.position;
        transform.rotation = Quaternion.identity;
        UpdateShotsText();
    }

    void UpdateShotsText()
    {
        if (shotsText == null) return;

        if (maxShots > 0)
        {
            shotsText.text = OutOfShots && !launched
                ? $"Shots: {ShotsUsed}/{maxShots}  Out of shots!"
                : $"Shots: {ShotsUsed}/{maxShots}";
        }
        else
        {
            shotsText.text = $"Shots: {ShotsUsed}";
        }
    }

    void DrawTrajectory(Vector2 start, Vector2 velocity)
    {
        Vector2 g = Physics2D.gravity * rb.gravityScale;
        line.positionCount = previewPoints;
        for (int i = 0; i < previewPoints; i++)
        {
            float t = i * previewTimeStep;
            Vector2 p = start + velocity * t + 0.5f * g * t * t;
            line.SetPosition(i, p);
        }
    }

    // ---------- Velocity (works on Unity 6 and older) ----------
    Vector2 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    void SetVelocity(Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    // ---------- Input (works with both input systems) ----------
    Vector2 MouseWorld()
    {
#if ENABLE_INPUT_SYSTEM
        Vector3 screen = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        Vector3 screen = Input.mousePosition;
#endif
        screen.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(screen);
    }

    bool MouseDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool MouseUp()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    bool ResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}