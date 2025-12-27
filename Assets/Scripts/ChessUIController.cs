// ChessUIController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ChessUIController : MonoBehaviour
{
    [Header("Optional (auto created if empty)")]
    public Text turnText;
    public Text statusText;
    public Button restartButton;

    // --- Unity 2023+ safe find helper (avoids CS0618) ---
    static T FindAny<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    public void EnsureUI(System.Action onRestart)
    {
        if (turnText != null && statusText != null && restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() => onRestart?.Invoke());
            return;
        }

        if (FindAny<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGO = new GameObject("ChessUI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.35f);

        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 1f);
        panelRT.anchorMax = new Vector2(0f, 1f);
        panelRT.pivot = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(10f, -10f);
        panelRT.sizeDelta = new Vector2(420f, 120f);

        turnText = CreateText(panelGO.transform, "TurnText", new Vector2(10f, -10f), new Vector2(400f, 30f), 18);
        statusText = CreateText(panelGO.transform, "StatusText", new Vector2(10f, -45f), new Vector2(400f, 30f), 16);

        restartButton = CreateButton(panelGO.transform, "RestartButton", new Vector2(10f, -80f), new Vector2(140f, 30f), "Restart");
        restartButton.onClick.AddListener(() => onRestart?.Invoke());
    }

    public void SetTurn(string text)
    {
        if (turnText != null) turnText.text = text;
    }

    public void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    Text CreateText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        return text;
    }

    Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.2f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = CreateText(go.transform, "Label", Vector2.zero, size, 14);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;

        return btn;
    }
}
