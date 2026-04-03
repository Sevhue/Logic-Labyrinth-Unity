using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using StarterAssets;

/// <summary>
/// Medieval-themed popup UI for swapping or discarding gates.
/// Swap mode: inventory full, player picks which gate to drop to make room for a new one.
/// Discard mode: player manually drops a gate (Q key).
/// Creates its own Canvas and UI entirely in code.
/// </summary>
public class SwapGateUI : MonoBehaviour
{
    public static SwapGateUI Instance { get; private set; }

    // ── State ──
    private Interactable pendingGate;       // The gate to collect AFTER dropping (null = discard only)
    private GameObject andPrefab, orPrefab, notPrefab;
    private Transform playerTransform;

    // ── Player control refs ──
    private FirstPersonController fpc;
    private StarterAssetsInputs inputs;
    private CharacterController charCtrl;

    // ── UI refs ──
    private Canvas canvas;
    private TMP_FontAsset medievalFont;

    // ── Medieval colors (match GameInventoryUI) ──
    private static readonly Color darkBG      = new Color(0.10f, 0.07f, 0.04f, 0.96f);
    private static readonly Color panelBG     = new Color(0.14f, 0.11f, 0.06f, 0.98f);
    private static readonly Color goldBorder  = new Color(0.72f, 0.58f, 0.30f, 1f);
    private static readonly Color goldText    = new Color(0.84f, 0.75f, 0.50f, 1f);
    private static readonly Color creamText   = new Color(0.95f, 0.90f, 0.75f, 1f);
    private static readonly Color dimGold     = new Color(0.50f, 0.40f, 0.22f, 0.6f);
    private static readonly Color btnNormal   = new Color(0.18f, 0.14f, 0.08f, 1f);
    private static readonly Color btnHover    = new Color(0.25f, 0.20f, 0.12f, 1f);
    private static readonly Color btnDisabled = new Color(0.12f, 0.10f, 0.06f, 0.5f);
    private static readonly Color andColor    = new Color(0.35f, 0.55f, 0.80f, 1f);
    private static readonly Color orColor     = new Color(0.85f, 0.65f, 0.25f, 1f);
    private static readonly Color notColor    = new Color(0.78f, 0.22f, 0.22f, 1f);
    private static readonly Color cancelRed   = new Color(0.6f, 0.2f, 0.15f, 1f);

    // ════════════════════════════════════════
    // STATIC API — call these from SimpleGateCollector
    // ════════════════════════════════════════

    /// <summary>
    /// Show the swap popup. Player must choose a gate to drop, then the pendingGate is collected.
    /// </summary>
    public static void ShowSwap(Interactable newGate, GameObject andPfb, GameObject orPfb, GameObject notPfb, Transform player)
    {
        if (Instance != null) return; // Already showing

        GameObject go = new GameObject("SwapGateUI");
        SwapGateUI ui = go.AddComponent<SwapGateUI>();
        ui.pendingGate = newGate;
        ui.andPrefab = andPfb;
        ui.orPrefab = orPfb;
        ui.notPrefab = notPfb;
        ui.playerTransform = player;
    }

    /// <summary>
    /// Show the discard popup. Player picks a gate to drop at their feet.
    /// </summary>
    public static void ShowDiscard(GameObject andPfb, GameObject orPfb, GameObject notPfb, Transform player)
    {
        if (Instance != null) return;

        GameObject go = new GameObject("SwapGateUI");
        SwapGateUI ui = go.AddComponent<SwapGateUI>();
        ui.pendingGate = null; // discard mode
        ui.andPrefab = andPfb;
        ui.orPrefab = orPfb;
        ui.notPrefab = notPfb;
        ui.playerTransform = player;
    }

    public static bool DiscardGateImmediately(string gateType, GameObject andPfb, GameObject orPfb, GameObject notPfb, Transform player)
    {
        if (InventoryManager.Instance == null || player == null)
            return false;

        string normalizedGateType = gateType.ToUpper();
        if (!InventoryManager.Instance.RemoveGate(normalizedGateType))
            return false;

        SpawnDroppedGateStatic(normalizedGateType, andPfb, orPfb, notPfb, player);

        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null)
            levelUI.ShowCollectionMessage($"Dropped {normalizedGateType} Gate", new Color(0.9f, 0.7f, 0.2f));

