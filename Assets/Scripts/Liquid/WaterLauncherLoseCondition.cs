
using UnityEngine;

// Level 4 only.
// Checks whether the player has used all water shots
// without collecting enough water in the reservoir.

public class WaterLauncherLoseCondition : MonoBehaviour
{
    [Header("References")]
    public PressureWaterLauncher launcher;
    public GoalReservoir goal;

    [Header("Lose Settings")]
    public float settleTime = 3f;

    private float settleTimer = 0f;
    private bool loseTriggered = false;

    void Update()
    {
        if (launcher == null || goal == null)
            return;

        if (goal.Finished || loseTriggered)
            return;

        // Don't lose while there are still shots available.
        if (!launcher.OutOfShots)
        {
            settleTimer = 0f;
            return;
        }

        // Wait until the last shot has finished spraying.
        if (launcher.IsSpraying)
        {
            settleTimer = 0f;
            return;
        }

        // Give the remaining water time to reach the reservoir.
        settleTimer += Time.deltaTime;

        if (settleTimer >= settleTime)
        {
            // The reservoir may have filled while we waited.
            if (goal.WaterInside >= goal.waterNeeded)
                return;

            loseTriggered = true;

            // Tell the existing UI to display the lose screen.
            goal.onLose.Invoke();
        }
    }
}
