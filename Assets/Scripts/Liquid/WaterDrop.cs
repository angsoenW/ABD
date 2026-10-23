using UnityEngine;
using UnityEngine.SceneManagement;

// Put on the water drop prefab. Keeps a count of live drops (used to cap the total),
// counts drops that fell out of the level (for analytics), and removes them.
public class WaterDrop : MonoBehaviour
{
    public static int Alive { get; private set; }
    public static int FellOff { get; private set; }   // drops lost off the level this attempt

    public float killY = -30f;   // drops below this height are deleted

    // Reset the counters when the game starts and whenever a level loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Alive = 0;
        FellOff = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { FellOff = 0; }

    void OnEnable() { Alive++; }
    void OnDisable() { Alive--; }

    void Update()
    {
        if (transform.position.y < killY)
        {
            FellOff++;
            Destroy(gameObject);
        }
    }
}