        Debug.Log($"[SwapGateUI] Direct-discarded {normalizedGateType} gate.");
        return true;
    }

    public static bool IsOpen => Instance != null;

    // ════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        LoadFont();
        EnsureEventSystem();
        SetPlayerControls(false);
        BuildUI();
    }

    void Update()
    {
        // Q or Escape to cancel
        bool cancelPressed = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape);
        if (!cancelPressed && UnityEngine.InputSystem.Keyboard.current != null)
            cancelPressed = UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame
                         || UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;

        if (cancelPressed)
            Close();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SetPlayerControls(true);
    }

    // ════════════════════════════════════════
    // BUILD UI
    // ════════════════════════════════════════

    private void BuildUI()
    {
        // Canvas
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();

        // ── Dark semi-transparent overlay ──
        GameObject overlay = MakeUI("Overlay", canvasRT);
        Stretch(overlay);
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);

        // ── Central panel with gold border ──
        GameObject borderPanel = MakeUI("PanelBorder", canvasRT);
        RectTransform borderRT = borderPanel.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0.5f, 0.5f);
        borderRT.anchorMax = new Vector2(0.5f, 0.5f);
        borderRT.pivot = new Vector2(0.5f, 0.5f);
        borderRT.sizeDelta = new Vector2(520, 320);
        borderPanel.AddComponent<Image>().color = goldBorder;

        // Inner panel
        GameObject panel = MakeUI("Panel", borderPanel.transform);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = new Vector2(3, 3);
        panelRT.offsetMax = new Vector2(-3, -3);
        panel.AddComponent<Image>().color = panelBG;

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 15f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // ── Title ──
        bool isSwap = pendingGate != null;
        string titleStr = isSwap
            ? "INVENTORY FULL"
            : "DISCARD A GATE";

        GameObject titleGO = MakeUI("Title", panel.transform);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = titleStr;
        titleTMP.fontSize = 28;
        titleTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        titleTMP.color = goldText;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.characterSpacing = 6;
        if (medievalFont != null) titleTMP.font = medievalFont;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 40;

        // ── Subtitle ──
        string subStr = isSwap
            ? $"Choose a gate to drop and collect the <color=#FFD700>{pendingGate.gateType}</color> Gate:"
            : "Choose a gate to drop at your feet:";

        GameObject subGO = MakeUI("Subtitle", panel.transform);
        TextMeshProUGUI subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text = subStr;
        subTMP.fontSize = 18;
        subTMP.color = creamText;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.richText = true;
        if (medievalFont != null) subTMP.font = medievalFont;
        LayoutElement subLE = subGO.AddComponent<LayoutElement>();
        subLE.preferredHeight = 30;

        // ── Gate buttons row ──
        GameObject row = MakeUI("ButtonRow", panel.transform);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 100;

        // Create a button for each gate type
        CreateGateButton(row.transform, "AND", andColor, InventoryManager.Instance.GetGateCount("AND"));
        CreateGateButton(row.transform, "OR", orColor, InventoryManager.Instance.GetGateCount("OR"));
        CreateGateButton(row.transform, "NOT", notColor, InventoryManager.Instance.GetGateCount("NOT"));

        // ── Cancel button ──
        GameObject cancelRow = MakeUI("CancelRow", panel.transform);
        LayoutElement cancelRowLE = cancelRow.AddComponent<LayoutElement>();
        cancelRowLE.preferredHeight = 45;

        HorizontalLayoutGroup cancelHLG = cancelRow.AddComponent<HorizontalLayoutGroup>();
        cancelHLG.childAlignment = TextAnchor.MiddleCenter;
        cancelHLG.childForceExpandWidth = false;
        cancelHLG.childForceExpandHeight = false;
        cancelHLG.childControlWidth = false;
        cancelHLG.childControlHeight = false;

        CreateCancelButton(cancelRow.transform);

        Debug.Log($"[SwapGateUI] Built UI. Mode: {(isSwap ? "SWAP" : "DISCARD")}");
    }

    private void CreateGateButton(Transform parent, string gateType, Color accent, int count)
    {
        // Outer border
        GameObject borderGO = MakeUI($"Btn_{gateType}_Border", parent);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(130, 100);
        borderGO.AddComponent<Image>().color = count > 0 ? dimGold : new Color(0.2f, 0.2f, 0.2f, 0.3f);
        LayoutElement le = borderGO.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.preferredHeight = 100;

        // Inner button
        GameObject btnGO = MakeUI($"Btn_{gateType}", borderGO.transform);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero;
        btnRT.anchorMax = Vector2.one;
        btnRT.offsetMin = new Vector2(2, 2);
        btnRT.offsetMax = new Vector2(-2, -2);

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = count > 0 ? btnNormal : btnDisabled;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = count > 0 ? btnNormal : btnDisabled;
        cb.highlightedColor = count > 0 ? btnHover : btnDisabled;
        cb.pressedColor = count > 0 ? new Color(0.3f, 0.25f, 0.15f, 1f) : btnDisabled;
        cb.disabledColor = btnDisabled;
        btn.colors = cb;
        btn.interactable = count > 0;

        // Colored accent stripe at top
        GameObject stripe = MakeUI("Stripe", btnGO.transform);
        RectTransform stripeRT = stripe.GetComponent<RectTransform>();
        stripeRT.anchorMin = new Vector2(0, 1);
        stripeRT.anchorMax = new Vector2(1, 1);
        stripeRT.pivot = new Vector2(0.5f, 1);
        stripeRT.anchoredPosition = Vector2.zero;
        stripeRT.sizeDelta = new Vector2(0, 4);
        Image stripeImg = stripe.AddComponent<Image>();
        stripeImg.color = count > 0 ? accent : new Color(accent.r, accent.g, accent.b, 0.3f);

        // Vertical layout for label + count
        VerticalLayoutGroup vlg = btnGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(5, 5, 10, 5);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Gate name
        GameObject nameGO = MakeUI("Name", btnGO.transform);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = gateType;
        nameTMP.fontSize = 20;
        nameTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        nameTMP.color = count > 0 ? goldText : new Color(goldText.r, goldText.g, goldText.b, 0.3f);
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.characterSpacing = 4;
        if (medievalFont != null) nameTMP.font = medievalFont;
        nameTMP.raycastTarget = false;

        // Count
        GameObject countGO = MakeUI("Count", btnGO.transform);
        TextMeshProUGUI countTMP = countGO.AddComponent<TextMeshProUGUI>();
        countTMP.text = $"x{count}";
        countTMP.fontSize = 26;
        countTMP.fontStyle = FontStyles.Bold;
        countTMP.color = count > 0 ? creamText : new Color(creamText.r, creamText.g, creamText.b, 0.3f);
        countTMP.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) countTMP.font = medievalFont;
        countTMP.raycastTarget = false;

        // Click handler
        if (count > 0)
        {
            string type = gateType; // Capture for lambda
            btn.onClick.AddListener(() => OnGateSelected(type));
        }
    }

    private void CreateCancelButton(Transform parent)
    {
        GameObject borderGO = MakeUI("CancelBorder", parent);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(160, 40);
        borderGO.AddComponent<Image>().color = dimGold;

        GameObject btnGO = MakeUI("CancelBtn", borderGO.transform);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero;
        btnRT.anchorMax = Vector2.one;
        btnRT.offsetMin = new Vector2(2, 2);
        btnRT.offsetMax = new Vector2(-2, -2);

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.10f, 0.06f, 1f);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.15f, 0.10f, 0.06f, 1f);
        cb.highlightedColor = cancelRed;
        cb.pressedColor = new Color(0.5f, 0.15f, 0.1f, 1f);
        btn.colors = cb;

        GameObject textGO = MakeUI("Text", btnGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "CANCEL (Q)";
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        tmp.color = creamText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 3;
        if (medievalFont != null) tmp.font = medievalFont;
        tmp.raycastTarget = false;

        btn.onClick.AddListener(Close);
    }

    // ════════════════════════════════════════
    // ACTIONS
    // ════════════════════════════════════════

    private void OnGateSelected(string gateType)
    {
        Debug.Log($"[SwapGateUI] Player chose to drop: {gateType}");

        // 1) Remove the selected gate from inventory
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RemoveGate(gateType);

        // 2) Spawn the dropped gate at the player's feet
        SpawnDroppedGate(gateType);

        // 3) If swap mode, collect the pending gate
        if (pendingGate != null)
        {
            if (FirstPersonArmAnimator.Instance != null)
                FirstPersonArmAnimator.Instance.PlayCollectAnimation();

            pendingGate.Interact();
            Debug.Log($"[SwapGateUI] Swapped {gateType} for {pendingGate.gateType}!");
        }
        else
        {
            // Discard mode — notify UI
            LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
            if (levelUI != null)
                levelUI.ShowCollectionMessage($"Dropped {gateType} Gate", new Color(0.9f, 0.7f, 0.2f));

            Debug.Log($"[SwapGateUI] Discarded {gateType} gate.");
        }

        // 4) Close
        Close();
    }

    private void SpawnDroppedGate(string gateType)
    {
        SpawnDroppedGateStatic(gateType, andPrefab, orPrefab, notPrefab, playerTransform);
    }

    private static void SpawnDroppedGateStatic(string gateType, GameObject andPfb, GameObject orPfb, GameObject notPfb, Transform player)
    {
        GameObject prefab = ResolveGatePrefab(gateType, andPfb, orPfb, notPfb);
        if (prefab == null || player == null)
        {
            Debug.LogWarning($"[SwapGateUI] Cannot spawn dropped gate — prefab or player missing.");
            return;
        }

        Vector3 dropPos = FindSafeDropPositionStatic(player);

        GameObject dropped = Instantiate(prefab, dropPos, Quaternion.identity);
        dropped.name = $"Dropped_{gateType.ToUpper()}_Gate";

        IgnoreDroppedGateWithPlayerStatic(dropped, player);

        GateSpawnDelay spawnDelay = dropped.AddComponent<GateSpawnDelay>();
        spawnDelay.delay = 1.2f;

        Debug.Log($"[SwapGateUI] Spawned dropped {gateType.ToUpper()} gate at {dropPos}");
    }

    private static GameObject ResolveGatePrefab(string gateType, GameObject andPfb, GameObject orPfb, GameObject notPfb)
    {
        switch (gateType.ToUpper())
        {
            case "AND": return andPfb;
            case "OR":  return orPfb;
            case "NOT": return notPfb;
            default:     return null;
        }
    }

    private static void IgnoreDroppedGateWithPlayerStatic(GameObject dropped, Transform player)
    {
        if (dropped == null)
            return;

        Collider[] gateCols = dropped.GetComponentsInChildren<Collider>(true);
        Collider[] playerCols = null;

        if (player != null)
            playerCols = player.GetComponentsInChildren<Collider>(true);

        // Fallback: resolve the actual moving player capsule if current transform
        // is just a child camera/holder and has no colliders under it.
        if (playerCols == null || playerCols.Length == 0)
        {
            GameObject playerCC = PauseMenuController.FindPlayerWithCharacterController();
            if (playerCC != null)
                playerCols = playerCC.GetComponentsInChildren<Collider>(true);
        }

        if (gateCols == null || playerCols == null || gateCols.Length == 0 || playerCols.Length == 0)
            return;

        for (int i = 0; i < gateCols.Length; i++)
        {
            Collider gateCol = gateCols[i];
            if (gateCol == null) continue;

            for (int j = 0; j < playerCols.Length; j++)
            {
                Collider playerCol = playerCols[j];
                if (playerCol == null) continue;
                Physics.IgnoreCollision(gateCol, playerCol, true);
            }
        }
    }

    /// <summary>
    /// Finds a safe position to drop a gate — not inside walls or floors.
    /// Tries: forward, backward, left, right, then directly at player feet.
    /// </summary>
    private static Vector3 FindSafeDropPositionStatic(Transform playerTransform)
    {
        Vector3 playerPos = playerTransform.position;
        Vector3 eyePos = playerPos + Vector3.up * 1f;
        CharacterController playerCC = playerTransform.GetComponent<CharacterController>();

        // Directions to try: forward, back, left, right
        Vector3[] directions = new Vector3[]
        {
            playerTransform.forward,
            -playerTransform.forward,
            -playerTransform.right,
            playerTransform.right
        };

        float dropDistance = 1.5f;

        foreach (Vector3 dir in directions)
        {
            // Check if there's a wall in this direction
            RaycastHit wallHit;
            bool wallBlocked = Physics.Raycast(eyePos, dir, out wallHit, dropDistance);

            Vector3 candidatePos;
            if (wallBlocked)
            {
                // Wall is closer than drop distance — place just before the wall
                // Only use this direction if there's enough room (at least 0.5m)
                if (wallHit.distance < 0.5f)
                    continue; // Too close to wall, try another direction

                candidatePos = eyePos + dir * (wallHit.distance - 0.3f);
            }
            else
            {
                candidatePos = eyePos + dir * dropDistance;
            }

            // Now raycast down from that position to find the floor
            RaycastHit floorHit;
            if (Physics.Raycast(candidatePos, Vector3.down, out floorHit, 10f))
            {
                Vector3 finalPos = floorHit.point + Vector3.up * 0.3f;

                // Keep a healthy distance from the player capsule to avoid delayed ejection
                // when dropped gate colliders get re-enabled.
                if (playerCC != null)
                {
                    Vector3 playerFeet = playerPos + Vector3.up * playerCC.skinWidth;
                    float minDropDistance = Mathf.Max(0.9f, playerCC.radius + 0.35f);
                    if (Vector3.Distance(finalPos, playerFeet) < minDropDistance)
                        continue;
                }

                if (IsDropPositionClearStatic(finalPos, playerTransform))
                    return finalPos;
            }
        }

        // Last resort: place the gate behind the player at a safer offset instead of feet.
        Vector3 fallbackProbe = eyePos - playerTransform.forward * 1.8f;
        if (Physics.Raycast(fallbackProbe, Vector3.down, out RaycastHit fallbackFloor, 12f))
        {
            Vector3 fallback = fallbackFloor.point + Vector3.up * 0.35f;
            if (IsDropPositionClearStatic(fallback, playerTransform))
                return fallback;
        }

        Debug.LogWarning("[SwapGateUI] Could not find ideal drop position — using conservative back-offset fallback.");
        return playerPos - playerTransform.forward * 1.4f + Vector3.up * 0.4f;
    }

    private static bool IsDropPositionClearStatic(Vector3 position, Transform playerTransform)
    {
        int overlapMask = ~0;
        if (playerTransform != null)
        {
            int playerLayer = playerTransform.gameObject.layer;
            if (playerLayer >= 0 && playerLayer <= 31)
                overlapMask &= ~(1 << playerLayer);
        }

        return !Physics.CheckSphere(position, 0.22f, overlapMask, QueryTriggerInteraction.Ignore);
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    // ════════════════════════════════════════
    // PLAYER CONTROLS
    // ════════════════════════════════════════

    private void SetPlayerControls(bool enable)
    {
        if (fpc == null) fpc = FindAnyObjectByType<FirstPersonController>();
        if (inputs == null) inputs = FindAnyObjectByType<StarterAssetsInputs>();
        if (charCtrl == null) charCtrl = FindAnyObjectByType<CharacterController>();

        if (fpc != null) fpc.enabled = enable;
        if (inputs != null) inputs.enabled = enable;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    // ════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════

    private void LoadFont()
    {
        medievalFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (medievalFont == null)
            medievalFont = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (medievalFont == null)
            medievalFont = TMP_Settings.defaultFontAsset;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    private GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}