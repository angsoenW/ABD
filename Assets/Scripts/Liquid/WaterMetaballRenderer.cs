
using UnityEngine;

public class WaterMetaballRenderer : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Camera waterCamera;
    public RenderTexture waterTexture;
    public Material metaballMaterial;

    [Header("Display")]
    public int sortingOrder = 5;

    private GameObject display;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || waterCamera == null ||
            waterTexture == null || metaballMaterial == null)
        {
            Debug.LogError("WaterMetaballRenderer: Missing references!");
            enabled = false;
            return;
        }

        runtimeMaterial = new Material(metaballMaterial);
        runtimeMaterial.SetTexture("_MainTex", waterTexture);

        display = GameObject.CreatePrimitive(PrimitiveType.Quad);
        display.name = "MetaballWaterDisplay";
        display.transform.SetParent(transform, false);

        Collider collider = display.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        meshRenderer = display.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = runtimeMaterial;
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = sortingOrder;

        // Prevent the water camera from capturing the display itself.
        display.layer = LayerMask.NameToLayer("Default");
    }

    void LateUpdate()
    {
        if (display == null)
            return;

        // Match camera position and zoom.
        waterCamera.transform.position =
            mainCamera.transform.position;

        waterCamera.transform.rotation =
            mainCamera.transform.rotation;

        waterCamera.orthographic =
            mainCamera.orthographic;

        waterCamera.orthographicSize =
            mainCamera.orthographicSize;

        // Match output resolution to the Game window.
        int width = Mathf.Max(1, mainCamera.pixelWidth);
        int height = Mathf.Max(1, mainCamera.pixelHeight);

        if (waterTexture.width != width ||
            waterTexture.height != height)
        {
            waterTexture.Release();
            waterTexture.width = width;
            waterTexture.height = height;
            waterTexture.Create();
        }

        float worldHeight = mainCamera.orthographicSize * 2f;
        float worldWidth = worldHeight *
            ((float)width / height);

        // Place quad in front of the camera.
        display.transform.position =
            mainCamera.transform.position +
            mainCamera.transform.forward * 5f;

        display.transform.rotation =
            mainCamera.transform.rotation;

        display.transform.localScale =
            new Vector3(worldWidth, worldHeight, 1f);
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        if (display != null)
            Destroy(display);
    }
}
