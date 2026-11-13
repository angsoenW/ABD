using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// One entry in a level's bird set: which bird, and how many of it.
[System.Serializable]
public class BirdStock
{
    public BirdType type;
    [Min(0)] public int count = 1;
}

// Put on the bird (next to Slingshot and TilemapDigger).
// Each level offers the player a SET of birds (e.g. 2 Heavy, 2 Fast, 2 Bomb) and, optionally,
// a Shot Limit lower than the number of birds (e.g. 4), so the player also chooses which birds to skip.
// A tray at the bottom-left shows them; before each shot the player picks one
// by clicking it or pressing 1, 2, 3... The tray is locked while a bird is flying.
// Bomb birds: click while flying to explode, or they explode by themselves shortly after hitting something.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Slingshot))]
[DefaultExecutionOrder(100)]   // set up AFTER Slingshot has placed the bird on the slingshot
public class BirdLoadout : MonoBehaviour
{
    public List<BirdStock> birds = new List<BirdStock>();   // what this level offers the player
    [Min(0)]
    public int shotLimit = 0;                                // shots allowed this level (0 = one per bird offered)
    public bool showTray = true;

    public BirdType Current { get; private set; }
    public int RemainingOf(int index) => remaining != null && index >= 0 && index < remaining.Length ? remaining[index] : 0;

    Rigidbody2D rb;
    Slingshot slingshot;
    TilemapDigger digger;
    SpriteRenderer sprite;
    Vector3 baseScale;
    float baseLaunchPower;

    int[] remaining;
    int currentIndex = -1;
    bool wasFlying;

    bool exploded;
    bool hitSomething;
    float timeSinceHit;
    readonly List<Collider2D> hits = new List<Collider2D>();
    static Sprite circleSprite;

