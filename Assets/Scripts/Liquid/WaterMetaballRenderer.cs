
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

    [Header("Performance")]
    [Range(0.25f, 1f)]
    public float resolutionScale = 1f;

    private GameObject display;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;
    private RenderTexture runtimeTexture;

    private int currentWidth;
    private int currentHeight;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null ||
            waterCamera == null ||
            metaballMaterial == null)
        {
            Debug.LogError(
                "WaterMetaballRenderer: Missing camera or material references!"
            );

            enabled = false;
            return;
        }

        if (!mainCamera.orthographic)
        {
            Debug.LogError(
                "WaterMetaballRenderer requires an orthographic Main Camera."
            );

            enabled = false;
            return;
        }

        // Create a separate material for this level.
        runtimeMaterial = new Material(metaballMaterial);

        // Create a display quad.
        display = GameObject.CreatePrimitive(
            PrimitiveType.Quad
        );

        display.name = "MetaballWaterDisplay";
        display.transform.SetParent(transform, false);

        Collider quadCollider = display.GetComponent<Collider>();

        if (quadCollider != null)
            Destroy(quadCollider);

        meshRenderer = display.GetComponent<MeshRenderer>();

        meshRenderer.sharedMaterial = runtimeMaterial;
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = sortingOrder;

        // The water camera should not render this quad.
        display.layer = LayerMask.NameToLayer("Default");

        UpdateRendering();
    }

    void LateUpdate()
    {
        UpdateRendering();
    }

    void UpdateRendering()
    {
        if (mainCamera == null ||
            waterCamera == null ||
            display == null)
            return;

        // Synchronize camera position and rotation.
        waterCamera.transform.position =
            mainCamera.transform.position;

        waterCamera.transform.rotation =
            mainCamera.transform.rotation;

        // Synchronize projection.
        waterCamera.orthographic = true;

        waterCamera.orthographicSize =
            mainCamera.orthographicSize;

        // Match the Main Camera's viewport.
        waterCamera.rect = mainCamera.rect;

        // Determine the actual display dimensions.
        int width = Mathf.Max(
            1,
            Mathf.RoundToInt(
                mainCamera.pixelWidth * resolutionScale
            )
        );

        int height = Mathf.Max(
            1,
            Mathf.RoundToInt(
                mainCamera.pixelHeight * resolutionScale
            )
        );

        // Recreate the texture only when dimensions change.
        if (runtimeTexture == null ||
            width != currentWidth ||
            height != currentHeight)
        {
            CreateRenderTexture(width, height);
        }

        // Match the actual aspect ratio.
        float aspect = (float)width / height;

        waterCamera.aspect = aspect;

        // Position the display in front of Main Camera.
        float distance = 5f;

        display.transform.position =
            mainCamera.transform.position +
            mainCamera.transform.forward * distance;

        display.transform.rotation =
            mainCamera.transform.rotation;

        float worldHeight =
            mainCamera.orthographicSize * 2f;

        float worldWidth =
            worldHeight * aspect;

        display.transform.localScale = new Vector3(
            worldWidth,
            worldHeight,
            1f
        );
    }

    void CreateRenderTexture(int width, int height)
    {
        if (runtimeTexture != null)
        {
            waterCamera.targetTexture = null;

            runtimeTexture.Release();
            Destroy(runtimeTexture);
        }

        RenderTextureDescriptor descriptor =
            new RenderTextureDescriptor(
                width,
                height,
                RenderTextureFormat.ARGB32,
                0
            );

        runtimeTexture = new RenderTexture(descriptor);

        runtimeTexture.name =
            "WaterMetaballRuntimeTexture";

        runtimeTexture.filterMode =
            FilterMode.Bilinear;

        runtimeTexture.wrapMode =
            TextureWrapMode.Clamp;

        runtimeTexture.Create();

        waterCamera.targetTexture =
            runtimeTexture;

        runtimeMaterial.SetTexture(
            "_MainTex",
            runtimeTexture
        );

        currentWidth = width;
        currentHeight = height;
    }

    void OnDestroy()
    {
        if (waterCamera != null &&
            waterCamera.targetTexture == runtimeTexture)
        {
            waterCamera.targetTexture = null;
        }

        if (runtimeTexture != null)
        {
            runtimeTexture.Release();
            Destroy(runtimeTexture);
        }

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        if (display != null)
            Destroy(display);
    }
}
