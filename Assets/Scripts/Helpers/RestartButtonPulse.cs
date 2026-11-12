using UnityEngine;

public class RestartButtonPulse : MonoBehaviour
{
    public float delayBeforePulse = 120f; // 120 seconds = 2 minutes
    public float pulseSpeed = 4f;         // Speed of the shrink/grow cycle
    public float pulseAmount = 0.15f;     // 0.15 = scales +/- 15%

    private float elapsedTime = 0f;
    private float pulseTimer = 0f;
    private bool forcePulse = false;
    private bool pulseStarted = false;
    private Vector3 originalScale;
    private Vector2 originalAnchoredPos;
    private RectTransform rt;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        originalScale = transform.localScale;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        if (forcePulse || elapsedTime >= delayBeforePulse)
        {
            // Capture the styled anchoredPosition right when pulsing begins
            if (!pulseStarted)
            {
                pulseStarted = true;
                if (rt != null) originalAnchoredPos = rt.anchoredPosition;
            }

            pulseTimer += Time.deltaTime;
            float scaleOffset = Mathf.Sin(pulseTimer * pulseSpeed) * pulseAmount;
            float scaleFactor = 1f + scaleOffset;
            transform.localScale = originalScale * scaleFactor;

            if (rt != null)
            {
                rt.anchoredPosition = originalAnchoredPos + new Vector2(
                    rt.sizeDelta.x * scaleOffset * 0.5f,
                    rt.sizeDelta.y * scaleOffset * 0.5f
                );
            }
        }
    }

    public void StartPulsingNow()
    {
        forcePulse = true;
    }
}