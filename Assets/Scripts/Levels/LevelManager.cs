using UnityEngine;
using UnityEngine.SceneManagement;

// Scene order in File > Build Profiles > Scene List:
//   0 = MainMenu, 1 = Tutorial, 2 = Level 1, 3 = Level 2, ...
// "Level numbers" (what the player sees: 1, 2, 3...) are NOT build indices: Level N is build index N + 1.
// Call these from anywhere: LevelManager.LoadNext(), LevelManager.Restart(), etc.
public static class LevelManager
{
    const string UnlockedKey = "UnlockedLevel";
    public const int MenuSceneIndex = 0;
    public const int TutorialSceneIndex = 1;
    public const int FirstLevelSceneIndex = 2;    // build index of Level 1

    // Number of real levels (the menu and the tutorial are not counted).
    public static int LevelCount => Mathf.Max(0, SceneManager.sceneCountInBuildSettings - FirstLevelSceneIndex);

    // Level number of a build index: 1, 2, 3... for levels, 0 for the tutorial, -1 for the menu.
    public static int LevelNumberOf(int buildIndex) => buildIndex - FirstLevelSceneIndex + 1;
    public static int BuildIndexOf(int level) => level + FirstLevelSceneIndex - 1;

    static int ActiveIndex => SceneManager.GetActiveScene().buildIndex;
    public static int CurrentLevel => LevelNumberOf(ActiveIndex);          // 0 in the tutorial, -1 on the menu
    public static bool InTutorial => ActiveIndex == TutorialSceneIndex;
    public static bool HasNextLevel =>
        ActiveIndex >= TutorialSceneIndex && ActiveIndex + 1 < SceneManager.sceneCountInBuildSettings;

    // Highest level the player may open (level 1 is always open).
    public static int HighestUnlocked =>
        Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, LevelCount));

    public static void LoadLevel(int level)
    {
        if (level >= 1 && level <= LevelCount) SceneManager.LoadScene(BuildIndexOf(level));
    }

    public static void LoadTutorial() => SceneManager.LoadScene(TutorialSceneIndex);

    // Tutorial -> Level 1 -> Level 2 -> ... -> menu after the last level.
    public static void LoadNext()
    {
        if (HasNextLevel) SceneManager.LoadScene(ActiveIndex + 1);
        else LoadMenu();
    }

    public static void Restart() => SceneManager.LoadScene(ActiveIndex);
    public static void LoadMenu() => SceneManager.LoadScene(MenuSceneIndex);

    // Called when a level is beaten: unlocks the next one (saved in the browser too, for WebGL).
    // The tutorial (level 0) unlocks nothing; Level 1 is always open.
    public static void MarkCompleted(int level)
    {
        if (level < 1) return;
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
