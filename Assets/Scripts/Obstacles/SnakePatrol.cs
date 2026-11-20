using UnityEngine;

// Used by the snakes in Level 1.
// A segmented, slithering snake built from simple round shapes when the game starts:
//   - the head leads, every body bead follows the exact same path (no stretching when it turns),
//   - it patrols a long thin loop: out along one lane, smooth U-turn, back along the other,
//   - a gentle wave runs down the body, the head faces where it is going,
//   - eyes blink and watch the bird, a forked tongue flicks now and then.
// Touching any part of a snake ends the shot (Slingshot.ResetProjectile), like before.
[DisallowMultipleComponent]
public class SnakePatrol : MonoBehaviour
{
    public enum Pattern { Bands, Spots }

    [Header("Patrol path")]
    [Tooltip("World units per second along the path.")]
    [Min(0f)] public float speed = 1.5f;
    [Tooltip("Length of the straight part of the loop.")]
    [Min(0f)] public float patrolDistance = 2.2f;
    [Tooltip("0 = left/right, 90 = up/down.")]
    [Range(-180f, 180f)] public float patrolAngle = 0f;
    [Tooltip("Where on the straight part the head starts (0 = start, 1 = end).")]
    [Range(0f, 1f)] public float startPhase = 0.5f;
    public bool startMovingRight = true;
    [Tooltip("Radius of the U-turn at each end. The two lanes are 2x this apart.")]
    [Min(0.05f)] public float turnRadius = 0.22f;

    [Header("Body")]
    [Range(3, 16)] public int segments = 8;
    [Min(0.05f)] public float segmentSpacing = 0.18f;
    [Min(0.05f)] public float headRadius = 0.22f;
    [Min(0.02f)] public float bodyRadius = 0.16f;
    [Min(0.02f)] public float tailRadius = 0.07f;

    [Header("Wiggle")]
    public float wiggleAmplitude = 0.06f;
    [Tooltip("Waves per second.")]
    public float wiggleSpeed = 2.2f;
    [Tooltip("Length of one wave along the body, in world units.")]
    [Min(0.1f)] public float wiggleWavelength = 1.3f;

    [Header("Look")]
    public Sprite discSprite;                       // plain white circle, 1 unit across
    public Sprite tongueSprite;                     // forked tongue, pivot on its left edge
    public Pattern pattern = Pattern.Bands;
    public Color outlineColor = new Color32(0x1E, 0x3D, 0x22, 0xFF);
    public Color bodyColor = new Color32(0x63, 0xC3, 0x4E, 0xFF);
    public Color patternColor = new Color32(0x3E, 0x95, 0x3B, 0xFF);
    public Color highlightColor = new Color32(0xA9, 0xE6, 0x7C, 0xFF);
    public Color tongueColor = new Color32(0xE8, 0x44, 0x5A, 0xFF);
    [Min(0f)] public float outlineWidth = 0.035f;
    [Tooltip("Lowest sorting order used. The snake uses this .. this + 4.")]
    public int sortingOrder = 2;

    [Header("Personality")]
    [Min(0.03f)] public float eyeSize = 0.085f;
    [Tooltip("Eyes follow the bird when it is closer than this.")]
    [Min(0f)] public float lookRange = 6f;
    public Vector2 blinkInterval = new Vector2(2.5f, 5f);
    public Vector2 tongueInterval = new Vector2(1.4f, 3f);

    [Header("Eat the bird")]
    [Tooltip("Play the chomp animation when the bird hits the snake. Off = the shot just ends.")]
    public bool eatBird = true;
    [Tooltip("Whole sequence: react, open wide, swallow, celebrate. Seconds.")]
    [Range(0.6f, 1.2f)] public float eatDuration = 0.95f;
    [Tooltip("Camera shake strength on impact (world units). 0 = none.")]
    [Min(0f)] public float eatShake = 0.12f;
    public Color mouthColor = new Color32(0x7A, 0x1F, 0x2E, 0xFF);

    // ---------- runtime ----------
    class Segment
    {
        public Transform root;
        public Rigidbody2D rb;
        public CircleCollider2D col;
        public float radius;
    }

