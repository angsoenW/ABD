using System.Collections.Generic;
using UnityEngine;

// Put on the bird (next to Slingshot).
// - Leaves a dotted trail behind the bird while it flies. The trail STAYS after the bird
//   returns, so while aiming the next shot the player can see where the last one went.
// - Shows a faint marker where the player released last time, so they can aim
//   "a bit further back than last time" instead of guessing.
// The trail is cleared when the next shot is launched.
[RequireComponent(typeof(Rigidbody2D))]
public class ShotTrail : MonoBehaviour
{
    [Header("Trail")]
    public Sprite dotSprite;                                 // optional: leave empty for a built-in circle
    public float dotSpacing = 0.35f;                         // world units between dots
    public float dotSize = 0.12f;
    public Color dotColor = new Color(1f, 1f, 1f, 0.6f);
    public int maxDots = 150;
    public int sortingOrder = 10;

    [Header("Last release point")]
    public bool showLastReleasePoint = true;
    public float releaseMarkerSize = 0.4f;
    public Color releaseMarkerColor = new Color(1f, 1f, 1f, 0.3f);

    Rigidbody2D rb;
    bool wasFlying;
    Vector2 lastDotPos;
    int dotsUsed;
    readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    SpriteRenderer releaseMarker;
    bool hasReleasePoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (dotSprite == null) dotSprite = MakeCircleSprite();

        releaseMarker = CreateDot("LastReleaseMarker", releaseMarkerSize, releaseMarkerColor);
        releaseMarker.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        bool flying = rb.bodyType == RigidbodyType2D.Dynamic;

        if (flying && !wasFlying) OnLaunch();
        if (flying) RecordDot();

        // Show the last release point only while aiming.
        if (releaseMarker != null)
            releaseMarker.gameObject.SetActive(showLastReleasePoint && hasReleasePoint && !flying);

        wasFlying = flying;
    }

    void OnLaunch()
    {
        // The bird hasn't moved yet this frame, so its position is where the player let go.
        releaseMarker.transform.position = rb.position;
        hasReleasePoint = true;

        // Clear the previous shot's trail.
        for (int i = 0; i < dotsUsed; i++) dots[i].gameObject.SetActive(false);
        dotsUsed = 0;
        lastDotPos = rb.position;
    }

    void RecordDot()
    {
        if (dotsUsed >= maxDots) return;
        if (Vector2.Distance(rb.position, lastDotPos) < dotSpacing) return;

        SpriteRenderer dot;
        if (dotsUsed < dots.Count)
        {
            dot = dots[dotsUsed];
        }
        else
        {
            dot = CreateDot("TrailDot", dotSize, dotColor);
            dots.Add(dot);
        }

        dot.transform.position = rb.position;
        dot.gameObject.SetActive(true);
        dotsUsed++;
        lastDotPos = rb.position;
    }

    SpriteRenderer CreateDot(string name, float size, Color color)
    {
        // Not a child of the bird, so dots stay where they were dropped.
        var go = new GameObject(name);
        go.transform.localScale = Vector3.one * size;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    // A soft-edged white circle, 1 world unit wide, so no sprite asset is needed.
    static Sprite MakeCircleSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);   // 1 inside, fades over the last pixel
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}