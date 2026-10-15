using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The goal basin. Put it on an object with a trigger collider covering the inside of a
// pocket in the terrain. Water stays visible and fills the basin up.
// WIN:  enough water sits inside for 'holdTime' seconds.
// LOSE: the player is out of shots (or too little water is left in the level)
//       and nothing changes for 'settleTime' seconds.
[RequireComponent(typeof(Collider2D))]
public class GoalReservoir : MonoBehaviour
{
    public int waterNeeded = 40;
    public string waterTag = "Water";
    public float holdTime = 0.5f;          // water must stay in, not just splash through

    [Header("Lose")]
    public Slingshot slingshot;            // drag the bird here
    public float settleTime = 3f;          // wait for water to settle before declaring a loss

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;

    public bool Finished { get; private set; }
    public bool Won { get; private set; }

    readonly HashSet<GameObject> inside = new HashSet<GameObject>();
    Rigidbody2D birdRb;
    float holdTimer;
    float settleTimer;

    public int WaterInside
    {
        get
        {
            inside.RemoveWhere(d => d == null || !d.activeInHierarchy);
            return inside.Count;
        }
    }

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        if (slingshot != null) birdRb = slingshot.GetComponent<Rigidbody2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(waterTag)) inside.Add(other.gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(waterTag)) inside.Remove(other.gameObject);
    }

    void Update()
    {
        if (Finished) return;

        // --- Win check ---
        if (WaterInside >= waterNeeded)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdTime) Win();
            return;
        }
        holdTimer = 0f;

        // --- Lose check (only after the first shot) ---
        if (slingshot == null || slingshot.ShotsUsed == 0) return;

        bool birdFlying = birdRb != null && birdRb.bodyType == RigidbodyType2D.Dynamic;
        bool outOfShots = slingshot.OutOfShots && !birdFlying;
        bool notEnoughWaterLeft = WaterDrop.Alive < waterNeeded;   // too much fell out of the level

        if (outOfShots || notEnoughWaterLeft)
        {
            settleTimer += Time.deltaTime;
            if (settleTimer >= settleTime) Lose();
        }
        else
        {
            settleTimer = 0f;
        }
    }

    void Win()
    {
        Finished = true;
        Won = true;
        LevelManager.MarkCompleted(LevelManager.CurrentLevel);
        onWin.Invoke();
    }

    void Lose()
    {
        Finished = true;
        Won = false;
        onLose.Invoke();
    }
}