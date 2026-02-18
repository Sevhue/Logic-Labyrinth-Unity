using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;

/// <summary>
/// Main controller for the logic gate puzzle table.
/// Manages the puzzle UI, gate palette, drop slots, submission, and attempts.
/// Attach this to the UITable prefab root (Canvas).
/// </summary>
public class PuzzleTableController : MonoBehaviour
{
    public static PuzzleTableController Instance { get; private set; }

    /// <summary>True while the puzzle UI is visible and active.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Puzzle Answer Key")]
    [Tooltip("The correct gate for each box slot, in order (Box1, Box2, Box3, ...)")]
    public GateType[] answerKey = new GateType[] {
        GateType.OR,   // Box1
        GateType.OR,   // Box2
        GateType.OR,   // Box3
        GateType.AND,  // Box4
        GateType.AND   // Box5
    };

    [Header("Attempt Settings")]
    public int maxAttempts = 3;

    [Header("References (auto-found if empty)")]
    public Transform puzzleContent;   // The Q1 parent that holds the boxes

    // Runtime state
    private List<GateDropSlot> dropSlots = new List<GateDropSlot>();
    private int attemptsUsed = 0;
    private bool puzzleSolved = false;
    private bool isGameOver = false;

    // Temporary inventory for this puzzle session (copied from player inventory)
    private Dictionary<GateType, int> sessionInventory = new Dictionary<GateType, int>();

    // UI elements built at runtime
    private GameObject palettePanel;
    private GameObject submitButton;
    private GameObject attemptsLabel;
    private GameObject feedbackPanel;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI attemptsText;
    private Dictionary<GateType, TextMeshProUGUI> paletteCountLabels = new Dictionary<GateType, TextMeshProUGUI>();
    private Dictionary<GateType, CanvasGroup> paletteItemGroups = new Dictionary<GateType, CanvasGroup>();

