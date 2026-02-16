using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using StarterAssets;

public class GameInventoryUI : MonoBehaviour
{
    public static GameInventoryUI Instance { get; private set; }

    // Runtime references (built in code, not serialized)
    private TextMeshProUGUI andCountText;
    private TextMeshProUGUI orCountText;
    private TextMeshProUGUI notCountText;
    private TextMeshProUGUI andLabelText;
    private TextMeshProUGUI orLabelText;
    private TextMeshProUGUI notLabelText;
    private TextMeshProUGUI totalCountText; // "X/5" display

    private Image andSlotBG;
    private Image orSlotBG;
    private Image notSlotBG;

    private Transform notificationParent;
    private GameObject inventoryBarGO;

    // ==============================
    // MEDIEVAL COLOR PALETTE
    // ==============================
    // Dark brown backgrounds (matching your Container/BG textures)
    private Color barBGColor       = new Color(0.12f, 0.09f, 0.05f, 0.92f);  // Very dark brown
    private Color slotBGColor      = new Color(0.16f, 0.12f, 0.07f, 0.90f);  // Dark brown slot
    private Color slotBorderColor  = new Color(0.65f, 0.52f, 0.28f, 0.80f);  // Gold border

    // Gold / amber text & accent colors (matching your Cinzel gold text)
    private Color goldText         = new Color(0.84f, 0.75f, 0.50f, 1f);     // Gold for labels
    private Color creamText        = new Color(0.95f, 0.90f, 0.75f, 1f);     // Cream for counts
    private Color goldBorder       = new Color(0.72f, 0.58f, 0.30f, 1f);     // Gold border outline
    private Color goldBorderDim    = new Color(0.50f, 0.40f, 0.22f, 0.6f);   // Dimmer gold for subtle borders

    // Gate accent colors (muted medieval tones)
    private Color andColor  = new Color(0.35f, 0.55f, 0.80f, 1f);   // Royal blue
    private Color orColor   = new Color(0.85f, 0.65f, 0.25f, 1f);   // Amber/gold
    private Color notColor  = new Color(0.78f, 0.22f, 0.22f, 1f);   // Crimson/burgundy

    // Notification background
    private Color notifBGColor = new Color(0.14f, 0.11f, 0.06f, 0.93f);

    // Flash color (golden glow instead of white)
    private Color flashColor = new Color(0.90f, 0.78f, 0.40f, 0.7f);

    // Stamina bar colors
    private Color staminaBarColor     = new Color(0.90f, 0.55f, 0.10f, 1f);   // Orange
    private Color staminaBarLowColor  = new Color(0.90f, 0.25f, 0.10f, 1f);   // Red-orange when low
    private Color staminaBarBGColor   = new Color(0.10f, 0.08f, 0.04f, 0.85f); // Very dark brown

    // Stamina UI references
    private Image staminaFillImage;
    private GameObject staminaBarRoot;
    private FirstPersonController fpsController;
    private CanvasGroup staminaCanvasGroup;
    private float staminaBarAlpha = 0f; // For fade in/out

