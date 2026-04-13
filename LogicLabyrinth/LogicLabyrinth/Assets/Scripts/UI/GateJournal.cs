using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using StarterAssets;

/// <summary>
/// Gate Journal — press J to review tutorial cards for every gate type collected so far.
/// Tabs across the top switch between NOT, AND, OR pages.
/// Press E or J again to close.
///
/// Automatically auto-creates itself the first time GateJournal.EnsureInstance() is called.
/// </summary>
public class GateJournal : MonoBehaviour
{
    // ── Singleton ──
    private static GateJournal _instance;

    // ── State ──
    private GameObject root;
    private bool isOpen = false;
    private string currentTab = "NOT";

    // ── Player control refs ──
    private FirstPersonController fpc;
    private StarterAssetsInputs inputs;

    // ── Constants ──
    private static readonly string[] GateOrder = { "NOT", "AND", "OR" };

    private static readonly Dictionary<string, string> GateImagePaths = new Dictionary<string, string>
    {
        { "NOT", "GateTutorials/New_NOTGate" },
        { "AND", "GateTutorials/New_ANDGate" },
        { "OR",  "GateTutorials/New_ORGate"  },
    };

    // ── Public API ──

    public static bool IsOpen => _instance != null && _instance.isOpen;

    /// <summary>
    /// Guarantees the journal singleton exists. Call from InventoryManager after
    /// the first gate is collected so J-key listening is ready.
    /// </summary>
    public static void EnsureInstance()
    {
        if (_instance != null) return;
        _instance = FindAnyObjectByType<GateJournal>();
        if (_instance != null) return;

        GameObject go = new GameObject("GateJournal");
        _instance = go.AddComponent<GateJournal>();
        DontDestroyOnLoad(go);
    }

