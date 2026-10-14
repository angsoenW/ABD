using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Put on the goal object (e.g. a pipe or basin) with a trigger collider.
// Water particles must have the tag "Water".
[RequireComponent(typeof(Collider2D))]
public class GoalZone : MonoBehaviour
{
    public int waterNeeded = 30;
    public string waterTag = "Water";
    public bool absorbWater = true;          // hide particles once they're counted
    public UnityEvent onLevelComplete;       // hook LevelUI.ShowWin here

    readonly HashSet<GameObject> counted = new HashSet<GameObject>();
    bool done;

    public int Collected => counted.Count;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (done || !other.CompareTag(waterTag)) return;
        if (!counted.Add(other.gameObject)) return;       // count each drop once

        if (absorbWater) other.gameObject.SetActive(false);

        if (counted.Count >= waterNeeded)
        {
            done = true;
            LevelManager.MarkCompleted(LevelManager.CurrentLevel);
            onLevelComplete.Invoke();
        }
    }
}