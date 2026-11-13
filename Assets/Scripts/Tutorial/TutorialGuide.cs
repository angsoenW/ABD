using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Guided tutorial. Put it on one GameObject in the Tutorial scene only.
// It builds its own overlay canvas at runtime and only READS the existing gameplay
// (Slingshot, GoalReservoir, the mud Tilemap, the level UI), so no other level is affected.
//
// Explanation pages pause the game (Time.timeScale = 0) and lock the slingshot.
// Practice pages un-pause and wait for the real player action.
// Restarting the Tutorial scene (Restart button, or Retry after a loss) resumes at the current
// step; opening the Tutorial from another scene (e.g. the menu) starts from the beginning.
// Finishing the level shows "Start Level 1"; "Skip tutorial" goes straight to Level 1.
[DefaultExecutionOrder(-50)]   // runs before Slingshot.Update so a click on the panel never starts a drag
public class TutorialGuide : MonoBehaviour
{
    [Header("Gameplay (found automatically if left empty)")]
    public Slingshot slingshot;
    public GoalReservoir goal;
    public Tilemap mud;                                   // the diggable tilemap ("Map/Dirt")
    public LevelUI levelUI;

    [Header("World areas to highlight (world units: x, y = bottom-left)")]
    public Rect waterArea = new Rect(0f, 2.1f, 2f, 2.3f);
    public Rect basinArea = new Rect(0f, -4.8f, 2f, 2.6f);
    public Rect mudArea = new Rect(0f, 0.5f, 2f, 1.6f);
    public Rect rockArea = new Rect(-11.4f, -8f, 3.4f, 7f);
    public float birdHighlightRadius = 0.85f;
    public Vector2 demoDrag = new Vector2(-1.6f, -0.8f);  // pull shown by the animated arrow

    [Header("Practice")]
    public float aimDistance = 0.9f;                       // how far back counts as "aimed"

    [Header("Look")]
    public TMP_FontAsset titleFont;
    public TMP_FontAsset bodyFont;
    public Sprite panelSprite;                             // the rounded UISprite used by the rest of the UI
    public Color panelColor = new Color(0.07f, 0.09f, 0.15f, 0.92f);
    public Color buttonNormal = new Color(0.10f, 0.13f, 0.21f, 0.88f);
    public Color buttonHover = new Color(0.22f, 0.30f, 0.45f, 0.95f);
    public Color accent = new Color(0.24f, 0.56f, 0.95f, 1f);
    public Color highlight = new Color(1f, 0.82f, 0.36f, 1f);
    [Range(0f, 1f)] public float dimAlpha = 0.45f;
    public int sortingOrder = 50;                          // below LevelHUD (100) so Restart/Menu stay clickable
    [Tooltip("Panel width in reference pixels (1920x1080); it shrinks on narrow screens.")]
    public float panelWidth = 560f;

    // ---------------- Pages ----------------
    enum Page { Welcome, Bird, Goal, Counter, Controls, Aim, Shoot, MudExplain, MudPractice, Objective, Complete }
    static readonly int[] StepOfPage = { 1, 2, 3, 4, 5, 6, 6, 7, 7, 8, 8 };
    const int StepCount = 8;

    static bool IsPractice(Page p) => p == Page.Aim || p == Page.Shoot || p == Page.MudPractice || p == Page.Objective;

    // ---------------- Session memory (survives a reload of the Tutorial scene) ----------------
    static int savedPage;
    static string lastUnloadedScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()   // domain reload is off in this project, so reset by hand
    {
        savedPage = 0;
        lastUnloadedScene = null;
        SceneManager.sceneUnloaded -= RememberUnloaded;
        SceneManager.sceneUnloaded += RememberUnloaded;
    }

    static void RememberUnloaded(Scene scene) => lastUnloadedScene = scene.name;

    // Read-only status, handy for debugging and automated checks.
    public string CurrentPage => page.ToString();
    public bool IsActive => active;
    public bool IsFailed => failed;
    public string Hint => hint;
    public string Title => titleText != null ? titleText.text : "";
    public string NextLabel => nextLabel != null && nextButton.activeSelf ? nextLabel.text : "";
    public RectTransform PanelRect => panel;
    public static int SavedPage => savedPage;

