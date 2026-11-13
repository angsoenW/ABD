using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put on the Canvas in the MainMenu scene.
// Creates one button per level automatically, so adding a level = adding a scene.
// The Tutorial button is a normal button in the scene that calls PlayTutorial().
public class LevelSelectMenu : MonoBehaviour
{
    public Button buttonPrefab;          // a UI Button (TextMeshPro) saved as a prefab
    public Transform buttonContainer;    // an empty UI object with a Grid Layout Group
    public bool unlockAllForTesting = true;   // keep ON for playtests so testers can pick any level

    void Start()
    {
        int unlocked = unlockAllForTesting ? LevelManager.LevelCount : LevelManager.HighestUnlocked;

        for (int i = 1; i <= LevelManager.LevelCount; i++)
        {
            int level = i;   // copy for the click handler
            Button b = Instantiate(buttonPrefab, buttonContainer);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = level.ToString();
            b.interactable = level <= unlocked;
            b.onClick.AddListener(() => LevelManager.LoadLevel(level));
        }
    }

    // Hooked up to the "Tutorial" button in the MainMenu scene.
    public void PlayTutorial() => LevelManager.LoadTutorial();
}
