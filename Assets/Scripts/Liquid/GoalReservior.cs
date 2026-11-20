
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class GoalReservoir : MonoBehaviour
{
    [Header("Water Settings")]
    public int waterNeeded = 40;
    public string waterTag = "Water";
    public float holdTime = 0.5f;

    [Header("Lose")]
    public Slingshot slingshot;
    public float settleTime = 3f;

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;
    public UnityEvent onContaminationLose;

    public enum LoseCause { NotEnoughWater, OutOfShots }

    // Why the normal (non-contamination) lose fired. Read by LevelUI.
    public LoseCause LoseReason { get; set; } = LoseCause.NotEnoughWater;

    public bool Finished { get; private set; }
    public bool Won { get; private set; }

    private readonly HashSet<GameObject> inside =
        new HashSet<GameObject>();

    private Rigidbody2D birdRb;
    private float holdTimer;
    private float settleTimer;

    public int WaterInside
    {
        get
        {
            inside.RemoveWhere(
                d => d == null || !d.activeInHierarchy
            );

            return inside.Count;
        }
    }

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        if (slingshot != null)
        {
            birdRb = slingshot.GetComponent<Rigidbody2D>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Finished || !other.CompareTag(waterTag))
            return;

        GameObject drop = other.gameObject;

        inside.Add(drop);

        WaterContamination contamination =
            drop.GetComponent<WaterContamination>();

        // Immediate loss when contaminated water enters.
        if (contamination != null &&
            contamination.isContaminated)
        {
            LoseFromContamination();
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (Finished || !other.CompareTag(waterTag))
            return;

        WaterContamination contamination =
            other.GetComponent<WaterContamination>();

        // Also detect water that becomes contaminated
        // while already inside the reservoir.
        if (contamination != null &&
            contamination.isContaminated)
        {
            LoseFromContamination();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(waterTag))
        {
            inside.Remove(other.gameObject);
        }
    }

    void Update()
    {
        if (Finished)
            return;

        // Check contamination before evaluating a win.
        foreach (GameObject drop in inside)
        {
            if (drop == null || !drop.activeInHierarchy)
                continue;

            WaterContamination contamination =
                drop.GetComponent<WaterContamination>();

            if (contamination != null &&
                contamination.isContaminated)
            {
                LoseFromContamination();
                return;
            }
        }

        // --- WIN ---
        if (WaterInside >= waterNeeded)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                Win();
            }

            return;
        }

        holdTimer = 0f;

        // --- Existing lose condition ---
        if (slingshot == null || slingshot.ShotsUsed == 0)
            return;

        bool birdFlying =
            birdRb != null &&
            birdRb.bodyType == RigidbodyType2D.Dynamic;

        bool outOfShots =
            slingshot.OutOfShots && !birdFlying;

        bool notEnoughWaterLeft =
            WaterDrop.Alive < waterNeeded;

        if (outOfShots || notEnoughWaterLeft)
        {
            settleTimer += Time.deltaTime;

            if (settleTimer >= settleTime)
            {
                // Using every shot takes precedence over the water check.
                LoseReason = outOfShots
                    ? LoseCause.OutOfShots
                    : LoseCause.NotEnoughWater;
                Lose();
            }
        }
        else
        {
            settleTimer = 0f;
        }
    }

    // --- WIN ---
    void Win()
    {
        if (Finished)
            return;

        Finished = true;
        Won = true;

        LevelManager.MarkCompleted(
            LevelManager.CurrentLevel
        );

        onWin.Invoke();
    }

    // --- LOSE: Not enough water / out of shots ---
    void Lose()
    {
        if (Finished)
            return;

        Finished = true;
        Won = false;

        onLose.Invoke();
    }

    // --- LOSE: Contaminated water ---
    void LoseFromContamination()
    {
        if (Finished)
            return;

        Finished = true;
        Won = false;

        Debug.Log("GAME OVER: Container contaminated!");

        onContaminationLose.Invoke();
    }
}
