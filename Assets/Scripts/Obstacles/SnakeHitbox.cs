using UnityEngine;

// Used by the snakes in Level 1. Sits on every head/body bead of a SnakePatrol snake and
// passes trigger contacts to the snake, so touching any part of it counts.
public class SnakeHitbox : MonoBehaviour
{
    public SnakePatrol snake;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (snake != null) snake.OnTouch(other);
    }
}
