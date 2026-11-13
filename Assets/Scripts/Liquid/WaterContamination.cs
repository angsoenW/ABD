
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WaterDropVisual))]
public class WaterContamination : MonoBehaviour
{
    [Header("Contamination Settings")]
    public bool isContaminated = false;

    public Color contaminatedColor =
        new Color(1f, 0.8f, 0.898f, 1f);

    [Header("Visual Transition")]
    [Tooltip("Time needed to turn completely pink.")]
    public float contaminationDuration = 1.5f;

    [Header("Contamination Spread")]
    [Tooltip("Delay before newly contaminated water can infect other droplets.")]
    public float spreadDelay = 0.5f;

    [Tooltip("How often nearby droplets are checked.")]
    public float spreadCheckInterval = 0.1f;

    [Tooltip("Distance at which contamination can spread.")]
    public float spreadRadius = 0.25f;

    private WaterDropVisual waterVisual;
    private Coroutine colorTransition;

    private bool canSpread = false;
    private bool contaminationStarted = false;

    void Awake()
    {
        waterVisual = GetComponent<WaterDropVisual>();
    }

    void Start()
    {
        if (isContaminated)
        {
            // Original pink water starts fully contaminated.
            waterVisual.waterColor = contaminatedColor;
            canSpread = true;
            contaminationStarted = true;
        }

        StartCoroutine(CheckNearbyWater());
    }

    public void Contaminate()
    {
        if (contaminationStarted)
            return;

        contaminationStarted = true;

        // Gameplay state changes immediately.
        isContaminated = true;

        // Visual transition begins.
        colorTransition = StartCoroutine(
            TransitionToPink()
        );

        // Wait before spreading to other droplets.
        StartCoroutine(EnableSpreading());
    }

    IEnumerator EnableSpreading()
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, spreadDelay)
        );

        canSpread = true;
    }

    IEnumerator TransitionToPink()
    {
        Color startingColor = waterVisual.waterColor;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, contaminationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            float smoothProgress = Mathf.SmoothStep(
                0f, 1f, progress
            );

            waterVisual.waterColor = Color.Lerp(
                startingColor,
                contaminatedColor,
                smoothProgress
            );

            yield return null;
        }

        waterVisual.waterColor = contaminatedColor;
        colorTransition = null;
    }

    IEnumerator CheckNearbyWater()
    {
        while (true)
        {
            if (canSpread)
            {
                Collider2D[] nearby =
                    Physics2D.OverlapCircleAll(
                        transform.position,
                        spreadRadius
                    );

                foreach (Collider2D hit in nearby)
                {
                    if (hit.gameObject == gameObject)
                        continue;

                    WaterContamination other =
                        hit.GetComponent<WaterContamination>();

                    if (other != null && !other.isContaminated)
                    {
                        other.Contaminate();
                    }
                }
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.02f, spreadCheckInterval)
            );
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        WaterContamination other =
            collision.gameObject.GetComponent<WaterContamination>();

        if (other == null)
            return;

        if (canSpread && !other.isContaminated)
        {
            other.Contaminate();
        }
        else if (other.canSpread && !isContaminated)
        {
            Contaminate();
        }
    }
}
