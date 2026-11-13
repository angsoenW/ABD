using System.Collections.Generic;
using UnityEngine;

// Put on the bird (next to Slingshot). Use together with turning OFF Bird <-> Water collisions
// in Project Settings > Physics 2D > Layer Collision Matrix.
// Instead of bouncing off drops like solid balls, the bird now:
//   - pushes nearby drops along with it and splashes them outward,
//   - gets slowed down a little while it is inside water (like real drag).
[RequireComponent(typeof(Rigidbody2D))]
public class WaterDisturber : MonoBehaviour
{
    public LayerMask waterLayer;                  // the layer the drops' colliders are on
    public float radius = 0.45f;                  // how far around the bird drops are affected

    [Header("Effect on the water")]
    public float carry = 0.35f;                   // how much of the bird's motion drops pick up
    public float splash = 2.5f;                   // outward push away from the bird
    public float maxDropSpeed = 9f;               // keeps drops from being fired through walls

    [Header("Effect on the bird")]
    [Range(0f, 5f)]
    public float waterDrag = 1.5f;                // slowdown per second when fully in water
    public int dropsForFullDrag = 12;             // how many drops around it count as "fully in water"

    Rigidbody2D rb;
    ContactFilter2D filter;
    readonly List<Collider2D> hits = new List<Collider2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        filter = new ContactFilter2D();
        filter.SetLayerMask(waterLayer);
        filter.useTriggers = true;
    }

    void FixedUpdate()
    {
        if (rb.bodyType != RigidbodyType2D.Dynamic) return;    // only while flying

        int count = Physics2D.OverlapCircle(rb.position, radius, filter, hits);
        if (count == 0) return;

        Vector2 birdVel = GetVelocity(rb);
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < count; i++)
        {
            Rigidbody2D drop = hits[i].attachedRigidbody;
            if (drop == null || drop == rb) continue;

            Vector2 away = drop.position - rb.position;
            float closeness = 1f - Mathf.Clamp01(away.magnitude / radius);   // 1 at the bird, 0 at the edge
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;

            Vector2 v = GetVelocity(drop);
            v = Vector2.Lerp(v, birdVel, carry * closeness);                   // dragged along
            v += dir * splash * closeness * dt * 10f;                          // pushed aside
            SetVelocity(drop, Vector2.ClampMagnitude(v, maxDropSpeed));
        }

        // Slow the bird down in proportion to how much water it is in.
        float amount = Mathf.Clamp01((float)count / Mathf.Max(1, dropsForFullDrag));
        SetVelocity(rb, birdVel * Mathf.Clamp01(1f - waterDrag * amount * dt));
    }

    static Vector2 GetVelocity(Rigidbody2D body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }

    static void SetVelocity(Rigidbody2D body, Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = v;
#else
        body.velocity = v;
#endif
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
