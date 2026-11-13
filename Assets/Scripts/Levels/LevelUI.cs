
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Shared UI for every level.
// Handles water progress, shots, win/lose messages,
// navigation, and HUD button styling.

public class LevelUI : MonoBehaviour
{
    [Header("Gameplay (found automatically if left empty)")]
    public GoalReservoir goal;
    public Slingshot slingshot;

    [Header("Water counter")]
    public TMP_Text progressText;
    public RectTransform waterFill;
    public Image waterFillImage;

    public Color fillColor =
        new Color(0.30f, 0.70f, 1f, 1f);

    public Color fillDoneColor =
        new Color(1f, 0.82f, 0.36f, 1f);

    [Header("Shots counter")]
    public TMP_Text shotsText;

    [Header("Result")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public GameObject winPanel;
    public GameObject losePanel;
    public GameObject nextButton;

    [Header("Menu / Restart button style")]
    public bool styleHudButtons = true;
    public Sprite roundedSprite;

    public Color buttonNormal =
        new Color(0.10f, 0.13f, 0.21f, 0.88f);

    public Color buttonHover =
        new Color(0.22f, 0.30f, 0.45f, 0.95f);

    public Color buttonPressed =
        new Color(0.24f, 0.56f, 0.95f, 1f);

    const float Margin = 24f;
    const float ButtonWidth = 168f;
    const float ButtonHeight = 60f;
    const float Gap = 12f;

    int styledFrames;
    bool levelOver;
    bool levelWon;

    void Start()
    {
        if (goal == null)
            goal = FindFirst<GoalReservoir>();

        if (slingshot == null)
            slingshot = FindFirst<Slingshot>();

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);

        SetResult("");

        if (goal != null)
        {
            goal.onWin.AddListener(ShowWin);
            goal.onLose.AddListener(ShowLose);

            // NEW: Separate contamination losing condition.
            goal.onContaminationLose.AddListener(
                ShowContaminationLose
            );
        }
        else
        {
            Debug.LogWarning(
                "[LevelUI] No GoalReservoir found in this level."
            );
        }
    }

    void Update()
    {
        // Style HUD buttons during the first few frames.
        if (styleHudButtons && styledFrames < 3)
        {
            StyleHudButtons();
            styledFrames++;
        }

        UpdateWater();
        UpdateShots();

        // Continue after winning or losing.
        if (levelOver && ContinuePressed())
        {
            if (levelWon)
                Next();
            else
                Restart();
        }
    }

    // ---------------- Counters ----------------

    void UpdateWater()
    {
        if (goal == null)
            return;

        if (progressText != null)
        {
            progressText.text =
                $"Water: {goal.WaterInside}/{goal.waterNeeded}";
        }

        if (waterFill != null)
        {
            float t = goal.waterNeeded > 0
                ? Mathf.Clamp01(
                    (float)goal.WaterInside / goal.waterNeeded
                )
                : 0f;

            waterFill.anchorMax = new Vector2(t, 1f);

            if (waterFillImage != null)
            {
                waterFillImage.color =
                    t >= 1f ? fillDoneColor : fillColor;
            }
        }
    }

    void UpdateShots()
    {
        if (shotsText == null || slingshot == null)
            return;

        shotsText.text = slingshot.maxShots > 0
            ? $"Shots: {slingshot.ShotsUsed}/{slingshot.maxShots}"
            : $"Shots: {slingshot.ShotsUsed}";
    }

    // ---------------- Win / Lose ----------------

    public void ShowWin()
    {
        levelOver = true;
        levelWon = true;

        SetResult(
            LevelManager.HasNextLevel
                ? "Level Complete!\nPress Enter for the next level"
                : "You beat the last level!\nPress Enter for the menu"
        );

        if (winPanel != null)
            winPanel.SetActive(true);

        if (nextButton != null)
            nextButton.SetActive(LevelManager.HasNextLevel);
    }

    // Normal losing condition.
    public void ShowLose()
    {
        levelOver = true;
        levelWon = false;

        SetResult(
            "Not enough water...\nPress Enter to try again"
        );

        if (losePanel != null)
            losePanel.SetActive(true);
    }

    // NEW: Contamination losing condition.
    public void ShowContaminationLose()
    {
        levelOver = true;
        levelWon = false;

        SetResult(
            "You Lost!\nWater contaminated!\nPress Enter to try again"
        );

        if (losePanel != null)
            losePanel.SetActive(true);
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
        if (resultText != null)
            resultText.text = message;

        if (resultPanel != null)
        {
            resultPanel.SetActive(
                !string.IsNullOrEmpty(message)
            );
        }
    }

    // ---------------- Navigation ----------------

    public void Next()
    {
        LevelManager.LoadNext();
    }

    public void Restart()
    {
        LevelManager.Restart();
    }

    public void Menu()
    {
        LevelManager.LoadMenu();
    }

    // ---------------- HUD Button Styling ----------------

    void StyleHudButtons()
    {
        foreach (
            var canvas in FindObjectsByType<Canvas>(
                FindObjectsSortMode.None
            )
        )
        {
            if (canvas.name != "LevelHUD")
                continue;

            StyleButton(
                canvas.transform.Find("RestartButton"), 0
            );

            StyleButton(
                canvas.transform.Find("MenuButton"), 1
            );
        }
    }

    void StyleButton(Transform t, int slot)
    {
        if (t == null)
            return;

        var rt = (RectTransform)t;

        rt.sizeDelta =
            new Vector2(ButtonWidth, ButtonHeight);

        rt.anchoredPosition =
            new Vector2(
                -Margin - slot * (ButtonWidth + Gap),
                -Margin
            );

        var image = t.GetComponent<Image>();

        if (image != null)
        {
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            image.color = Color.white;
        }

        var button = t.GetComponent<Button>();

        if (button != null)
        {
            var colors = button.colors;

            colors.normalColor = buttonNormal;
            colors.highlightedColor = buttonHover;
            colors.pressedColor = buttonPressed;
            colors.selectedColor = buttonNormal;

            colors.disabledColor =
                new Color(0.3f, 0.3f, 0.3f, 0.5f);

            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;

            button.colors = colors;
        }

        if (t.GetComponent<Shadow>() == null)
        {
            var shadow =
                t.gameObject.AddComponent<Shadow>();

            shadow.effectColor =
                new Color(0f, 0f, 0f, 0.35f);

            shadow.effectDistance =
                new Vector2(0f, -3f);
        }

        var text = t.GetComponentInChildren<TMP_Text>();

        if (text != null)
        {
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4;

            text.color =
                new Color(0.93f, 0.96f, 1f, 1f);
        }
    }

    // ---------------- Input Helpers ----------------

    bool ContinuePressed()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;

        if (kb != null)
        {
            pressed |= kb.enterKey.wasPressedThisFrame
                    || kb.numpadEnterKey.wasPressedThisFrame
                    || kb.spaceKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
#endif

        if (pressed)
        {
            Debug.Log("[LevelUI] Continue pressed");
        }

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
