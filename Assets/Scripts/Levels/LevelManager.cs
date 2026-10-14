using UnityEngine;
using UnityEngine.SceneManagement;

// Scene order in File > Build Profiles > Scene List:
//   0 = MainMenu, 1 = Level 1, 2 = Level 2, ...
// Call these from anywhere: LevelManager.LoadNext(), LevelManager.Restart(), etc.
public static class LevelManager
{
    const string UnlockedKey = "UnlockedLevel";
    public const int MenuSceneIndex = 0;

    public static int LevelCount => SceneManager.sceneCountInBuildSettings - 1;
    public static int CurrentLevel => SceneManager.GetActiveScene().buildIndex;   // 0 on the menu
    public static bool HasNextLevel => CurrentLevel < LevelCount;

    // Highest level the player may open (level 1 is always open).
    public static int HighestUnlocked =>
        Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, LevelCount));

    public static void LoadLevel(int level)
    {
        if (level >= 1 && level <= LevelCount) SceneManager.LoadScene(level);
    }

    public static void LoadNext()
    {
        if (HasNextLevel) LoadLevel(CurrentLevel + 1);
        else LoadMenu();
    }

    public static void Restart() => SceneManager.LoadScene(CurrentLevel);
    public static void LoadMenu() => SceneManager.LoadScene(MenuSceneIndex);

    // Called when a level is beaten: unlocks the next one (saved in the browser too, for WebGL).
    public static void MarkCompleted(int level)
    {
        if (level + 1 > PlayerPrefs.GetInt(UnlockedKey, 1))
        {
            PlayerPrefs.SetInt(UnlockedKey, level + 1);
            PlayerPrefs.Save();
        }
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(UnlockedKey);
        PlayerPrefs.Save();
    }
}