    // ---------------- State ----------------
    Page page;
    bool active;
    bool pausedByTutorial;
    bool blockPress;          // the current press started on the panel: keep the slingshot locked
    bool prevHeld;
    bool failed;
    bool leaving;             // a scene load was requested

    Rigidbody2D birdRb;
    Camera cam;
    bool wasFlying;
    float lastPull;
    bool shotLaunched;        // Shoot page: launched, waiting for the bird to come back
    bool practiceShot;        // MudPractice page: a shot was fired on this page
    int tilesDug;
    bool mudSuccess;
    string hint = "";

    RectTransform statsPanel, restartButton, menuButton;   // UI targets, found when needed

    // ---------------- UI ----------------
    RectTransform canvasRect, panel;
    TutorialDim dim;
    TMP_Text stepText, titleText, bodyText, hintText, nextLabel;
    GameObject backButton, nextButton, skipButton;
    LayoutElement nextLayout;
    readonly List<Image> dots = new List<Image>();
    readonly List<Frame> frames = new List<Frame>();
    RectTransform demoRoot, demoShaft, demoHead, demoHand;
    TMP_Text demoLabel;
    CanvasGroup demoGroup;
    Sprite outlineSprite, ringSprite, discSprite, triangleSprite;
    readonly List<Rect> holeBuffer = new List<Rect>();

    class Frame
    {
        public RectTransform rect;
        public Image image;
        public GameObject tag;
        public RectTransform tagRect;
        public TMP_Text tagText;
    }

    struct Target
    {
        public bool circle;
        public Vector2 center;    // world
        public Vector2 size;      // world
        public RectTransform ui;  // or a screen-space UI element
        public string label;
        public bool labelLeft;    // put the label beside the frame (mud sits right under the water pocket)
    }
    readonly List<Target> targets = new List<Target>();

    // =====================================================================
    void Start()
    {
        // Coming from another scene (menu, a level...) = a fresh tutorial. Reloading this scene = resume.
        if (lastUnloadedScene != gameObject.scene.name) savedPage = 0;

        if (slingshot == null) slingshot = FindAnyObjectByType<Slingshot>();
        if (goal == null) goal = FindAnyObjectByType<GoalReservoir>();
        if (levelUI == null) levelUI = FindAnyObjectByType<LevelUI>();
        if (mud == null && slingshot != null)
        {
            var digger = slingshot.GetComponent<TilemapDigger>();
            if (digger != null) mud = digger.terrain;
        }
        if (slingshot == null) { Debug.LogWarning("[Tutorial] No Slingshot found, tutorial disabled."); enabled = false; return; }

        if (levelUI != null)
        {
            if (panelSprite == null) panelSprite = levelUI.roundedSprite;
            statsPanel = levelUI.transform.Find("StatsPanel") as RectTransform;
        }

        birdRb = slingshot.GetComponent<Rigidbody2D>();
        cam = Camera.main;
        // Slingshot.Start may not have run yet (we can lock it first); put the bird on its anchor now.
        slingshot.ResetProjectile();
        wasFlying = false;

        BuildUI();
        Tilemap.tilemapTileChanged += OnTilesChanged;
        active = true;
        GoTo((Page)Mathf.Clamp(savedPage, 0, (int)Page.Objective));
    }

    void OnDestroy()
    {
        Tilemap.tilemapTileChanged -= OnTilesChanged;
        Unpause();
        if (slingshot != null) slingshot.enabled = true;
    }

    // =====================================================================
    void Update()
    {
        if (!active) return;

        // The level ended while the tutorial was open.
        if (goal != null && goal.Finished)
        {
            if (goal.Won) { if (page != Page.Complete) GoTo(Page.Complete); }
            else if (!failed) ShowFailed();
        }

        HandleKeyboard();
        GuardPanelClicks();
        TrackBird();
        UpdateVisuals();
    }

    // The tutorial panel shows win/lose itself, so keep the level's own result message hidden.
    // Done here (not in the shared LevelUI script) so nothing tutorial-specific touches other levels.
    // LevelUI only shows the message from Update/events, so hiding it in LateUpdate means it never renders.
    void LateUpdate()
    {
        if (!active || levelUI == null || goal == null || !goal.Finished) return;
        if (levelUI.resultPanel != null && levelUI.resultPanel.activeSelf) levelUI.resultPanel.SetActive(false);
        if (levelUI.winPanel != null && levelUI.winPanel.activeSelf) levelUI.winPanel.SetActive(false);
        if (levelUI.losePanel != null && levelUI.losePanel.activeSelf) levelUI.losePanel.SetActive(false);
    }

