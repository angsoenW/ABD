using UnityEngine;

// One bird's settings, saved as an asset so the team can make and tune birds in the Inspector.
// Create one with: right-click in the Project window > Create > Birds > Bird Type.
[CreateAssetMenu(fileName = "NewBird", menuName = "Birds/Bird Type")]
public class BirdType : ScriptableObject
{
    [Header("Look")]
    public string displayName = "Normal";
    public Color color = Color.white;
    public float size = 1f;                      // multiplies the bird's normal scale

    [Header("Flight")]
    public float mass = 1f;                      // heavier pushes boxes and water harder
    public float launchPowerMultiplier = 1f;     // >1 = faster launch, <1 = slower
    public float gravityScale = 1f;              // <1 = flatter, longer arc

    [Header("Digging")]
    public float holeRadius = 0.6f;              // tunnel width (world units)
    public int maxDigs = 6;                      // how deep one shot can go
    [Range(0f, 1f)]
    public float speedKeptPerDig = 0.85f;        // higher = ploughs through more dirt

    [Header("Bomb (leave off for normal birds)")]
    public bool explodes = false;
    public float explosionRadius = 1.6f;         // crater size (world units)
    public float explosionForce = 8f;            // push on boxes and water
    public float autoExplodeDelay = 1.5f;        // explodes by itself this long after first hit (0 = only on click)
}
