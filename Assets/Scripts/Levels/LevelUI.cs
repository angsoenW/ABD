using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Put on the Canvas in each level scene.
// Shows "Water: 12/40" while playing and a win/lose message at the end.
// After the level ends: Enter = next level (if won) or retry (if lost).
// Panels and buttons are optional; the text alone is enough for testing.
public class LevelUI : MonoBehaviour
{
    public GoalReservoir goal;
    public TMP_Text progressText;       // e.g. "Water: 12/40"
    public TMP_Text resultText;         // big centered text for the win/lose message

    [Header("Optional panels")]
    public GameObject winPanel;
    public GameObject losePanel;
    public GameObject nextButton;       // hidden on the last level

    void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (resultText != null) resultText.text = "";

        // Hook up the goal automatically so you don't have to wire the events by hand.
        if (goal != null)
        {
            goal.onWin.AddListener(ShowWin);
            goal.onLose.AddListener(ShowLose);
        }
    }

    void Update()
    {
        if (progressText != null && goal != null)
            progressText.text = $"Water: {goal.WaterInside}/{goal.waterNeeded}";

        if (goal != null && goal.Finished && EnterPressed())
        {
            if (goal.Won) Next();
            else Restart();
        }
    }

    public void ShowWin()
    {
        if (resultText != null)
            resultText.text = LevelManager.HasNextLevel
                ? "Level Complete!\nPress Enter for the next level"
                : "You beat the last level!\nPress Enter for the menu";
        if (winPanel != null) winPanel.SetActive(true);
        if (nextButton != null) nextButton.SetActive(LevelManager.HasNextLevel);
    }

    public void ShowLose()
    {
        if (resultText != null) resultText.text = "Not enough water...\nPress Enter to try again";
        if (losePanel != null) losePanel.SetActive(true);
    }

    public void Next() => LevelManager.LoadNext();
    public void Restart() => LevelManager.Restart();
    public void Menu() => LevelManager.LoadMenu();

    bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}