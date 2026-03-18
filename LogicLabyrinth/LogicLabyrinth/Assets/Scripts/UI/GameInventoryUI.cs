using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;

/// <summary>
/// Hotbar-style inventory UI with 8 individually selectable slots.
/// Items (AND/OR/NOT gates, Keys) appear as icons in the slots.
/// Press 1-8 to select a slot. Selected slot is highlighted with a brighter border.
/// </summary>
public class GameInventoryUI : MonoBehaviour
{
    public static GameInventoryUI Instance { get; private set; }

    // ═══════════════════════════════════
    // SLOT DATA
    // ═══════════════════════════════════
    public const int SLOT_COUNT = 8;

    /// <summary>Type of item that can occupy a hotbar slot.</summary>
    public enum ItemType { None, AND, OR, NOT, Key, Candle, Adrenaline }

    private ItemType[] slotItems = new ItemType[SLOT_COUNT];
    private int selectedSlot = -1; // 0-based index of the currently selected slot, -1 when nothing is selected

    // UI references per slot
    private GameObject[] slotRoots = new GameObject[SLOT_COUNT];
    private Image[] slotBorders = new Image[SLOT_COUNT];
    private Image[] slotBackgrounds = new Image[SLOT_COUNT];
    private Image[] slotIcons = new Image[SLOT_COUNT];
    private TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[SLOT_COUNT];
    private TextMeshProUGUI[] slotNumbers = new TextMeshProUGUI[SLOT_COUNT];

    // Other UI
    private Transform notificationParent;
    private GameObject inventoryBarGO;
    private GameObject hotbarRoot; // The outer frame

    // ═══════════════════════════════════
    // MEDIEVAL COLOR PALETTE
    // ═══════════════════════════════════
    private Color barBGColor         = new Color(0.10f, 0.07f, 0.03f, 0.92f);
    private Color slotBGColor        = new Color(0.14f, 0.10f, 0.05f, 0.92f);
    private Color slotBorderNormal   = new Color(0.45f, 0.35f, 0.18f, 0.85f);
    private Color slotBorderSelected = new Color(0.95f, 0.78f, 0.35f, 1f);
    private Color slotBGSelected     = new Color(0.20f, 0.15f, 0.08f, 0.95f);
    private Color goldText           = new Color(0.84f, 0.75f, 0.50f, 1f);
    private Color creamText          = new Color(0.95f, 0.90f, 0.75f, 1f);
    private Color dimText            = new Color(0.45f, 0.38f, 0.25f, 0.6f);

    // Gate accent colors
    private Color andColor    = new Color(0.35f, 0.55f, 0.80f, 1f);
    private Color orColor     = new Color(0.85f, 0.65f, 0.25f, 1f);
    private Color notColor    = new Color(0.78f, 0.22f, 0.22f, 1f);
    private Color keyColor    = new Color(0.95f, 0.82f, 0.30f, 1f);
    private Color candleColor = new Color(1f, 0.85f, 0.55f, 1f);
    private Color adrenalineColor = new Color(0.95f, 0.30f, 0.30f, 1f);

    // Notification
    private Color notifBGColor = new Color(0.14f, 0.11f, 0.06f, 0.93f);
    private Color flashColor   = new Color(0.90f, 0.78f, 0.40f, 0.7f);
    private Color goldBorderDim = new Color(0.50f, 0.40f, 0.22f, 0.6f);

    // Stamina
    private Color staminaBarColor     = new Color(0.90f, 0.55f, 0.10f, 1f);
    private Color staminaBarLowColor  = new Color(0.90f, 0.25f, 0.10f, 1f);
    private Color staminaBarBGColor   = new Color(0.10f, 0.08f, 0.04f, 0.85f);

    // Health
    private Color healthBarColor      = new Color(0.88f, 0.12f, 0.12f, 1f);
    private Color healthBarLowColor   = new Color(0.45f, 0.06f, 0.06f, 1f);
    private Color healthBarBGColor    = new Color(0.14f, 0.04f, 0.04f, 0.90f);

