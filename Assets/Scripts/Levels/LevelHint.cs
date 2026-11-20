using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LevelHint : MonoBehaviour
{
    public enum AdvanceCondition
    {
        SelectProjectile,       // Advances when a specific bird card/key is selected
        ShotLaunched,           // Advances immediately when slingshot.ShotsUsed increases
        AirborneProjectileDone, // Stays visible while shot is in flight, clears after landing/explosion
        ClickAnywhere           // Advances on any mouse click
    }

    [Serializable]
    public class HintStep
    {
        [TextArea(2, 3)]
        public string instructionText;

        [Header("Optional UI Card Highlight (e.g. 'bOOOmB')")]
        public string uiCardTextMatch;
        public int hotkeyNumber; // e.g., 3 for Key 3 (0 = none)

        [Header("Optional World Highlight (Box Sprite OR Parent with 2+ Waypoint Children)")]
        public GameObject worldHighlight;

        [Header("When to Advance to Next Step")]
        public AdvanceCondition advanceOn = AdvanceCondition.AirborneProjectileDone;
    }

    [Header("Sequential Hint Steps")]
    public List<HintStep> steps = new List<HintStep>();

    [Header("Hollow Outline / Arc & Pulse Settings")]
    public Color outlineColor = new Color(1f, 0.9f, 0.1f, 1f); // Bright yellow
    public float outlineThickness = 0.08f;
    public float pulseSpeed = 5f;
    public float pulseAmount = 0.12f;

    private Slingshot slingshot;
    private GameObject bannerPanel;
    private TMP_Text bannerText;

    private int currentStepIndex = -1;
    private bool hintFinished;

    private RectTransform activeCardRT;
    private Vector3 activeCardBaseScale = Vector3.one;
    private GameObject activeCardOverlay;
    private Vector3 activeWorldBaseScale = Vector3.one;
    private bool activeHighlightIsPath;
    private int shotsAtStepStart;
    private bool matchingCardClicked;
    private bool sawAirborneProjectile;

    private void Start()
    {
        slingshot = FindAnyObjectByType<Slingshot>();

        foreach (var step in steps)
        {
            if (step.worldHighlight != null)
            {
                SetupWorldHighlight(step.worldHighlight);
                step.worldHighlight.SetActive(false);
            }
        }
    }

    public void ActivateHint()
    {
        if (steps == null || steps.Count == 0) return;
        if (currentStepIndex >= 0 && !hintFinished) return;

        hintFinished = false;
        CreateHintBannerIfNeeded();
        bannerPanel.SetActive(true);
        EnterStep(0);
    }

    private void EnterStep(int index)
    {
        CleanupCurrentStep();

        if (index >= steps.Count)
        {
            FinishHint();
            return;
        }

        currentStepIndex = index;
        HintStep step = steps[currentStepIndex];

        bannerText.text = step.instructionText;
        shotsAtStepStart = slingshot != null ? slingshot.ShotsUsed : 0;
        matchingCardClicked = false;
        sawAirborneProjectile = false;

        if (!string.IsNullOrEmpty(step.uiCardTextMatch))
        {
            FindAndHighlightCard(step.uiCardTextMatch);

            if (step.advanceOn == AdvanceCondition.SelectProjectile &&
                IsProjectileAlreadyActive(step.uiCardTextMatch))
            {
                EnterStep(currentStepIndex + 1);
                return;
            }
        }

        if (step.worldHighlight != null)
        {
            activeHighlightIsPath = step.worldHighlight.transform.childCount >= 2;
            activeWorldBaseScale = step.worldHighlight.transform.localScale;
            if (activeHighlightIsPath)
                BuildSmoothArc(step.worldHighlight); // Refresh points in case objects moved
            step.worldHighlight.SetActive(true);
        }
    }

    private void Update()
    {
        if (currentStepIndex < 0 || hintFinished || currentStepIndex >= steps.Count)
            return;

        HintStep step = steps[currentStepIndex];
        float wave = Mathf.Sin(Time.time * pulseSpeed);
        float pulse = 1f + wave * pulseAmount;

        if (activeCardRT != null)
            activeCardRT.localScale = activeCardBaseScale * pulse;

        if (step.worldHighlight != null && step.worldHighlight.activeSelf)
        {
            if (activeHighlightIsPath)
            {
                // Pulse thickness & brightness so the arc ends stay locked on the ball and goal
                var lr = step.worldHighlight.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.widthMultiplier = 1f + wave * 0.25f;
                    Color c = outlineColor;
                    c.a = Mathf.Lerp(0.55f, 1f, (wave + 1f) * 0.5f);
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }
            else
            {
                step.worldHighlight.transform.localScale = activeWorldBaseScale * pulse;
            }
        }

        if (IsStepComplete(step))
        {
            EnterStep(currentStepIndex + 1);
        }
    }

    private bool IsStepComplete(HintStep step)
    {
        switch (step.advanceOn)
        {
            case AdvanceCondition.SelectProjectile:
                if (matchingCardClicked) return true;
                if (step.hotkeyNumber > 0 && WasNumberKeyPressed(step.hotkeyNumber)) return true;
                if (!string.IsNullOrEmpty(step.uiCardTextMatch) &&
                    IsProjectileAlreadyActive(step.uiCardTextMatch)) return true;
                return false;

            case AdvanceCondition.ShotLaunched:
                return slingshot != null && slingshot.ShotsUsed > shotsAtStepStart;

            case AdvanceCondition.AirborneProjectileDone:
                bool airborneNow = AnyProjectileAirborne();
                if (airborneNow) sawAirborneProjectile = true;
                return sawAirborneProjectile && !airborneNow;

            case AdvanceCondition.ClickAnywhere:
                return WasMouseClicked();

            default:
                return false;
        }
    }

    private void CleanupCurrentStep()
    {
        if (activeCardRT != null)
        {
            activeCardRT.localScale = activeCardBaseScale;
            activeCardRT = null;
        }

        if (activeCardOverlay != null)
        {
            Destroy(activeCardOverlay);
            activeCardOverlay = null;
        }

        if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            var prevHighlight = steps[currentStepIndex].worldHighlight;
            if (prevHighlight != null)
            {
                prevHighlight.transform.localScale = activeWorldBaseScale;
                prevHighlight.SetActive(false);
            }
        }
    }

    private void FinishHint()
    {
        CleanupCurrentStep();
        currentStepIndex = -1;
        hintFinished = true;
        if (bannerPanel != null)
            bannerPanel.SetActive(false);
    }

    // ---------------- Visual Highlight Builders ----------------

    private void SetupWorldHighlight(GameObject target)
    {
        if (target.transform.childCount >= 2)
            BuildSmoothArc(target);
        else
            ConvertToHollowOutline(target);
    }

    // Connects 2+ child objects into a smooth Catmull-Rom curved arc
    private void BuildSmoothArc(GameObject target)
    {
        int count = target.transform.childCount;
        var pts = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 p = target.transform.GetChild(i).position;
            p.z = 0f;
            pts.Add(p);
        }

        var smoothPoints = new List<Vector3>();
        int subdivisions = 14;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];

            for (int step = 0; step < subdivisions; step++)
            {
                float t = step / (float)subdivisions;
                smoothPoints.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        smoothPoints.Add(pts[pts.Count - 1]);

        var lr = target.GetComponent<LineRenderer>();
        if (lr == null) lr = target.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = smoothPoints.Count;
        lr.SetPositions(smoothPoints.ToArray());
        lr.startWidth = outlineThickness * 1.25f;
        lr.endWidth = outlineThickness * 1.25f;
        lr.numCapVertices = 6;
        lr.sortingOrder = 50;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // Disables solid sprite fill and draws a hollow yellow border only
    private void ConvertToHollowOutline(GameObject target)
    {
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        var lr = target.GetComponent<LineRenderer>();
        if (lr == null) lr = target.AddComponent<LineRenderer>();

        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 4;
        lr.numCornerVertices = 4;
        lr.startWidth = outlineThickness;
        lr.endWidth = outlineThickness;
        lr.sortingOrder = 50;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;

        lr.SetPositions(new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f)
        });
    }

    private void FindAndHighlightCard(string matchText)
    {
        string lowerMatch = matchText.ToLower();
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text.text != null && text.text.ToLower().Contains(lowerMatch))
            {
                Button btn = text.GetComponentInParent<Button>();
                if (btn != null)
                {
                    activeCardRT = btn.GetComponent<RectTransform>();
                    activeCardBaseScale = activeCardRT.localScale;
                    btn.onClick.AddListener(OnMatchedCardClicked);

                    activeCardOverlay = new GameObject("HintCardGlow", typeof(RectTransform), typeof(Image));
                    activeCardOverlay.transform.SetParent(activeCardRT, false);
                    var rt = activeCardOverlay.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(-4, -4);
                    rt.offsetMax = new Vector2(4, 4);

                    var img = activeCardOverlay.GetComponent<Image>();
                    img.color = new Color(1f, 0.85f, 0.2f, 0.35f);
                    img.raycastTarget = false;
                    return;
                }
            }
        }
    }

    private void OnMatchedCardClicked() => matchingCardClicked = true;

    private bool IsProjectileAlreadyActive(string matchText)
    {
        string lower = matchText.ToLower();
        foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
        {
            if (rb.gameObject.activeInHierarchy && rb.name.ToLower().Contains(lower))
                return true;
        }
        return false;
    }

    private bool AnyProjectileAirborne()
    {
        foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
        {
            if (!rb.gameObject.activeInHierarchy || rb.bodyType != RigidbodyType2D.Dynamic)
                continue;

            string n = rb.name.ToLower();
            if (n.Contains("water") || n.Contains("drop"))
                continue;

            if (rb.linearVelocity.sqrMagnitude > 0.15f)
                return true;
        }
        return false;
    }

    private bool WasNumberKeyPressed(int number)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (number == 1 && kb.digit1Key.wasPressedThisFrame) return true;
            if (number == 2 && kb.digit2Key.wasPressedThisFrame) return true;
            if (number == 3 && kb.digit3Key.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha0 + number)) return true;
#endif
        return false;
    }

    private bool WasMouseClicked()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0)) return true;
#endif
        return false;
    }

    private void CreateHintBannerIfNeeded()
    {
        if (bannerPanel != null) return;

        Canvas hudCanvas = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.name == "LevelHUD") { hudCanvas = c; break; }
        }
        if (hudCanvas == null) return;

        bannerPanel = new GameObject("HintBanner", typeof(RectTransform), typeof(Image));
        bannerPanel.transform.SetParent(hudCanvas.transform, false);

        var rt = bannerPanel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(840, 68);
        rt.anchoredPosition = new Vector2(0, -104);

        bannerPanel.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.21f, 0.92f);

        var textGo = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bannerPanel.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(18, 8);
        trt.offsetMax = new Vector2(-18, -8);

        bannerText = textGo.GetComponent<TextMeshProUGUI>();
        bannerText.fontSize = 24;
        bannerText.color = Color.white;
        bannerText.alignment = TextAlignmentOptions.Center;
    }
}