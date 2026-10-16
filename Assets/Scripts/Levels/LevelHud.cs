using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Adds "Restart" and "Menu" buttons to the top-right corner of EVERY level automatically.
// No setup needed: just put this file anywhere in the project.
// The MainMenu (scene 0) and scenes not in the Build Profiles scene list are skipped.
public static class LevelHUD
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // avoid double-subscribing
        SceneManager.sceneLoaded += OnSceneLoaded;
        Build(SceneManager.GetActiveScene());        // the scene that was already open
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    static void Build(Scene scene)
    {
        if (scene.buildIndex <= LevelManager.MenuSceneIndex) return;

        // Screen-space canvas that sits on top of everything else.
        var root = new GameObject("LevelHUD", typeof(RectTransform), typeof(Canvas),
                                  typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        CreateButton(root.transform, "Restart", new Vector2(-20, -20), LevelManager.Restart);
        CreateButton(root.transform, "Menu", new Vector2(-200, -20), LevelManager.LoadMenu);

        EnsureEventSystem(scene);
    }

    static void CreateButton(Transform parent, string label, Vector2 offsetFromTopRight,
                             UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);   // top-right corner
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(160, 60);
        rt.anchoredPosition = offsetFromTopRight;

        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 30;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
    }

    // Buttons need an EventSystem in the scene to receive clicks.
    static void EnsureEventSystem(Scene scene)
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        SceneManager.MoveGameObjectToScene(es, scene);
    }
}