    // ---------------- Page flow ----------------
    void GoTo(Page p)
    {
        page = p;
        savedPage = (int)(p == Page.Complete ? Page.Objective : p);
        hint = "";
        shotLaunched = practiceShot = mudSuccess = false;
        tilesDug = 0;
        lastPull = 0f;

        if (IsPractice(p) || p == Page.Complete) Unpause(); else Pause();
        ApplySlingshotLock();
        RefreshPanel();
    }

    public void Next()
    {
        if (leaving) return;
        if (failed) { Leave(LevelManager.Restart); return; }       // resumes at this step
        if (page == Page.Complete) { Leave(LevelManager.LoadNext); return; }   // Tutorial -> Level 1
        if (IsPractice(page)) return;                       // practice pages advance on real actions
        GoTo(page + 1);
    }

    public void Back()
    {
        if (leaving || failed || page == Page.Welcome || page == Page.Complete) return;
        GoTo(page - 1);
    }

    public void Skip()
    {
        if (leaving) return;
        Leave(() => LevelManager.LoadLevel(1));
    }

    void Leave(System.Action load)
    {
        leaving = true;
        Unpause();
        load();
    }

    void ShowFailed()
    {
        failed = true;
        Unpause();
        ApplySlingshotLock();
        RefreshPanel();
    }

    void Pause()
    {
        Time.timeScale = 0f;
        pausedByTutorial = true;
    }

    void Unpause()
    {
        if (!pausedByTutorial) return;
        Time.timeScale = 1f;
        pausedByTutorial = false;
    }

    void ApplySlingshotLock()
    {
        if (slingshot == null) return;
        bool allowed = active && !failed && IsPractice(page) && !blockPress;
        if (slingshot.enabled != allowed) slingshot.enabled = allowed;
    }

    // ---------------- Practice tracking ----------------
    void TrackBird()
    {
        if (birdRb == null || slingshot.anchor == null) return;

        bool flying = birdRb.bodyType == RigidbodyType2D.Dynamic;
        bool launched = flying && !wasFlying;
        bool returned = !flying && wasFlying;
        float pull = flying ? 0f : Vector2.Distance(birdRb.position, slingshot.anchor.position);
        if (!flying && pull > 0.05f) lastPull = pull;
        wasFlying = flying;

        if (failed) return;

        switch (page)
        {
            case Page.Aim:
                if (!flying && pull >= aimDistance)
                {
                    GoTo(Page.Shoot);                       // still holding: next step says "let go"
                }
                else if (launched)
                {
                    if (lastPull >= aimDistance) { GoTo(Page.Shoot); OnShotLaunched(); }
                    else SetHint("Too soft! Drag the bird further back before you let go.");
                }
                break;

            case Page.Shoot:
                if (launched && !shotLaunched) OnShotLaunched();
                else if (shotLaunched && (returned || !flying)) GoTo(Page.MudExplain);
                break;

            case Page.MudPractice:
                if (launched) { practiceShot = true; tilesDug = 0; SetHint(""); }
                if (practiceShot && !mudSuccess && tilesDug > 0)
                {
                    mudSuccess = true;
                    savedPage = (int)Page.Objective;
                    titleText.text = "Great digging!";
                    bodyText.text = "See the tunnel? Water flows through the paths you dig.";
                }
                if (returned || (practiceShot && !flying))
                {
                    if (mudSuccess) GoTo(Page.Objective);
                    else if (practiceShot)
                    {
                        practiceShot = false;
                        SetHint("Missed the mud! Aim at the brown ground and try again.");
                    }
                }
                break;
        }
    }

    void OnShotLaunched()
    {
        shotLaunched = true;
        savedPage = (int)Page.MudExplain;   // the shot counts even if the level is restarted mid-flight
        titleText.text = "Nice shot!";
        bodyText.text = "Watch your bird fly. It comes back to the pillar when it stops.";
        SetHint("");
        backButton.SetActive(false);
    }

