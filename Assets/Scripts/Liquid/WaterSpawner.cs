using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A reservoir of water. Place it inside a pocket of terrain.
// - Fill On Start: fills the blue rectangle with drops when the level starts.
// - Drops Per Second > 0: also works as a faucet that keeps pouring.
// The Scene view shows how many drops this spawner makes, and (when selected) the level's total
// water compared with what the goal needs, so you can't build a level that is impossible to win.
public class WaterSpawner : MonoBehaviour
{
    public GameObject dropPrefab;
    public int maxDrops = 300;               // cap on ALL drops in the level (performance)

    [Header("Fill On Start (reservoir)")]
    public bool fillOnStart = true;
    public Vector2 areaSize = new Vector2(2f, 1f);
    [Min(0.01f)]
    public float spacing = 0.2f;             // at least one drop diameter, or drops spawn overlapping

    [Header("Faucet (optional)")]
    public float dropsPerSecond = 0f;        // 0 = off
    public Vector2 pourVelocity = Vector2.zero;

    [Header("Info (calculated - editing it does nothing)")]
    [Tooltip("How many drops Fill On Start creates with the current Area Size and Spacing.")]
    [SerializeField] int dropsThisSpawner;

    int Columns => Mathf.Max(1, Mathf.FloorToInt(areaSize.x / Mathf.Max(0.01f, spacing)));
    int Rows => Mathf.Max(1, Mathf.FloorToInt(areaSize.y / Mathf.Max(0.01f, spacing)));

    // Drops this spawner creates when the level starts.
    public int ExpectedDrops => fillOnStart ? Columns * Rows : 0;

    float timer;

    void OnValidate()
    {
        dropsThisSpawner = ExpectedDrops;
    }

    void Start()
    {
        if (!fillOnStart) return;

        int made = Fill();
        if (made < ExpectedDrops)
            Debug.LogWarning($"[WaterSpawner] {name} made only {made} of {ExpectedDrops} drops: " +
                             $"Max Drops ({maxDrops}) was reached. Raise Max Drops or make less water.");
    }

    void Update()
    {
        if (dropsPerSecond <= 0f) return;
        timer += Time.deltaTime;
        float interval = 1f / dropsPerSecond;
        while (timer >= interval)
        {
            timer -= interval;
            Spawn(transform.position, pourVelocity + Random.insideUnitCircle * 0.2f);
        }
    }

    // Returns how many drops were actually created.
    public int Fill()
    {
        int made = 0;
        Vector2 start = (Vector2)transform.position - areaSize * 0.5f + Vector2.one * spacing * 0.5f;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                Vector2 jitter = Random.insideUnitCircle * spacing * 0.1f;
                if (Spawn(start + new Vector2(x * spacing, y * spacing) + jitter, Vector2.zero)) made++;
            }
        }
        return made;
    }

    bool Spawn(Vector2 pos, Vector2 velocity)
    {
        if (dropPrefab == null || WaterDrop.Alive >= maxDrops) return false;
        GameObject d = Instantiate(dropPrefab, pos, Quaternion.identity);
        Rigidbody2D rb = d.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }
        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        if (fillOnStart) Gizmos.DrawWireCube(transform.position, areaSize);
        else Gizmos.DrawWireSphere(transform.position, 0.15f);

#if UNITY_EDITOR
        if (fillOnStart)
            Handles.Label(transform.position + Vector3.up * (areaSize.y * 0.5f + 0.35f), $"{ExpectedDrops} drops");
#endif
    }

#if UNITY_EDITOR
    // When selected: the whole level's water vs. what the goal needs.
    void OnDrawGizmosSelected()
    {
        int total = 0;
        foreach (var s in FindObjectsByType<WaterSpawner>(FindObjectsSortMode.None)) total += s.ExpectedDrops;

        var goal = FindAnyObjectByType<GoalReservoir>();
        if (goal == null) return;

        int needed = goal.waterNeeded;
        float percent = total > 0 ? 100f * needed / total : 999f;

        var style = new GUIStyle(EditorStyles.boldLabel);
        string verdict;
        if (needed > total) { style.normal.textColor = new Color(1f, 0.35f, 0.35f); verdict = "IMPOSSIBLE"; }
        else if (percent > 85f) { style.normal.textColor = new Color(1f, 0.7f, 0.2f); verdict = "very tight"; }
        else { style.normal.textColor = new Color(0.5f, 1f, 0.6f); verdict = "ok"; }

        Handles.Label(transform.position - Vector3.up * (areaSize.y * 0.5f + 0.25f),
                      $"Level water: {total} · Goal needs {needed} ({percent:0}%) - {verdict}", style);
    }
#endif
}