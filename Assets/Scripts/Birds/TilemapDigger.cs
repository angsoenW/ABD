using UnityEngine;
using UnityEngine.Tilemaps;

// Tilemap version of TerrainDigger. Attach to the projectile (same object as Slingshot).
// When it hits the terrain Tilemap, it removes the tiles in a circle and keeps flying,
// so one shot digs a tunnel. Tiles on any OTHER tilemap (e.g. "Rock") are never dug.
[RequireComponent(typeof(Rigidbody2D))]
public class TilemapDigger : MonoBehaviour
{
    public Tilemap terrain;                 // the diggable tilemap
    public float holeRadius = 0.6f;         // in world units
    public int maxDigs = 6;                 // how many bites one shot can take
    [Range(0f, 1f)]
    public float speedKeptPerDig = 0.85f;   // how much speed survives each bite

    Rigidbody2D rb;
    Vector2 lastVelocity;
    int digsLeft;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        digsLeft = maxDigs;
    }

    void FixedUpdate()
    {
        lastVelocity = GetVelocity();

        // Refill digs whenever the projectile is back on the slingshot (Kinematic).
        if (rb.bodyType == RigidbodyType2D.Kinematic) digsLeft = maxDigs;
    }

    void OnCollisionEnter2D(Collision2D col) { TryDig(col); }
    void OnCollisionStay2D(Collision2D col) { TryDig(col); }

    void TryDig(Collision2D col)
    {
        if (digsLeft <= 0 || terrain == null) return;
        if (col.collider.GetComponent<Tilemap>() != terrain) return;
        if (lastVelocity.magnitude < 1f) return;    // too slow to dig

        digsLeft--;

        // Aim the hole slightly into the ground, along the flight direction.
        Vector2 hit = col.GetContact(0).point + lastVelocity.normalized * terrain.cellSize.x * 0.5f;
        DigAt(hit);

        // Keep flying instead of bouncing off.
        SetVelocity(lastVelocity * speedKeptPerDig);
    }

    public void DigAt(Vector2 worldPos)
    {
        Vector3Int center = terrain.WorldToCell(worldPos);
        int r = Mathf.CeilToInt(holeRadius / terrain.cellSize.x) + 1;
        float r2 = holeRadius * holeRadius;

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                Vector3Int cell = center + new Vector3Int(dx, dy, 0);
                Vector2 cellCenter = terrain.GetCellCenterWorld(cell);
                if ((cellCenter - worldPos).sqrMagnitude <= r2)
                    terrain.SetTile(cell, null);
            }
        }
    }

    Vector2 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    void SetVelocity(Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}