using UnityEngine;

// A reservoir of water. Place it inside a pocket of terrain.
// - Fill On Start: fills the blue rectangle with drops when the level starts.
// - Drops Per Second > 0: also works as a faucet that keeps pouring.
public class WaterSpawner : MonoBehaviour
{
    public GameObject dropPrefab;
    public int maxDrops = 300;               // cap on ALL drops in the level (performance)

    [Header("Fill On Start (reservoir)")]
    public bool fillOnStart = true;
    public Vector2 areaSize = new Vector2(2f, 1f);
    public float spacing = 0.2f;             // about one drop diameter

    [Header("Faucet (optional)")]
    public float dropsPerSecond = 0f;        // 0 = off
    public Vector2 pourVelocity = Vector2.zero;

    float timer;

    void Start()
    {
        if (fillOnStart) Fill();
    }

    void Update()
    {
        if (dropsPerSecond <= 0f) return;
        timer += Time.deltaTime;
        float interval = 1f / dropsPerSecond;
        while (timer >= interval)
        {
            timer -= interval;
            Spawn(transform.position, pourVelocity + Random.insideUnitCircle * 0.2f);
        }
    }

    public void Fill()
    {
        int nx = Mathf.Max(1, Mathf.FloorToInt(areaSize.x / spacing));
        int ny = Mathf.Max(1, Mathf.FloorToInt(areaSize.y / spacing));
        Vector2 start = (Vector2)transform.position - areaSize * 0.5f + Vector2.one * spacing * 0.5f;

        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < nx; x++)
            {
                Vector2 jitter = Random.insideUnitCircle * spacing * 0.1f;
                Spawn(start + new Vector2(x * spacing, y * spacing) + jitter, Vector2.zero);
            }
        }
    }

    void Spawn(Vector2 pos, Vector2 velocity)
    {
        if (dropPrefab == null || WaterDrop.Alive >= maxDrops) return;
        GameObject d = Instantiate(dropPrefab, pos, Quaternion.identity);
        Rigidbody2D rb = d.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        if (fillOnStart) Gizmos.DrawWireCube(transform.position, areaSize);
        else Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}