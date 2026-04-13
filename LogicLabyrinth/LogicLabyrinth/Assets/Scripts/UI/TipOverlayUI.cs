using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared right-side tip overlay with typewriter text.
/// Use TipOverlayUI.ShowTip("message") from any gameplay script.
/// </summary>
public class TipOverlayUI : MonoBehaviour
{
    private static TipOverlayUI instance;

    private GameObject root;
    private CanvasGroup rootCanvasGroup;
    private TextMeshProUGUI headingTMP;
    private TextMeshProUGUI messageTMP;
    private Coroutine showRoutine;

    public static void ShowTip(string message, float visibleSeconds = 7f, float charsPerSecond = 40f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (!IsGameplayScene(SceneManager.GetActiveScene().name)) return;

        message = message.ToUpperInvariant();

        EnsureInstance();
        if (instance == null) return;

        instance.PlayTip(message, Mathf.Max(0.5f, visibleSeconds), Mathf.Max(12f, charsPerSecond));
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        instance = FindAnyObjectByType<TipOverlayUI>();
        if (instance != null) return;

        GameObject go = new GameObject("TipOverlayUI");
        instance = go.AddComponent<TipOverlayUI>();
        DontDestroyOnLoad(go);
    }

    private void PlayTip(string message, float visibleSeconds, float charsPerSecond)
    {
        BuildUIIfNeeded();

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowTipRoutine(message, visibleSeconds, charsPerSecond));
    }

    private void BuildUIIfNeeded()
    {
        if (root != null && headingTMP != null && messageTMP != null && rootCanvasGroup != null)
            return;

        if (root != null)
            Destroy(root);

        root = new GameObject("TipRoot");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1250;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        rootCanvasGroup = root.AddComponent<CanvasGroup>();
        rootCanvasGroup.alpha = 0f;

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(root.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.02f, 0.34f);
        panelRT.anchorMax = new Vector2(0.27f, 0.57f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        GameObject headingGO = new GameObject("Heading");
        headingGO.transform.SetParent(panelGO.transform, false);
        RectTransform headingRT = headingGO.AddComponent<RectTransform>();
        headingRT.anchorMin = new Vector2(0.08f, 0.72f);
        headingRT.anchorMax = new Vector2(0.92f, 0.98f);
        headingRT.offsetMin = Vector2.zero;
        headingRT.offsetMax = Vector2.zero;

        headingTMP = headingGO.AddComponent<TextMeshProUGUI>();
        headingTMP.text = "TIP";
        headingTMP.fontSize = 30f;
        headingTMP.fontStyle = FontStyles.Bold;
        headingTMP.alignment = TextAlignmentOptions.Left;
        headingTMP.color = new Color(1f, 0.90f, 0.60f, 1f);
        headingTMP.raycastTarget = false;

        GameObject messageGO = new GameObject("Message");
        messageGO.transform.SetParent(panelGO.transform, false);
        RectTransform msgRT = messageGO.AddComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.08f, 0.08f);
        msgRT.anchorMax = new Vector2(0.92f, 0.60f);
        msgRT.offsetMin = Vector2.zero;
        msgRT.offsetMax = Vector2.zero;

        messageTMP = messageGO.AddComponent<TextMeshProUGUI>();
        messageTMP.text = string.Empty;
        messageTMP.fontSize = 24f;
        messageTMP.fontStyle = FontStyles.Bold;
        messageTMP.alignment = TextAlignmentOptions.Left;
        messageTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        messageTMP.enableWordWrapping = true;
        messageTMP.raycastTarget = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene.name))
            return;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 0f;

        if (messageTMP != null)
            messageTMP.text = string.Empty;
    }

    private static bool IsGameplayScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return sceneName.StartsWith("Level") || sceneName == "Chapter3" || sceneName == "Chapter4";
    }

    private IEnumerator ShowTipRoutine(string message, float visibleSeconds, float charsPerSecond)
    {
        if (rootCanvasGroup == null || messageTMP == null)
            yield break;

        rootCanvasGroup.alpha = 1f;
        messageTMP.text = string.Empty;

        float interval = 1f / charsPerSecond;
        for (int i = 1; i <= message.Length; i++)
        {
            if (messageTMP == null) yield break;
            messageTMP.text = message.Substring(0, i) + "|";
            yield return new WaitForSecondsRealtime(interval);
        }

        if (messageTMP == null) yield break;
        messageTMP.text = message;

        yield return new WaitForSecondsRealtime(visibleSeconds);

        float fadeTime = 0.35f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            if (rootCanvasGroup == null) yield break;
            rootCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
            yield return null;
        }

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 0f;

        showRoutine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
