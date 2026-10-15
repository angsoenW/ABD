using UnityEngine;
using DTerrain;

// Put this on the Terrain object (next to Basic Paintable Layer and CenterTerrain).
// DTerrain only builds the terrain when you press Play, so the Scene view is empty while editing.
// This shows a temporary picture of the terrain in the editor so you can line up the
// bird, boxes, goal and water. It is never saved and is removed automatically in Play mode.
[ExecuteAlways]
[RequireComponent(typeof(BasicPaintableLayer))]
public class TerrainEditorPreview : MonoBehaviour
{
    [Range(0f, 1f)]
    public float previewAlpha = 1f;

    GameObject preview;
    Sprite sprite;

    void OnEnable()
    {
        if (!Application.isPlaying) Rebuild();
    }

    void OnDisable()
    {
        Clear();
    }

#if UNITY_EDITOR
    // Refresh the picture when you change the texture, PPU or alpha in the Inspector.
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying) Rebuild();
        };
    }
#endif

    void Rebuild()
    {
        Clear();

        var layer = GetComponent<BasicPaintableLayer>();
        Texture2D tex = layer.OriginalTexture;
        if (tex == null || layer.PPU <= 0) return;

        // With CenterTerrain, the object's position is the image center; without it, the bottom-left.
        bool centered = GetComponent<CenterTerrain>() != null;
        Vector2 pivot = centered ? new Vector2(0.5f, 0.5f) : Vector2.zero;

        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, layer.PPU);
        sprite.hideFlags = HideFlags.HideAndDontSave;

        preview = new GameObject("TerrainEditorPreview");
        preview.hideFlags = HideFlags.HideAndDontSave;
        preview.transform.SetParent(transform, false);

        var sr = preview.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 1f, 1f, previewAlpha);
        sr.sortingLayerID = layer.SortingLayerID;
    }

    void Clear()
    {
        if (preview != null) DestroyImmediate(preview);
        if (sprite != null) DestroyImmediate(sprite);
        preview = null;
        sprite = null;
    }
}