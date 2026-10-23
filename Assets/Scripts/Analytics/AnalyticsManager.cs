using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Sends one row of data to a Google Form every time a level attempt ends.
// No scene setup needed: this creates itself when the game starts and works in every level.
//
// One row = one attempt. An attempt ends when the player:
//   - wins (Result = Win), loses (Result = Lose),
//   - restarts after taking at least one shot (Result = Restart),
//   - or goes to the menu after taking at least one shot (Result = Left).
//
// Covers Metric 1 (shots), 2 (attempts), 3 (water outcome) and 4 (first impact point).
public class AnalyticsManager : MonoBehaviour
{
    // ============ PASTE YOUR GOOGLE FORM SETTINGS HERE ============
    // Form URL must end in /formResponse (not /viewform).
    const string FormUrl = "https://docs.google.com/forms/d/e/1FAIpQLSf_98SobpV6c4qKAg_wG64KOsvSGsrFq4UiNw6V55rT0Jv1Dw/formResponse";
    const string EntrySession = "entry.487798508";
    const string EntryBuild = "entry.960618949";
    const string EntryLevel = "entry.617814834";
    const string EntryAttempt = "entry.122026111";
    const string EntryResult = "entry.615331701";
    const string EntryShots = "entry.179239322";
    const string EntryWaterGoal = "entry.1075054142";
    const string EntryWaterStuck = "entry.1837226746";
    const string EntryWaterLost = "entry.496046423";
    const string EntryWaterTotal = "entry.304472262";
    const string EntryHitX = "entry.194364735";
    const string EntryHitY = "entry.2092730314";
    const string EntrySeconds = "entry.31474410";

    const string BuildVersion = "alpha";   // change for each milestone so data stays separate
    const bool SendFromEditor = false;     // false = editor test runs only log, they don't pollute the data
    // ===============================================================

    static AnalyticsManager instance;
    static readonly string SessionId = System.Guid.NewGuid().ToString("N").Substring(0, 8);

    readonly Dictionary<int, int> attemptsPerLevel = new Dictionary<int, int>();

    struct Snapshot
    {
        public int shots, waterInGoal, waterAlive, waterFellOff;
        public bool hasImpact;
        public Vector2 impact;
        public float seconds;
    }

    int currentLevel = -1;
    bool reported;
    Snapshot snap;
    Slingshot slingshot;
    GoalReservoir goal;
    FirstImpactRecorder impactRecorder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("AnalyticsManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AnalyticsManager>();
    }

    void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The previous attempt ended without a win/lose (restart or menu). Report it if the player tried.
        if (currentLevel > 0 && !reported && snap.shots > 0)
            Report(currentLevel, scene.buildIndex == currentLevel ? "Restart" : "Left", snap);

        currentLevel = scene.buildIndex;
        reported = false;
        snap = default;
        slingshot = null;
        goal = null;
        impactRecorder = null;

        if (currentLevel <= LevelManager.MenuSceneIndex)
        {
            currentLevel = -1;   // menu or a scene not in the build list: nothing to track
            return;
        }

        slingshot = FindFirst<Slingshot>();
        goal = FindFirst<GoalReservoir>();

        if (slingshot != null)
        {
            impactRecorder = slingshot.GetComponent<FirstImpactRecorder>();
            if (impactRecorder == null) impactRecorder = slingshot.gameObject.AddComponent<FirstImpactRecorder>();
        }

        if (goal != null)
        {
            goal.onWin.AddListener(() => Finish("Win"));
            goal.onLose.AddListener(() => Finish("Lose"));
        }
    }

    void Update()
    {
        // Keep a fresh copy of the numbers, because the level objects are gone once the scene changes.
        if (currentLevel > 0 && !reported) TakeSnapshot();
    }

    void TakeSnapshot()
    {
        if (slingshot != null) snap.shots = slingshot.ShotsUsed;
        if (goal != null) snap.waterInGoal = goal.WaterInside;
        snap.waterAlive = WaterDrop.Alive;
        snap.waterFellOff = WaterDrop.FellOff;
        if (impactRecorder != null && impactRecorder.HasImpact)
        {
            snap.hasImpact = true;
            snap.impact = impactRecorder.Point;
        }
        snap.seconds = Time.timeSinceLevelLoad;
    }

    void Finish(string result)
    {
        if (reported) return;
        TakeSnapshot();
        reported = true;
        Report(currentLevel, result, snap);
    }

    void Report(int level, string result, Snapshot s)
    {
        attemptsPerLevel.TryGetValue(level, out int attempts);
        attempts++;
        attemptsPerLevel[level] = attempts;

        int stuck = Mathf.Max(0, s.waterAlive - s.waterInGoal);
        int total = s.waterAlive + s.waterFellOff;
        var inv = CultureInfo.InvariantCulture;

        var form = new WWWForm();
        form.AddField(EntrySession, SessionId);
        form.AddField(EntryBuild, BuildVersion);
        form.AddField(EntryLevel, level);
        form.AddField(EntryAttempt, attempts);
        form.AddField(EntryResult, result);
        form.AddField(EntryShots, s.shots);
        form.AddField(EntryWaterGoal, s.waterInGoal);
        form.AddField(EntryWaterStuck, stuck);
        form.AddField(EntryWaterLost, s.waterFellOff);
        form.AddField(EntryWaterTotal, total);
        form.AddField(EntryHitX, s.hasImpact ? s.impact.x.ToString("F2", inv) : "");
        form.AddField(EntryHitY, s.hasImpact ? s.impact.y.ToString("F2", inv) : "");
        form.AddField(EntrySeconds, s.seconds.ToString("F1", inv));

        string summary = $"Level {level}, attempt {attempts}, {result}, shots {s.shots}, " +
                         $"water goal/stuck/lost {s.waterInGoal}/{stuck}/{s.waterFellOff}";

        if (string.IsNullOrEmpty(FormUrl))
        {
            Debug.LogWarning("[Analytics] Form URL not set yet. Would send: " + summary);
            return;
        }
        if (Application.isEditor && !SendFromEditor)
        {
            Debug.Log("[Analytics] (editor, not sent) " + summary);
            return;
        }

        StartCoroutine(Post(form, summary));
    }

    IEnumerator Post(WWWForm form, string summary)
    {
        using (UnityWebRequest request = UnityWebRequest.Post(FormUrl, form))
        {
            yield return request.SendWebRequest();
            // In WebGL builds the browser blocks reading Google's reply (CORS), so Unity may report an
            // error here even though the row WAS saved. Check the Google Sheet to confirm.
            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[Analytics] Sent: " + summary);
            else
                Debug.Log("[Analytics] Sent (reply unreadable, usually fine in WebGL): " + summary);
        }
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