    // ── Lifecycle ──

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (isOpen)
                CloseJournal();
            else
                OpenJournal();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.E))
            CloseJournal();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Open / Close ──

    private void OpenJournal()
    {
        // Don't open during cutscene, pause, or while a tutorial card is already up
        if (CutsceneController.IsPlaying || PauseMenuController.IsPaused) return;
        if (GateTutorialCard.IsOpen) return;

        if (GateTutorialCard.SeenGateTypes.Count == 0)
        {
            TipOverlayUI.ShowTip("Collect a gate first to unlock your Gate Journal.", 6f, 40f);
            return;
        }

        // Default tab = first seen gate in order
        currentTab = "NOT";
        foreach (string g in GateOrder)
        {
            if (GateTutorialCard.SeenGateTypes.Contains(g))
            {
                currentTab = g;
                break;
            }
        }

        BuildJournalUI();
        SetPlayerControls(false);
        isOpen = true;
    }

    private void CloseJournal()
    {
        isOpen = false;
        if (root != null) Destroy(root);
        root = null;
        SetPlayerControls(true);
    }

    // ── UI Builder ──

    private void BuildJournalUI()
    {
        if (root != null) Destroy(root);

        root = new GameObject("GateJournalRoot");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1350;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        root.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");

        // ── Full-screen dim ──
        GameObject dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(root.transform, false);
        RectTransform dimRT = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        Image dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);
        dimImg.raycastTarget = true;

        // ── Main panel (centered, 70% wide × 85% tall) ──
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(root.transform, false);
        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.15f, 0.08f);
        panelRT.anchorMax = new Vector2(0.85f, 0.95f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0.07f, 0.045f, 0.015f, 0.97f);

        // ── "GATE JOURNAL" heading ──
        GameObject headingGO = new GameObject("Heading");
        headingGO.transform.SetParent(panelGO.transform, false);
        RectTransform headingRT = headingGO.AddComponent<RectTransform>();
        headingRT.anchorMin = new Vector2(0f, 0.89f);
        headingRT.anchorMax = new Vector2(1f, 1f);
        headingRT.offsetMin = Vector2.zero;
        headingRT.offsetMax = Vector2.zero;
        TextMeshProUGUI headingTMP = headingGO.AddComponent<TextMeshProUGUI>();
        headingTMP.text = "GATE JOURNAL";
        headingTMP.fontSize = 38f;
        headingTMP.fontStyle = FontStyles.Bold;
        headingTMP.alignment = TextAlignmentOptions.Center;
        headingTMP.color = new Color(1f, 0.90f, 0.60f, 1f);
        headingTMP.raycastTarget = false;
        if (font != null) headingTMP.font = font;

        // ── Tab row (sits between heading and image area) ──
        const float tabRowBottom = 0.78f;
        const float tabRowTop    = 0.88f;

        // Count how many gate types are unlocked
        int seenCount = 0;
        foreach (string g in GateOrder)
            if (GateTutorialCard.SeenGateTypes.Contains(g)) seenCount++;

        float tabWidth = seenCount > 0 ? 0.88f / seenCount : 0.88f;
        float tabX = 0.06f;

        foreach (string g in GateOrder)
        {
            if (!GateTutorialCard.SeenGateTypes.Contains(g)) continue;

            string capturedGate = g; // capture for closure

            // Tab background button
            GameObject tabGO = new GameObject($"Tab_{g}");
            tabGO.transform.SetParent(panelGO.transform, false);
            RectTransform tabRT = tabGO.AddComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(tabX, tabRowBottom);
            tabRT.anchorMax = new Vector2(tabX + tabWidth - 0.012f, tabRowTop);
            tabRT.offsetMin = Vector2.zero;
            tabRT.offsetMax = Vector2.zero;

            Image tabImg = tabGO.AddComponent<Image>();
            bool isActiveTab = (g == currentTab);
            tabImg.color = isActiveTab
                ? new Color(0.55f, 0.38f, 0.10f, 1f)   // lit up gold-brown
                : new Color(0.20f, 0.13f, 0.04f, 1f);  // dark unselected

            Button tabBtn = tabGO.AddComponent<Button>();
            tabBtn.targetGraphic = tabImg;
            tabBtn.onClick.AddListener(() =>
            {
                currentTab = capturedGate;
                BuildJournalUI(); // Rebuild so tab highlights update
            });

            // Tab label
            GameObject tabLabelGO = new GameObject("Label");
            tabLabelGO.transform.SetParent(tabGO.transform, false);
            RectTransform tabLabelRT = tabLabelGO.AddComponent<RectTransform>();
            tabLabelRT.anchorMin = Vector2.zero;
            tabLabelRT.anchorMax = Vector2.one;
            tabLabelRT.offsetMin = Vector2.zero;
            tabLabelRT.offsetMax = Vector2.zero;
            TextMeshProUGUI tabTMP = tabLabelGO.AddComponent<TextMeshProUGUI>();
            tabTMP.text = $"{g} GATE";
            tabTMP.fontSize = 22f;
            tabTMP.fontStyle = FontStyles.Bold;
            tabTMP.alignment = TextAlignmentOptions.Center;
            tabTMP.color = isActiveTab
                ? new Color(1f, 0.95f, 0.70f, 1f)
                : new Color(0.75f, 0.65f, 0.45f, 1f);
            tabTMP.raycastTarget = false;
            if (font != null) tabTMP.font = font;

            tabX += tabWidth;
        }

        // ── Gate card image area ──
        GameObject imageAreaGO = new GameObject("ImageArea");
        imageAreaGO.transform.SetParent(panelGO.transform, false);
        RectTransform imageAreaRT = imageAreaGO.AddComponent<RectTransform>();
        imageAreaRT.anchorMin = new Vector2(0.03f, 0.07f);
        imageAreaRT.anchorMax = new Vector2(0.97f, tabRowBottom - 0.04f);
        imageAreaRT.offsetMin = Vector2.zero;
        imageAreaRT.offsetMax = Vector2.zero;

        Image gateCardImg = imageAreaGO.AddComponent<Image>();
        gateCardImg.preserveAspect = true;
        gateCardImg.raycastTarget = false;
        LoadTabImage(currentTab, gateCardImg);

        // ── "PRESS E TO CLOSE" footer ──
        GameObject closeLabelGO = new GameObject("CloseLabel");
        closeLabelGO.transform.SetParent(root.transform, false);
        RectTransform closeLabelRT = closeLabelGO.AddComponent<RectTransform>();
        closeLabelRT.anchorMin = new Vector2(0f, 0.01f);
        closeLabelRT.anchorMax = new Vector2(1f, 0.07f);
        closeLabelRT.offsetMin = Vector2.zero;
        closeLabelRT.offsetMax = Vector2.zero;
        TextMeshProUGUI closeTMP = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "PRESS  E  OR  J  TO  CLOSE";
        closeTMP.fontSize = 22f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color = new Color(1f, 0.92f, 0.68f, 0.65f);
        closeTMP.raycastTarget = false;
        if (font != null) closeTMP.font = font;
    }

    // ── Helpers ──

    private void LoadTabImage(string gateType, Image target)
    {
        if (!GateImagePaths.TryGetValue(gateType, out string path)) return;
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex == null) { target.enabled = false; return; }
        target.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        target.enabled = true;
    }

    private void SetPlayerControls(bool enable)
    {
        if (fpc == null) fpc = FindAnyObjectByType<FirstPersonController>();
        if (inputs == null) inputs = FindAnyObjectByType<StarterAssetsInputs>();

        if (fpc != null) fpc.enabled = enable;
        if (inputs != null) inputs.enabled = enable;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }
}