    Segment[] segs;
    Transform[] eyes, pupils;
    Transform tongue;
    Rigidbody2D birdRb;
    Slingshot bird;
    Vector2 center;
    float s;                 // head distance along the loop
    float blinkTimer, blinkT = -1f;
    float tongueTimer, tongueT = -1f;
    float happyT;
    bool harmful = true;
    bool built;

    // eat-the-bird animation
    Transform mouth;
    bool eating, celebrated;
    float eatT;
    float speedScale = 1f;       // slows the patrol while chomping, eases back to 1
    float lookBlend;             // how much the head turns toward the bird
    Vector2 hitPos, eatFocus;
    float ghostStartRot;
    Transform ghost;
    Slingshot eaten;
    Rigidbody2D eatenRb;
    SpriteRenderer[] eatenSprites;
    bool[] eatenSpriteOn;
    Collider2D[] eatenCols;
    bool[] eatenColOn;
    Camera shakeCam;
    Vector3 shakeBase;
    float shakeT;
    Transform fxRoot;
    SpriteRenderer[] bits;
    Vector2[] bitVel;
    float[] bitAge;
    SpriteRenderer flash;
    float flashAge = 1f;
    const float BitLife = 0.5f, FlashLife = 0.28f, ShakeDur = 0.28f;

    public int SegmentCount => segments;
    public Vector2 Axis => Dir(patrolAngle);
    public Vector2 Center => built ? center : (Vector2)transform.position;
    float LoopLength => 2f * patrolDistance + 2f * Mathf.PI * turnRadius;

    static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

    // ---------- path ----------
    // Local loop: lane 1 at y=-r going +x, U-turn around (+D/2,0), lane 2 at y=+r going -x, U-turn around (-D/2,0).
    void PathPoint(float dist, out Vector2 local)
    {
        float D = patrolDistance, r = turnRadius, arc = Mathf.PI * r;
        float L = LoopLength;
        dist = Mathf.Repeat(dist, L);
        if (dist < D) { local = new Vector2(-D * 0.5f + dist, -r); return; }
        dist -= D;
        if (dist < arc) { float a = -Mathf.PI * 0.5f + dist / r; local = new Vector2(D * 0.5f + Mathf.Cos(a) * r, Mathf.Sin(a) * r); return; }
        dist -= arc;
        if (dist < D) { local = new Vector2(D * 0.5f - dist, r); return; }
        dist -= D;
        { float a = Mathf.PI * 0.5f + dist / r; local = new Vector2(-D * 0.5f + Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
    }

    Vector2 ToWorld(Vector2 local)
    {
        Vector2 u = Axis, n = new Vector2(-u.y, u.x);
        return Center + u * local.x + n * local.y;
    }

    Vector2 PathWorld(float dist) { PathPoint(dist, out var l); return ToWorld(l); }

    float StartDistance()
    {
        float D = patrolDistance;
        return startMovingRight ? startPhase * D : D + Mathf.PI * turnRadius + (1f - startPhase) * D;
    }

    public float SegmentRadius(int i)
    {
        if (i == 0) return headRadius;
        float t = segments <= 2 ? 1f : (i - 1) / (float)(segments - 2);
        // body keeps its thickness for the first half, then tapers to the tail
        return Mathf.Lerp(bodyRadius, tailRadius, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t)));
    }

    // Where segment i will be 'seconds' from now (no wiggle) - used for warnings and testing.
    public Vector2 PredictPoint(float seconds, int i = 0)
    {
        float baseS = built ? s : StartDistance();
        return PathWorld(baseS + speed * Mathf.Max(0f, seconds) - i * segmentSpacing);
    }

    public Vector2 SegmentPosition(int i) => built ? segs[i].rb.position : PredictPoint(0f, i);

    public void SetHarmful(bool on)
    {
        if (harmful == on || !built) { harmful = on; return; }
        harmful = on;
        foreach (var sg in segs) sg.col.enabled = on;
    }

    // ---------- build ----------
    void Awake()
    {
        center = transform.position;
        s = StartDistance();
        // the old single-sprite setup is not used any more
        var oldSr = GetComponent<SpriteRenderer>(); if (oldSr != null) oldSr.enabled = false;
        var oldCol = GetComponent<Collider2D>(); if (oldCol != null) oldCol.enabled = false;
        Build();
        bird = FindAnyObjectByType<Slingshot>();
        if (bird != null) birdRb = bird.GetComponent<Rigidbody2D>();
        blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        tongueTimer = Random.Range(0.3f, tongueInterval.y);
        Place(0f);
    }

