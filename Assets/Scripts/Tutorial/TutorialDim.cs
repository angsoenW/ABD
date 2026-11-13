using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A full-screen dim layer with rectangular "windows" cut out of it,
// so the objects the tutorial is talking about stay bright while the rest is gently darkened.
// Holes are given in this graphic's local space (see TutorialGuide).
[RequireComponent(typeof(CanvasRenderer))]
public class TutorialDim : MaskableGraphic
{
    readonly List<Rect> holes = new List<Rect>();
    readonly List<float> xs = new List<float>();
    readonly List<float> ys = new List<float>();

    public void SetHoles(List<Rect> newHoles)
    {
        if (SameAs(newHoles)) return;
        holes.Clear();
        holes.AddRange(newHoles);
        SetVerticesDirty();
    }

    bool SameAs(List<Rect> other)
    {
        if (other.Count != holes.Count) return false;
        for (int i = 0; i < other.Count; i++)
        {
            if ((other[i].min - holes[i].min).sqrMagnitude > 0.25f) return false;
            if ((other[i].max - holes[i].max).sqrMagnitude > 0.25f) return false;
        }
        return true;
    }

    // Splits the screen into a grid along every hole edge and fills each cell that isn't inside a hole.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;

        xs.Clear(); ys.Clear();
        xs.Add(r.xMin); xs.Add(r.xMax);
        ys.Add(r.yMin); ys.Add(r.yMax);
        foreach (Rect h in holes)
        {
            xs.Add(Mathf.Clamp(h.xMin, r.xMin, r.xMax)); xs.Add(Mathf.Clamp(h.xMax, r.xMin, r.xMax));
            ys.Add(Mathf.Clamp(h.yMin, r.yMin, r.yMax)); ys.Add(Mathf.Clamp(h.yMax, r.yMin, r.yMax));
        }
        xs.Sort(); ys.Sort();

        for (int i = 0; i < xs.Count - 1; i++)
        {
            if (xs[i + 1] - xs[i] < 0.01f) continue;
            for (int j = 0; j < ys.Count - 1; j++)
            {
                if (ys[j + 1] - ys[j] < 0.01f) continue;
                Vector2 c = new Vector2((xs[i] + xs[i + 1]) * 0.5f, (ys[j] + ys[j + 1]) * 0.5f);
                if (InsideHole(c)) continue;
                AddQuad(vh, new Vector2(xs[i], ys[j]), new Vector2(xs[i + 1], ys[j + 1]));
            }
        }
    }

    bool InsideHole(Vector2 p)
    {
        foreach (Rect h in holes) if (h.Contains(p)) return true;
        return false;
    }

    void AddQuad(VertexHelper vh, Vector2 min, Vector2 max)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
