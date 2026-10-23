
using UnityEngine;
using UnityEngine.Tilemaps;

// Attach ONLY to the PressureWaterDrop prefab.
// Water digs while pressurized, then behaves like normal water.

[RequireComponent(typeof(Rigidbody2D))]
public class WaterErosion : MonoBehaviour
{
    [Header("Terrain")]
    public Tilemap dirt;

    [Header("Tunnel Settings")]
    public float erosionRadius = 0.35f;
    public float erosionCooldown = 0.05f;
    public float digAheadDistance = 0.25f;

    [Header("Pressure Settings")]
    public float maxErosionTime = 1.2f;
    public float minForwardSpeed = 2f;

    private Rigidbody2D rb;
    private float spawnTime;
    private float lastErosionTime = -100f;

    private bool pressureActive = true;
    private Vector2 initialDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        spawnTime = Time.time;

        // Find the Dirt Tilemap in the scene.
        if (dirt == null)
        {
            GameObject dirtObject = GameObject.Find("Dirt");

            if (dirtObject != null)
                dirt = dirtObject.GetComponent<Tilemap>();
        }

        if (dirt == null)
        {
            Debug.LogError(
                "WaterErosion: Dirt Tilemap not found!"
            );

            enabled = false;
            return;
        }

        // Store the initial horizontal launch direction.
        initialDirection = rb.linearVelocity.normalized;
    }

    void FixedUpdate()
    {
        if (!pressureActive)
            return;

        float age = Time.time - spawnTime;

        // Water loses its ability to erode after a short time.
        if (age >= maxErosionTime)
        {
            pressureActive = false;
            return;
        }

        // Stop erosion if the water loses forward momentum.
        float forwardSpeed = Vector2.Dot(
            rb.linearVelocity,
            initialDirection
        );

        if (forwardSpeed < minForwardSpeed)
        {
            pressureActive = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryErode(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryErode(collision);
    }

    void TryErode(Collision2D collision)
    {
        if (!pressureActive || dirt == null)
            return;

        if (Time.time - lastErosionTime < erosionCooldown)
            return;

        Tilemap hitTilemap =
            collision.collider.GetComponent<Tilemap>();

        if (hitTilemap != dirt)
            return;

        if (collision.contactCount == 0)
            return;

        Vector2 velocity = rb.linearVelocity;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        lastErosionTime = Time.time;

        // Dig slightly ahead of the water's movement.
        Vector2 hitPoint = collision.GetContact(0).point;

        Vector2 digPosition =
            hitPoint +
            velocity.normalized * digAheadDistance;

        EraseTiles(digPosition);
    }

    void EraseTiles(Vector2 worldPosition)
    {
        Vector3Int center = dirt.WorldToCell(worldPosition);

        float cellSize = Mathf.Min(
            dirt.cellSize.x,
            dirt.cellSize.y
        );

        int radius = Mathf.CeilToInt(
            erosionRadius / cellSize
        ) + 1;

        float radiusSquared =
            erosionRadius * erosionRadius;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int cell = center +
                    new Vector3Int(x, y, 0);

                Vector2 tileCenter =
                    dirt.GetCellCenterWorld(cell);

                if ((tileCenter - worldPosition)
                    .sqrMagnitude <= radiusSquared)
                {
                    dirt.SetTile(cell, null);
                }
            }
        }
    }
}
