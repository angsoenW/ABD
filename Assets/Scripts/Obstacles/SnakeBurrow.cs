using UnityEngine;
using UnityEngine.Tilemaps;

// Used by the snakes in Level 1. Add next to SnakePatrol to make a snake that travels through the mud.
// - Give the snake a sorting order below the mud tilemap so the mud hides it; it only shows in
//   open space (cavities and tunnels the player digs).
// - While every body bead is inside mud it is harmless.
// - Before it breaks into open space, a ripple ring and little dirt chunks pop out at that spot.
//   It only becomes dangerous after that warning has played for 'warningTime' seconds -
//   also when a shot digs straight into it.
[RequireComponent(typeof(SnakePatrol))]
public class SnakeBurrow : MonoBehaviour
{
    public Tilemap mud;                         // the diggable dirt tilemap
    [Min(0.1f)] public float warningTime = 1f;

    [Header("Emerge effect")]
    public Sprite ringSprite;
    public Sprite discSprite;
    public Color rippleColor = new Color32(0xF3, 0xD9, 0xA4, 0xFF);
    public Color dirtColorA = new Color32(0xD9, 0xA0, 0x66, 0xFF);   // light enough to read on mud and in dark tunnels
    public Color dirtColorB = new Color32(0xA8, 0x70, 0x3E, 0xFF);
    public int fxSortingOrder = 7;

    SnakePatrol snake;
    float armTimer;
    Vector2 warnPoint;

    // effect objects
    Transform fxRoot;
    SpriteRenderer[] rings;
    float[] ringAge;
    SpriteRenderer[] chunks;
    Vector2[] chunkVel;
    float[] chunkAge;
    float spawnTimer, ringTimer;
    int nextChunk, nextRing;
    const float ChunkLife = 0.45f, RingLife = 0.55f;

    public bool Emerged { get; private set; }   // part of the body is in open space
    public bool Armed { get; private set; }     // emerged and dangerous
    public bool Warning { get; private set; }   // emerge effect playing

    void Awake()
    {
        snake = GetComponent<SnakePatrol>();
        snake.SetHarmful(false);
        BuildFx();
    }

    void BuildFx()
    {
        fxRoot = new GameObject(name + " - Emerge FX").transform;   // not parented: stays put while the snake moves
        rings = new SpriteRenderer[3];
        ringAge = new float[3];
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = NewSprite("Ripple", ringSprite, rippleColor);
            ringAge[i] = RingLife;
        }
        chunks = new SpriteRenderer[14];
        chunkVel = new Vector2[chunks.Length];
        chunkAge = new float[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i] = NewSprite("Dirt", discSprite, i % 2 == 0 ? dirtColorA : dirtColorB);
            chunkAge[i] = ChunkLife;
        }
    }

    SpriteRenderer NewSprite(string n, Sprite sp, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(fxRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = c;
        sr.sortingOrder = fxSortingOrder;
        sr.enabled = false;
        return sr;
    }

    void FixedUpdate()
    {
        bool open = AnyBeadOpen(out Vector2 openNow);
        Vector2 soonAt = default;
        bool soon = false;
        if (!open)
        {
            for (float t = 0.05f; t <= warningTime; t += 0.05f)
            {
                Vector2 p = snake.PredictPoint(t, 0);
                if (IsOpen(p)) { soonAt = p; soon = true; break; }
            }
        }

        if (open || soon) armTimer += Time.fixedDeltaTime;
        else armTimer = 0f;

        Emerged = open;
        Armed = open && armTimer >= warningTime;
        snake.SetHarmful(Armed);
        Warning = soon || (open && !Armed);
        if (Warning) warnPoint = soon ? soonAt : openNow;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Warning)
        {
            // ripple rings, one after another
            ringTimer -= dt;
            if (ringTimer <= 0f)
            {
                ringTimer = 0.22f;
                ringAge[nextRing] = 0f;
                rings[nextRing].transform.position = new Vector3(warnPoint.x, warnPoint.y, 0f);
                nextRing = (nextRing + 1) % rings.Length;
            }
            // little dirt chunks hopping out
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = 0.07f;
                for (int k = 0; k < 2; k++)
                {
                    int i = nextChunk; nextChunk = (nextChunk + 1) % chunks.Length;
                    chunkAge[i] = 0f;
                    chunkVel[i] = new Vector2(Random.Range(-1.3f, 1.3f), Random.Range(1.4f, 2.6f));
                    float size = Random.Range(0.08f, 0.14f);
                    chunks[i].transform.localScale = new Vector3(size, size, 1f);
                    chunks[i].transform.position = new Vector3(warnPoint.x + Random.Range(-0.12f, 0.12f), warnPoint.y + Random.Range(-0.05f, 0.05f), 0f);
                }
            }
        }

        for (int i = 0; i < rings.Length; i++)
        {
            ringAge[i] += dt;
            float t = ringAge[i] / RingLife;
            bool on = t < 1f;
            rings[i].enabled = on;
            if (!on) continue;
            float sz = Mathf.Lerp(0.3f, 1.15f, 1f - (1f - t) * (1f - t));
            rings[i].transform.localScale = new Vector3(sz, sz * 0.75f, 1f);
            var c = rippleColor; c.a = 0.95f * (1f - t * t); rings[i].color = c;
        }
        for (int i = 0; i < chunks.Length; i++)
        {
            chunkAge[i] += dt;
            bool on = chunkAge[i] < ChunkLife;
            chunks[i].enabled = on;
            if (!on) continue;
            chunkVel[i] += Physics2D.gravity * dt;
            chunks[i].transform.position += (Vector3)(chunkVel[i] * dt);
            var c = chunks[i].color; c.a = 1f - chunkAge[i] / ChunkLife; chunks[i].color = c;
        }
    }

    void OnDestroy()
    {
        if (fxRoot != null) Destroy(fxRoot.gameObject);
    }

    bool IsOpen(Vector2 p) => mud == null || !mud.HasTile(mud.WorldToCell(p));

    bool AnyBeadOpen(out Vector2 at)
    {
        for (int i = 0; i < snake.SegmentCount; i++)
        {
            Vector2 p = snake.SegmentPosition(i);
            if (IsOpen(p)) { at = p; return true; }
        }
        at = snake.SegmentPosition(0);
        return false;
    }
}
