using UnityEngine;

// Put on the water drop prefab. Keeps a count of live drops (used to cap the total)
// and removes drops that fall out of the level.
public class WaterDrop : MonoBehaviour
{
    public static int Alive { get; private set; }

    public float killY = -30f;   // drops below this height are deleted

    void OnEnable() { Alive++; }
    void OnDisable() { Alive--; }

    void Update()
    {
        if (transform.position.y < killY) Destroy(gameObject);
    }
}