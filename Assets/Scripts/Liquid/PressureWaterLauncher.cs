
using System.Collections;
using UnityEngine;
using TMPro;

// Controls the water launcher for Level 3.
// Each shot releases a fixed amount of water.
// Updates the existing shots counter.

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

    void Start()
    {
        if (aim == null || waterSpawner == null)
        {
            Debug.LogError(
                "PressureWaterLauncher: Assign Aim and WaterSpawner!"
            );

            enabled = false;
            return;
        }

        // Prevent water from spawning automatically.
        waterSpawner.fillOnStart = false;
        waterSpawner.dropsPerSecond = 0f;

        // Listen for the player's drag-and-release action.
        aim.OnShot += StartShot;

        UpdateShotsText();
    }

    void OnDestroy()
    {
        if (aim != null)
            aim.OnShot -= StartShot;
    }

    void StartShot(Vector2 velocity)
    {
        // No new shot while spraying or when out of shots.
        if (spraying || shotsUsed >= maxShots)
            return;

        shotsUsed++;
        UpdateShotsText();

        // Notify other systems that this shot is valid.
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

        // Approximate number of drops per shot.
        float duration = dropsPerShot /
                         Mathf.Max(1f, dropsPerSecond);

        yield return new WaitForSeconds(duration);

        // Stop spraying after the burst.
        waterSpawner.dropsPerSecond = 0f;

        spraying = false;
        sprayCoroutine = null;

        Debug.Log(
            $"Shot complete! {shotsUsed}/{maxShots} used."
        );
    }

    void UpdateShotsText()
    {
        if (shotsText == null)
            return;

        int remaining = Mathf.Max(
            0,
            maxShots - shotsUsed
        );

        shotsText.text =
            $"Shots: {remaining}/{maxShots}";
    }

    void OnDisable()
    {
        if (aim != null)
            aim.OnShot -= StartShot;

        if (sprayCoroutine != null)
        {
            StopCoroutine(sprayCoroutine);
            sprayCoroutine = null;
        }

        spraying = false;

        if (waterSpawner != null)
            waterSpawner.dropsPerSecond = 0f;
    }
}
