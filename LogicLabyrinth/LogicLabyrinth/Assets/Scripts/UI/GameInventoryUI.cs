using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine.SceneManagement;

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
    private const int ADR_SLOT_INDEX = 5; // Slot 6
    private const int LAN_SLOT_INDEX = 6; // Slot 7
    private const int SCN_SLOT_INDEX = 7; // Slot 8

    /// <summary>Type of item that can occupy a hotbar slot.</summary>
    public enum ItemType { None, AND, OR, NOT, Key, Candle, Adrenaline, Lantern, Scanner }

    private ItemType[] slotItems = new ItemType[SLOT_COUNT];
    private int selectedSlot = -1; // 0-based index of the currently selected slot, -1 when nothing is selected

    // UI references per slot
    private GameObject[] slotRoots = new GameObject[SLOT_COUNT];
    private Image[] slotBorders = new Image[SLOT_COUNT];
    private Image[] slotBackgrounds = new Image[SLOT_COUNT];
    private Image[] slotIcons = new Image[SLOT_COUNT];
    private TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[SLOT_COUNT];
    private TextMeshProUGUI[] slotCounts = new TextMeshProUGUI[SLOT_COUNT];
    private TextMeshProUGUI[] slotOverlays = new TextMeshProUGUI[SLOT_COUNT];
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
    private Color lanternColor = new Color(1f, 0.78f, 0.20f, 1f);
    private Color scannerColor = new Color(0.38f, 0.86f, 0.92f, 1f);

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
    private string candleOverlayText = "";
    private Color candleOverlayColor = Color.white;
    private string scannerOverlayText = "";
    private Color scannerOverlayColor = Color.white;

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
        CollectibleCandle.UpdateCandleUsage();
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
    /// Keeps existing slot positions when possible, and only fills empty slots for new items.
    /// </summary>
    private void RebuildSlots(int andCount, int orCount, int notCount)
    {
        // Build target counts from current inventory state.
        bool hasKey = TutorialDoor.PlayerHasKey || SuccessDoor.PlayerHasSuccessKey;
        bool hasCandle = (InventoryManager.Instance != null && InventoryManager.Instance.HasCandle);
        bool hasAdrenaline = (AccountManager.Instance != null && AccountManager.Instance.GetAdrenalineCount() > 0);
        bool hasLantern = (AccountManager.Instance != null && AccountManager.Instance.HasStoreItem("Lantern"));
        bool hasScanner = (AccountManager.Instance != null && AccountManager.Instance.GetScannerCount() > 0);
        int targetAnd = Mathf.Max(0, andCount);
        int targetOr = Mathf.Max(0, orCount);
        int targetNot = Mathf.Max(0, notCount);
        int targetGateTotal = targetAnd + targetOr + targetNot;
        int targetKey = GetTargetKeySlotCount(hasKey);
        int targetCandle = hasCandle ? 1 : 0;
        int targetAdrenaline = hasAdrenaline ? 1 : 0;
        int targetLantern = hasLantern ? 1 : 0;
        int targetScanner = hasScanner ? 1 : 0;

        int previousSelectedSlot = selectedSlot;
        ItemType previousSelectedItem = (previousSelectedSlot >= 0 && previousSelectedSlot < SLOT_COUNT)
            ? slotItems[previousSelectedSlot]
            : ItemType.None;

        ItemType[] oldSlots = new ItemType[SLOT_COUNT];
        ItemType[] newSlots = new ItemType[SLOT_COUNT];
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            oldSlots[i] = slotItems[i];
            newSlots[i] = ItemType.None;
        }

        int oldGateTotal = 0;
        int oldAdrenalineTotal = 0;
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (IsGateItem(oldSlots[i])) oldGateTotal++;
            if (oldSlots[i] == ItemType.Adrenaline) oldAdrenalineTotal++;
        }

        // First pass: keep existing items in the same slot when still needed.
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            ItemType item = oldSlots[i];
            bool kept = false;

            switch (item)
            {
                case ItemType.AND:
                    if (targetAnd > 0) { newSlots[i] = ItemType.AND; targetAnd--; kept = true; }
                    break;
                case ItemType.OR:
                    if (targetOr > 0) { newSlots[i] = ItemType.OR; targetOr--; kept = true; }
                    break;
                case ItemType.NOT:
                    if (targetNot > 0) { newSlots[i] = ItemType.NOT; targetNot--; kept = true; }
                    break;
                case ItemType.Key:
                    if (targetKey > 0) { newSlots[i] = ItemType.Key; targetKey--; kept = true; }
                    break;
                case ItemType.Candle:
                    if (targetCandle > 0) { newSlots[i] = ItemType.Candle; targetCandle--; kept = true; }
                    break;
                case ItemType.Adrenaline:
                    if (targetAdrenaline > 0) { newSlots[i] = ItemType.Adrenaline; targetAdrenaline--; kept = true; }
                    break;
                case ItemType.Lantern:
                    if (targetLantern > 0) { newSlots[i] = ItemType.Lantern; targetLantern--; kept = true; }
                    break;
                case ItemType.Scanner:
                    if (targetScanner > 0) { newSlots[i] = ItemType.Scanner; targetScanner--; kept = true; }
                    break;
            }

            if (!kept)
                newSlots[i] = ItemType.None;
        }

        // Second pass: place newly gained items into remaining empty slots.
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (newSlots[i] != ItemType.None) continue;

            if (targetAnd > 0) { newSlots[i] = ItemType.AND; targetAnd--; continue; }
            if (targetOr > 0) { newSlots[i] = ItemType.OR; targetOr--; continue; }
            if (targetNot > 0) { newSlots[i] = ItemType.NOT; targetNot--; continue; }
            if (targetKey > 0) { newSlots[i] = ItemType.Key; targetKey--; continue; }
            if (targetCandle > 0) { newSlots[i] = ItemType.Candle; targetCandle--; continue; }
            if (targetLantern > 0) { newSlots[i] = ItemType.Lantern; targetLantern--; continue; }
            if (targetAdrenaline > 0) { newSlots[i] = ItemType.Adrenaline; targetAdrenaline--; continue; }
            if (targetScanner > 0) { newSlots[i] = ItemType.Scanner; targetScanner--; continue; }
        }

        // Global hotbar behavior: keep items contiguous from left to right.
        // Do not force reserved fixed slots for store items.

        // If gates were dropped, compact remaining gates left into open slots.
        // Non-gate items (key/candle/adrenaline) keep their current positions.
        if (targetGateTotal < oldGateTotal)
            CompactGatesLeft(newSlots);

        // If adrenaline was consumed and removed from a leading slot, compact all items left
        // so slot numbering descends naturally (e.g., slot 2 shifts into slot 1).
        if (targetAdrenaline < oldAdrenalineTotal)
            CompactAllItemsLeft(newSlots);

        CompactAllItemsLeft(newSlots);

        // Copy back to live slots.
        for (int i = 0; i < SLOT_COUNT; i++)
            slotItems[i] = newSlots[i];

        // Keep selection if it still points to a valid item; otherwise clear it.
        if (previousSelectedSlot >= 0 && previousSelectedSlot < SLOT_COUNT && slotItems[previousSelectedSlot] == previousSelectedItem)
        {
            selectedSlot = previousSelectedSlot;
        }
        else
        {
            if (previousSelectedItem == ItemType.Candle)
                CollectibleCandle.Unequip();
            selectedSlot = -1;
        }

        // Global quality-of-life: if items exist but nothing is selected, auto-select first slot.
        if (selectedSlot < 0)
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (slotItems[i] != ItemType.None)
                {
                    selectedSlot = i;
                    break;
                }
            }
        }

        if (hasAdrenaline && targetAdrenaline > 0)
            Debug.LogWarning("[Hotbar] Adrenaline available but hotbar is full, so ADR cannot be shown.");

        if (hasAdrenaline)
        {
            int adrCount = AccountManager.Instance != null ? AccountManager.Instance.GetAdrenalineCount() : 0;
            int usedSlots = 0;
            for (int i = 0; i < SLOT_COUNT; i++)
                if (slotItems[i] != ItemType.None) usedSlots++;
            Debug.Log($"[Hotbar] RebuildSlots -> adrenalineCount={adrCount}, hasAdrenaline={hasAdrenaline}, usedSlots={usedSlots}/{SLOT_COUNT}");
        }

        UpdateSlotVisuals();
    }

    private int GetTargetKeySlotCount(bool hasAnyKeyFlag)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "Chapter3")
        {
            int chapter3Keys = Mathf.Max(0, Chapter3KeyFlowController.CurrentCollectedKeyCount);
            if (chapter3Keys > 0)
                return Mathf.Min(chapter3Keys, SLOT_COUNT);
        }

        return hasAnyKeyFlag ? 1 : 0;
    }

    private static bool IsStoreItem(ItemType item)
    {
        return item == ItemType.Adrenaline || item == ItemType.Lantern || item == ItemType.Scanner;
    }

    private void EnforceStoreReservedSlots(ItemType[] slots, bool hasAdrenaline, bool hasLantern, bool hasScanner)
    {
        if (slots == null || slots.Length != SLOT_COUNT)
            return;

        // Remove store items from non-reserved slots so each store item lives in one stable slot.
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!IsStoreItem(slots[i]))
                continue;

            if ((slots[i] == ItemType.Adrenaline && i != ADR_SLOT_INDEX)
                || (slots[i] == ItemType.Lantern && i != LAN_SLOT_INDEX)
                || (slots[i] == ItemType.Scanner && i != SCN_SLOT_INDEX))
            {
                slots[i] = ItemType.None;
            }
        }

        PlaceReservedItem(slots, ADR_SLOT_INDEX, ItemType.Adrenaline, hasAdrenaline, hasAdrenaline, hasLantern, hasScanner);
        PlaceReservedItem(slots, LAN_SLOT_INDEX, ItemType.Lantern, hasLantern, hasAdrenaline, hasLantern, hasScanner);
        PlaceReservedItem(slots, SCN_SLOT_INDEX, ItemType.Scanner, hasScanner, hasAdrenaline, hasLantern, hasScanner);
    }

    private void PlaceReservedItem(ItemType[] slots, int reservedIndex, ItemType reservedType, bool shouldExist,
        bool hasAdrenaline, bool hasLantern, bool hasScanner)
    {
        if (!shouldExist)
            return;

        if (slots[reservedIndex] == reservedType)
            return;

        ItemType displaced = slots[reservedIndex];
        slots[reservedIndex] = reservedType;

        if (displaced == ItemType.None || IsStoreItem(displaced))
            return;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (i == ADR_SLOT_INDEX && hasAdrenaline) continue;
            if (i == LAN_SLOT_INDEX && hasLantern) continue;
            if (i == SCN_SLOT_INDEX && hasScanner) continue;
            if (slots[i] != ItemType.None) continue;

            slots[i] = displaced;
            return;
        }
    }

    private static bool IsGateItem(ItemType item)
    {
        return item == ItemType.AND || item == ItemType.OR || item == ItemType.NOT;
    }

    private void CompactGatesLeft(ItemType[] slots)
    {
        ItemType[] gates = new ItemType[SLOT_COUNT];
        int gateCount = 0;

        // Pull out all gates, keep their left-to-right order.
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!IsGateItem(slots[i])) continue;
            gates[gateCount++] = slots[i];
            slots[i] = ItemType.None;
        }

        // Reinsert gates into earliest empty slots, skipping occupied non-gate slots.
        int gateIndex = 0;
        for (int i = 0; i < SLOT_COUNT && gateIndex < gateCount; i++)
        {
            if (slots[i] != ItemType.None) continue;
            slots[i] = gates[gateIndex++];
        }
    }

    private void CompactAllItemsLeft(ItemType[] slots)
    {
        ItemType[] packed = new ItemType[SLOT_COUNT];
        int write = 0;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (slots[i] == ItemType.None) continue;
            packed[write++] = slots[i];
        }

        for (int i = 0; i < SLOT_COUNT; i++)
            slots[i] = packed[i];
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
            case "LANTERN":   return ItemType.Lantern;
            case "SCANNER":   return ItemType.Scanner;
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

        GameObject countGO = CreateUIObject("Count", bgGO.transform);
        countGO.AddComponent<CanvasRenderer>();
        RectTransform countRT = countGO.GetComponent<RectTransform>();
        countRT.anchorMin = new Vector2(0.48f, 0.56f);
        countRT.anchorMax = new Vector2(0.95f, 0.95f);
        countRT.offsetMin = Vector2.zero;
        countRT.offsetMax = Vector2.zero;

        TextMeshProUGUI countTMP = countGO.AddComponent<TextMeshProUGUI>();
        countTMP.text = "";
        countTMP.fontSize = 12;
        countTMP.fontStyle = FontStyles.Bold;
        countTMP.alignment = TextAlignmentOptions.TopRight;
        countTMP.color = creamText;
        countTMP.raycastTarget = false;
        if (medievalFont != null) countTMP.font = medievalFont;
        slotCounts[index] = countTMP;

        GameObject overlayGO = CreateUIObject("Overlay", bgGO.transform);
        overlayGO.AddComponent<CanvasRenderer>();
        RectTransform overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0f, 0.18f);
        overlayRT.anchorMax = new Vector2(1f, 0.88f);
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        TextMeshProUGUI overlayTMP = overlayGO.AddComponent<TextMeshProUGUI>();
        overlayTMP.text = "";
        overlayTMP.fontSize = 16;
        overlayTMP.fontStyle = FontStyles.Bold;
        overlayTMP.alignment = TextAlignmentOptions.Center;
        overlayTMP.color = scannerColor;
        overlayTMP.raycastTarget = false;
        if (medievalFont != null) overlayTMP.font = medievalFont;
        slotOverlays[index] = overlayTMP;

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
        frameBG.raycastTarget = false;

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
        qSlotBorderImage.raycastTarget = false;

        // ── Inner background ──
        GameObject bgGO = CreateUIObject("BG", borderGO.transform);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(2f, 2f);
        bgRT.offsetMax = new Vector2(-2f, -2f);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = slotBGColor;
        bgImg.raycastTarget = false;

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

        // Q slot is only a static key hint for discard. It should not mirror selected gates.
        qSlotIconImage.color = Color.clear;
        qSlotLabelTMP.text = "";
        if (qSlotBorderImage != null)
            qSlotBorderImage.color = new Color(0.55f, 0.18f, 0.12f, 0.85f);
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

            int stackCount = GetItemStackCount(item);
            if (slotCounts[i] != null)
            {
                slotCounts[i].text = stackCount > 0 ? $"x{stackCount}" : "";
                slotCounts[i].color = isSelected ? goldText : creamText;
            }

            if (slotOverlays[i] != null)
            {
                string overlayText = GetItemOverlayText(item);
                slotOverlays[i].text = overlayText;
                slotOverlays[i].color = GetItemOverlayColor(item, isSelected);
            }

            // Icon + Label
            if (item == ItemType.None)
            {
                // Empty slot
                if (slotIcons[i] != null) slotIcons[i].color = Color.clear;
                if (slotLabels[i] != null) slotLabels[i].text = "";
                if (slotCounts[i] != null) slotCounts[i].text = "";
                if (slotOverlays[i] != null) slotOverlays[i].text = "";
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
            case ItemType.Lantern:    return lanternColor;
            case ItemType.Scanner:    return scannerColor;
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
            case ItemType.Lantern:    return "LAN";
            case ItemType.Scanner:    return "SCN";
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
            case ItemType.Scanner: return "?";
            default:              return "";
        }
    }

    private int GetItemStackCount(ItemType type)
    {
        if (AccountManager.Instance == null)
            return 0;

        switch (type)
        {
            case ItemType.Adrenaline:
                return AccountManager.Instance.GetAdrenalineCount();
            case ItemType.Scanner:
                return AccountManager.Instance.GetScannerCount();
            default:
                return 0;
        }
    }

    private string GetItemOverlayText(ItemType type)
    {
        switch (type)
        {
            case ItemType.Candle:
                return candleOverlayText;
            case ItemType.Scanner:
                return scannerOverlayText;
            default:
                return "";
        }
    }

    private Color GetItemOverlayColor(ItemType type, bool isSelected)
    {
        switch (type)
        {
            case ItemType.Candle:
                return isSelected ? goldText : candleOverlayColor;
            case ItemType.Scanner:
                return isSelected ? goldText : scannerOverlayColor;
            default:
                return creamText;
        }
    }

    public void SetCandleOverlayText(string text, Color color)
    {
        candleOverlayText = text ?? "";
        candleOverlayColor = color;
        UpdateSlotVisuals();
    }

    public void ClearCandleOverlayText()
    {
        if (string.IsNullOrEmpty(candleOverlayText))
            return;

        candleOverlayText = "";
        UpdateSlotVisuals();
    }

    public void SetScannerOverlayText(string text, Color color)
    {
        scannerOverlayText = text ?? "";
        scannerOverlayColor = color;
        UpdateSlotVisuals();
    }

    public void ClearScannerOverlayText()
    {
        if (string.IsNullOrEmpty(scannerOverlayText))
            return;

        scannerOverlayText = "";
        UpdateSlotVisuals();
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
