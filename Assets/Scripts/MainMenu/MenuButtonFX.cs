using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// MainMenu only. Hover / press / disabled feel for a menu button.
// Everything animates the child "Visual" (never the button root), so the clickable area never moves
// and the layout group is left alone. Uses unscaled time so it works whatever Time.timeScale is.
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    public RectTransform visual;                 // the part that animates
    public Graphic fill;                         // recoloured on hover / press
    public Color fillNormal = new Color(0.52f, 0.33f, 0.19f);
    public Color fillHover = new Color(0.62f, 0.41f, 0.24f);
    public Color fillPressed = new Color(0.40f, 0.25f, 0.14f);
    public Color fillDisabled = new Color(0.31f, 0.24f, 0.19f);

    [Header("Motion")]
    public float hoverScale = 1.07f;
    public float pressScale = 0.94f;
    public float hoverLift = 8f;                 // pixels up while hovered
    public float hoverTilt = 1.5f;               // degrees
    public float response = 14f;

    Button button;
    bool hover, down, selected;
    float scale = 1f, lift, tilt;
    Color color;
    Vector2 basePos;

    void Awake()
    {
        button = GetComponent<Button>();
        if (visual == null) visual = (RectTransform)transform;
        basePos = visual.anchoredPosition;
        if (fill != null) color = fill.color;
    }

    void OnEnable()
    {
        hover = down = selected = false;
        scale = 1f; lift = 0f; tilt = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;

    void Update()
    {
        bool live = button == null || button.interactable;
        bool lit = live && (hover || selected);

        float targetScale = !live ? 1f : down ? pressScale : lit ? hoverScale : 1f;
        float targetLift = lit && !down ? hoverLift : 0f;
        float targetTilt = lit && !down ? hoverTilt : 0f;

        float k = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
        scale = Mathf.Lerp(scale, targetScale, k);
        lift = Mathf.Lerp(lift, targetLift, k);
        tilt = Mathf.Lerp(tilt, targetTilt, k);

        if (fill != null)
        {
            Color c = !live ? fillDisabled : down ? fillPressed : lit ? fillHover : fillNormal;
            color = Color.Lerp(color, c, k);
            fill.color = color;
        }
        Apply();
    }

    void Apply()
    {
        if (visual == null) return;
        visual.localScale = new Vector3(scale, scale, 1f);
        visual.anchoredPosition = basePos + new Vector2(0f, lift);
        visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }
}
