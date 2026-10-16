using UnityEngine;

// Put on the water drop prefab (root). The sprite must be on a CHILD object,
// because the root's rotation is frozen and this script rotates the visual.
// - Foam: the drop fades from blue to white as it speeds up.
// - Stretch: fast drops stretch along their direction of motion.
[RequireComponent(typeof(Rigidbody2D))]
public class WaterDropVisual : MonoBehaviour
{
    public SpriteRenderer visual;                          // the child sprite
    public Color calmColor = new Color(0.25f, 0.6f, 1f, 1f);
    public Color foamColor = new Color(0.9f, 0.97f, 1f, 1f);
    public float foamSpeed = 6f;                           // speed at which the drop is fully white
    public float maxStretch = 1.6f;                        // 1 = never stretch
    public float stretchSpeed = 8f;                        // speed at which stretch is at its max
    public float smoothing = 12f;                          // higher = colors react faster

    Rigidbody2D rb;
    Vector3 baseScale;
    float shownFoam;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        baseScale = visual.transform.localScale;
    }

    void LateUpdate()
    {
#if UNITY_6000_0_OR_NEWER
        Vector2 v = rb.linearVelocity;
#else
        Vector2 v = rb.velocity;
#endif
        float speed = v.magnitude;

        // Foam: ease in so slow drift stays blue and only real splashes go white.
        float targetFoam = Mathf.Clamp01(speed / foamSpeed);
        targetFoam *= targetFoam;
        shownFoam = Mathf.Lerp(shownFoam, targetFoam, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        visual.color = Color.Lerp(calmColor, foamColor, shownFoam);

        // Stretch: longer along the motion, a bit thinner across it.
        float stretch = Mathf.Lerp(1f, maxStretch, Mathf.Clamp01(speed / stretchSpeed));
        visual.transform.localScale = new Vector3(
            baseScale.x * stretch,
            baseScale.y / Mathf.Sqrt(stretch),
            baseScale.z);

        if (speed > 0.1f)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}