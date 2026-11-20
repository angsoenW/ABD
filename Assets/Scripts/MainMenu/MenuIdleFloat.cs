using UnityEngine;

// MainMenu only. Slow sine float for a UI element or a world object (title, decorations).
// Works on RectTransform (anchored position, pixels) or a plain Transform (local position, world units).
public class MenuIdleFloat : MonoBehaviour
{
    public Vector2 amplitude = new Vector2(0f, 6f);
    public float rotation = 0.6f;              // degrees
    public float period = 4.5f;                // seconds per cycle
    public float phase;

    RectTransform rt;
    Vector2 home;
    float homeRot;

    void Awake()
    {
        rt = transform as RectTransform;
        home = rt != null ? rt.anchoredPosition : (Vector2)transform.localPosition;
        homeRot = transform.localEulerAngles.z;
    }

    void Update()
    {
        float a = (Time.unscaledTime / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f;
        Vector2 off = new Vector2(Mathf.Sin(a * 0.5f) * amplitude.x, Mathf.Sin(a) * amplitude.y);
        if (rt != null)
        {
            rt.anchoredPosition = home + off;
            rt.localRotation = Quaternion.Euler(0f, 0f, homeRot + Mathf.Sin(a * 0.5f) * rotation);
        }
        else
        {
            transform.localPosition = new Vector3(home.x + off.x, home.y + off.y, transform.localPosition.z);
            transform.localRotation = Quaternion.Euler(0f, 0f, homeRot + Mathf.Sin(a * 0.5f) * rotation);
        }
    }
}