    // Player state — cached for enable/disable
    private GameObject player;
    private FirstPersonController cachedFPC;
    private StarterAssetsInputs cachedInputs;
    private CharacterController cachedCC;
    private SimpleGateCollector cachedCollector;
    private MonoBehaviour cachedCinemachineBrain; // Cinemachine brain if present
    private List<MonoBehaviour> disabledScripts = new List<MonoBehaviour>();

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        // Don't auto-open here: InteractiveTable sets answerKey/maxAttempts AFTER
        // Instantiate(), which triggers OnEnable before values are assigned.
        // InteractiveTable will call OpenPuzzle() explicitly.
    }

    void OnDisable()
    {
        IsOpen = false;
        Instance = null;
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // ESC to close puzzle (blocked while tutorial dialogue is playing)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!CutsceneController.IsTableDialogueActive)
                ClosePuzzle();
        }
    }

    // ===================== OPEN / CLOSE =====================

    public void OpenPuzzle()
    {
        puzzleSolved = false;
        isGameOver = false;
        attemptsUsed = 0;
        IsOpen = true;

        // Copy player inventory into session
        LoadSessionInventory();

        // Find the Q1 content area with the boxes
        FindPuzzleContent();

        // Set up drop slots on all Box children
        SetupDropSlots();

        // Build the gate palette UI (BOTTOM — horizontal)
        BuildPaletteUI();

        // Build submit button (TOP-RIGHT) + attempts display + close button
        BuildControlsUI();

        // Enable UI mode (cursor, disable player controls)
        SetUIMode(true);

        Debug.Log("[PuzzleTable] Puzzle opened");
    }

    public void ClosePuzzle()
    {
        // Block closing while first-time tutorial dialogue is playing
        if (CutsceneController.IsTableDialogueActive)
        {
            Debug.Log("[PuzzleTable] Cannot close — tutorial dialogue still playing.");
            return;
        }

        // Return any placed gates to inventory (don't actually consume them)
        foreach (var slot in dropSlots)
        {
            slot.ClearSlot();
        }

        IsOpen = false;

        // Re-enable player controls
        SetUIMode(false);

        // Hide the puzzle UI
        gameObject.SetActive(false);

        Debug.Log("[PuzzleTable] Puzzle closed");
    }

    // ===================== INVENTORY MANAGEMENT =====================

    private void LoadSessionInventory()
    {
        sessionInventory.Clear();

        if (InventoryManager.Instance != null)
        {
            sessionInventory[GateType.AND] = InventoryManager.Instance.GetGateCount("AND");
            sessionInventory[GateType.OR] = InventoryManager.Instance.GetGateCount("OR");
            sessionInventory[GateType.NOT] = InventoryManager.Instance.GetGateCount("NOT");
        }
        else
        {
            // Fallback for testing
            sessionInventory[GateType.AND] = 2;
            sessionInventory[GateType.OR] = 3;
            sessionInventory[GateType.NOT] = 1;
            Debug.LogWarning("[PuzzleTable] InventoryManager not found, using test values");
        }

        Debug.Log($"[PuzzleTable] Session inventory: AND={sessionInventory[GateType.AND]}, OR={sessionInventory[GateType.OR]}, NOT={sessionInventory[GateType.NOT]}");
    }

    public bool CanUseGate(GateType type)
    {
        return sessionInventory.ContainsKey(type) && sessionInventory[type] > 0;
    }

    public void UseGateFromInventory(GateType type)
    {
        if (sessionInventory.ContainsKey(type) && sessionInventory[type] > 0)
        {
            sessionInventory[type]--;
            RefreshPalette();
        }
    }

    public void ReturnGateToInventory(GateType type)
    {
        if (sessionInventory.ContainsKey(type))
        {
            sessionInventory[type]++;
            RefreshPalette();
        }
    }

    // ===================== SETUP =====================

    private void FindPuzzleContent()
    {
        if (puzzleContent != null) return;

        // Auto-find Q1 (or any child named Q1, Q2, etc.)
        puzzleContent = FindDeepChild(transform, "Q1");
        if (puzzleContent == null)
        {
            puzzleContent = FindDeepChild(transform, "Background");
        }
        if (puzzleContent == null)
        {
            puzzleContent = transform;
        }
    }

    private void SetupDropSlots()
    {
        dropSlots.Clear();

        // Find all children named "Box1", "Box2", etc. (with or without leading space)
        List<Transform> boxTransforms = new List<Transform>();
        FindAllBoxChildren(puzzleContent, boxTransforms);

        // Sort by trimmed name to ensure correct order (Box1, Box2, ...)
        boxTransforms.Sort((a, b) => a.name.Trim().CompareTo(b.name.Trim()));

        for (int i = 0; i < boxTransforms.Count; i++)
        {
            Transform boxTrans = boxTransforms[i];

            // Add or get GateDropSlot component
            GateDropSlot slot = boxTrans.GetComponent<GateDropSlot>();
            if (slot == null)
                slot = boxTrans.gameObject.AddComponent<GateDropSlot>();

            // Determine correct answer for this slot
            GateType answer = (i < answerKey.Length) ? answerKey[i] : GateType.AND;
            slot.Initialize(i, answer, this);

            dropSlots.Add(slot);
            Debug.Log($"[PuzzleTable] Slot {i}: \"{boxTrans.name}\" (trimmed=\"{boxTrans.name.Trim()}\") → Expected answer: {answer} (int={(int)answer})");
        }

        Debug.Log($"[PuzzleTable] Total drop slots found: {dropSlots.Count}, Answer key length: {answerKey.Length}");
    }

    private void FindAllBoxChildren(Transform parent, List<Transform> results)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Trim().StartsWith("Box"))
            {
                results.Add(child);
            }
            FindAllBoxChildren(child, results);
        }
    }

    // ===================== BUILD UI =====================

    /// <summary>
    /// Build the gate palette at the BOTTOM of the screen (horizontal).
    /// Player drags from bottom up to the boxes.
    /// </summary>
    private void BuildPaletteUI()
    {
        if (palettePanel != null)
            Destroy(palettePanel);

        paletteCountLabels.Clear();
        paletteItemGroups.Clear();

        // ── Bottom bar palette ──
        palettePanel = new GameObject("GatePalette");
        palettePanel.transform.SetParent(transform, false);

        RectTransform palRect = palettePanel.AddComponent<RectTransform>();
        palRect.anchorMin = new Vector2(0.15f, 0.02f);
        palRect.anchorMax = new Vector2(0.85f, 0.12f);
        palRect.offsetMin = Vector2.zero;
        palRect.offsetMax = Vector2.zero;

        Image palBg = palettePanel.AddComponent<Image>();
        palBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        Outline palOutline = palettePanel.AddComponent<Outline>();
        palOutline.effectColor = new Color(0.6f, 0.5f, 0.2f, 0.8f);
        palOutline.effectDistance = new Vector2(2, -2);

        // Horizontal layout for the bottom palette
        HorizontalLayoutGroup hlg = palettePanel.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Title on the left
        CreatePaletteTitle(palettePanel.transform);

        // Gate items side by side
        CreatePaletteItem(palettePanel.transform, GateType.AND);
        CreatePaletteItem(palettePanel.transform, GateType.OR);
        CreatePaletteItem(palettePanel.transform, GateType.NOT);
    }

    private void CreatePaletteTitle(Transform parent)
    {
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(parent, false);

        titleGO.AddComponent<RectTransform>();

        LayoutElement le = titleGO.AddComponent<LayoutElement>();
        le.preferredWidth = 120;
        le.flexibleWidth = 0;

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "YOUR\nGATES";
        titleText.fontSize = 14;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.84f, 0.75f, 0.5f, 1f); // Gold
    }

    private void CreatePaletteItem(Transform parent, GateType type)
    {
        GameObject itemGO = new GameObject($"Gate_{type}");
        itemGO.transform.SetParent(parent, false);

        itemGO.AddComponent<RectTransform>();

        Image itemBg = itemGO.AddComponent<Image>();
        itemBg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        Outline itemOutline = itemGO.AddComponent<Outline>();
        itemOutline.effectColor = GetGateColor(type) * 0.7f;
        itemOutline.effectDistance = new Vector2(1, -1);

        CanvasGroup cg = itemGO.AddComponent<CanvasGroup>();
        paletteItemGroups[type] = cg;

        // Add drag functionality
        GateDragItem dragItem = itemGO.AddComponent<GateDragItem>();
        dragItem.Initialize(type, this);

        // Layout for text inside
        HorizontalLayoutGroup itemHLG = itemGO.AddComponent<HorizontalLayoutGroup>();
        itemHLG.padding = new RectOffset(8, 8, 4, 4);
        itemHLG.spacing = 4f;
        itemHLG.childAlignment = TextAnchor.MiddleCenter;
        itemHLG.childForceExpandWidth = true;
        itemHLG.childForceExpandHeight = true;

        // Gate name
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(itemGO.transform, false);
        nameGO.AddComponent<RectTransform>();
        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = type.ToString();
        nameText.fontSize = 18;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = GetGateColor(type);
        nameText.raycastTarget = false;
        LayoutElement nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 2f;

        // Count
        GameObject countGO = new GameObject("Count");
        countGO.transform.SetParent(itemGO.transform, false);
        countGO.AddComponent<RectTransform>();
        TextMeshProUGUI countText = countGO.AddComponent<TextMeshProUGUI>();
        int count = sessionInventory.ContainsKey(type) ? sessionInventory[type] : 0;
        countText.text = $"x{count}";
        countText.fontSize = 16;
        countText.alignment = TextAlignmentOptions.Center;
        countText.color = Color.white;
        countText.raycastTarget = false;
        LayoutElement countLE = countGO.AddComponent<LayoutElement>();
        countLE.flexibleWidth = 1f;

        paletteCountLabels[type] = countText;
    }

    private void BuildControlsUI()
    {
        // ═══════════════════════════════
        // CLOSE BUTTON (top-right X)
        // ═══════════════════════════════
        GameObject closeBtn = new GameObject("CloseButton");
        closeBtn.transform.SetParent(transform, false);

        RectTransform closeRect = closeBtn.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.93f, 0.91f);
        closeRect.anchorMax = new Vector2(0.99f, 0.98f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;

        Image closeBg = closeBtn.AddComponent<Image>();
        closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);

        Button closeBtnComp = closeBtn.AddComponent<Button>();
        closeBtnComp.onClick.AddListener(ClosePuzzle);

        GameObject closeLbl = new GameObject("X");
        closeLbl.transform.SetParent(closeBtn.transform, false);
        RectTransform xRect = closeLbl.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.offsetMin = Vector2.zero;
        xRect.offsetMax = Vector2.zero;
        TextMeshProUGUI xText = closeLbl.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 18;
        xText.fontStyle = FontStyles.Bold;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Color.white;
        xText.raycastTarget = false;

        // ═══════════════════════════════
        // SUBMIT BUTTON (top-right, left of X)
        // ═══════════════════════════════
        if (submitButton != null) Destroy(submitButton);

        submitButton = new GameObject("SubmitButton");
        submitButton.transform.SetParent(transform, false);

        RectTransform btnRect = submitButton.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.78f, 0.91f);
        btnRect.anchorMax = new Vector2(0.92f, 0.98f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        Image btnBg = submitButton.AddComponent<Image>();
        btnBg.color = new Color(0.15f, 0.4f, 0.15f, 0.95f);

        Outline btnOutline = submitButton.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.3f, 0.8f, 0.3f, 0.8f);
        btnOutline.effectDistance = new Vector2(2, -2);

        Button btn = submitButton.AddComponent<Button>();
        btn.onClick.AddListener(OnSubmit);

        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.1f, 1f);
        btn.colors = colors;

        GameObject btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(submitButton.transform, false);
        RectTransform lblRect = btnLabel.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero;
        lblRect.offsetMax = Vector2.zero;
        TextMeshProUGUI lblText = btnLabel.AddComponent<TextMeshProUGUI>();
        lblText.text = "SUBMIT";
        lblText.fontSize = 16;
        lblText.fontStyle = FontStyles.Bold;
        lblText.alignment = TextAlignmentOptions.Center;
        lblText.color = Color.white;
        lblText.raycastTarget = false;

        // ═══════════════════════════════
        // ATTEMPTS DISPLAY (top-right, left of Submit)
        // ═══════════════════════════════
        if (attemptsLabel != null) Destroy(attemptsLabel);

        attemptsLabel = new GameObject("AttemptsLabel");
        attemptsLabel.transform.SetParent(transform, false);

        RectTransform attRect = attemptsLabel.AddComponent<RectTransform>();
        attRect.anchorMin = new Vector2(0.60f, 0.91f);
        attRect.anchorMax = new Vector2(0.77f, 0.98f);
        attRect.offsetMin = Vector2.zero;
        attRect.offsetMax = Vector2.zero;

        attemptsText = attemptsLabel.AddComponent<TextMeshProUGUI>();
        attemptsText.fontSize = 14;
        attemptsText.alignment = TextAlignmentOptions.Center;
        attemptsText.color = Color.white;
        UpdateAttemptsDisplay();

        // ═══════════════════════════════
        // FEEDBACK PANEL (center, hidden by default)
        // ═══════════════════════════════
        if (feedbackPanel != null) Destroy(feedbackPanel);

        feedbackPanel = new GameObject("FeedbackPanel");
        feedbackPanel.transform.SetParent(transform, false);

        RectTransform fbRect = feedbackPanel.AddComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.2f, 0.4f);
        fbRect.anchorMax = new Vector2(0.8f, 0.6f);
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        Image fbBg = feedbackPanel.AddComponent<Image>();
        fbBg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        Outline fbOutline = feedbackPanel.AddComponent<Outline>();
        fbOutline.effectColor = new Color(0.8f, 0.6f, 0.2f, 1f);
        fbOutline.effectDistance = new Vector2(3, -3);

        feedbackText = new GameObject("Text").AddComponent<TextMeshProUGUI>();
        feedbackText.transform.SetParent(feedbackPanel.transform, false);
        RectTransform ftRect = feedbackText.GetComponent<RectTransform>();
        ftRect.anchorMin = Vector2.zero;
        ftRect.anchorMax = Vector2.one;
        ftRect.offsetMin = new Vector2(20, 10);
        ftRect.offsetMax = new Vector2(-20, -10);
        feedbackText.fontSize = 24;
        feedbackText.fontStyle = FontStyles.Bold;
        feedbackText.alignment = TextAlignmentOptions.Center;

        feedbackPanel.SetActive(false);
    }

    // ===================== SUBMIT / CHECK =====================

    private void OnSubmit()
    {
        if (puzzleSolved || isGameOver) return;

        // Check if all slots are filled
        bool allFilled = true;
        foreach (var slot in dropSlots)
        {
            if (slot.IsEmpty)
            {
                allFilled = false;
                break;
            }
        }

        if (!allFilled)
        {
            ShowFeedback("Place a gate in every slot!", new Color(1f, 0.8f, 0.2f), 2f);
            return;
        }

        // Check answers with detailed logging
        bool allCorrect = true;
        int correctCount = 0;

        Debug.Log("[PuzzleTable] ═══ CHECKING ANSWERS ═══");
        for (int i = 0; i < dropSlots.Count; i++)
        {
            GateType? placed = dropSlots[i].PlacedGate;
            GateType expected = dropSlots[i].correctGateType;
            bool correct = dropSlots[i].IsCorrect();

            Debug.Log($"[PuzzleTable]  Slot {i} (\"{dropSlots[i].gameObject.name.Trim()}\"): Placed={placed} (int={(placed.HasValue ? (int)placed.Value : -1)}), Expected={expected} (int={(int)expected}), Match={correct}");

            if (correct)
            {
                correctCount++;
                dropSlots[i].ShowCorrectFeedback();
            }
            else
            {
                allCorrect = false;
                dropSlots[i].ShowWrongFeedback();
            }
        }
        Debug.Log($"[PuzzleTable] Result: {correctCount}/{dropSlots.Count} correct. AllCorrect={allCorrect}");

        if (allCorrect)
        {
            // PUZZLE SOLVED!
            puzzleSolved = true;
            ShowFeedback("CORRECT! Puzzle Solved!", new Color(0.2f, 1f, 0.3f), 0f);
            OnPuzzleComplete();
        }
        else
        {
            attemptsUsed++;
            UpdateAttemptsDisplay();

            if (attemptsUsed >= maxAttempts)
            {
                isGameOver = true;
                ShowFeedback("GAME OVER!\nNo attempts remaining.", new Color(1f, 0.2f, 0.2f), 0f);
                OnGameOver();
            }
            else
            {
                int remaining = maxAttempts - attemptsUsed;
                ShowFeedback($"Wrong! {correctCount}/{dropSlots.Count} correct.\n{remaining} attempt{(remaining == 1 ? "" : "s")} left.",
                    new Color(1f, 0.4f, 0.3f), 3f);
            }
        }
    }

    private void UpdateAttemptsDisplay()
    {
        if (attemptsText != null)
        {
            int remaining = maxAttempts - attemptsUsed;
            attemptsText.text = $"Attempts: {remaining}/{maxAttempts}";

            if (remaining <= 1)
                attemptsText.color = new Color(1f, 0.3f, 0.3f);
            else
                attemptsText.color = Color.white;
        }
    }

    // ===================== PALETTE REFRESH =====================

    public void RefreshPalette()
    {
        foreach (var kvp in paletteCountLabels)
        {
            int count = sessionInventory.ContainsKey(kvp.Key) ? sessionInventory[kvp.Key] : 0;
            kvp.Value.text = $"x{count}";

            if (paletteItemGroups.ContainsKey(kvp.Key))
            {
                paletteItemGroups[kvp.Key].alpha = count > 0 ? 1f : 0.4f;
            }
        }
    }

    // ===================== FEEDBACK =====================

    private void ShowFeedback(string message, Color color, float autoHideDelay)
    {
        if (feedbackPanel == null || feedbackText == null) return;

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackPanel.SetActive(true);

        if (autoHideDelay > 0f)
        {
            StartCoroutine(HideFeedbackAfterDelay(autoHideDelay));
        }
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);
    }

    // ===================== GAME EVENTS =====================

    private void OnPuzzleComplete()
    {
        Debug.Log("[PuzzleTable] PUZZLE COMPLETE!");

        if (submitButton != null)
        {
            Button btn = submitButton.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        // ── Save puzzle completion to Firebase ──
        // 1. Mark the specific puzzle variant as completed
        if (PuzzleManager.Instance != null)
        {
            // Try to get puzzle variant from LevelManager first
            string puzzleId = null;
            if (LevelManager.Instance != null && LevelManager.Instance.currentLevelPuzzle != null)
            {
                puzzleId = LevelManager.Instance.currentLevelPuzzle.variantId;
            }

            // Fallback: use current level name as puzzle ID
            if (string.IsNullOrEmpty(puzzleId))
            {
                int currentLevel = LevelManager.Instance != null ? LevelManager.Instance.GetCurrentLevel() : 1;
                puzzleId = $"Level{currentLevel}_Puzzle";
            }

            PuzzleManager.Instance.CompletePuzzle(puzzleId);
            Debug.Log($"[PuzzleTable] Puzzle '{puzzleId}' marked as completed in Firebase");
        }

        // 2. Unlock next level + save progress to Firebase
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.PuzzleCompleted();
            Debug.Log("[PuzzleTable] Called LevelManager.PuzzleCompleted() — level progress saved to Firebase");
        }
        else
        {
            // Fallback: save directly through AccountManager
            if (AccountManager.Instance != null)
            {
                AccountManager.Instance.UnlockNextLevel();
                Debug.Log("[PuzzleTable] Fallback: Called AccountManager.UnlockNextLevel() directly");
            }
        }

        StartCoroutine(DelayedPuzzleComplete());
    }

    private IEnumerator DelayedPuzzleComplete()
    {
        yield return new WaitForSecondsRealtime(2f);

        SetUIMode(false);
        IsOpen = false;

        // Note: LevelManager.PuzzleCompleted() already calls ShowPuzzleComplete() 
        // and handles level transitions, so we only show it here as a fallback
        // if LevelManager didn't handle it.
        if (LevelManager.Instance == null && UIManager.Instance != null)
        {
            UIManager.Instance.ShowPuzzleComplete();
        }

        gameObject.SetActive(false);
    }

    private void OnGameOver()
    {
        Debug.Log("[PuzzleTable] GAME OVER - No attempts remaining");

        if (submitButton != null)
        {
            Button btn = submitButton.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        StartCoroutine(DelayedGameOver());
    }

    private IEnumerator DelayedGameOver()
    {
        yield return new WaitForSecondsRealtime(3f);

        SetUIMode(false);
        IsOpen = false;
        gameObject.SetActive(false);
    }

    // ===================== UI MODE (cursor + player controls) =====================

    private void SetUIMode(bool enableUI)
    {
        Cursor.visible = enableUI;
        Cursor.lockState = enableUI ? CursorLockMode.None : CursorLockMode.Locked;

        // ── Find player hierarchy ──
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                player = GameObject.Find("FirstPersonPlayer");
        }
        if (player == null)
        {
            Debug.LogWarning("[PuzzleTable] Cannot find player — controls won't be toggled.");
            return;
        }

        // Cache important components from the ENTIRE player hierarchy (children included)
        if (cachedFPC == null)
            cachedFPC = player.GetComponentInChildren<FirstPersonController>();
        if (cachedInputs == null)
            cachedInputs = player.GetComponentInChildren<StarterAssetsInputs>();
        if (cachedCC == null)
            cachedCC = player.GetComponentInChildren<CharacterController>();
        if (cachedCollector == null)
            cachedCollector = player.GetComponentInChildren<SimpleGateCollector>();

        // Try to find Cinemachine brain on main camera
        if (cachedCinemachineBrain == null && Camera.main != null)
        {
            foreach (var comp in Camera.main.GetComponents<MonoBehaviour>())
            {
                if (comp.GetType().Name.Contains("CinemachineBrain"))
                {
                    cachedCinemachineBrain = comp;
                    break;
                }
            }
        }

        if (enableUI)
        {
            // ── Disable ALL player movement, look, and interaction ──
            // NOTE: Do NOT disable CharacterController — disabling it removes
            // collision/ground detection and causes the player to clip through
            // the floor and teleport when re-enabled.
            if (cachedFPC != null) cachedFPC.enabled = false;
            if (cachedInputs != null) cachedInputs.enabled = false;
            // cachedCC stays ENABLED to keep player grounded
            if (cachedCollector != null) cachedCollector.enabled = false;
            if (cachedCinemachineBrain != null) cachedCinemachineBrain.enabled = false;

            // Also disable any remaining scripts on the player root + children
            disabledScripts.Clear();
            MonoBehaviour[] allScripts = player.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in allScripts)
            {
                if (script != null && script.enabled && script != this
                    && script != cachedFPC && script != cachedInputs
                    && script != cachedCollector && script != cachedCinemachineBrain)
                {
                    // Only disable player-related scripts, not UI ones
                    if (script is PlayerController || script is FirstPersonArmAnimator)
                    {
                        disabledScripts.Add(script);
                        script.enabled = false;
                    }
                }
            }

            Debug.Log("[PuzzleTable] Player controls DISABLED");
        }
        else
        {
            // ── Re-enable everything ──
            if (cachedFPC != null) cachedFPC.enabled = true;
            if (cachedInputs != null) cachedInputs.enabled = true;
            // cachedCC was never disabled — no need to re-enable
            if (cachedCollector != null) cachedCollector.enabled = true;
            if (cachedCinemachineBrain != null) cachedCinemachineBrain.enabled = true;

            foreach (MonoBehaviour script in disabledScripts)
            {
                if (script != null) script.enabled = true;
            }
            disabledScripts.Clear();

            Debug.Log("[PuzzleTable] Player controls RE-ENABLED");
        }
    }

    // ===================== UTILITIES =====================

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Trim() == name)
                return child;

            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private Color GetGateColor(GateType type)
    {
        switch (type)
        {
            case GateType.AND: return new Color(0.3f, 0.7f, 1f, 1f);
            case GateType.OR:  return new Color(1f, 0.7f, 0.2f, 1f);
            case GateType.NOT: return new Color(1f, 0.3f, 0.4f, 1f);
            default:           return Color.white;
        }
    }
}
