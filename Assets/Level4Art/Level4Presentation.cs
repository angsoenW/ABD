using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Level 4 only (lives in the Level_4 scene). Purely visual:
// - restyles the Menu/Restart buttons that LevelHUD creates at runtime,
// - drives the water progress bar under the water counter,
// - shows a backing panel behind the win/lose message.
// It never touches gameplay objects or their settings.
public class Level4Presentation : MonoBehaviour
{
    public GoalReservoir goal;
    public RectTransform waterFill;          // child of the bar; its width tracks the water level
    public Image waterFillImage;
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Buttons")]
    public Sprite roundedSprite;
    public Color buttonNormal = new Color(0.10f, 0.13f, 0.21f, 0.88f);
    public Color buttonHover = new Color(0.22f, 0.30f, 0.45f, 0.95f);
    public Color buttonPressed = new Color(0.24f, 0.56f, 0.95f, 1f);
    public Color fillColor = new Color(0.30f, 0.70f, 1f, 1f);
    public Color fillDoneColor = new Color(1f, 0.82f, 0.36f, 1f);

    const float Margin = 24f, ButtonWidth = 168f, ButtonHeight = 60f, Gap = 12f;
    int styledFrames;

    void Update()
    {
        // LevelHUD builds its buttons when the scene loads; style them over the first frames.
        if (styledFrames < 3) { StyleHudButtons(); styledFrames++; }

        if (goal != null && waterFill != null)
        {
            float t = goal.waterNeeded > 0 ? Mathf.Clamp01((float)goal.WaterInside / goal.waterNeeded) : 0f;
            waterFill.anchorMax = new Vector2(t, 1f);
            if (waterFillImage != null) waterFillImage.color = t >= 1f ? fillDoneColor : fillColor;
        }

        if (resultPanel != null && resultText != null)
        {
            bool show = !string.IsNullOrEmpty(resultText.text);
            if (resultPanel.activeSelf != show) resultPanel.SetActive(show);
        }
    }

    void StyleHudButtons()
    {
        foreach (var canvas in FindObjectsByType<Canvas>())
        {
            if (canvas.name != "LevelHUD") continue;
            Style(canvas.transform.Find("RestartButton"), 0);
            Style(canvas.transform.Find("MenuButton"), 1);
        }
    }

    void Style(Transform t, int slot)
    {
        if (t == null) return;
        var rt = (RectTransform)t;
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rt.anchoredPosition = new Vector2(-Margin - slot * (ButtonWidth + Gap), -Margin);

        var image = t.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;                       // the button's colour block does the tinting

        var button = t.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonNormal;
        colors.highlightedColor = buttonHover;
        colors.pressedColor = buttonPressed;
        colors.selectedColor = buttonNormal;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (t.GetComponent<Shadow>() == null)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        var text = t.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4;
            text.color = new Color(0.93f, 0.96f, 1f, 1f);
        }
    }
}
