using UnityEngine;

public class BallOutOfBounds : MonoBehaviour
{
    [Header("Lose Message")]
    [TextArea]
    public string message = "The red ball fell out of bounds!\nPress Restart or Enter to try again";

    private bool triggered = false;
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (triggered || mainCam == null) return;

        // Convert world position to camera viewport coordinates (0 to 1 is on-screen)
        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);

        // Small buffer (-0.05 to 1.05) so it triggers cleanly once completely outside the screen
        if (viewportPos.x < -0.05f || viewportPos.x > 1.05f ||
            viewportPos.y < -0.05f || viewportPos.y > 1.05f)
        {
            triggered = true;

#if UNITY_2023_1_OR_NEWER
            LevelUI ui = FindAnyObjectByType<LevelUI>();
#else
            LevelUI ui = FindObjectOfType<LevelUI>();
#endif
            if (ui != null)
            {
                ui.ShowLose(message);
            }
        }
    }
}