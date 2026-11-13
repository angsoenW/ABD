
using System.Collections;
using UnityEngine;
using TMPro;

// Controls the pressurized water launcher.
// Each accepted shot releases a fixed burst of water.
// Stops shooting when the level is finished.

public class PressureWaterLauncher : MonoBehaviour
{
    [Header("References")]
    public WaterLauncherAim aim;
    public WaterSpawner waterSpawner;

    [Header("Water Settings")]
    public int maxShots = 4;
    public int dropsPerShot = 25;
    public float dropsPerSecond = 20f;

    [Header("UI")]
    public TMP_Text shotsText;

    public event System.Action<Vector2> OnAcceptedShot;

    private int shotsUsed = 0;
    private bool spraying = false;
    private Coroutine sprayCoroutine;

    // Reference to the level's goal.
    private GoalReservoir goal;

    public int ShotsUsed => shotsUsed;

    public bool OutOfShots =>
        maxShots > 0 && shotsUsed >= maxShots;

    public bool IsSpraying => spraying;

    void Start()
    {
        if (aim == null || waterSpawner == null)
        {
            Debug.LogError(
                "PressureWaterLauncher: Assign Aim and WaterSpawner!",
                this
            );

            enabled = false;
            return;
        }

        // Find the goal reservoir.
        goal = FindFirst<GoalReservoir>();

        // Prevent water from spawning automatically.
        waterSpawner.fillOnStart = false;
        waterSpawner.dropsPerSecond = 0f;

        // Find the existing UI counter.
        if (shotsText == null)
        {
            LevelUI levelUI = FindFirst<LevelUI>();

            if (levelUI != null)
            {
                shotsText = levelUI.shotsText;
            }
        }

        // Listen for shooting input.
        aim.OnShot += StartShot;

        UpdateShotsText();
    }

    void Update()
    {
        // Stop spraying immediately when the level ends.
        if (goal != null && goal.Finished && spraying)
        {
            StopSpraying();
        }
    }

    void LateUpdate()
    {
        // Override the Slingshot UI counter with
        // the correct PressureWaterLauncher count.
        UpdateShotsText();
    }

    void StartShot(Vector2 velocity)
    {
        // NEW: No shooting after winning or losing.
        if (goal != null && goal.Finished)
            return;

        // Prevent overlapping bursts.
        if (spraying)
            return;

        // Enforce the shot limit.
        if (OutOfShots)
            return;

        shotsUsed++;

        UpdateShotsText();

        // Notify erosion and other subscribed systems.
        OnAcceptedShot?.Invoke(velocity);

        sprayCoroutine = StartCoroutine(
            SprayBurst(velocity)
        );
    }

    IEnumerator SprayBurst(Vector2 velocity)
    {
        spraying = true;

        waterSpawner.pourVelocity = velocity;
        waterSpawner.dropsPerSecond = dropsPerSecond;

        // Preserve the existing burst duration.
        float duration =
            dropsPerShot / Mathf.Max(1f, dropsPerSecond);

        yield return new WaitForSeconds(duration);

        StopSpraying();

        Debug.Log(
            $"Shot complete! {shotsUsed}/{maxShots} used."
        );
    }

    void StopSpraying()
    {
        if (sprayCoroutine != null)
        {
            StopCoroutine(sprayCoroutine);
            sprayCoroutine = null;
        }

        spraying = false;

        if (waterSpawner != null)
        {
            waterSpawner.dropsPerSecond = 0f;
        }
    }

    void UpdateShotsText()
    {
        if (shotsText == null)
            return;

        shotsText.text = maxShots > 0
            ? $"Shots: {shotsUsed}/{maxShots}"
            : $"Shots: {shotsUsed}";
    }

    void OnDisable()
    {
        if (aim != null)
        {
            aim.OnShot -= StartShot;
        }

        StopSpraying();
    }

    void OnDestroy()
    {
        if (aim != null)
        {
            aim.OnShot -= StartShot;
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
