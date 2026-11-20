
using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class PinkWaterWarningText : MonoBehaviour
{
    [Header("Timing")]
    public float displayDuration = 3.5f;
    public float fadeDuration = 1.5f;

    private TMP_Text warningText;
    private Color originalColor;

    void Awake()
    {
        warningText = GetComponent<TMP_Text>();
        originalColor = warningText.color;
    }

    void OnEnable()
    {
        StartCoroutine(ShowWarning());
    }

    IEnumerator ShowWarning()
    {
        warningText.color = originalColor;

        yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, fadeDuration)
            );

            Color color = originalColor;
            color.a = Mathf.Lerp(
                originalColor.a,
                0f,
                progress
            );

            warningText.color = color;

            yield return null;
        }

        warningText.alpha = 0f;
    }
}