    // Cached font
    private TMP_FontAsset medievalFont;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        LoadMedievalFont();
        BuildUI();
        RefreshFromInventory();
    }

    void Update()
    {
        UpdateStaminaBar();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Loads the Cinzel TMP font from Resources to match the medieval theme.
    /// </summary>
    private void LoadMedievalFont()
    {
        // Try to load Cinzel font first (your medieval font)
        medievalFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");

        if (medievalFont == null)
        {
            // Fallback: try loading from other common paths
            medievalFont = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        }

        if (medievalFont == null)
        {
            // Final fallback: use the default TMP font
            medievalFont = TMP_Settings.defaultFontAsset;
            Debug.Log("GameInventoryUI: Cinzel font not found in Resources, using default TMP font.");
        }
        else
        {
            Debug.Log("GameInventoryUI: Cinzel medieval font loaded successfully.");
        }
    }

    /// <summary>
    /// Builds the entire inventory UI hierarchy in code with medieval styling.
    /// </summary>
    private void BuildUI()
    {
        Canvas targetCanvas = FindLevelCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("GameInventoryUI: No LevelCanvas found — cannot build UI.");
            return;
        }

        RectTransform canvasRT = targetCanvas.GetComponent<RectTransform>();

        // ===== INVENTORY BAR (bottom-center, same width as stamina bar) =====
        // Outer gold border frame
        GameObject barBorderGO = CreateUIObject("InventoryBarBorder", canvasRT);
        RectTransform borderRT = barBorderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0.3f, 0f);
        borderRT.anchorMax = new Vector2(0.7f, 0f);
        borderRT.pivot = new Vector2(0.5f, 0f);
        borderRT.anchoredPosition = new Vector2(0f, 6f);
        borderRT.sizeDelta = new Vector2(0f, 62f);

        Image borderBG = barBorderGO.AddComponent<Image>();
        borderBG.color = goldBorder;

        // Inner dark brown bar (the actual inventory content area)
        inventoryBarGO = CreateUIObject("InventoryBar", barBorderGO.transform);
        RectTransform barRT = inventoryBarGO.GetComponent<RectTransform>();
        barRT.anchorMin = Vector2.zero;
        barRT.anchorMax = Vector2.one;
        barRT.offsetMin = new Vector2(2f, 2f);   // 2px gold border on all sides
        barRT.offsetMax = new Vector2(-2f, -2f);

        Image barBG = inventoryBarGO.AddComponent<Image>();
        barBG.color = barBGColor;

        // Horizontal layout
        HorizontalLayoutGroup hlg = inventoryBarGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(6, 6, 3, 3);
        hlg.spacing = 5f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Add a subtle top decorative line (gold separator at top of bar)
        GameObject topLine = CreateUIObject("TopDecor", barBorderGO.transform);
        RectTransform topLineRT = topLine.GetComponent<RectTransform>();
        topLineRT.anchorMin = new Vector2(0.1f, 1f);
        topLineRT.anchorMax = new Vector2(0.9f, 1f);
        topLineRT.pivot = new Vector2(0.5f, 1f);
        topLineRT.anchoredPosition = new Vector2(0f, 1f);
        topLineRT.sizeDelta = new Vector2(0f, 1f);
        Image topLineImg = topLine.AddComponent<Image>();
        topLineImg.color = new Color(goldBorder.r, goldBorder.g, goldBorder.b, 0.5f);

        // Create 3 gate slots with medieval styling
        CreateMedievalSlot(inventoryBarGO.transform, "AND", andColor, out andSlotBG, out andLabelText, out andCountText);
        CreateMedievalSlot(inventoryBarGO.transform, "OR", orColor, out orSlotBG, out orLabelText, out orCountText);
        CreateMedievalSlot(inventoryBarGO.transform, "NOT", notColor, out notSlotBG, out notLabelText, out notCountText);

        // ── Total counter (X/5) ──
        GameObject totalGO = CreateUIObject("TotalCount", inventoryBarGO.transform);
        totalGO.AddComponent<CanvasRenderer>();
        LayoutElement totalLE = totalGO.AddComponent<LayoutElement>();
        totalLE.preferredWidth = 45f;
        totalLE.flexibleWidth = 0f;

        totalCountText = totalGO.AddComponent<TextMeshProUGUI>();
        totalCountText.text = "0/5";
        totalCountText.fontSize = 14;
        totalCountText.fontStyle = FontStyles.Bold;
        totalCountText.color = goldBorderDim;
        totalCountText.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) totalCountText.font = medievalFont;

        // ===== SETTINGS ICON (top-right corner, gear icon) =====
        BuildSettingsIcon(canvasRT);

        // ===== NOTIFICATION AREA (right side) =====
        GameObject notifArea = CreateUIObject("NotificationArea", canvasRT);
        RectTransform notifRT = notifArea.GetComponent<RectTransform>();
        notifRT.anchorMin = new Vector2(1f, 0.3f);
        notifRT.anchorMax = new Vector2(1f, 0.8f);
        notifRT.pivot = new Vector2(1f, 1f);
        notifRT.anchoredPosition = new Vector2(-15f, 0f);
        notifRT.sizeDelta = new Vector2(320f, 0f);

        VerticalLayoutGroup vlg = notifArea.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperRight;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = notifArea.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        notificationParent = notifArea.transform;

        // ===== STAMINA BAR (below inventory bar, or above it) =====
        BuildStaminaBar(canvasRT);

        Debug.Log("GameInventoryUI: Medieval-themed UI built successfully.");
    }

    /// <summary>
    /// Builds a medieval-styled stamina bar above the inventory bar.
    /// It fades in when sprinting/stamina is not full and fades out when full.
    /// </summary>
    private void BuildStaminaBar(RectTransform canvasRT)
    {
        // Root container (positioned just above the inventory bar)
        staminaBarRoot = CreateUIObject("StaminaBar", canvasRT);
        RectTransform rootRT = staminaBarRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.3f, 0f);
        rootRT.anchorMax = new Vector2(0.7f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(0f, 72f); // Just above the smaller inventory bar
        rootRT.sizeDelta = new Vector2(0f, 12f);

        // Canvas group for fading
        staminaCanvasGroup = staminaBarRoot.AddComponent<CanvasGroup>();
        staminaCanvasGroup.alpha = 0f;

        // Gold border frame
        Image borderImg = staminaBarRoot.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        // Dark background (inside the border)
        GameObject bgGO = CreateUIObject("BG", staminaBarRoot.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(1.5f, 1.5f);
        bgRT.offsetMax = new Vector2(-1.5f, -1.5f);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = staminaBarBGColor;

        // Orange fill bar (anchored left, stretches by width)
        GameObject fillGO = CreateUIObject("Fill", bgGO.transform);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f); // Will be controlled by code
        fillRT.offsetMin = new Vector2(1f, 1f);
        fillRT.offsetMax = new Vector2(-1f, -1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        staminaFillImage = fillGO.AddComponent<Image>();
        staminaFillImage.color = staminaBarColor;

        // Small "STAMINA" label (optional, subtle)
        GameObject labelGO = CreateUIObject("Label", staminaBarRoot.transform);
        labelGO.AddComponent<CanvasRenderer>();
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "STAMINA";
        labelTMP.fontSize = 8;
        labelTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        labelTMP.color = new Color(1f, 1f, 1f, 0.4f);
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.characterSpacing = 8f;
        if (medievalFont != null) labelTMP.font = medievalFont;
    }

    private Canvas FindLevelCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.gameObject.name == "LevelCanvas" && c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.isRootCanvas)
                return c;
        }
        return null;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// Creates a single gate slot with medieval styling: gold-bordered dark panel,
    /// colored accent stripe, gate label in gold, count in cream.
    /// </summary>
    private void CreateMedievalSlot(Transform parent, string gateType, Color accentColor,
        out Image slotBG, out TextMeshProUGUI labelText, out TextMeshProUGUI countText)
    {
        // Outer border (gold frame per slot)
        GameObject slotBorderGO = CreateUIObject($"{gateType}_SlotBorder", parent);
        RectTransform slotBorderRT = slotBorderGO.GetComponent<RectTransform>();
        slotBorderRT.sizeDelta = new Vector2(100f, 50f);

        Image borderImg = slotBorderGO.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        // Add layout element so the HLG knows how to size it
        LayoutElement slotLE = slotBorderGO.AddComponent<LayoutElement>();
        slotLE.flexibleWidth = 1f;

        // Inner dark background
        GameObject slotGO = CreateUIObject($"{gateType}_Slot", slotBorderGO.transform);
        RectTransform slotRT = slotGO.GetComponent<RectTransform>();
        slotRT.anchorMin = Vector2.zero;
        slotRT.anchorMax = Vector2.one;
        slotRT.offsetMin = new Vector2(1.5f, 1.5f);   // 1.5px gold border
        slotRT.offsetMax = new Vector2(-1.5f, -1.5f);

        slotBG = slotGO.AddComponent<Image>();
        slotBG.color = slotBGColor;

        // Colored accent stripe at the top of the slot
        GameObject stripeGO = CreateUIObject("AccentStripe", slotGO.transform);
        RectTransform stripeRT = stripeGO.GetComponent<RectTransform>();
        stripeRT.anchorMin = new Vector2(0f, 1f);
        stripeRT.anchorMax = new Vector2(1f, 1f);
        stripeRT.pivot = new Vector2(0.5f, 1f);
        stripeRT.anchoredPosition = Vector2.zero;
        stripeRT.sizeDelta = new Vector2(0f, 3f);
        Image stripeImg = stripeGO.AddComponent<Image>();
        Color stripeColor = accentColor;
        stripeColor.a = 0.8f;
        stripeImg.color = stripeColor;

        // Vertical layout for label + count
        VerticalLayoutGroup vlg = slotGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(3, 3, 2, 2);
        vlg.spacing = 0f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Gate label (gold medieval text)
        GameObject labelGO = CreateUIObject("Label", slotGO.transform);
        labelGO.AddComponent<CanvasRenderer>();
        labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = gateType;
        labelText.fontSize = 11;
        labelText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        labelText.color = goldText;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.characterSpacing = 4f;
        if (medievalFont != null) labelText.font = medievalFont;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(80f, 16f);

        // Count text (cream/white, larger)
        GameObject countGO = CreateUIObject("Count", slotGO.transform);
        countGO.AddComponent<CanvasRenderer>();
        countText = countGO.AddComponent<TextMeshProUGUI>();
        countText.text = "0";
        countText.fontSize = 18;
        countText.fontStyle = FontStyles.Bold;
        countText.color = creamText;
        countText.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) countText.font = medievalFont;
        RectTransform countRT = countGO.GetComponent<RectTransform>();
        countRT.sizeDelta = new Vector2(80f, 26f);

        // Add subtle outline on count text for readability
        countText.outlineWidth = 0.15f;
        countText.outlineColor = new Color32(0, 0, 0, 128);
    }

    // ============================
    // PUBLIC API
    // ============================

    public void UpdateCounts(int andCount, int orCount, int notCount)
    {
        if (andCountText != null) andCountText.text = andCount.ToString();
        if (orCountText != null) orCountText.text = orCount.ToString();
        if (notCountText != null) notCountText.text = notCount.ToString();

        // Update total counter
        if (totalCountText != null)
        {
            int total = andCount + orCount + notCount;
            totalCountText.text = $"{total}/{InventoryManager.MAX_GATES}";

            // Change color when full
            if (total >= InventoryManager.MAX_GATES)
                totalCountText.color = new Color(0.9f, 0.3f, 0.2f, 1f); // Red when full
            else
                totalCountText.color = goldBorderDim;
        }
    }

    public void RefreshFromInventory()
    {
        if (InventoryManager.Instance != null)
        {
            UpdateCounts(
                InventoryManager.Instance.GetGateCount("AND"),
                InventoryManager.Instance.GetGateCount("OR"),
                InventoryManager.Instance.GetGateCount("NOT")
            );
        }
    }

    public void OnGateCollected(string gateType)
    {
        RefreshFromInventory();
        FlashSlot(gateType);
        ShowNotification(gateType);
    }

    // ============================
    // VISUAL EFFECTS
    // ============================

    private void FlashSlot(string gateType)
    {
        Image slotBG = null;
        switch (gateType.ToUpper())
        {
            case "AND": slotBG = andSlotBG; break;
            case "OR":  slotBG = orSlotBG;  break;
            case "NOT": slotBG = notSlotBG; break;
        }

        if (slotBG != null)
            StartCoroutine(FlashSlotCoroutine(slotBG));
    }

    private IEnumerator FlashSlotCoroutine(Image slotBG)
    {
        Color originalColor = slotBG.color;

        // Flash with golden glow (medieval feel)
        slotBG.color = flashColor;
        yield return new WaitForSeconds(0.12f);
        slotBG.color = originalColor;
        yield return new WaitForSeconds(0.08f);
        slotBG.color = flashColor;
        yield return new WaitForSeconds(0.08f);
        slotBG.color = originalColor;
    }

    /// <summary>
    /// Shows a medieval-styled notification when a gate is collected.
    /// Dark brown panel with gold border and parchment-style text.
    /// </summary>
    private void ShowNotification(string gateType)
    {
        if (notificationParent == null) return;

        Color accentColor = GetGateColor(gateType);

        // Outer gold border
        GameObject notifBorderGO = CreateUIObject($"Notif_{gateType}_Border", notificationParent);
        RectTransform borderRT = notifBorderGO.GetComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(260f, 48f);

        Image borderBG = notifBorderGO.AddComponent<Image>();
        borderBG.color = goldBorderDim;

        // Inner dark panel
        GameObject notifGO = CreateUIObject($"Notif_{gateType}", notifBorderGO.transform);
        RectTransform notifInnerRT = notifGO.GetComponent<RectTransform>();
        notifInnerRT.anchorMin = Vector2.zero;
        notifInnerRT.anchorMax = Vector2.one;
        notifInnerRT.offsetMin = new Vector2(1.5f, 1.5f);
        notifInnerRT.offsetMax = new Vector2(-1.5f, -1.5f);

        Image notifBG = notifGO.AddComponent<Image>();
        notifBG.color = notifBGColor;

        // Colored accent stripe on the left
        GameObject leftStripeGO = CreateUIObject("LeftStripe", notifGO.transform);
        RectTransform stripeRT = leftStripeGO.GetComponent<RectTransform>();
        stripeRT.anchorMin = new Vector2(0f, 0f);
        stripeRT.anchorMax = new Vector2(0f, 1f);
        stripeRT.pivot = new Vector2(0f, 0.5f);
        stripeRT.anchoredPosition = Vector2.zero;
        stripeRT.sizeDelta = new Vector2(4f, 0f);
        Image stripeImg = leftStripeGO.AddComponent<Image>();
        stripeImg.color = accentColor;

        // Notification text
        GameObject textGO = CreateUIObject("Text", notifGO.transform);
        textGO.AddComponent<CanvasRenderer>();
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(14, 5);
        textRT.offsetMax = new Vector2(-10, -5);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = $"+ {gateType} Gate Collected!";
        tmp.fontSize = 18;
        tmp.color = goldText;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (medievalFont != null) tmp.font = medievalFont;

        // Small icon/symbol indicator using the accent color
        GameObject symbolGO = CreateUIObject("Symbol", notifGO.transform);
        RectTransform symbolRT = symbolGO.GetComponent<RectTransform>();
        symbolRT.anchorMin = new Vector2(1f, 0.5f);
        symbolRT.anchorMax = new Vector2(1f, 0.5f);
        symbolRT.pivot = new Vector2(1f, 0.5f);
        symbolRT.anchoredPosition = new Vector2(-10f, 0f);
        symbolRT.sizeDelta = new Vector2(8f, 8f);
        Image symbolImg = symbolGO.AddComponent<Image>();
        symbolImg.color = accentColor;

        StartCoroutine(AnimateNotification(notifBorderGO));
    }

    private IEnumerator AnimateNotification(GameObject notifGO)
    {
        if (notifGO == null) yield break;

        RectTransform rt = notifGO.GetComponent<RectTransform>();
        CanvasGroup cg = notifGO.AddComponent<CanvasGroup>();

        float startX = 320f;
        float targetX = 0f;
        float slideDuration = 0.35f;
        float elapsed = 0f;

        rt.anchoredPosition = new Vector2(startX, rt.anchoredPosition.y);
        cg.alpha = 0f;

        // Slide in with fade
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            float smoothT = t * t * (3f - 2f * t);  // Smoothstep
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, smoothT), rt.anchoredPosition.y);
            cg.alpha = Mathf.Lerp(0f, 1f, smoothT);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(targetX, rt.anchoredPosition.y);
        cg.alpha = 1f;

        // Stay visible
        yield return new WaitForSeconds(2.5f);

        // Fade out elegantly
        float fadeDuration = 0.6f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            cg.alpha = 1f - (t * t);  // Ease-out fade
            yield return null;
        }

        Destroy(notifGO);
    }

    private Color GetGateColor(string gateType)
    {
        switch (gateType.ToUpper())
        {
            case "AND": return andColor;
            case "OR":  return orColor;
            case "NOT": return notColor;
            default:    return goldText;
        }
    }

    // ============================
    // SETTINGS ICON (GEAR)
    // ============================

    /// <summary>
    /// Builds a small gear/settings icon button in the top-right corner of the screen.
    /// Clicking it opens the pause menu.
    /// </summary>
    private void BuildSettingsIcon(RectTransform canvasRT)
    {
        // Outer border (gold frame)
        GameObject iconBorderGO = CreateUIObject("SettingsIconBorder", canvasRT);
        RectTransform borderRT = iconBorderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(1f, 1f);
        borderRT.anchorMax = new Vector2(1f, 1f);
        borderRT.pivot = new Vector2(1f, 1f);
        borderRT.anchoredPosition = new Vector2(-15f, -15f);
        borderRT.sizeDelta = new Vector2(44f, 44f);

        Image borderImg = iconBorderGO.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        // Inner dark background
        GameObject iconBGGO = CreateUIObject("SettingsIconBG", iconBorderGO.transform);
        RectTransform bgRT = iconBGGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(1.5f, 1.5f);
        bgRT.offsetMax = new Vector2(-1.5f, -1.5f);

        Image bgImg = iconBGGO.AddComponent<Image>();
        bgImg.color = new Color(barBGColor.r, barBGColor.g, barBGColor.b, 0.85f);

        // Gear icon built from UI primitives (avoids font/Unicode issues)
        BuildGearGraphic(iconBGGO.transform, goldText);

        // Button component on the border
        Button settingsBtn = iconBorderGO.AddComponent<Button>();
        settingsBtn.targetGraphic = bgImg;

        var colors = settingsBtn.colors;
        colors.normalColor = bgImg.color;
        colors.highlightedColor = new Color(0.22f, 0.18f, 0.10f, 0.95f);
        colors.pressedColor = new Color(0.28f, 0.22f, 0.14f, 0.95f);
        settingsBtn.colors = colors;

        settingsBtn.onClick.AddListener(() =>
        {
            if (PauseMenuController.Instance != null)
                PauseMenuController.Instance.Pause();
            else
                Debug.LogWarning("[GameInventoryUI] PauseMenuController not found!");
        });
    }

    /// <summary>
    /// Draws a simple hamburger menu icon (three horizontal bars).
    /// Uses only basic UI Image components — no fonts, no sprites, no external resources.
    /// </summary>
    private void BuildGearGraphic(Transform parent, Color color)
    {
        // Container for the three bars
        GameObject iconRoot = CreateUIObject("MenuIcon", parent);
        RectTransform rootRT = iconRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.2f, 0.2f);
        rootRT.anchorMax = new Vector2(0.8f, 0.8f);
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Three horizontal bars evenly spaced
        float[] barYPositions = { 0.78f, 0.45f, 0.12f }; // top, middle, bottom
        float barHeight = 0.18f;

        for (int i = 0; i < 3; i++)
        {
            GameObject bar = CreateUIObject("Bar" + i, iconRoot.transform);
            RectTransform barRT = bar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.1f, barYPositions[i]);
            barRT.anchorMax = new Vector2(0.9f, barYPositions[i] + barHeight);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = Vector2.zero;

            Image barImg = bar.AddComponent<Image>();
            barImg.color = color;
            barImg.raycastTarget = false;
        }
    }

    // ============================
    // STAMINA BAR UPDATE
    // ============================

    private void UpdateStaminaBar()
    {
        if (staminaFillImage == null || staminaCanvasGroup == null) return;

        // Find the controller if we don't have it yet
        if (fpsController == null)
        {
            fpsController = FindFirstObjectByType<FirstPersonController>();
            if (fpsController == null) return;
        }

        float pct = fpsController.StaminaPercent;

        // Update fill bar width (anchorMax.x = percentage)
        RectTransform fillRT = staminaFillImage.GetComponent<RectTransform>();
        fillRT.anchorMax = new Vector2(pct, 1f);

        // Color: orange normally, red-orange when below 25%
        staminaFillImage.color = pct < 0.25f
            ? Color.Lerp(staminaBarLowColor, staminaBarColor, pct / 0.25f)
            : staminaBarColor;

        // Fade in/out: show when stamina is not full, hide when full
        float targetAlpha = pct < 0.99f ? 1f : 0f;
        staminaBarAlpha = Mathf.MoveTowards(staminaBarAlpha, targetAlpha, Time.deltaTime * 3f);
        staminaCanvasGroup.alpha = staminaBarAlpha;
    }
}
