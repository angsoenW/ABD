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
        s = Mathf.Repeat(s + speed * Time.fixedDeltaTime, LoopLength);
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
                    segs[0].rb.MoveRotation(Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg);
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

        // eyes follow the bird when it is close, otherwise look ahead
        Vector2 headPos = head.position;
        Vector2 lookWorld = head.right;
        if (birdRb != null && Vector2.Distance(birdRb.position, headPos) < lookRange)
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
        if (!harmful) return;
        var b = other.GetComponentInParent<Slingshot>();
        if (b == null) return;
        var rb = b.GetComponent<Rigidbody2D>();
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;   // only a bird in flight
        b.ResetProjectile();
        happyT = 0.7f;                 // pleased little squint
        tongueT = 0f;                  // and a flick
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
