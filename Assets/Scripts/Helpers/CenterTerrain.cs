using UnityEngine;
using DTerrain;

// Put this on the same object as BasicPaintableLayer.
// DTerrain draws the image with its bottom-left corner at the object's position.
// This shifts the object so the image's CENTER lands where you placed it.
[DefaultExecutionOrder(-100)]   // run before the layer spawns its chunks
[RequireComponent(typeof(BasicPaintableLayer))]
public class CenterTerrain : MonoBehaviour
{
    void Awake()
    {
        var layer = GetComponent<BasicPaintableLayer>();
        Texture2D tex = layer.OriginalTexture;
        if (tex == null || layer.PPU <= 0) return;

        Vector3 halfSize = new Vector3(tex.width, tex.height, 0f) / layer.PPU * 0.5f;
        transform.position -= halfSize;
    }

    // Shows the image outline in the Scene view, centered on the object, before you press Play.
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        var layer = GetComponent<BasicPaintableLayer>();
        if (layer == null || layer.OriginalTexture == null || layer.PPU <= 0) return;

        Vector3 size = new Vector3(layer.OriginalTexture.width, layer.OriginalTexture.height, 0f) / layer.PPU;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
