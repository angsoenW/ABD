using UnityEngine;

// Added to the bird automatically by AnalyticsManager.
// Remembers where the FIRST shot of the level first hit something (Metric 4).
[RequireComponent(typeof(Rigidbody2D))]
public class FirstImpactRecorder : MonoBehaviour
{
    public bool HasImpact { get; private set; }
    public Vector2 Point { get; private set; }

    Rigidbody2D rb;
    Slingshot slingshot;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        slingshot = GetComponent<Slingshot>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (HasImpact) return;
        if (rb.bodyType != RigidbodyType2D.Dynamic) return;          // only while flying
        if (slingshot != null && slingshot.ShotsUsed != 1) return;    // only the first shot

        HasImpact = true;
        Point = collision.GetContact(0).point;
    }
}