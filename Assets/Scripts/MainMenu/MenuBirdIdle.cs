using UnityEngine;

// MainMenu only. Idle life for the decorative bird that sits on the hill: slow breathing squash,
// a tiny bob, and every few seconds a small hop with squash-and-stretch. No physics, no gameplay scripts.
public class MenuBirdIdle : MonoBehaviour
{
    public float breathePeriod = 2.4f;
    public float breatheAmount = 0.045f;
    public Vector2 hopInterval = new Vector2(3f, 6f);
    public float hopHeight = 0.35f;
    public float hopTime = 0.5f;

    Vector3 homePos, homeScale;
    float hopTimer, hopT = -1f;

    void Awake()
    {
        homePos = transform.localPosition;
        homeScale = transform.localScale;
        hopTimer = Random.Range(hopInterval.x, hopInterval.y);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float b = Mathf.Sin(Time.time / breathePeriod * Mathf.PI * 2f) * breatheAmount;
        float sx = 1f - b * 0.6f, sy = 1f + b;
        float y = 0f;

        hopTimer -= dt;
        if (hopT < 0f && hopTimer <= 0f) { hopT = 0f; hopTimer = Random.Range(hopInterval.x, hopInterval.y); }
        if (hopT >= 0f)
        {
            hopT += dt / hopTime;
            float t = Mathf.Clamp01(hopT);
            y = 4f * t * (1f - t) * hopHeight;                       // parabola
            if (t < 0.15f) { sx *= 1.12f; sy *= 0.85f; }            // crouch
            else if (t > 0.85f) { sx *= 1.12f; sy *= 0.88f; }       // land
            else { sx *= 0.92f; sy *= 1.1f; }                       // stretch in the air
            if (hopT >= 1f) hopT = -1f;
        }

        transform.localPosition = homePos + new Vector3(0f, y + Mathf.Abs(b) * 0.2f, 0f);
        transform.localScale = new Vector3(homeScale.x * sx, homeScale.y * sy, homeScale.z);
    }
}
