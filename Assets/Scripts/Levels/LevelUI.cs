using UnityEngine;
using TMPro;

// Put on the Canvas in each level scene.
// Hook the win panel's buttons to Next(), Restart() and Menu().
public class LevelUI : MonoBehaviour
{
    public GoalZone goal;
    public GameObject winPanel;
    public GameObject nextButton;       // hidden on the last level
    public TMP_Text progressText;       // optional, e.g. "Water: 12/30"

    void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }

    void Update()
    {
        if (progressText != null && goal != null)
            progressText.text = $"Water: {goal.Collected}/{goal.waterNeeded}";
    }

    public void ShowWin()
    {
        if (winPanel != null) winPanel.SetActive(true);
        if (nextButton != null) nextButton.SetActive(LevelManager.HasNextLevel);
    }

    public void Next() => LevelManager.LoadNext();
    public void Restart() => LevelManager.Restart();
    public void Menu() => LevelManager.LoadMenu();
}