    void Build()
    {
        var holder = new GameObject("Body").transform;
        holder.SetParent(transform, false);
        holder.localRotation = Quaternion.identity;

        segs = new Segment[segments];
        for (int i = 0; i < segments; i++)
        {
            float r = SegmentRadius(i);
            var go = new GameObject(i == 0 ? "Head" : "Segment " + i);
            go.transform.SetParent(holder, false);
            go.layer = gameObject.layer;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = r;
            col.enabled = harmful;
            go.AddComponent<SnakeHitbox>().snake = this;

            // z keeps the head-end in front of the tail-end inside the same sorting order
            float z = i * 0.002f;
            Disc(go.transform, "Outline", r + outlineWidth, outlineColor, sortingOrder, z + 0.0015f);
            Color fill = bodyColor;
            if (pattern == Pattern.Bands && i > 0 && i % 2 == 0) fill = patternColor;
            Disc(go.transform, "Fill", r, fill, sortingOrder + 1, z);
            if (i > 0)
            {
                Disc(go.transform, "Highlight", r * 0.38f, highlightColor, sortingOrder + 1, z - 0.0005f, new Vector2(0f, 0f));
                if (pattern == Pattern.Spots && i % 2 == 1)
                    Disc(go.transform, "Spot", r * 0.32f, patternColor, sortingOrder + 1, z - 0.0007f, new Vector2(0f, (i % 4 == 1 ? 1f : -1f) * r * 0.45f));
            }
            segs[i] = new Segment { root = go.transform, rb = rb, col = col, radius = r };
        }

        // face
        var head = segs[0].root;
        eyes = new Transform[2];
        pupils = new Transform[2];
        for (int k = 0; k < 2; k++)
        {
            float side = k == 0 ? 1f : -1f;
            var eye = new GameObject(k == 0 ? "Eye L" : "Eye R").transform;
            eye.SetParent(head, false);
            eye.localPosition = new Vector3(headRadius * 0.25f, side * headRadius * 0.52f, -0.004f);
            Disc(eye, "Outline", eyeSize + outlineWidth * 0.8f, outlineColor, sortingOrder + 2, 0.0005f);
            Disc(eye, "White", eyeSize, Color.white, sortingOrder + 2, 0f);
            var pupil = Disc(eye, "Pupil", eyeSize * 0.55f, new Color32(0x16, 0x1A, 0x16, 0xFF), sortingOrder + 3, 0f);
            Disc(pupil, "Glint", 0.38f, Color.white, sortingOrder + 4, 0f, new Vector2(0.18f, 0.18f), true);
            eyes[k] = eye;
            pupils[k] = pupil;
        }

        if (tongueSprite != null)
        {
            var t = new GameObject("Tongue").transform;
            t.SetParent(head, false);
            t.localPosition = new Vector3(headRadius * 0.75f, 0f, 0.003f);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = tongueSprite;
            sr.color = tongueColor;
            sr.sortingOrder = sortingOrder;
            t.localScale = new Vector3(0f, 1f, 1f);
            tongue = t;
        }
        // mouth: closed (zero size) until the snake eats the bird
        if (discSprite != null)
        {
            mouth = new GameObject("Mouth").transform;
            mouth.SetParent(head, false);
            mouth.localPosition = new Vector3(headRadius * 0.6f, 0f, -0.003f);
            mouth.localScale = new Vector3(0f, 0f, 1f);
            Disc(mouth, "Outline", 1.3f, outlineColor, sortingOrder + 1, 0.0005f, default, true);
            Disc(mouth, "Inside", 1f, mouthColor, sortingOrder + 1, 0f, default, true);
        }
        built = true;
    }

    // A circle of the given world radius. 'local' disc = sized relative to its parent instead.
    Transform Disc(Transform parent, string name, float radius, Color color, int order, float z, Vector2 offset = default, bool local = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, z);
        float d = local ? radius : radius * 2f;
        go.transform.localScale = new Vector3(d, d, 1f);
        if (local) go.transform.localPosition = new Vector3(offset.x, offset.y, z);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = discSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    // ---------- motion ----------
    void FixedUpdate()
    {
        if (!built) return;
        s = Mathf.Repeat(s + speed * speedScale * Time.fixedDeltaTime, LoopLength);
        Place(Time.time);
    }

