using UnityEngine;
using DTerrain;

// Attach to the projectile (same object as Slingshot).
// When it hits DTerrain, it carves a circle and keeps flying,
// so one shot digs a tunnel. Each hit costs one "dig".
[RequireComponent(typeof(Rigidbody2D))]
public class TerrainDigger : MonoBehaviour
{
    public BasicPaintableLayer terrain;     // drag the terrain layer object here
    public int holeRadiusPixels = 16;       // hole size, in terrain texture pixels
    public int maxDigs = 6;                 // how many bites one shot can take
    [Range(0f, 1f)]
    public float speedKeptPerDig = 0.85f;   // how much speed survives each bite

    Rigidbody2D rb;
    Shape hole;
    Vector2 lastVelocity;
    int digsLeft;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hole = Shape.GenerateShapeCircle(holeRadiusPixels);
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
        if (col.collider.GetComponentInParent<BasicPaintableLayer>() != terrain) return;
        if (lastVelocity.magnitude < 1f) return;   // too slow to dig

        digsLeft--;
        Vector2 hit = col.GetContact(0).point;
        DigAt(hit);

        // Keep flying instead of bouncing off.
        SetVelocity(lastVelocity * speedKeptPerDig);
    }

    public void DigAt(Vector2 worldPos)
    {
        Vector3 local = (Vector3)worldPos - terrain.transform.position;
        int px = (int)(local.x * terrain.PPU) - holeRadiusPixels;
        int py = (int)(local.y * terrain.PPU) - holeRadiusPixels;
        if (px < 0 || py < 0) return;   // DTerrain doesn't handle negative coords

        terrain.Paint(new PaintingParameters
        {
            Color = Color.clear,
            Position = new Vector2Int(px, py),
            Shape = hole,
            PaintingMode = PaintingMode.REPLACE_COLOR,
            DestructionMode = DestructionMode.DESTROY
        });
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