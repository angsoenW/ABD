
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WarningSignBlink : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseDuration = 3.5f;
    public float startingPulseSpeed = 1.2f;
    public float endingPulseSpeed = 5f;
    public float maxScale = 1.15f;

    [Range(0f, 1f)]
    public float minimumPulseAlpha = 0.15f;

    [Header("Fade Settings")]
    public float fadeDuration = 1.5f;

    [Range(0f, 1f)]
    public float waitingAlpha = 0.25f;

    [Header("Gameplay")]
    public PressureWaterLauncher launcher;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Color originalColor;
    private Coroutine animationRoutine;

    private bool playerStarted = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        originalColor = spriteRenderer.color;
    }

    void OnEnable()
    {
        playerStarted = false;

        transform.localScale = originalScale;
        spriteRenderer.color = originalColor;
        spriteRenderer.enabled = true;

        if (launcher != null)
            launcher.OnAcceptedShot += HandleShot;

        animationRoutine = StartCoroutine(AnimateWarning());
    }

    void OnDisable()
    {
        if (launcher != null)
            launcher.OnAcceptedShot -= HandleShot;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    void HandleShot(Vector2 velocity)
    {
        OnPlayerStarted();
    }

    IEnumerator AnimateWarning()
    {
        float elapsed = 0f;
        float phase = 0f;

        // Phase 1: Pulsing and blinking.
        while (elapsed < pulseDuration && !playerStarted)
        {
            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, pulseDuration)
            );

            float speed = Mathf.Lerp(
                startingPulseSpeed,
                endingPulseSpeed,
                progress
            );

            phase += Time.deltaTime * speed * Mathf.PI * 2f;

            float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;

            transform.localScale = originalScale *
                Mathf.Lerp(1f, maxScale, pulse);

            Color color = originalColor;

            color.a = originalColor.a *
                Mathf.Lerp(minimumPulseAlpha, 1f, pulse);

            spriteRenderer.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerStarted)
            yield break;

        // Phase 2: Fade to transparent.
        elapsed = 0f;

        Color startColor = spriteRenderer.color;
        Vector3 startScale = transform.localScale;

        while (elapsed < fadeDuration && !playerStarted)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, fadeDuration)
                )
            );

            Color color = startColor;

            color.a = Mathf.Lerp(
                startColor.a,
                originalColor.a * waitingAlpha,
                progress
            );

            spriteRenderer.color = color;

            transform.localScale = Vector3.Lerp(
                startScale,
                originalScale,
                progress
            );

            yield return null;
        }

        if (playerStarted)
            yield break;

        // Phase 3: Stay faintly visible.
        transform.localScale = originalScale;

        Color waitingColor = originalColor;
        waitingColor.a = originalColor.a * waitingAlpha;
        spriteRenderer.color = waitingColor;

        animationRoutine = null;
    }

    public void OnPlayerStarted()
    {
        if (playerStarted)
            return;

        playerStarted = true;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        // Completely disappear when gameplay begins.
        spriteRenderer.enabled = false;
    }
}
