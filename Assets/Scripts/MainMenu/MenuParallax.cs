using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// MainMenu only. A very small parallax: the sky drifts against the (static) terrain and the UI floats the
// other way, following the pointer. With no pointer (touch screens) it sways on its own.
// The terrain is never moved, so the water physics is not disturbed.
public class MenuParallax : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Transform target;
        public Vector2 strength = new Vector2(0.3f, 0.15f);   // world units (or pixels for UI) at full tilt
        [HideInInspector] public Vector3 home;
    }

    public Layer[] layers;
    public float smoothing = 4f;
    public float idleSway = 0.25f;           // 0-1: how much the automatic sway counts without a pointer

    Vector2 current;

    void Awake()
    {
        foreach (var l in layers) if (l.target != null) l.home = l.target.localPosition;
    }

    void Update()
    {
        Vector2 aim = Vector2.zero;
        bool havePointer = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 p = Mouse.current.position.ReadValue();
            if (Screen.width > 0 && Screen.height > 0 &&
                p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height)
            {
                aim = new Vector2(p.x / Screen.width, p.y / Screen.height) * 2f - Vector2.one;
                havePointer = true;
            }
        }
#endif
        if (!havePointer)
            aim = new Vector2(Mathf.Sin(Time.time * 0.23f), Mathf.Sin(Time.time * 0.17f + 1f)) * idleSway;

        current = Vector2.Lerp(current, aim, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));

        foreach (var l in layers)
        {
            if (l.target == null) continue;
            l.target.localPosition = l.home + new Vector3(current.x * l.strength.x, current.y * l.strength.y, 0f);
        }
    }
}
