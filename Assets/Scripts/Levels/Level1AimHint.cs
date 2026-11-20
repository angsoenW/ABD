using UnityEngine;

// Level 1 only. Put it on the "Left Click, hold and drag!" text.
// The text is shown when the level starts. The moment the first bird is really launched
// (Slingshot.ShotsUsed goes up; clicking or dragging alone does not count) it fades out and stays hidden.
// Restarting the level reloads the scene, so the text is back for the next attempt.
// It only READS the Slingshot: launching, shots and everything else are untouched.
[RequireComponent(typeof(CanvasGroup))]
public class Level1AimHint : MonoBehaviour
{
    public Slingshot slingshot;                 // found automatically if left empty
    [Min(0.01f)] public float fadeDuration = 0.3f;

    CanvasGroup group;
    float fadeT = -1f;                          // < 0 = not fading yet
    bool done;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;           // a hint must never swallow clicks meant for the bird
        group.interactable = false;
    }

    void Start()
    {
        if (slingshot == null) slingshot = FindAnyObjectByType<Slingshot>();
    }

    void Update()
    {
        if (done) return;

        if (fadeT < 0f)
        {
            if (slingshot != null && slingshot.ShotsUsed > 0) fadeT = 0f;
            else return;
        }

        // unscaled so the fade still plays if something pauses the game
        fadeT += Time.unscaledDeltaTime / fadeDuration;
        group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeT));
        if (fadeT >= 1f)
        {
            done = true;
            gameObject.SetActive(false);
        }
    }
}