    // Tray UI
    class TrayItem
    {
        public Button button;
        public Image background;
        public TMP_Text label;
    }
    readonly List<TrayItem> trayItems = new List<TrayItem>();
    TMP_Text hintText;
    static readonly Color SlotNormal = new Color(0.10f, 0.13f, 0.21f, 0.88f);
    static readonly Color SlotSelected = new Color(0.24f, 0.56f, 0.95f, 0.95f);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        slingshot = GetComponent<Slingshot>();
        digger = GetComponent<TilemapDigger>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
        baseLaunchPower = slingshot.launchPower;
    }

    void Start()
    {
        remaining = new int[birds.Count];
        int total = 0;
        for (int i = 0; i < birds.Count; i++)
        {
            remaining[i] = birds[i].type != null ? birds[i].count : 0;
            total += remaining[i];
        }
        // e.g. 2 of each of 3 birds (6 offered) with Shot Limit 4: the player picks which 4 to use.
        int shots = shotLimit > 0 ? Mathf.Min(shotLimit, total) : total;
        if (shots > 0) slingshot.maxShots = shots;

        if (showTray && birds.Count > 0) BuildTray();

        int first = FirstAvailable();
        if (first < 0)
        {
            Debug.LogWarning("[BirdLoadout] No birds available: every Count is 0 or every Type is empty. " +
                             "Shooting is disabled until the Birds list is filled in.");
            slingshot.enabled = false;   // never let a plain, unconfigured ball be shot
            RefreshTray();
            return;
        }
        Select(first, force: true);
    }

    void Update()
    {
        bool flying = rb.bodyType == RigidbodyType2D.Dynamic;

        if (flying && !wasFlying)                         // just launched: use up the loaded bird
        {
            if (currentIndex >= 0) remaining[currentIndex]--;
            exploded = false;
            hitSomething = false;
            timeSinceHit = 0f;
            RefreshTray();
        }

        if (!flying && wasFlying)                         // came back: reload
        {
            int next = (currentIndex >= 0 && remaining[currentIndex] > 0) ? currentIndex : FirstAvailable();
            Select(next, force: true);
        }

        if (!flying) HandlePickKeys();

        if (flying && Current != null && Current.explodes && !exploded)
        {
            if (hitSomething) timeSinceHit += Time.deltaTime;
            bool autoTime = Current.autoExplodeDelay > 0f && hitSomething && timeSinceHit >= Current.autoExplodeDelay;
            if (ClickPressed() || autoTime) Explode();
        }

        wasFlying = flying;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic) hitSomething = true;
    }

    // ---------------- Picking ----------------

    int FirstAvailable()
    {
        for (int i = 0; i < remaining.Length; i++)
            if (remaining[i] > 0) return i;
        return -1;
    }

    public void Select(int index) => Select(index, false);

    void Select(int index, bool force)
    {
        if (!force && wasFlying) return;                                  // can't swap mid-flight
        if (index < 0 || index >= birds.Count || remaining[index] <= 0) { RefreshTray(); return; }

        currentIndex = index;
        Apply(birds[index].type);
        RefreshTray();
    }

    void HandlePickKeys()
    {
        if (MouseHeld()) return;                                          // don't swap while aiming
        for (int i = 0; i < birds.Count && i < 9; i++)
            if (NumberPressed(i)) Select(i);
    }

    void Apply(BirdType type)
    {
        Current = type;
        if (type == null) return;

        transform.localScale = baseScale * type.size;
        rb.mass = type.mass;
        rb.gravityScale = type.gravityScale;
        slingshot.launchPower = baseLaunchPower * type.launchPowerMultiplier;
        if (sprite != null) sprite.color = type.color;

        if (digger != null)
        {
            digger.holeRadius = type.holeRadius;
            digger.maxDigs = type.maxDigs;
            digger.speedKeptPerDig = type.speedKeptPerDig;
        }
    }

    // ---------------- Bomb ----------------

    void Explode()
    {
        exploded = true;
        Vector2 center = rb.position;

        if (digger != null)
        {
            float normalRadius = digger.holeRadius;
            digger.holeRadius = Current.explosionRadius;
            digger.DigAt(center);
            digger.holeRadius = normalRadius;
        }

        var filter = new ContactFilter2D();
        filter.NoFilter();
        float reach = Current.explosionRadius * 1.5f;
        int count = Physics2D.OverlapCircle(center, reach, filter, hits);
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = hits[i].attachedRigidbody;
            if (body == null || body == rb || body.bodyType != RigidbodyType2D.Dynamic) continue;

            Vector2 away = body.position - center;
            float falloff = 1f - Mathf.Clamp01(away.magnitude / reach);
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
            body.AddForce(dir * Current.explosionForce * falloff * body.mass, ForceMode2D.Impulse);
        }

        StartCoroutine(BlastEffect(center, Current.explosionRadius));
        slingshot.ResetProjectile();
    }

    IEnumerator BlastEffect(Vector2 center, float radius)
    {
        var go = new GameObject("BlastEffect");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSprite();
        sr.sortingOrder = 20;

        const float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 2f, k) * radius;
            sr.color = new Color(1f, 0.85f, 0.4f, 1f - k);
            yield return null;
        }
        Destroy(go);
    }

    // ---------------- Tray UI ----------------

    void BuildTray()
    {
        var root = new GameObject("BirdTray", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Row of bird slots, bottom-left.
        var row = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(root.transform, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = Vector2.zero;
        rowRt.anchoredPosition = new Vector2(24, 24);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < birds.Count; i++)
        {
            int index = i;
            BirdType type = birds[i].type;
            if (type == null) { trayItems.Add(null); continue; }

            var slot = new GameObject("Slot_" + type.displayName, typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(row.transform, false);
            slot.GetComponent<RectTransform>().sizeDelta = new Vector2(230, 76);
            var bg = slot.GetComponent<Image>();
            bg.color = SlotNormal;
            var button = slot.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select(index));

            var swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatch.transform.SetParent(slot.transform, false);
            var swRt = swatch.GetComponent<RectTransform>();
            swRt.anchorMin = swRt.anchorMax = new Vector2(0f, 0.5f);
            swRt.pivot = new Vector2(0f, 0.5f);
            swRt.anchoredPosition = new Vector2(14, 0);
            swRt.sizeDelta = new Vector2(44, 44) * Mathf.Clamp(type.size, 0.6f, 1.3f);
            var swImg = swatch.GetComponent<Image>();
            swImg.sprite = CircleSprite();
            swImg.color = type.color;
            swImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(slot.transform, false);
            var lRt = labelGo.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(72, 4);
            lRt.offsetMax = new Vector2(-10, -4);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 24;
            label.color = new Color(0.93f, 0.96f, 1f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;

            trayItems.Add(new TrayItem { button = button, background = bg, label = label });
        }

        // Hint line above the tray (used for the bomb).
        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(root.transform, false);
        var hRt = hintGo.GetComponent<RectTransform>();
        hRt.anchorMin = hRt.anchorMax = hRt.pivot = Vector2.zero;
        hRt.anchoredPosition = new Vector2(28, 112);
        hRt.sizeDelta = new Vector2(700, 40);
        hintText = hintGo.GetComponent<TextMeshProUGUI>();
        hintText.fontSize = 24;
        hintText.color = new Color(1f, 0.85f, 0.4f, 1f);
        hintText.raycastTarget = false;

        EnsureEventSystem();
    }

    void RefreshTray()
    {
        bool flying = rb.bodyType == RigidbodyType2D.Dynamic;
        bool noShotsLeft = slingshot.OutOfShots;

        for (int i = 0; i < trayItems.Count; i++)
        {
            var item = trayItems[i];
            if (item == null) continue;

            int left = remaining[i];
            bool usable = left > 0 && !noShotsLeft;
            bool selected = i == currentIndex && usable;
            item.label.text = $"<b>{i + 1}</b>  {birds[i].type.displayName}  ×{left}";
            item.background.color = selected ? SlotSelected : SlotNormal;
            item.button.interactable = usable && !flying;

            var group = item.button.GetComponent<CanvasGroup>();
            if (group == null) group = item.button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = usable ? 1f : 0.35f;
        }

        if (hintText != null)
            hintText.text = noShotsLeft ? ""
                : Current != null && Current.explodes && RemainingOf(currentIndex) > 0
                    ? "Bomb loaded: click mid-air to explode"
                    : "";
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    // ---------------- Helpers ----------------

    static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 20f)));
            }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    bool ClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool MouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    bool NumberPressed(int i)   // i = 0 -> key 1
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Alpha1 + i);
#endif
    }
}