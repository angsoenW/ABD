using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The one UI script for every level. Put it on the level UI Canvas and save that Canvas as a prefab.
// - Water counter text + progress bar
// - Shots counter
// - Win/lose message with its backing panel; Enter = next level (won) or retry (lost)
// - Restyles the Menu/Restart buttons that LevelHUD creates at runtime
// Goal and Slingshot are found automatically, so the prefab works in any level without re-wiring.
public class LevelUI : MonoBehaviour
{
    [Header("Gameplay (found automatically if left empty)")]
    public GoalReservoir goal;
    public Slingshot slingshot;

    [Header("Water counter")]
    public TMP_Text progressText;                // "Water: 12/40"
    public RectTransform waterFill;              // child of the bar; its width tracks the water level
    public Image waterFillImage;
    public Color fillColor = new Color(0.30f, 0.70f, 1f, 1f);
    public Color fillDoneColor = new Color(1f, 0.82f, 0.36f, 1f);

    [Header("Shots counter")]
    public TMP_Text shotsText;                   // "Shots: 1/3"

    [Header("Result")]
    public GameObject resultPanel;               // backing panel behind the message
    public TMP_Text resultText;
    public GameObject winPanel;                  // optional
    public GameObject losePanel;                 // optional
    public GameObject nextButton;                // optional, hidden on the last level

    [Header("Menu / Restart button style")]
    public bool styleHudButtons = true;
    public Sprite roundedSprite;
    public Color buttonNormal = new Color(0.10f, 0.13f, 0.21f, 0.88f);
    public Color buttonHover = new Color(0.22f, 0.30f, 0.45f, 0.95f);
    public Color buttonPressed = new Color(0.24f, 0.56f, 0.95f, 1f);

    const float Margin = 24f, ButtonWidth = 168f, ButtonHeight = 60f, Gap = 12f;
    int styledFrames;
    bool levelOver;     // set when the win/lose message is shown
    bool levelWon;

    void Start()
    {
        if (goal == null) goal = FindFirst<GoalReservoir>();
        if (slingshot == null) slingshot = FindFirst<Slingshot>();

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        SetResult("");

        if (goal != null)
        {
            goal.onWin.AddListener(ShowWin);
            goal.onLose.AddListener(ShowLose);
        }
        else
        {
            Debug.LogWarning("[LevelUI] No GoalReservoir found in this level.");
        }
    }

    void Update()
    {
        // LevelHUD builds its buttons when the scene loads; style them over the first frames.
        if (styleHudButtons && styledFrames < 3) { StyleHudButtons(); styledFrames++; }

        UpdateWater();
        UpdateShots();

        // Uses the UI's own record of the result, so it works no matter which goal ended the level.
        if (levelOver && ContinuePressed())
        {
            if (levelWon) Next();
            else Restart();
        }
    }

    // ---------------- Counters ----------------

    void UpdateWater()
    {
        if (goal == null) return;

        if (progressText != null)
            progressText.text = $"Water: {goal.WaterInside}/{goal.waterNeeded}";

        if (waterFill != null)
        {
            float t = goal.waterNeeded > 0 ? Mathf.Clamp01((float)goal.WaterInside / goal.waterNeeded) : 0f;
            waterFill.anchorMax = new Vector2(t, 1f);
            if (waterFillImage != null) waterFillImage.color = t >= 1f ? fillDoneColor : fillColor;
        }
    }

    void UpdateShots()
    {
        if (shotsText == null || slingshot == null) return;

        shotsText.text = slingshot.maxShots > 0
            ? $"Shots: {slingshot.ShotsUsed}/{slingshot.maxShots}"
            : $"Shots: {slingshot.ShotsUsed}";
    }

    // ---------------- Win / lose ----------------

    public void ShowWin()
    {
        levelOver = true;
        levelWon = true;
        SetResult(LevelManager.HasNextLevel
            ? "Level Complete!\nPress Enter for the next level"
            : "You beat the last level!\nPress Enter for the menu");
        if (winPanel != null) winPanel.SetActive(true);
        if (nextButton != null) nextButton.SetActive(LevelManager.HasNextLevel);
    }

    public void ShowLose()
    {
        levelOver = true;
        levelWon = false;
        SetResult("Not enough water...\nPress Enter to try again");
        if (losePanel != null) losePanel.SetActive(true);
    }

    public void ShowLose(string customMessage)
    {
        if (levelOver) return; // Don't overwrite if the player already won
        levelOver = true;
        levelWon = false;
        SetResult(customMessage);
        if (losePanel != null) losePanel.SetActive(true);

        // Immediately start pulsing the Restart button in LevelHUD
        foreach (var pulse in FindObjectsByType<RestartButtonPulse>(FindObjectsSortMode.None))
        {
            pulse.StartPulsingNow();
        }
    }

    void SetResult(string message)
    {
        if (resultText != null) resultText.text = message;
        if (resultPanel != null) resultPanel.SetActive(!string.IsNullOrEmpty(message));
    }

    public void Next() => LevelManager.LoadNext();
    public void Restart() => LevelManager.Restart();
    public void Menu() => LevelManager.LoadMenu();

    // ---------------- LevelHUD button style ----------------

    void StyleHudButtons()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.name != "LevelHUD") continue;
            StyleButton(canvas.transform.Find("RestartButton"), 0);
            StyleButton(canvas.transform.Find("MenuButton"), 1);
        }
    }

    void StyleButton(Transform t, int slot)
    {
        if (t == null) return;
        var rt = (RectTransform)t;
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rt.anchoredPosition = new Vector2(-Margin - slot * (ButtonWidth + Gap), -Margin);

        var image = t.GetComponent<Image>();
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
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

    // ---------------- Helpers ----------------

    // Enter, keypad Enter or Space. Checks whichever input system(s) the project has enabled.
    bool ContinuePressed()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
            pressed |= kb.enterKey.wasPressedThisFrame
                    || kb.numpadEnterKey.wasPressedThisFrame
                    || kb.spaceKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
#endif
        if (pressed) Debug.Log("[LevelUI] Continue pressed");
        return pressed;
    }

    static T FindFirst<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<T>();
#else
        return FindObjectOfType<T>();
#endif
    }
}