using UnityEngine;

public class ButtonController : MonoBehaviour
{
    [Header("Target to Open")]
    public GameObject waterBlockingObstacle; // Drag 'WaterGate' here

    [Header("Visual Feedback Settings")]
    public float pressedScaleY = 0.3f;       // Squish factor for Y scale
    public Color pressedColor = Color.green; // Color change upon activation

    private bool isPressed = false;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private BoxCollider2D triggerCollider;
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        originalScale = transform.localScale;
        originalPosition = transform.position;

        // Grab the trigger BoxCollider2D so we can keep its detection box from shrinking away from the ball
        foreach (BoxCollider2D col in GetComponents<BoxCollider2D>())
        {
            if (col.isTrigger)
            {
                triggerCollider = col;
                originalColliderSize = col.size;
                originalColliderOffset = col.offset;
                break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isPressed && !collision.isTrigger && collision.gameObject.name == "Ball")
        {
            isPressed = true;
            
            // 1. Calculate height reduction
            float originalHeight = originalScale.y;
            float newHeight = originalHeight * pressedScaleY;
            float heightDifference = originalHeight - newHeight;

            // 2. Resize and offset Y position down so it squishes against the floor
            transform.localScale = new Vector3(originalScale.x, newHeight, originalScale.z);
            transform.position = originalPosition - new Vector3(0f, heightDifference / 2f, 0f);

            // Keep the trigger zone tall enough so it doesn't lose contact with the resting ball
            if (triggerCollider != null)
            {
                float scaleRatio = 1f / pressedScaleY;
                triggerCollider.size = new Vector2(originalColliderSize.x, originalColliderSize.y * scaleRatio);
                triggerCollider.offset = new Vector2(originalColliderOffset.x, originalColliderOffset.y + (originalColliderSize.y * (scaleRatio - 1f)) / 2f);
            }

            // 3. Update visual color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = pressedColor;
            }

            // 4. Disable the water gate
            if (waterBlockingObstacle != null)
            {
                waterBlockingObstacle.SetActive(false);
            }

            Debug.Log("Button pressed cleanly! Gate opened.");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (isPressed && !collision.isTrigger && collision.gameObject.name == "Ball")
        {
            isPressed = false;

            // 1. Restore original scale, position, and trigger collider
            transform.localScale = originalScale;
            transform.position = originalPosition;

            if (triggerCollider != null)
            {
                triggerCollider.size = originalColliderSize;
                triggerCollider.offset = originalColliderOffset;
            }

            // 2. Restore original color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            // 3. Re-enable the water gate
            if (waterBlockingObstacle != null)
            {
                waterBlockingObstacle.SetActive(true);
            }

            Debug.Log("Ball left button! Gate closed.");
        }
    }
}