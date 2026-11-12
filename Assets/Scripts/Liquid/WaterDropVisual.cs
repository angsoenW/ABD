
using UnityEngine;

// Controls the visual appearance of water droplets.
// Attach to the water drop prefab.
// The visible sprite should be on a child object.

[RequireComponent(typeof(Rigidbody2D))]
public class WaterDropVisual : MonoBehaviour
{
    [Header("Visual Reference")]
    public SpriteRenderer visual;

    [Header("Water Appearance")]
    public Color waterColor = new Color(
        0.30f, 0.72f, 1f, 1f
    );

    [Header("Movement Appearance")]
    public bool stretchWhenMoving = true;

    [Range(1f, 2f)]
    public float maxStretch = 1.15f;

    public float stretchSpeed = 10f;
    public float smoothing = 12f;

    private Rigidbody2D rb;
    private Vector3 baseScale;
    private float currentStretch = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>();

        if (visual == null)
{
        Debug.LogError(
            "WaterDropVisual: Missing SpriteRenderer on " + gameObject.name,
            gameObject
        );

        enabled = false;
        return;
}
        baseScale = visual.transform.localScale;
    }

    void LateUpdate()
    {
        // Keep water color consistent.
        visual.color = waterColor;

        if (!stretchWhenMoving)
        {
            visual.transform.localScale = baseScale;
            return;
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 velocity = rb.linearVelocity;
#else
        Vector2 velocity = rb.velocity;
#endif

        float speed = velocity.magnitude;

        float targetStretch = Mathf.Lerp(
            1f,
            maxStretch,
            Mathf.Clamp01(speed / Mathf.Max(0.01f, stretchSpeed))
        );

        currentStretch = Mathf.Lerp(
            currentStretch,
            targetStretch,
            1f - Mathf.Exp(-smoothing * Time.deltaTime)
        );

        visual.transform.localScale = new Vector3(
            baseScale.x * currentStretch,
            baseScale.y / Mathf.Sqrt(currentStretch),
            baseScale.z
        );

        if (speed > 0.1f)
        {
            float angle = Mathf.Atan2(
                velocity.y,
                velocity.x
            ) * Mathf.Rad2Deg;

            visual.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle);
        }
    }
}
