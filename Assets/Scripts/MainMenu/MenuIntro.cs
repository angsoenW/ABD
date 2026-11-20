using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// MainMenu only. Soft entrance: the screen fades up from dark, the title drops in, Tutorial pops,
// the "Select level" label fades in and the level buttons follow one by one.
// The level buttons are created by LevelSelectMenu.Start(), so this waits a frame before animating them.
public class MenuIntro : MonoBehaviour
{
    public CanvasGroup fade;                 // full-screen dark overlay (raycasts off)
    public RectTransform title;
    public RectTransform tutorial;
    public RectTransform selectLabel;
    public RectTransform levelRow;           // parent of the level buttons
    public float startDelay = 0.15f;

    CanvasGroup titleGroup, labelGroup, rowGroup;
    Vector2 titleHome;

    void Awake()
    {
        titleGroup = Group(title);
        labelGroup = Group(selectLabel);
        rowGroup = Group(levelRow);
        if (title != null) titleHome = title.anchoredPosition;

        // hidden state before the first frame is drawn
        if (fade != null) fade.alpha = 1f;
        if (titleGroup != null) titleGroup.alpha = 0f;
        if (labelGroup != null) labelGroup.alpha = 0f;
        if (rowGroup != null) rowGroup.alpha = 0f;
        if (tutorial != null) tutorial.localScale = Vector3.zero;
        if (title != null) title.anchoredPosition = titleHome + new Vector2(0f, 120f);
    }

    static CanvasGroup Group(RectTransform rt)
    {
        if (rt == null) return null;
        var g = rt.GetComponent<CanvasGroup>();
        return g != null ? g : rt.gameObject.AddComponent<CanvasGroup>();
    }

    IEnumerator Start()
    {
        yield return null;       // let LevelSelectMenu create its buttons

        var buttons = new System.Collections.Generic.List<Transform>();
        if (levelRow != null)
            foreach (Transform c in levelRow) { buttons.Add(c); c.localScale = Vector3.zero; }
        if (rowGroup != null) rowGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(startDelay);

        StartCoroutine(Tween(0.7f, t => { if (fade != null) fade.alpha = 1f - Smooth(t); }));
        StartCoroutine(Tween(0.7f, t =>
        {
            float e = OutBack(t, 1.4f);
            if (titleGroup != null) titleGroup.alpha = Mathf.Clamp01(t * 2.5f);
            if (title != null) title.anchoredPosition = titleHome + new Vector2(0f, 120f * (1f - e));
        }));

        yield return new WaitForSecondsRealtime(0.35f);
        yield return Tween(0.45f, t =>
        {
            if (tutorial != null) { float s = OutBack(t, 1.9f); tutorial.localScale = new Vector3(s, s, 1f); }
        });

        StartCoroutine(Tween(0.4f, t => { if (labelGroup != null) labelGroup.alpha = Smooth(t); }));
        for (int i = 0; i < buttons.Count; i++)
        {
            StartCoroutine(Pop(buttons[i], 0.4f));
            yield return new WaitForSecondsRealtime(0.08f);
        }
        if (fade != null) fade.blocksRaycasts = false;
    }

    IEnumerator Pop(Transform t, float dur)
    {
        yield return Tween(dur, k => { if (t != null) { float s = OutBack(k, 1.9f); t.localScale = new Vector3(s, s, 1f); } });
        if (t != null) t.localScale = Vector3.one;
    }

    static IEnumerator Tween(float dur, System.Action<float> step)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            step(Mathf.Clamp01(t));
            yield return null;
        }
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static float OutBack(float t, float overshoot)
    {
        float c1 = overshoot, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