    void OnTilesChanged(Tilemap tilemap, Tilemap.SyncTile[] tiles)
    {
        if (!active || tilemap != mud || page != Page.MudPractice || !practiceShot) return;
        foreach (var t in tiles) if (t.tile == null) tilesDug++;
    }

    void SetHint(string text)
    {
        hint = text;
        hintText.text = text;
        hintText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    // ---------------- Input ----------------
    void HandleKeyboard()
    {
        // After a win or a loss LevelUI already uses Enter (next level / retry), so don't double up.
        if (failed || page == Page.Complete || leaving) return;
        bool next = false, back = false, skip = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            next |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                 || kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            back |= kb.leftArrowKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
            skip |= kb.escapeKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        next |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
             || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.RightArrow);
        back |= Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace);
        skip |= Input.GetKeyDown(KeyCode.Escape);
#endif
        if (skip) { Skip(); return; }
        if (IsPractice(page)) return;
        if (next) Next();
        else if (back) Back();
    }

    // A press that starts on the tutorial panel must not grab or launch the bird.
    void GuardPanelClicks()
    {
        bool pressed = false, held = false;
        Vector2 pos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        if (p != null)
        {
            pressed = p.press.wasPressedThisFrame;
            held = p.press.isPressed;
            pos = p.position.ReadValue();
        }
#else
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        pos = Input.mousePosition;
#endif
        pressed |= held && !prevHeld;     // also catch the press edge from the held state
        prevHeld = held;

        if (pressed && panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, pos, null))
            blockPress = true;
        else if (!held && !pressed)
            blockPress = false;

        ApplySlingshotLock();
    }

    // ---------------- Panel content ----------------
    void RefreshPanel()
    {
        int need = goal != null ? goal.waterNeeded : 40;
        string title, body, next = "Next";
        bool showBack = page != Page.Welcome;
        bool showSkip = true;

        if (failed)
        {
            title = "Too much water spilled";
            body = "Not enough water is left to fill the basin. Press <b>Retry</b> to reset the level. You'll continue from this step.";
            next = "Retry";
            showBack = false;
        }
        else switch (page)
        {
            case Page.Welcome:
                title = "Welcome!";
                body = "This short tutorial teaches you how to play. Use <b>Next</b> and <b>Back</b> to move between steps.";
                break;
            case Page.Bird:
                title = "Meet your bird";
                body = "The <b>white ball</b> on the stone pillar is your bird. You launch it like a slingshot.";
                break;
            case Page.Goal:
                title = "Your goal";
                body = $"Water is trapped in the pocket at the top. Get <b>{need} drops</b> into the <b>basin</b> below to win.";
                break;
            case Page.Counter:
                title = "Water counter";
                body = $"<b>Water</b> shows how many drops are in the basin, out of {need}. The bar fills as you go. <b>Shots</b> counts your launches.";
                break;
            case Page.Controls:
                title = "Restart & Menu";
                body = "If too much water spills out of the level, you lose. Press <b>Restart</b> to try again, or <b>Menu</b> to leave. Press <b>R</b> to call your bird back.";
                break;
            case Page.Aim:
                title = "Try it: aim";
                body = "Click and hold on the bird, then drag it back. The line shows where it will fly.";
                break;
            case Page.Shoot:
                title = "Let go to launch!";
                body = "Release to launch. The bird flies the opposite way you pulled. Pull further for a stronger shot.";
                break;
            case Page.MudExplain:
                title = "Mud and rock";
                body = "Brown <b>mud</b> is soft: your bird digs a tunnel through it and keeps flying. Grey <b>rock</b> can't be dug.";
                break;
            case Page.MudPractice:
                title = "Try it: dig";
                body = "Launch the bird into the brown mud to dig a tunnel.";
                break;
            case Page.Objective:
                title = "Fill the basin";
                body = $"Dig through the mud below the water so it pours into the basin. Get {need} drops in to finish!";
                break;
            default:   // Complete
                title = "Tutorial complete!";
                body = "Great job! In Level 1, watch out for <b>snakes</b>: if your bird touches one, the shot ends.";
                next = "Start Level 1";
                showBack = false;
                showSkip = false;
                break;
        }

        stepText.text = $"STEP {StepOfPage[(int)page]} OF {StepCount}";
        for (int i = 0; i < dots.Count; i++)
        {
            int step = StepOfPage[(int)page] - 1;
            dots[i].color = i < step ? new Color(accent.r, accent.g, accent.b, 0.55f)
                          : i == step ? highlight
                          : new Color(1f, 1f, 1f, 0.18f);
        }
        titleText.text = title;
        bodyText.text = body;
        SetHint(hint);

        bool explanation = failed || !IsPractice(page);
        nextButton.SetActive(explanation);
        nextLabel.text = next;
        nextLayout.preferredWidth = Mathf.Max(210f, nextLabel.GetPreferredValues(next).x + 56f);
        backButton.SetActive(showBack);
        skipButton.SetActive(showSkip);
    }

    // ---------------- Per-frame visuals ----------------
    void UpdateVisuals()
    {
        FitPanel();
        targets.Clear();
        bool dragging = birdRb != null && birdRb.bodyType != RigidbodyType2D.Dynamic
                        && Vector2.Distance(birdRb.position, slingshot.anchor.position) > 0.05f;
        bool flying = birdRb != null && birdRb.bodyType == RigidbodyType2D.Dynamic;
        Vector2 bird = birdRb != null ? birdRb.position : Vector2.zero;
        Vector2 birdBox = Vector2.one * birdHighlightRadius * 2f;

        if (!failed)
        {
            switch (page)
            {
                case Page.Bird:
                    targets.Add(new Target { circle = true, center = bird, size = birdBox, label = "Your bird" });
                    break;
                case Page.Goal:
                    targets.Add(Box(waterArea, "Water"));
                    targets.Add(Box(basinArea, "Basin"));
                    break;
                case Page.Counter:
                    if (statsPanel != null) targets.Add(new Target { ui = statsPanel, label = "Water counter" });
                    targets.Add(Box(basinArea, "Basin"));
                    break;
                case Page.Controls:
                    FindHudButtons();
                    if (restartButton != null) targets.Add(new Target { ui = restartButton });   // the buttons carry their own text
                    if (menuButton != null) targets.Add(new Target { ui = menuButton });
                    break;
                case Page.Aim:
                    targets.Add(new Target { circle = true, center = bird, size = birdBox });
                    break;
                case Page.Shoot:
                    if (!flying) targets.Add(new Target { circle = true, center = bird, size = birdBox, label = dragging ? "Let go!" : null });
                    break;
                case Page.MudExplain:
                    targets.Add(Box(mudArea, "Mud: dig through", true));
                    targets.Add(Box(rockArea, "Rock: can't dig"));
                    break;
                case Page.MudPractice:
                    targets.Add(Box(mudArea, "Mud", true));
                    break;
                case Page.Objective:
                    targets.Add(Box(waterArea, "Water"));
                    targets.Add(Box(mudArea, "Dig here", true));
                    targets.Add(Box(basinArea, "Basin"));
                    break;
            }
        }

        // Gentle dim only on explanation pages; practice keeps the whole play area clear.
        bool showDim = !failed && !IsPractice(page) && page != Page.Complete;
        dim.gameObject.SetActive(showDim);
        holeBuffer.Clear();

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        Rect canvasArea = canvasRect.rect;
        for (int i = 0; i < frames.Count; i++)
        {
            Frame f = frames[i];
            if (i >= targets.Count) { f.rect.gameObject.SetActive(false); continue; }

            Target t = targets[i];
            Rect r = t.ui != null ? UIToCanvas(t.ui)
                                  : WorldToCanvas(t.center - t.size * 0.5f, t.center + t.size * 0.5f);
            if (t.circle) r = Square(r);
            if (showDim) holeBuffer.Add(r);

            Rect fr = Pad(r, 8f);
            f.rect.gameObject.SetActive(true);
            f.rect.anchoredPosition = fr.center;
            f.rect.sizeDelta = fr.size;
            f.image.sprite = t.circle ? ringSprite : outlineSprite;
            f.image.type = t.circle ? Image.Type.Simple : Image.Type.Sliced;
            f.image.color = new Color(highlight.r, highlight.g, highlight.b, pulse);

            bool hasLabel = !string.IsNullOrEmpty(t.label);
            f.tag.SetActive(hasLabel);
            if (hasLabel)
            {
                f.tagText.text = t.label;
                if (t.labelLeft)
                {
                    f.tagRect.anchorMin = f.tagRect.anchorMax = new Vector2(0f, 0.5f);
                    f.tagRect.pivot = new Vector2(1f, 0.5f);
                    f.tagRect.anchoredPosition = new Vector2(-10f, 0f);
                }
                else
                {
                    // Label above the frame, or below it when the frame touches the top of the screen.
                    bool below = fr.yMax + 60f > canvasArea.yMax;
                    f.tagRect.anchorMin = f.tagRect.anchorMax = new Vector2(0.5f, below ? 0f : 1f);
                    f.tagRect.pivot = new Vector2(0.5f, below ? 1f : 0f);
                    // Keep the label on screen when the frame runs off the left/right edge (narrow screens).
                    float half = (f.tagText.GetPreferredValues(t.label).x + 28f) * 0.5f;
                    float dx = Mathf.Clamp(fr.center.x, canvasArea.xMin + half + 8f, canvasArea.xMax - half - 8f) - fr.center.x;
                    f.tagRect.anchoredPosition = new Vector2(dx, below ? -8f : 8f);
                }
            }
        }
        if (showDim) dim.SetHoles(holeBuffer);

        UpdateDemo(!failed && !dragging && !flying && (page == Page.Aim || (page == Page.Shoot && !shotLaunched)));
    }

    void FindHudButtons()
    {
        if (restartButton != null && menuButton != null) return;
        foreach (var c in FindObjectsByType<Canvas>())
        {
            if (c.name != "LevelHUD") continue;
            restartButton = c.transform.Find("RestartButton") as RectTransform;
            menuButton = c.transform.Find("MenuButton") as RectTransform;
        }
    }

    // Narrow screens get a narrower panel so the water, mud and basin stay visible.
    void FitPanel()
    {
        float w = Mathf.Min(panelWidth, canvasRect.rect.width * 0.34f);
        if (Mathf.Abs(panel.sizeDelta.x - w) > 0.5f) panel.sizeDelta = new Vector2(w, panel.sizeDelta.y);
    }

    static Target Box(Rect world, string label, bool labelLeft = false) =>
        new Target { center = world.center, size = world.size, label = label, labelLeft = labelLeft };

    // Animated "click, hold and drag back" hint next to the bird.
    void UpdateDemo(bool show)
    {
        demoRoot.gameObject.SetActive(show);
        if (!show) return;

        Vector2 start = WorldToLocal(slingshot.anchor.position);
        Vector2 end = WorldToLocal((Vector2)slingshot.anchor.position + demoDrag);

        const float cycle = 2.6f;
        float t = Time.unscaledTime % cycle;
        float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.6f, t));
        float alpha = t < 0.15f ? t / 0.15f : t > 2.3f ? 1f - (t - 2.3f) / 0.3f : 1f;
        demoGroup.alpha = alpha;

        Vector2 hand = Vector2.Lerp(start, end, move);
        Vector2 dir = end - start;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(start, hand);

        demoShaft.anchoredPosition = start;
        demoShaft.localEulerAngles = new Vector3(0, 0, angle);
        demoShaft.sizeDelta = new Vector2(Mathf.Max(0f, length - 18f), 10f);
        demoHead.gameObject.SetActive(length > 30f);
        demoHead.anchoredPosition = hand;
        demoHead.localEulerAngles = new Vector3(0, 0, angle);

        float press = t < 0.5f ? 1f - 0.25f * Mathf.Sin(t / 0.5f * Mathf.PI) : 0.8f;
        demoHand.anchoredPosition = hand;
        demoHand.localScale = Vector3.one * press;

        demoLabel.text = page == Page.Shoot ? "Drag back, let go" : t < 0.5f ? "Click & hold" : "Drag back";
        ((RectTransform)demoLabel.transform).anchoredPosition = hand + new Vector2(0f, -56f);
    }

    // ---------------- Coordinate helpers ----------------
    Vector2 WorldToLocal(Vector2 world)
    {
        Vector2 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }

    Rect WorldToCanvas(Vector2 worldMin, Vector2 worldMax)
    {
        Vector2 a = WorldToLocal(worldMin), b = WorldToLocal(worldMax);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    // Screen-space-overlay UI: its world corners are screen pixels.
    readonly Vector3[] corners = new Vector3[4];
    Rect UIToCanvas(RectTransform ui)
    {
        ui.GetWorldCorners(corners);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, corners[0], null, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, corners[2], null, out Vector2 b);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    static Rect Square(Rect r)
    {
        float s = Mathf.Max(r.width, r.height);
        return new Rect(r.center - Vector2.one * s * 0.5f, Vector2.one * s);
    }

    static Rect Pad(Rect r, float p) => Rect.MinMaxRect(r.xMin - p, r.yMin - p, r.xMax + p, r.yMax + p);

    // =====================================================================
    // UI construction
    // =====================================================================
    void BuildUI()
    {
        outlineSprite = MakeOutlineSprite();
        ringSprite = MakeRingSprite();
        discSprite = MakeDiscSprite();
        triangleSprite = MakeTriangleSprite();

        var root = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)root.transform;

        // Dim
        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialDim));
        dimGo.transform.SetParent(root.transform, false);
        Stretch((RectTransform)dimGo.transform);
        dim = dimGo.GetComponent<TutorialDim>();
        dim.color = new Color(0f, 0f, 0f, dimAlpha);
        dim.raycastTarget = false;

        // Highlight frames (pooled)
        for (int i = 0; i < 3; i++) frames.Add(MakeFrame(root.transform));

        BuildDemo(root.transform);
        BuildPanel(root.transform);
    }

    Frame MakeFrame(Transform parent)
    {
        var f = new Frame();
        var go = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        f.rect = (RectTransform)go.transform;
        f.image = go.GetComponent<Image>();
        f.image.raycastTarget = false;

        f.tag = new GameObject("Label", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        f.tag.transform.SetParent(go.transform, false);
        f.tagRect = (RectTransform)f.tag.transform;
        f.tagRect.anchorMin = f.tagRect.anchorMax = new Vector2(0.5f, 1f);
        f.tagRect.pivot = new Vector2(0.5f, 0f);
        f.tagRect.anchoredPosition = new Vector2(0f, 8f);
        var bg = f.tag.GetComponent<Image>();
        bg.sprite = panelSprite; bg.type = Image.Type.Sliced;
        bg.color = highlight; bg.raycastTarget = false;
        var h = f.tag.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(14, 14, 4, 4);
        h.childControlWidth = h.childControlHeight = true;
        var fit = f.tag.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        f.tagText = MakeText(f.tag.transform, "Text", titleFont, 30, new Color(0.07f, 0.09f, 0.15f, 1f));
        f.tagText.textWrappingMode = TextWrappingModes.NoWrap;
        return f;
    }

    void BuildDemo(Transform parent)
    {
        var go = new GameObject("AimDemo", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        demoRoot = (RectTransform)go.transform;
        Stretch(demoRoot);
        demoGroup = go.GetComponent<CanvasGroup>();
        demoGroup.blocksRaycasts = false;
        demoGroup.interactable = false;

        demoShaft = MakeImage(demoRoot, "ArrowShaft", null, Color.white);
        demoShaft.pivot = new Vector2(0f, 0.5f);
        demoHead = MakeImage(demoRoot, "ArrowHead", triangleSprite, Color.white);
        demoHead.pivot = new Vector2(0.75f, 0.5f);
        demoHead.sizeDelta = new Vector2(40f, 40f);
        demoHand = MakeImage(demoRoot, "Pointer", ringSprite, highlight);
        demoHand.sizeDelta = new Vector2(64f, 64f);
        var inner = MakeImage(demoHand, "Dot", discSprite, highlight);
        inner.sizeDelta = new Vector2(22f, 22f);

        demoLabel = MakeText(demoRoot, "DemoLabel", titleFont, 34, Color.white);
        demoLabel.alignment = TextAlignmentOptions.Center;
        demoLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var lr = (RectTransform)demoLabel.transform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(360f, 50f);
        demoLabel.outlineWidth = 0.25f;
        demoLabel.outlineColor = new Color32(10, 14, 25, 255);
    }

    void BuildPanel(Transform parent)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Shadow),
                                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        panel = (RectTransform)go.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-36f, -30f);
        panel.sizeDelta = new Vector2(panelWidth, 0f);

        var bg = go.GetComponent<Image>();
        bg.sprite = panelSprite; bg.type = Image.Type.Sliced; bg.color = panelColor;
        bg.raycastTarget = true;   // swallow clicks on the panel background
        var sh = go.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
        sh.effectDistance = new Vector2(0f, -3f);

        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(32, 32, 26, 30);
        v.spacing = 12f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Header: step counter + skip
        var header = Row(panel, "Header", TextAnchor.MiddleLeft);
        stepText = MakeText(header, "StepCounter", bodyFont, 24, new Color(0.70f, 0.78f, 0.92f, 1f));
        stepText.fontStyle = FontStyles.Bold;
        stepText.characterSpacing = 4;
        stepText.textWrappingMode = TextWrappingModes.NoWrap;
        stepText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        skipButton = MakeButton(header, "Skip tutorial", 190f, 46f, buttonNormal, 24, Skip, out _);

        // Progress dots
        var dotRow = Row(panel, "Progress", TextAnchor.MiddleLeft);
        dotRow.GetComponent<HorizontalLayoutGroup>().spacing = 10f;
        dotRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
        for (int i = 0; i < StepCount; i++)
        {
            var d = MakeImage(dotRow, "Dot", discSprite, Color.white);
            var le = d.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 14f;
            dots.Add(d.GetComponent<Image>());
        }

        titleText = MakeText(panel, "Title", titleFont, 54, Color.white);
        titleText.characterSpacing = 2;
        bodyText = MakeText(panel, "Body", bodyFont, 31, new Color(0.88f, 0.92f, 0.98f, 1f));
        bodyText.lineSpacing = 6f;
        hintText = MakeText(panel, "Hint", bodyFont, 28, highlight);
        hintText.fontStyle = FontStyles.Bold;

        // Buttons
        var buttons = Row(panel, "Buttons", TextAnchor.MiddleRight);
        var hb = buttons.GetComponent<HorizontalLayoutGroup>();
        hb.spacing = 14f;
        hb.padding = new RectOffset(0, 0, 10, 0);
        backButton = MakeButton(buttons, "Back", 150f, 66f, buttonNormal, 30, Back, out _);
        nextButton = MakeButton(buttons, "Next", 210f, 66f, accent, 32, Next, out nextLabel);
        nextLayout = nextButton.GetComponent<LayoutElement>();
    }

    // ---------------- Small UI helpers ----------------
    RectTransform Row(Transform parent, string name, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = align;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return (RectTransform)go.transform;
    }

    GameObject MakeButton(Transform parent, string label, float w, float h, Color normal, float fontSize,
                          UnityEngine.Events.UnityAction onClick, out TMP_Text text)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;

        var img = go.GetComponent<Image>();
        img.sprite = panelSprite; img.type = Image.Type.Sliced; img.color = Color.white;

        var b = go.GetComponent<Button>();
        var c = b.colors;
        c.normalColor = normal;
        c.highlightedColor = normal == accent ? Color.Lerp(accent, Color.white, 0.2f) : buttonHover;
        c.pressedColor = normal == accent ? Color.Lerp(accent, Color.black, 0.2f) : accent;
        c.selectedColor = normal;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;
        b.onClick.AddListener(onClick);
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;   // Enter/Space handled by the tutorial

        text = MakeText(go.transform, "Text", titleFont, fontSize, new Color(0.95f, 0.97f, 1f, 1f));
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 3;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = label;
        Stretch((RectTransform)text.transform);
        return go;
    }

    TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    static RectTransform MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ---------------- Procedural sprites (no extra assets needed) ----------------
    static Sprite MakeOutlineSprite()
    {
        const int size = 64; const float radius = 20f, thick = 5f;
        var tex = NewTex(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - Vector2.one * size * 0.5f;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (size * 0.5f - 1f - radius);
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thick + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(26, 26, 26, 26));
    }

    static Sprite MakeRingSprite() => MakeCircle(128, 7f);
    static Sprite MakeDiscSprite() => MakeCircle(64, 999f);

    static Sprite MakeCircle(int size, float thick)
    {
        var tex = NewTex(size);
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) - r;
                float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thick + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
    }

    static Sprite MakeTriangleSprite()   // points right
    {
        const int size = 64;
        var tex = NewTex(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                float edge = (1f - u) - v;   // inside when > 0
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge * size * 0.5f)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
    }

    static Texture2D NewTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