    private Image staminaFillImage;
    private GameObject staminaBarRoot;
    private Image healthFillImage;
    private GameObject healthBarRoot;
    private FirstPersonController fpsController;
    private CanvasGroup staminaCanvasGroup;
    private float staminaBarAlpha = 0f;

    [SerializeField, Range(0f, 1f)]
    private float healthPercent = 1f;

    private TMP_FontAsset medievalFont;

    // Q-Drop slot
    private GameObject qSlotRoot;
    private Image qSlotIconImage;
    private TextMeshProUGUI qSlotLabelTMP;
    private Image qSlotBorderImage;

    // ═══════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Safety: reset stale static key flags so the hotbar starts clean.
        // These statics can persist from a previous play session in the editor
        // or from a previous level that didn't go through InventoryManager.ResetInventory().
        TutorialDoor.PlayerHasKey = false;
        SuccessDoor.PlayerHasSuccessKey = false;

        LoadMedievalFont();
        BuildUI();
        RefreshFromInventory();
    }

    void Update()
    {
        HandleNumberKeyInput();
        UpdateStaminaBar();
        UpdateHealthBar();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════
    // NUMBER KEY INPUT (1-8)
    // ═══════════════════════════════════

    private void HandleNumberKeyInput()
    {
        // Block input during UI/cutscene
        if (PuzzleTableController.IsOpen || PauseMenuController.IsPaused ||
            CutsceneController.IsPlaying || CutsceneController.CameraOnlyMode)
            return;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectSlot(i);
                break;
            }
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SLOT_COUNT) return;

        if (index == selectedSlot)
        {
            if (selectedSlot >= 0 && selectedSlot < SLOT_COUNT && slotItems[selectedSlot] == ItemType.Candle)
                CollectibleCandle.Unequip();

            selectedSlot = -1;
            UpdateSlotVisuals();
            Debug.Log($"[Hotbar] Deselected slot {index + 1}");
            return;
        }

        // Check if we're de-selecting a candle slot (unequip)
        if (selectedSlot >= 0 && selectedSlot < SLOT_COUNT && slotItems[selectedSlot] == ItemType.Candle)
        {
            if (index != selectedSlot)
                CollectibleCandle.Unequip();
        }

        selectedSlot = index;
        UpdateSlotVisuals();

        // If new selection is a candle, equip it
        if (slotItems[index] == ItemType.Candle)
        {
            CollectibleCandle.Equip();
        }

        Debug.Log($"[Hotbar] Selected slot {index + 1}: {slotItems[index]}");
    }

    public ItemType GetSelectedItem()
    {
        if (selectedSlot < 0 || selectedSlot >= SLOT_COUNT)
            return ItemType.None;

        return slotItems[selectedSlot];
    }

    public int GetSelectedSlotIndex() => selectedSlot;

    // ═══════════════════════════════════
    // PUBLIC API (InventoryManager calls these)
    // ═══════════════════════════════════

    /// <summary>
    /// Rebuild slot contents from InventoryManager gate counts + key state.
    /// Called when gate counts change.
    /// </summary>
    public void UpdateCounts(int andCount, int orCount, int notCount)
    {
        RebuildSlots(andCount, orCount, notCount);
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

        // Flash the slot that just received this gate
        int slotIndex = FindLastSlotOf(StringToItemType(gateType));
        if (slotIndex >= 0) FlashSlot(slotIndex);

        ShowNotification(gateType);
    }

    /// <summary>
    /// Call when a key is picked up to add it to the hotbar.
    /// </summary>
    public void OnKeyCollected()
    {
        RefreshFromInventory(); // Will include key in rebuild
        int slotIndex = FindLastSlotOf(ItemType.Key);
        if (slotIndex >= 0) FlashSlot(slotIndex);
        ShowNotification("KEY");
    }

    /// <summary>
    /// Call when a candle is picked up to add it to the hotbar.
    /// </summary>
    public void OnCandleCollected()
    {
        RefreshFromInventory();
        int slotIndex = FindLastSlotOf(ItemType.Candle);
        if (slotIndex >= 0) FlashSlot(slotIndex);
        ShowNotification("CANDLE");
    }

    // ═══════════════════════════════════
    // SLOT MANAGEMENT
    // ═══════════════════════════════════

    /// <summary>
    /// Rebuild all 8 slot items from current inventory state.
    /// Order: AND gates, OR gates, NOT gates, Key (if held), then empty.
    /// </summary>
    private void RebuildSlots(int andCount, int orCount, int notCount)
    {
        // Clear
        for (int i = 0; i < SLOT_COUNT; i++)
            slotItems[i] = ItemType.None;

        int slot = 0;

        // Fill AND gates
        for (int i = 0; i < andCount && slot < SLOT_COUNT; i++, slot++)
            slotItems[slot] = ItemType.AND;

        // Fill OR gates
        for (int i = 0; i < orCount && slot < SLOT_COUNT; i++, slot++)
            slotItems[slot] = ItemType.OR;

        // Fill NOT gates
        for (int i = 0; i < notCount && slot < SLOT_COUNT; i++, slot++)
            slotItems[slot] = ItemType.NOT;

        // Fill Key if player has one (either tutorial or success key)
        bool hasKey = TutorialDoor.PlayerHasKey || SuccessDoor.PlayerHasSuccessKey;
        if (hasKey && slot < SLOT_COUNT)
        {
            slotItems[slot] = ItemType.Key;
            slot++;
        }

        // Fill Candle if player has one
        bool hasCandle = (InventoryManager.Instance != null && InventoryManager.Instance.HasCandle);
        if (hasCandle && slot < SLOT_COUNT)
        {
            slotItems[slot] = ItemType.Candle;
            slot++;
        }

        // Fill Adrenaline if player has at least one from the store
        bool hasAdrenaline = (AccountManager.Instance != null && AccountManager.Instance.GetAdrenalineCount() > 0);
        if (hasAdrenaline && slot < SLOT_COUNT)
        {
            slotItems[slot] = ItemType.Adrenaline;
            slot++;
        }
        else if (hasAdrenaline && slot >= SLOT_COUNT)
        {
            Debug.LogWarning("[Hotbar] Adrenaline available but hotbar is full, so ADR cannot be shown.");
        }

        if (hasAdrenaline)
        {
            int adrCount = AccountManager.Instance != null ? AccountManager.Instance.GetAdrenalineCount() : 0;
            Debug.Log($"[Hotbar] RebuildSlots -> adrenalineCount={adrCount}, hasAdrenaline={hasAdrenaline}, usedSlots={slot}/{SLOT_COUNT}");
        }

        UpdateSlotVisuals();
    }

    private int FindLastSlotOf(ItemType type)
    {
        int last = -1;
        for (int i = 0; i < SLOT_COUNT; i++)
            if (slotItems[i] == type) last = i;
        return last;
    }

    private ItemType StringToItemType(string gateType)
    {
        switch (gateType.ToUpper())
        {
            case "AND":    return ItemType.AND;
            case "OR":     return ItemType.OR;
            case "NOT":    return ItemType.NOT;
            case "KEY":    return ItemType.Key;
            case "CANDLE": return ItemType.Candle;
            case "ADRENALINE": return ItemType.Adrenaline;
            default:       return ItemType.None;
        }
    }

    // ═══════════════════════════════════
    // BUILD UI
    // ═══════════════════════════════════

    private void LoadMedievalFont()
    {
        medievalFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (medievalFont == null) medievalFont = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (medievalFont == null) medievalFont = TMP_Settings.defaultFontAsset;
    }

    private void BuildUI()
    {
        Canvas targetCanvas = FindLevelCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[Hotbar] No LevelCanvas found — cannot build UI.");
            return;
        }

        RectTransform canvasRT = targetCanvas.GetComponent<RectTransform>();

        // ═══ OUTER FRAME ═══
        hotbarRoot = CreateUIObject("HotbarFrame", canvasRT);
        RectTransform frameRT = hotbarRoot.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0.5f, 0f);
        frameRT.anchorMax = new Vector2(0.5f, 0f);
        frameRT.pivot = new Vector2(0.5f, 0f);
        frameRT.anchoredPosition = new Vector2(0f, 8f);
        // Width = 8 slots * 64px + 7 gaps * 4px + 16px padding = 556px
        frameRT.sizeDelta = new Vector2(556f, 68f);

        Image frameBG = hotbarRoot.AddComponent<Image>();
        frameBG.color = new Color(barBGColor.r, barBGColor.g, barBGColor.b, 0.75f);

        // Subtle outer border
        Outline frameOutline = hotbarRoot.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0.40f, 0.32f, 0.16f, 0.6f);
        frameOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Horizontal layout
        HorizontalLayoutGroup hlg = hotbarRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(6, 6, 5, 5);
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        // ═══ CREATE 8 SLOTS ═══
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            CreateSlot(hotbarRoot.transform, i);
        }

        // ═══ SETTINGS ICON (top-right) ═══
        BuildSettingsIcon(canvasRT);

        // ═══ NOTIFICATION AREA (right side) ═══
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

        // ═══ STAMINA BAR ═══
        BuildStaminaBar(canvasRT);
        BuildHealthBar(canvasRT);

        // ═══ Q DROP SLOT ═══
        BuildQSlot(canvasRT);

        Debug.Log("[Hotbar] Hotbar inventory UI built successfully.");
    }

    /// <summary>Creates a single hotbar slot.</summary>
    private void CreateSlot(Transform parent, int index)
    {
        // ── Border (outer) ──
        GameObject borderGO = CreateUIObject($"Slot{index + 1}_Border", parent);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(62f, 58f);

        LayoutElement le = borderGO.AddComponent<LayoutElement>();
        le.preferredWidth = 62f;
        le.preferredHeight = 58f;

        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = slotBorderNormal;
        slotBorders[index] = borderImg;

        slotRoots[index] = borderGO;

        // ── Inner background ──
        GameObject bgGO = CreateUIObject("BG", borderGO.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(2f, 2f);
        bgRT.offsetMax = new Vector2(-2f, -2f);

        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = slotBGColor;
        slotBackgrounds[index] = bgImg;

        // ── Item icon (colored square representing the item) ──
        GameObject iconGO = CreateUIObject("Icon", bgGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.22f);
        iconRT.anchorMax = new Vector2(0.85f, 0.88f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.clear; // Hidden until item placed
        iconImg.raycastTarget = false;
        slotIcons[index] = iconImg;

        // ── Item label text (AND/OR/NOT/KEY) ──
        GameObject labelGO = CreateUIObject("Label", bgGO.transform);
        labelGO.AddComponent<CanvasRenderer>();
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.15f);
        labelRT.anchorMax = new Vector2(1f, 0.90f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "";
        labelTMP.fontSize = 14;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color = creamText;
        labelTMP.enableWordWrapping = false;
        labelTMP.raycastTarget = false;
        if (medievalFont != null) labelTMP.font = medievalFont;
        slotLabels[index] = labelTMP;

        // ── Slot number (small, bottom-right corner) ──
        GameObject numGO = CreateUIObject("Number", bgGO.transform);
        numGO.AddComponent<CanvasRenderer>();
        RectTransform numRT = numGO.GetComponent<RectTransform>();
        numRT.anchorMin = new Vector2(0.65f, 0f);
        numRT.anchorMax = new Vector2(1f, 0.30f);
        numRT.offsetMin = Vector2.zero;
        numRT.offsetMax = Vector2.zero;

        TextMeshProUGUI numTMP = numGO.AddComponent<TextMeshProUGUI>();
        numTMP.text = (index + 1).ToString();
        numTMP.fontSize = 10;
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.alignment = TextAlignmentOptions.BottomRight;
        numTMP.color = dimText;
        numTMP.raycastTarget = false;
        if (medievalFont != null) numTMP.font = medievalFont;
        slotNumbers[index] = numTMP;
    }

    // ═══════════════════════════════════
    // Q-DROP SLOT
    // ═══════════════════════════════════

    private void BuildQSlot(RectTransform canvasRT)
    {
        // Hotbar: 556px wide, centered. Left edge = -278px from center.
        // Gap = 16px. Q frame width = 74px (slot 62 + padding 6+6).
        // Q frame center x = -278 - 16 - 37 = -331px
        qSlotRoot = CreateUIObject("QDropSlot", canvasRT);
        RectTransform rootRT = qSlotRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(-331f, 8f);
        rootRT.sizeDelta = new Vector2(74f, 68f);

        Image frameBG = qSlotRoot.AddComponent<Image>();
        frameBG.color = new Color(barBGColor.r, barBGColor.g, barBGColor.b, 0.75f);

        Outline frameOutline = qSlotRoot.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0.40f, 0.32f, 0.16f, 0.6f);
        frameOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // ── Slot border ──
        GameObject borderGO = CreateUIObject("QSlot_Border", qSlotRoot.transform);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0.5f, 0.5f);
        borderRT.anchorMax = new Vector2(0.5f, 0.5f);
        borderRT.pivot = new Vector2(0.5f, 0.5f);
        borderRT.anchoredPosition = Vector2.zero;
        borderRT.sizeDelta = new Vector2(62f, 58f);
        qSlotBorderImage = borderGO.AddComponent<Image>();
        qSlotBorderImage.color = new Color(0.55f, 0.18f, 0.12f, 0.85f);

        // ── Inner background ──
        GameObject bgGO = CreateUIObject("BG", borderGO.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(2f, 2f);
        bgRT.offsetMax = new Vector2(-2f, -2f);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = slotBGColor;

        // ── "Q" key badge — centered, behind icon and label ──
        GameObject numGO = CreateUIObject("QKey", bgGO.transform);
        numGO.AddComponent<CanvasRenderer>();
        RectTransform numRT = numGO.GetComponent<RectTransform>();
        numRT.anchorMin = new Vector2(0f, 0f);
        numRT.anchorMax = new Vector2(1f, 1f);
        numRT.offsetMin = Vector2.zero;
        numRT.offsetMax = Vector2.zero;
        TextMeshProUGUI numTMP = numGO.AddComponent<TextMeshProUGUI>();
        numTMP.text = "Q";
        numTMP.fontSize = 22;
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.alignment = TextAlignmentOptions.Center;
        numTMP.characterSpacing = 3f;
        numTMP.color = new Color(0.82f, 0.28f, 0.22f, 0.95f);
        numTMP.raycastTarget = false;
        if (medievalFont != null) numTMP.font = medievalFont;

        // ── Icon (mirrors selected slot) ──
        GameObject iconGO = CreateUIObject("Icon", bgGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.22f);
        iconRT.anchorMax = new Vector2(0.85f, 0.88f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        qSlotIconImage = iconGO.AddComponent<Image>();
        qSlotIconImage.color = Color.clear;
        qSlotIconImage.raycastTarget = false;

        // ── Item label ──
        GameObject labelGO = CreateUIObject("Label", bgGO.transform);
        labelGO.AddComponent<CanvasRenderer>();
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.15f);
        labelRT.anchorMax = new Vector2(1f, 0.90f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        qSlotLabelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        qSlotLabelTMP.text = "";
        qSlotLabelTMP.fontSize = 14;
        qSlotLabelTMP.fontStyle = FontStyles.Bold;
        qSlotLabelTMP.alignment = TextAlignmentOptions.Center;
        qSlotLabelTMP.color = creamText;
        qSlotLabelTMP.enableWordWrapping = false;
        qSlotLabelTMP.raycastTarget = false;
        if (medievalFont != null) qSlotLabelTMP.font = medievalFont;

        UpdateQSlot();
    }

    private void UpdateQSlot()
    {
        if (qSlotIconImage == null || qSlotLabelTMP == null) return;

        ItemType selectedItem = (selectedSlot >= 0 && selectedSlot < SLOT_COUNT) ? slotItems[selectedSlot] : ItemType.None;
        bool isGate = selectedItem == ItemType.AND || selectedItem == ItemType.OR || selectedItem == ItemType.NOT;

        if (!isGate)
        {
            qSlotIconImage.color = Color.clear;
            qSlotLabelTMP.text = "";
            if (qSlotBorderImage != null)
                qSlotBorderImage.color = new Color(0.55f, 0.18f, 0.12f, 0.85f);
        }
        else
        {
            ItemType item = selectedItem;
            Color itemColor = GetItemColor(item);
            qSlotIconImage.color = new Color(itemColor.r, itemColor.g, itemColor.b, 0.25f);
            qSlotLabelTMP.text = GetItemLabel(item);
            qSlotLabelTMP.color = itemColor;
            if (qSlotBorderImage != null)
                qSlotBorderImage.color = new Color(0.72f, 0.20f, 0.15f, 0.95f);
        }
    }

    // ═══════════════════════════════════
    // UPDATE SLOT VISUALS
    // ═══════════════════════════════════

    private void UpdateSlotVisuals()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            bool isSelected = (i == selectedSlot);
            ItemType item = slotItems[i];

            // Border color
            if (slotBorders[i] != null)
                slotBorders[i].color = isSelected ? slotBorderSelected : slotBorderNormal;

            // Background color
            if (slotBackgrounds[i] != null)
                slotBackgrounds[i].color = isSelected ? slotBGSelected : slotBGColor;

            // Number text brightness
            if (slotNumbers[i] != null)
                slotNumbers[i].color = isSelected ? goldText : dimText;

            // Icon + Label
            if (item == ItemType.None)
            {
                // Empty slot
                if (slotIcons[i] != null) slotIcons[i].color = Color.clear;
                if (slotLabels[i] != null) slotLabels[i].text = "";
            }
            else
            {
                Color itemColor = GetItemColor(item);
                string itemLabel = GetItemLabel(item);
                string itemSymbol = GetItemSymbol(item);

                // Show colored icon square
                if (slotIcons[i] != null)
                {
                    slotIcons[i].color = new Color(itemColor.r, itemColor.g, itemColor.b, 0.25f);
                }

                // Show label
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = itemLabel;
                    slotLabels[i].color = itemColor;
                }
            }
        }
            UpdateQSlot();
    }

    private Color GetItemColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.AND:    return andColor;
            case ItemType.OR:     return orColor;
            case ItemType.NOT:    return notColor;
            case ItemType.Key:    return keyColor;
            case ItemType.Candle: return candleColor;
            case ItemType.Adrenaline: return adrenalineColor;
            default:              return creamText;
        }
    }

    private string GetItemLabel(ItemType type)
    {
        switch (type)
        {
            case ItemType.AND:    return "AND";
            case ItemType.OR:     return "OR";
            case ItemType.NOT:    return "NOT";
            case ItemType.Key:    return "KEY";
            case ItemType.Candle: return "CDL";
            case ItemType.Adrenaline: return "ADR";
            default:              return "";
        }
    }

    private string GetItemSymbol(ItemType type)
    {
        switch (type)
        {
            case ItemType.AND:    return "&";
            case ItemType.OR:     return "|";
            case ItemType.NOT:    return "!";
            case ItemType.Key:    return "\u26BF"; // key symbol
            case ItemType.Candle: return "\u2602"; // candle/umbrella symbol as fallback
            case ItemType.Adrenaline: return "+";
            default:              return "";
        }
    }

    // ═══════════════════════════════════
    // VISUAL EFFECTS
    // ═══════════════════════════════════

    private void FlashSlot(int index)
    {
        if (index < 0 || index >= SLOT_COUNT) return;
        if (slotBorders[index] != null)
            StartCoroutine(FlashSlotCoroutine(index));
    }

    private IEnumerator FlashSlotCoroutine(int index)
    {
        Image border = slotBorders[index];
        if (border == null) yield break;

        Color original = border.color;

        for (int flash = 0; flash < 3; flash++)
        {
            border.color = flashColor;
            yield return new WaitForSeconds(0.08f);
            border.color = original;
            yield return new WaitForSeconds(0.06f);
        }

        // Restore correct color
        border.color = (index == selectedSlot) ? slotBorderSelected : slotBorderNormal;
    }

    private void ShowNotification(string itemType)
    {
        if (notificationParent == null) return;

        Color accentColor;
        string label;
        switch (itemType.ToUpper())
        {
            case "AND":    accentColor = andColor;    label = "+ AND Gate Collected!"; break;
            case "OR":     accentColor = orColor;     label = "+ OR Gate Collected!";  break;
            case "NOT":    accentColor = notColor;    label = "+ NOT Gate Collected!"; break;
            case "KEY":    accentColor = keyColor;    label = "+ Key Collected!";      break;
            case "CANDLE": accentColor = candleColor; label = "+ Candle Collected!";   break;
            case "ADRENALINE": accentColor = adrenalineColor; label = "+ Adrenaline Acquired!"; break;
            default:       accentColor = goldText;    label = $"+ {itemType} Collected!"; break;
        }

        // Outer gold border
        GameObject notifBorderGO = CreateUIObject($"Notif_{itemType}_Border", notificationParent);
        RectTransform borderRT = notifBorderGO.GetComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(260f, 48f);

        Image borderBG = notifBorderGO.AddComponent<Image>();
        borderBG.color = goldBorderDim;

        // Inner dark panel
        GameObject notifGO = CreateUIObject($"Notif_{itemType}", notifBorderGO.transform);
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

        // Text
        GameObject textGO = CreateUIObject("Text", notifGO.transform);
        textGO.AddComponent<CanvasRenderer>();
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(14, 5);
        textRT.offsetMax = new Vector2(-10, -5);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.color = goldText;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (medievalFont != null) tmp.font = medievalFont;

        // Small colored dot
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

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            float smoothT = t * t * (3f - 2f * t);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, smoothT), rt.anchoredPosition.y);
            cg.alpha = Mathf.Lerp(0f, 1f, smoothT);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(targetX, rt.anchoredPosition.y);
        cg.alpha = 1f;

        yield return new WaitForSeconds(2.5f);

        float fadeDuration = 0.6f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            cg.alpha = 1f - (t * t);
            yield return null;
        }

        Destroy(notifGO);
    }

    // ═══════════════════════════════════
    // SETTINGS ICON
    // ═══════════════════════════════════

    private void BuildSettingsIcon(RectTransform canvasRT)
    {
        GameObject iconBorderGO = CreateUIObject("SettingsIconBorder", canvasRT);
        RectTransform borderRT = iconBorderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(1f, 1f);
        borderRT.anchorMax = new Vector2(1f, 1f);
        borderRT.pivot = new Vector2(1f, 1f);
        borderRT.anchoredPosition = new Vector2(-15f, -15f);
        borderRT.sizeDelta = new Vector2(44f, 44f);

        Image borderImg = iconBorderGO.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        GameObject iconBGGO = CreateUIObject("SettingsIconBG", iconBorderGO.transform);
        RectTransform bgRT = iconBGGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(1.5f, 1.5f);
        bgRT.offsetMax = new Vector2(-1.5f, -1.5f);

        Image bgImg = iconBGGO.AddComponent<Image>();
        bgImg.color = new Color(barBGColor.r, barBGColor.g, barBGColor.b, 0.85f);

        BuildMenuIcon(iconBGGO.transform, goldText);

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
                Debug.LogWarning("[Hotbar] PauseMenuController not found!");
        });
    }

    private void BuildMenuIcon(Transform parent, Color color)
    {
        GameObject iconRoot = CreateUIObject("MenuIcon", parent);
        RectTransform rootRT = iconRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.2f, 0.2f);
        rootRT.anchorMax = new Vector2(0.8f, 0.8f);
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        float[] barYPositions = { 0.78f, 0.45f, 0.12f };
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

    // ═══════════════════════════════════
    // HEALTH BAR
    // ═══════════════════════════════════

    public void SetHealthPercent(float normalized)
    {
        healthPercent = Mathf.Clamp01(normalized);
        UpdateHealthBar();
    }

    private void BuildHealthBar(RectTransform canvasRT)
    {
        healthBarRoot = CreateUIObject("HealthBar", canvasRT);
        RectTransform rootRT = healthBarRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(0f, 96f); // Above stamina bar
        rootRT.sizeDelta = new Vector2(556f, 12f);      // Match hotbar width

        Image borderImg = healthBarRoot.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        GameObject bgGO = CreateUIObject("BG", healthBarRoot.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(1.5f, 1.5f);
        bgRT.offsetMax = new Vector2(-1.5f, -1.5f);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = healthBarBGColor;

        GameObject fillGO = CreateUIObject("Fill", bgGO.transform);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = new Vector2(1f, 1f);
        fillRT.offsetMax = new Vector2(-1f, -1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        healthFillImage = fillGO.AddComponent<Image>();
        healthFillImage.color = healthBarColor;

        GameObject labelGO = CreateUIObject("Label", healthBarRoot.transform);
        labelGO.AddComponent<CanvasRenderer>();
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "HEALTH";
        labelTMP.fontSize = 8;
        labelTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        labelTMP.color = new Color(1f, 1f, 1f, 0.45f);
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.characterSpacing = 8f;
        if (medievalFont != null) labelTMP.font = medievalFont;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthFillImage == null || healthBarRoot == null) return;

        if (fpsController == null)
            fpsController = FindFirstObjectByType<FirstPersonController>();

        if (fpsController != null)
            healthPercent = Mathf.Clamp01(fpsController.HealthPercent);

        RectTransform fillRT = healthFillImage.GetComponent<RectTransform>();
        fillRT.anchorMax = new Vector2(healthPercent, 1f);

        healthFillImage.color = healthPercent < 0.25f
            ? Color.Lerp(healthBarLowColor, healthBarColor, healthPercent / 0.25f)
            : healthBarColor;
    }

    // ═══════════════════════════════════
    // STAMINA BAR
    // ═══════════════════════════════════

    private void BuildStaminaBar(RectTransform canvasRT)
    {
        staminaBarRoot = CreateUIObject("StaminaBar", canvasRT);
        RectTransform rootRT = staminaBarRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(0f, 80f); // Just above the hotbar
        rootRT.sizeDelta = new Vector2(556f, 12f);       // Same width as hotbar

        staminaCanvasGroup = staminaBarRoot.AddComponent<CanvasGroup>();
        staminaCanvasGroup.alpha = 1f;

        Image borderImg = staminaBarRoot.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        GameObject bgGO = CreateUIObject("BG", staminaBarRoot.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(1.5f, 1.5f);
        bgRT.offsetMax = new Vector2(-1.5f, -1.5f);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = staminaBarBGColor;

        GameObject fillGO = CreateUIObject("Fill", bgGO.transform);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = new Vector2(1f, 1f);
        fillRT.offsetMax = new Vector2(-1f, -1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        staminaFillImage = fillGO.AddComponent<Image>();
        staminaFillImage.color = staminaBarColor;

        GameObject labelGO = CreateUIObject("Label", staminaBarRoot.transform);
        labelGO.AddComponent<CanvasRenderer>();
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
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

    private void UpdateStaminaBar()
    {
        if (staminaFillImage == null || staminaCanvasGroup == null) return;

        if (fpsController == null)
        {
            fpsController = FindFirstObjectByType<FirstPersonController>();
            if (fpsController == null) return;
        }

        float pct = fpsController.StaminaPercent;

        RectTransform fillRT = staminaFillImage.GetComponent<RectTransform>();
        fillRT.anchorMax = new Vector2(pct, 1f);

        staminaFillImage.color = pct < 0.25f
            ? Color.Lerp(staminaBarLowColor, staminaBarColor, pct / 0.25f)
            : staminaBarColor;

        staminaCanvasGroup.alpha = 1f;
    }

    // ═══════════════════════════════════
    // UTILITIES
    // ═══════════════════════════════════

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
}