    void Place(float time)
    {
        Vector2 prev = default;
        for (int i = 0; i < segs.Length; i++)
        {
            float d = s - i * segmentSpacing;
            Vector2 p = PathWorld(d);
            Vector2 ahead = PathWorld(d + 0.05f);
            Vector2 tan = (ahead - p).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x);

            // wave along the body: small at the head, full in the middle, softer at the tail
            float along = i * segmentSpacing;
            float body01 = segments <= 1 ? 0f : i / (float)(segments - 1);
            float amp = wiggleAmplitude * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(body01 * 3f)) * Mathf.Lerp(1f, 0.7f, body01);
            float wave = Mathf.Sin(2f * Mathf.PI * (wiggleSpeed * time - along / wiggleWavelength));
            p += nrm * (amp * wave);

            if (Application.isPlaying) segs[i].rb.MovePosition(p);
            else segs[i].root.position = p;
            if (i == 1)
            {
                // head points away from the first body bead, so it turns smoothly with the body
                Vector2 look = (prev - p);
                if (look.sqrMagnitude > 1e-6f)
                {
                    float ang = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
                    if (lookBlend > 0.001f)
                    {
                        // while eating, the head swings round to face the bird
                        Vector2 to = eatFocus - prev;
                        if (to.sqrMagnitude > 1e-4f)
                            ang = Mathf.LerpAngle(ang, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, lookBlend);
                    }
                    segs[0].rb.MoveRotation(ang);
                }
            }
            prev = p;
        }
    }

    // ---------- personality ----------
    void Update()
    {
        if (!built) return;
        float dt = Time.deltaTime;
        var head = segs[0].root;

        if (eating) TickEat(dt);
        TickFx(dt);

        // eyes follow the bird when it is close, otherwise look ahead
        Vector2 headPos = head.position;
        Vector2 lookWorld = head.right;
        if (eating && ghost != null && ghost.gameObject.activeSelf)
            lookWorld = ((Vector2)ghost.position - headPos).normalized;
        else if (birdRb != null && Vector2.Distance(birdRb.position, headPos) < lookRange)
            lookWorld = (birdRb.position - headPos).normalized;
        Vector2 lookLocal = head.InverseTransformDirection(lookWorld);
        for (int k = 0; k < 2; k++)
            pupils[k].localPosition = Vector3.Lerp(pupils[k].localPosition,
                new Vector3(lookLocal.x * eyeSize * 0.4f, lookLocal.y * eyeSize * 0.4f, -0.0005f), 12f * dt);

        // blink / happy squint (after catching the bird)
        float squash = 1f;
        if (happyT > 0f) { happyT -= dt; squash = 0.25f; }
        else
        {
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blinkT = 0f; blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y); }
            if (blinkT >= 0f)
            {
                blinkT += dt;
                squash = Mathf.Lerp(1f, 0.1f, Mathf.Sin(Mathf.Clamp01(blinkT / 0.16f) * Mathf.PI));
                if (blinkT > 0.16f) blinkT = -1f;
            }
        }
        for (int k = 0; k < 2; k++) eyes[k].localScale = new Vector3(squash, 1f, 1f);

        // tongue flick: out, tiny wobble, back in
        if (tongue != null)
        {
            tongueTimer -= dt;
            if (tongueTimer <= 0f) { tongueT = 0f; tongueTimer = Random.Range(tongueInterval.x, tongueInterval.y); }
            float ext = 0f;
            if (tongueT >= 0f)
            {
                tongueT += dt;
                const float total = 0.38f;
                float t = tongueT / total;
                ext = t < 0.25f ? t / 0.25f : t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                if (tongueT > total) tongueT = -1f;
            }
            float wobble = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            tongue.localScale = new Vector3(Mathf.Clamp01(ext) * headRadius * 1.6f, headRadius * 1.6f * wobble, 1f);
        }
    }

    // Called by every body bead's trigger.
    public void OnTouch(Collider2D other)
    {
        if (!harmful || eating) return;
        var b = other.GetComponentInParent<Slingshot>();
        if (b == null) return;
        var rb = b.GetComponent<Rigidbody2D>();
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;   // only a bird in flight
        // BeginEat makes the bird kinematic at once, so no other bead or snake can catch it twice.
        if (eatBird && discSprite != null && mouth != null) { BeginEat(b, rb); return; }
        b.ResetProjectile();
        happyT = 0.7f;                 // pleased little squint
        tongueT = 0f;                  // and a flick
    }

    // ---------- eat the bird ----------
    // Timeline (fractions of eatDuration):
    //   0.00-0.15  react: impact squash + burst + shake, head swings round, mouth opens, patrol slows
    //   0.15-0.55  the bird is sucked into the mouth, spinning and shrinking
    //   0.55-0.75  two quick chomps (the mouth ends closed)
    //   0.70-0.95  a gulp bulge runs down the body
    //   0.75-1.00  happy bounce + squint + tongue flick, patrol speed eases back up
    // The real bird is frozen and hidden meanwhile (its collider off, so it never blocks water) and a
    // copy of its sprite plays the animation; when it ends the bird goes back to the slingshot exactly
    // as ResetProjectile always did. Shots used are never touched.
    void BeginEat(Slingshot b, Rigidbody2D rb)
    {
        eating = true;
        celebrated = false;
        eatT = 0f;
        eaten = b;
        eatenRb = rb;
        hitPos = rb.position;
        eatFocus = hitPos;

        // freeze the bird where it hit (kinematic = no longer "in flight", so nothing else reacts to it)
        rb.bodyType = RigidbodyType2D.Kinematic;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif
        rb.angularVelocity = 0f;

        eatenCols = b.GetComponentsInChildren<Collider2D>();
        eatenColOn = new bool[eatenCols.Length];
        for (int i = 0; i < eatenCols.Length; i++) { eatenColOn[i] = eatenCols[i].enabled; eatenCols[i].enabled = false; }

        // a copy of the bird's sprite plays the animation
        eatenSprites = b.GetComponentsInChildren<SpriteRenderer>();
        eatenSpriteOn = new bool[eatenSprites.Length];
        var head = segs[0].root;
        int layerId = head.GetComponentInChildren<SpriteRenderer>().sortingLayerID;
        ghostStartRot = b.transform.eulerAngles.z;
        ghost = new GameObject("Bird (being eaten)").transform;
        ghost.position = new Vector3(hitPos.x, hitPos.y, head.position.z - 0.004f);
        ghost.rotation = Quaternion.Euler(0f, 0f, ghostStartRot);
        Color birdColor = Color.white;
        int n = 0;
        for (int i = 0; i < eatenSprites.Length; i++)
        {
            var sr = eatenSprites[i];
            eatenSpriteOn[i] = sr.enabled;
            if (!sr.enabled || sr.sprite == null) continue;
            if (n == 0) birdColor = sr.color;
            var g = new GameObject("Sprite " + n).transform;
            g.SetParent(ghost, false);
            g.localPosition = new Vector3(0f, 0f, -n * 0.0001f);
            g.localScale = sr.transform.lossyScale;
            var gs = g.gameObject.AddComponent<SpriteRenderer>();
            gs.sprite = sr.sprite;
            gs.color = sr.color;
            gs.flipX = sr.flipX;
            gs.flipY = sr.flipY;
            gs.sharedMaterial = sr.sharedMaterial;
            gs.sortingLayerID = layerId;
            gs.sortingOrder = sortingOrder + 1;     // over the head and mouth, under the eyes
            n++;
            sr.enabled = false;
        }

        SpawnImpact(hitPos, birdColor);

        if (eatShake > 0f && Camera.main != null)
        {
            shakeCam = Camera.main;
            if (shakeT <= 0f) shakeBase = shakeCam.transform.localPosition;
            shakeT = ShakeDur;
        }
    }

    void TickEat(float dt)
    {
        eatT += dt;
        float u = Mathf.Clamp01(eatT / Mathf.Max(0.5f, eatDuration));
        var head = segs[0].root;

        // patrol slows right down, then eases back to full speed
        speedScale = u < 0.15f ? Mathf.SmoothStep(1f, 0.12f, u / 0.15f)
                   : u < 0.7f ? 0.12f
                   : Mathf.SmoothStep(0.12f, 1f, (u - 0.7f) / 0.3f);

        // head swings toward the bird, then back along the path
        lookBlend = u < 0.15f ? Mathf.SmoothStep(0f, 0.85f, u / 0.15f)
                  : u < 0.75f ? 0.85f
                  : Mathf.SmoothStep(0.85f, 0f, (u - 0.75f) / 0.25f);

        // mouth: opens, stays wide while the bird goes in, then two chomps and shut
        float o;
        if (u < 0.15f) o = Mathf.SmoothStep(0f, 1f, u / 0.15f);
        else if (u < 0.55f) o = 1f;
        else if (u < 0.75f) o = 0.5f + 0.5f * Mathf.Cos(3f * Mathf.PI * (u - 0.55f) / 0.2f);
        else o = 0f;
        mouth.localScale = new Vector3(headRadius * o, headRadius * 1.25f * o, 1f);

        // head squash and stretch
        float sx = 1f, sy = 1f;
        if (u < 0.15f) { float k = Mathf.Sin(Mathf.PI * u / 0.15f); sx = 1f + 0.3f * k; sy = 1f - 0.15f * k; }
        else if (u >= 0.55f && u < 0.75f) { float c = 1f - o; sx = 1f - 0.12f * c; sy = 1f + 0.12f * c; }
        else if (u >= 0.75f)
        {
            float k = (u - 0.75f) / 0.25f;
            float bnc = Mathf.Sin(4f * Mathf.PI * k) * (1f - k) * 0.18f;
            sx = 1f - bnc; sy = 1f + bnc;
        }
        head.localScale = new Vector3(sx, sy, 1f);

        // gulp bulge travelling down the body
        for (int i = 1; i < segs.Length; i++)
        {
            float k = Mathf.Clamp01((u - 0.7f - i * 0.022f) / 0.1f);
            float p = 1f + 0.35f * Mathf.Sin(Mathf.PI * k);
            segs[i].root.localScale = new Vector3(p, p, 1f);
        }

        // the bird copy: impact squash, then sucked into the mouth
        if (ghost != null)
        {
            float k = Mathf.Clamp01((u - 0.15f) / 0.4f);
            float e = k * k;
            Vector2 pos = Vector2.Lerp(hitPos, mouth.position, e);
            float sc = Mathf.Lerp(1f, 0.05f, e);
            float gx = 1f, gy = 1f;
            if (u < 0.15f) { float q = Mathf.Sin(Mathf.PI * u / 0.15f); gx = 1f + 0.3f * q; gy = 1f - 0.25f * q; }
            ghost.position = new Vector3(pos.x, pos.y, head.position.z - 0.004f);
            ghost.localScale = new Vector3(sc * gx, sc * gy, 1f);
            ghost.rotation = Quaternion.Euler(0f, 0f, ghostStartRot + 540f * e);
            if (k >= 1f && ghost.gameObject.activeSelf) ghost.gameObject.SetActive(false);
        }

        if (!celebrated && u >= 0.75f)
        {
            celebrated = true;
            happyT = 0.45f;     // happy squint
            tongueT = 0f;       // and a flick
        }

        if (u >= 1f) FinishEat();
    }

    void FinishEat()
    {
        RestoreBird();
        ResetEatVisuals();
        // the shot ends exactly as before: the bird returns to the slingshot
        // (unless it was already launched again, e.g. after an early auto-return)
        if (eaten != null && eatenRb != null && eatenRb.bodyType != RigidbodyType2D.Dynamic)
            eaten.ResetProjectile();
        eaten = null;
        eatenRb = null;
    }

    // Back to the normal patrol look: full speed, head straight, mouth shut, no squash.
    void ResetEatVisuals()
    {
        eating = false;
        speedScale = 1f;
        lookBlend = 0f;
        if (!built) return;
        for (int i = 0; i < segs.Length; i++) segs[i].root.localScale = Vector3.one;
        if (mouth != null) mouth.localScale = new Vector3(0f, 0f, 1f);
    }

    // Puts the real bird's sprite and colliders back and removes the animated copy.
    void RestoreBird()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
        if (eatenSprites != null)
            for (int i = 0; i < eatenSprites.Length; i++)
                if (eatenSprites[i] != null) eatenSprites[i].enabled = eatenSpriteOn[i];
        if (eatenCols != null)
            for (int i = 0; i < eatenCols.Length; i++)
                if (eatenCols[i] != null) eatenCols[i].enabled = eatenColOn[i];
        eatenSprites = null;
        eatenCols = null;
    }

    // Burst of bits plus a quick flash where the bird hit.
    void SpawnImpact(Vector2 at, Color birdColor)
    {
        int fxOrder = Mathf.Max(sortingOrder + 5, 8);
        if (fxRoot == null)
        {
            fxRoot = new GameObject(name + " - Eat FX").transform;   // not parented: stays put while the snake moves
            bits = new SpriteRenderer[12];
            bitVel = new Vector2[bits.Length];
            bitAge = new float[bits.Length];
            for (int i = 0; i < bits.Length; i++)
            {
                var go = new GameObject("Bit");
                go.transform.SetParent(fxRoot, false);
                bits[i] = go.AddComponent<SpriteRenderer>();
                bits[i].sprite = discSprite;
                bits[i].sortingOrder = fxOrder;
                bits[i].enabled = false;
                bitAge[i] = BitLife;
            }
            var f = new GameObject("Flash");
            f.transform.SetParent(fxRoot, false);
            flash = f.AddComponent<SpriteRenderer>();
            flash.sprite = discSprite;
            flash.sortingOrder = fxOrder - 1;
            flash.enabled = false;
        }

        Color warm = new Color32(0xFF, 0xD9, 0x4D, 0xFF);
        for (int i = 0; i < bits.Length; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            bitVel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(2f, 4.5f) + Vector2.up * 1.2f;
            bitAge[i] = 0f;
            float size = Random.Range(0.07f, 0.16f);
            bits[i].transform.position = new Vector3(at.x, at.y, 0f);
            bits[i].transform.localScale = new Vector3(size, size, 1f);
            bits[i].color = i % 3 == 0 ? birdColor : i % 3 == 1 ? Color.white : warm;
            bits[i].enabled = true;
        }
        flashAge = 0f;
        flash.transform.position = new Vector3(at.x, at.y, 0f);
    }

    void TickFx(float dt)
    {
        if (shakeCam != null && shakeT > 0f)
        {
            shakeT -= dt;
            float f = Mathf.Max(0f, shakeT) / ShakeDur;
            shakeCam.transform.localPosition = shakeT > 0f
                ? shakeBase + (Vector3)(Random.insideUnitCircle * eatShake * f)
                : shakeBase;
        }

        if (fxRoot == null) return;
        for (int i = 0; i < bits.Length; i++)
        {
            if (!bits[i].enabled) continue;
            bitAge[i] += dt;
            float t = bitAge[i] / BitLife;
            if (t >= 1f) { bits[i].enabled = false; continue; }
            bitVel[i] += Physics2D.gravity * 0.5f * dt;
            bits[i].transform.position += (Vector3)(bitVel[i] * dt);
            var c = bits[i].color; c.a = 1f - t * t; bits[i].color = c;
        }
        flashAge += dt;
        float ft = flashAge / FlashLife;
        flash.enabled = ft < 1f;
        if (ft < 1f)
        {
            float sz = Mathf.Lerp(0.3f, 1.1f, 1f - (1f - ft) * (1f - ft));
            flash.transform.localScale = new Vector3(sz, sz, 1f);
            flash.color = new Color(1f, 1f, 1f, 0.8f * (1f - ft));
        }
    }

    void OnDisable()
    {
        if (eating)
        {
            RestoreBird();
            ResetEatVisuals();
            // bird is visible again; the slingshot's own auto-return brings it home
            // (no ResetProjectile here: the anchor may already be gone during scene teardown)
            eaten = null;
            eatenRb = null;
        }
        if (shakeCam != null && shakeT > 0f) { shakeCam.transform.localPosition = shakeBase; shakeT = 0f; }
    }

    void OnDestroy()
    {
        if (fxRoot != null) Destroy(fxRoot.gameObject);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        int n = 48;
        Vector2 last = PathWorld(0f);
        for (int i = 1; i <= n; i++)
        {
            Vector2 p = PathWorld(LoopLength * i / n);
            Gizmos.DrawLine(last, p);
            last = p;
        }
        Gizmos.DrawWireSphere(PredictPoint(0f), headRadius);
    }
}
