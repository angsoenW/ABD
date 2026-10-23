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

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore water drop triggers or self-collisions if needed
        // Only trigger on solid physical objects (like the secondary ball)
        if (!isPressed && !collision.isTrigger)
        {
            isPressed = true;
            
            // 1. Calculate height reduction
            Vector3 startScale = transform.localScale;
            float originalHeight = startScale.y;
            float newHeight = originalHeight * pressedScaleY;
            float heightDifference = originalHeight - newHeight;

            // 2. Resize and offset Y position down so it squishes against the floor
            transform.localScale = new Vector3(startScale.x, newHeight, startScale.z);
            transform.position -= new Vector3(0f, heightDifference / 2f, 0f);

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
}