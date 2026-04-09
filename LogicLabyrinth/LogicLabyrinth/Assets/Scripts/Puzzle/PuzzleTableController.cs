using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
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

    /// <summary>True if the puzzle was solved in this session (read by InteractiveTable).</summary>
    public bool WasPuzzleSolved => puzzleSolved;
    public bool WasGameOver => isGameOver;

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

    /// <summary>
    /// Set by InteractiveTable before OpenPuzzle().
    /// -1 = single-question mode (find Q1 or Background).
    /// 0..N = multi-question mode (activate Q{index+1}, deactivate others).
    /// </summary>
    [HideInInspector]
    public int selectedQuestionIndex = -1;
    [HideInInspector]
    public int currentLevelNumber = 1;
    [HideInInspector]
    public string selectedQuestionExpression = "";
    [HideInInspector]
    public int requiredAnd;
    [HideInInspector]
    public int requiredOr;
    [HideInInspector]
    public int requiredNot;

    [Header("References (auto-found if empty)")]
    public Transform puzzleContent;   // The Q1 parent that holds the boxes

    [Header("Question Number Display")]
    public TextMeshProUGUI questionNumberText;

    [Header("Layout Tweaks")]
    [Tooltip("Scales the legacy puzzle board content (Level 1-5 style) slightly larger.")]
    public float legacyBoardScale = 1.08f;
    [Tooltip("Optional vertical offset for legacy board (pixels on RectTransform, local Y on Transform).")]
    public float legacyBoardYOffset = 0f;

    public void SetLegacyBoardScale(float scale, bool applyNow = true)
    {
        legacyBoardScale = Mathf.Clamp(scale, 0.75f, 1.35f);
        if (!applyNow) return;

        if (!IsFreeFormCanvasMode() && puzzleContent != null)
            puzzleContent.localScale = Vector3.one * legacyBoardScale;
    }

    public void SetLegacyBoardVerticalOffset(float yOffset, bool applyNow = true)
    {
        legacyBoardYOffset = yOffset;
        if (!applyNow) return;

        if (IsFreeFormCanvasMode() || puzzleContent == null)
            return;

        RectTransform rt = puzzleContent as RectTransform;
        if (rt != null)
        {
            Vector2 anchored = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(anchored.x, legacyBoardYOffset);
        }
        else
        {
            Vector3 local = puzzleContent.localPosition;
            puzzleContent.localPosition = new Vector3(local.x, legacyBoardYOffset, local.z);
        }
    }

    // Runtime state
    private List<GateDropSlot> dropSlots = new List<GateDropSlot>();
    private int attemptsUsed = 0;
    private bool puzzleSolved = false;
    private bool isGameOver = false;
    private int currentQuestion = 0;   // 1-based question number
    private int totalQuestions = 0;    // total question count

    // Temporary inventory for this puzzle session (copied from player inventory)
    private Dictionary<GateType, int> sessionInventory = new Dictionary<GateType, int>();

    // UI elements built at runtime
    private GameObject palettePanel;
    private GameObject submitButton;
    private GameObject attemptsLabel;
    private GameObject feedbackPanel;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI attemptsText;
    private TextMeshProUGUI expressionText;
    private TextMeshProUGUI requirementText;
    private GameObject freeFormGuideRoot;
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

    private bool IsFreeFormCanvasMode()
    {
        return currentLevelNumber >= 6;
    }

    private void ResolveCurrentLevelNumber()
    {
        if (currentLevelNumber > 1)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) &&
            sceneName.StartsWith("Level") &&
            int.TryParse(sceneName.Substring(5), out int parsed) &&
            parsed > 1)
        {
            currentLevelNumber = parsed;
        }
    }

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

    /// <summary>
    /// Sets the question number display (called by InteractiveTable for multi-question levels).
    /// </summary>
    public void SetQuestionNumber(int current, int total)
    {
        currentQuestion = current;
        totalQuestions = total;
    }

    public void OpenPuzzle()
    {
        ResolveCurrentLevelNumber();

        puzzleSolved = false;
        isGameOver = false;
        attemptsUsed = 0;
        IsOpen = true;

        // Reset puzzleContent so FindPuzzleContent picks the correct Q panel
        puzzleContent = null;

        // Copy player inventory into session
        LoadSessionInventory();

        // Find the Q content area with the boxes (uses selectedQuestionIndex for multi-Q levels)
        FindPuzzleContent();

        if (!IsFreeFormCanvasMode() && puzzleContent != null)
            puzzleContent.localScale = Vector3.one * Mathf.Max(1f, legacyBoardScale);

        if (IsFreeFormCanvasMode())
        {
            // Level 6+ uses free-form canvas, so hide legacy pre-drawn scaffold.
            HideLegacyTemplateVisuals();
            BuildFreeFormInputGuide();
            dropSlots.Clear();
        }
        else
        {
            // Set up drop slots on all Box children
            SetupDropSlots();
        }

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

        // First, ensure all intermediate parents (Level2, Level3, etc.) are active
        // Prefabs may have them saved as inactive
        ActivateAllChildren(transform);

        // Resolve the level-specific UI root first so we don't accidentally pick
        // Background/Q panels from another embedded level template in UITable.
        string expectedLevelRootName = $"Level{currentLevelNumber}";
        Transform levelRoot = string.Equals(transform.name.Trim(), expectedLevelRootName, StringComparison.OrdinalIgnoreCase)
            ? transform
            : FindDeepChild(transform, expectedLevelRootName);
        if (levelRoot == null)
        {
            // Panel-only level prefabs may be wrapped/renamed at runtime.
            // If this root already contains the puzzle hierarchy, treat it as the level root.
            Transform maybeBackground = FindDeepChild(transform, "Background");
            if (maybeBackground != null)
                levelRoot = transform;
        }
        Transform searchRoot = levelRoot != null ? levelRoot : transform;
        if (levelRoot == null)
            Debug.LogWarning($"[PuzzleTable] Level root 'Level{currentLevelNumber}' not found. Falling back to global search.");

        // Free-form levels (Level 6+) do not rely on Q1..Q5 panel objects.
        // Use the background root directly to avoid noisy Qx lookup warnings.
        if (IsFreeFormCanvasMode())
        {
            puzzleContent = FindActiveDeepChild(searchRoot, "Background");
            if (puzzleContent == null)
                puzzleContent = FindDeepChild(searchRoot, "Background");
            if (puzzleContent == null)
                puzzleContent = transform;

            Debug.Log($"[PuzzleTable] Free-form mode: using puzzleContent '{puzzleContent.name}' (path: {GetTransformPath(puzzleContent)})");
            return;
        }

        // ── Multi-question mode: activate the selected Q panel, deactivate others ──
        if (selectedQuestionIndex >= 0)
        {
            string targetQName = $"Q{selectedQuestionIndex + 1}";
            Debug.Log($"[PuzzleTable] Multi-question mode: looking for '{targetQName}'");

            // Find Background parent (contains Q1..Q5)
            Transform background = FindDeepChild(searchRoot, "Background");
            if (background == null && searchRoot != transform)
                background = FindDeepChild(transform, "Background");
            if (background != null)
            {
                // Deactivate ALL Q panels, then activate the selected one
                for (int i = 0; i < background.childCount; i++)
                {
                    Transform child = background.GetChild(i);
                    if (child.name.Trim().StartsWith("Q"))
                    {
                        child.gameObject.SetActive(child.name.Trim() == targetQName);
                    }
                }

                // Now find the activated Q panel
                puzzleContent = FindActiveDeepChild(background, targetQName);
                if (puzzleContent == null)
                {
                    // Fallback: find even if inactive
                    puzzleContent = FindDeepChild(background, targetQName);
                    if (puzzleContent != null)
                        puzzleContent.gameObject.SetActive(true);
                }
            }

            if (puzzleContent != null)
            {
                Debug.Log($"[PuzzleTable] Selected question panel: '{targetQName}' (path: {GetTransformPath(puzzleContent)})");
                return;
            }
            else
            {
                Debug.LogWarning($"[PuzzleTable] Could not find '{targetQName}' — falling back to default search.");
            }
        }

        // ── Single-question mode (original logic) ──
        // Search only ACTIVE children to avoid picking up inactive levels
        puzzleContent = FindActiveDeepChild(searchRoot, "Q1");
        if (puzzleContent == null)
        {
            puzzleContent = FindActiveDeepChild(searchRoot, "Background");
        }
        // Fallback: search all children (including inactive) if nothing active found
        if (puzzleContent == null)
        {
            puzzleContent = FindDeepChild(searchRoot, "Q1");
        }
        if (puzzleContent == null)
        {
            puzzleContent = FindDeepChild(searchRoot, "Background");
        }
        if (puzzleContent == null)
        {
            puzzleContent = transform;
        }

        Debug.Log($"[PuzzleTable] puzzleContent resolved to: \"{puzzleContent.name}\" (path: {GetTransformPath(puzzleContent)})");
    }

    /// <summary>
    /// Activates all children in the hierarchy (non-recursive for the top 2 levels).
    /// This ensures intermediate panels like "Level2", "Level3" etc. are active
    /// even if the prefab saved them as inactive.
    /// Does NOT activate Q panels (those are managed by multi-question logic).
    /// </summary>
    private void ActivateAllChildren(Transform root)
    {
        foreach (Transform child in root)
        {
            // Don't auto-activate Q panels — those are managed by multi-question logic
            if (child.name.Trim().StartsWith("Q") && char.IsDigit(child.name.Trim().Length > 1 ? child.name.Trim()[1] : ' '))
                continue;

            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
                Debug.Log($"[PuzzleTable] Activated inactive child: '{child.name}'");
            }

            // Also activate grandchildren (Background, etc.)
            foreach (Transform grandchild in child)
            {
                if (grandchild.name.Trim().StartsWith("Q") && grandchild.name.Trim().Length > 1 && char.IsDigit(grandchild.name.Trim()[1]))
                    continue;

                if (!grandchild.gameObject.activeSelf)
                {
                    grandchild.gameObject.SetActive(true);
                    Debug.Log($"[PuzzleTable] Activated inactive grandchild: '{grandchild.name}'");
                }
            }
        }
    }

    private void SetupDropSlots()
    {
        dropSlots.Clear();

        // Find all children named "Box1", "Box2", etc. (with or without leading space)
        List<Transform> boxTransforms = new List<Transform>();
        FindAllBoxChildren(puzzleContent, boxTransforms);

        // Fallback: if no boxes found in puzzleContent, search the ENTIRE hierarchy
        if (boxTransforms.Count == 0)
        {
            Debug.LogWarning($"[PuzzleTable] No Box children found in puzzleContent \"{puzzleContent.name}\". Searching entire hierarchy...");
            FindAllBoxChildren(transform, boxTransforms);
        }

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

        if (dropSlots.Count == 0)
        {
            Debug.LogError("[PuzzleTable] ERROR: No drop slots found! Make sure the puzzle UI has children named Box1, Box2, etc.");
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

        bool freeFormMode = IsFreeFormCanvasMode();

        // ── Palette container ──
        palettePanel = new GameObject("GatePalette");
        palettePanel.transform.SetParent(transform, false);

        RectTransform palRect = palettePanel.AddComponent<RectTransform>();
        if (freeFormMode)
        {
            // Not at the bottom: compact toolbar near top for blank-canvas mode.
            palRect.anchorMin = new Vector2(0.24f, 0.87f);
            palRect.anchorMax = new Vector2(0.76f, 0.93f);
        }
        else
        {
            palRect.anchorMin = new Vector2(0.15f, 0.02f);
            palRect.anchorMax = new Vector2(0.85f, 0.12f);
        }
        palRect.offsetMin = Vector2.zero;
        palRect.offsetMax = Vector2.zero;

        Image palBg = palettePanel.AddComponent<Image>();
        palBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        Outline palOutline = palettePanel.AddComponent<Outline>();
        palOutline.effectColor = new Color(0.6f, 0.5f, 0.2f, 0.8f);
        palOutline.effectDistance = new Vector2(2, -2);

        // Horizontal layout for palette items
        HorizontalLayoutGroup hlg = palettePanel.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 6, 6);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Legacy mode keeps the "YOUR GATES" label; free-form mode omits it.
        if (!freeFormMode)
            CreatePaletteTitle(palettePanel.transform);

        // Gate items side by side
        CreatePaletteItem(palettePanel.transform, GateType.AND);
        CreatePaletteItem(palettePanel.transform, GateType.OR);
        CreatePaletteItem(palettePanel.transform, GateType.NOT);
        if (freeFormMode)
            CreatePaletteItem(palettePanel.transform, GateType.WIRE);
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
        if (type == GateType.WIRE)
        {
            countText.text = "x∞";
        }
        else
        {
            int count = sessionInventory.ContainsKey(type) ? sessionInventory[type] : 0;
            countText.text = $"x{count}";
        }
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
        attemptsText.fontSize = 20;
        attemptsText.fontStyle = FontStyles.Bold;
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

        // ═══════════════════════════════
        // QUESTION NUMBER (top-left, only for multi-question levels)
        // ═══════════════════════════════
        if (totalQuestions > 1)
        {
            if (questionNumberText == null)
            {
                GameObject qNumGO = new GameObject("QuestionNumberText");
                qNumGO.transform.SetParent(transform, false);
                RectTransform qNumRect = qNumGO.AddComponent<RectTransform>();
                qNumRect.anchorMin = new Vector2(0.01f, 0.91f);
                qNumRect.anchorMax = new Vector2(0.25f, 0.98f);
                qNumRect.offsetMin = Vector2.zero;
                qNumRect.offsetMax = Vector2.zero;
                questionNumberText = qNumGO.AddComponent<TextMeshProUGUI>();
                questionNumberText.fontSize = 16;
                questionNumberText.fontStyle = FontStyles.Bold;
                questionNumberText.alignment = TextAlignmentOptions.Center;
                questionNumberText.color = new Color(0.84f, 0.75f, 0.5f, 1f); // Gold
            }
            questionNumberText.text = $"Question {currentQuestion}/{totalQuestions}";
            questionNumberText.gameObject.SetActive(true);
        }

        BuildQuestionInfoUI();
    }

    private void BuildQuestionInfoUI()
    {
        if (expressionText == null)
        {
            GameObject exprGO = new GameObject("ExpressionText");
            exprGO.transform.SetParent(transform, false);
            RectTransform exprRect = exprGO.AddComponent<RectTransform>();
            exprRect.anchorMin = new Vector2(0.02f, 0.82f);
            exprRect.anchorMax = new Vector2(0.70f, 0.90f);
            exprRect.offsetMin = Vector2.zero;
            exprRect.offsetMax = Vector2.zero;

            expressionText = exprGO.AddComponent<TextMeshProUGUI>();
            expressionText.fontSize = 20;
            expressionText.fontStyle = FontStyles.Bold;
            expressionText.alignment = TextAlignmentOptions.Left;
            expressionText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        }

        if (requirementText == null)
        {
            GameObject reqGO = new GameObject("GateRequirementText");
            reqGO.transform.SetParent(transform, false);
            RectTransform reqRect = reqGO.AddComponent<RectTransform>();
            reqRect.anchorMin = new Vector2(0.02f, 0.76f);
            reqRect.anchorMax = new Vector2(0.70f, 0.82f);
            reqRect.offsetMin = Vector2.zero;
            reqRect.offsetMax = Vector2.zero;

            requirementText = reqGO.AddComponent<TextMeshProUGUI>();
            requirementText.fontSize = 15;
            requirementText.alignment = TextAlignmentOptions.Left;
            requirementText.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        }

        if (!string.IsNullOrEmpty(selectedQuestionExpression))
        {
            expressionText.text = $"F = {selectedQuestionExpression}";
            expressionText.gameObject.SetActive(true);
        }
        else
        {
            expressionText.gameObject.SetActive(false);
        }

        if (requiredAnd > 0 || requiredOr > 0 || requiredNot > 0)
        {
            requirementText.text = $"Required: AND x{requiredAnd}, OR x{requiredOr}, NOT x{requiredNot}";
            requirementText.gameObject.SetActive(true);
        }
        else
        {
            requirementText.gameObject.SetActive(false);
        }
    }

    // ===================== SUBMIT / CHECK =====================

    private void OnSubmit()
    {
        if (puzzleSolved || isGameOver) return;

        if (IsFreeFormCanvasMode() && dropSlots.Count == 0)
        {
            ShowFeedback("Blank canvas mode active. Place gates and wire connections; graph validation is the next step.", new Color(0.95f, 0.85f, 0.3f), 2.5f);
            return;
        }

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

        // Level 6+ expression mode: validate by required gate composition instead of fixed slot order.
        if (!string.IsNullOrEmpty(selectedQuestionExpression) &&
            (requiredAnd > 0 || requiredOr > 0 || requiredNot > 0))
        {
            int andCount = 0;
            int orCount = 0;
            int notCount = 0;

            foreach (var slot in dropSlots)
            {
                if (!slot.PlacedGate.HasValue) continue;
                switch (slot.PlacedGate.Value)
                {
                    case GateType.AND: andCount++; break;
                    case GateType.OR: orCount++; break;
                    case GateType.NOT: notCount++; break;
                }
            }

            bool compositionMatch =
                andCount == requiredAnd &&
                orCount == requiredOr &&
                notCount == requiredNot;

            Debug.Log($"[PuzzleTable] Expression mode check => AND {andCount}/{requiredAnd}, OR {orCount}/{requiredOr}, NOT {notCount}/{requiredNot}");

            if (compositionMatch)
            {
                puzzleSolved = true;
                ShowFeedback("CORRECT! Composition matches expression requirements.", new Color(0.2f, 1f, 0.3f), 0f);
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
                    ShowFeedback(
                        $"Wrong gate composition.\nNeed AND x{requiredAnd}, OR x{requiredOr}, NOT x{requiredNot}\n{remaining} attempt{(remaining == 1 ? "" : "s")} left.",
                        new Color(1f, 0.4f, 0.3f), 3f);
                }
            }
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
            if (kvp.Key == GateType.WIRE)
            {
                kvp.Value.text = "x∞";
            }
            else
            {
                int count = sessionInventory.ContainsKey(kvp.Key) ? sessionInventory[kvp.Key] : 0;
                kvp.Value.text = $"x{count}";
            }

            if (paletteItemGroups.ContainsKey(kvp.Key))
            {
                if (kvp.Key == GateType.WIRE)
                {
                    paletteItemGroups[kvp.Key].alpha = 1f;
                }
                else
                {
                    int count = sessionInventory.ContainsKey(kvp.Key) ? sessionInventory[kvp.Key] : 0;
                    paletteItemGroups[kvp.Key].alpha = count > 0 ? 1f : 0.4f;
                }
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

            // Fallback: use current level name + question number as puzzle ID
            if (string.IsNullOrEmpty(puzzleId))
            {
                int currentLevel = currentLevelNumber;
                if (LevelManager.Instance != null)
                    currentLevel = LevelManager.Instance.GetCurrentLevel();
                if (totalQuestions > 1)
                    puzzleId = $"Level{currentLevel}_Puzzle_Q{currentQuestion}";
                else
                    puzzleId = $"Level{currentLevel}_Puzzle";
            }

            PuzzleManager.Instance.CompletePuzzle(puzzleId);
            Debug.Log($"[PuzzleTable] Puzzle '{puzzleId}' marked as completed in Firebase");
        }

        // 2. Save progress to Firebase (unlock next level data) but do NOT auto-transition.
        //    The level transition is triggered when the player opens Door_Success with the success key.
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.UnlockNextLevel();
            Debug.Log("[PuzzleTable] Progress saved — next level unlocked in Firebase.");
        }

        StartCoroutine(DelayedPuzzleComplete());
    }

    private IEnumerator DelayedPuzzleComplete()
    {
        yield return new WaitForSecondsRealtime(2f);

        SetUIMode(false);
        IsOpen = false;

        // Close the puzzle UI — the success key will spawn via InteractiveTable.
        // The player must collect it and open Door_Success to proceed to the next level.

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

        // Return placed gates before closing so slots do not persist visually across openings.
        foreach (var slot in dropSlots)
            slot.ClearSlot();

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

    /// <summary>
    /// Find a child by name, only traversing ACTIVE GameObjects.
    /// This prevents finding elements inside inactive level panels.
    /// </summary>
    private Transform FindActiveDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            // Skip inactive children entirely
            if (!child.gameObject.activeSelf) continue;

            if (child.name.Trim() == name)
                return child;

            Transform found = FindActiveDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

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

    private string GetTransformPath(Transform t)
    {
        string path = t.name;
        Transform current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private Color GetGateColor(GateType type)
    {
        switch (type)
        {
            case GateType.AND: return new Color(0.3f, 0.7f, 1f, 1f);
            case GateType.OR:  return new Color(1f, 0.7f, 0.2f, 1f);
            case GateType.NOT: return new Color(1f, 0.3f, 0.4f, 1f);
            case GateType.WIRE: return new Color(0.7f, 1f, 1f, 1f);
            default:           return Color.white;
        }
    }

    private void HideLegacyTemplateVisuals()
    {
        if (puzzleContent == null) return;
        
        // In free-form mode, keep the paper/background root itself,
        // but disable ALL legacy template children (boxes, old lines, old labels, etc.).
        int hiddenCount = 0;
        foreach (Transform child in puzzleContent)
        {
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                hiddenCount++;
            }
        }

        // Some legacy prefabs put labels directly on the root.
        TextMeshProUGUI[] legacyTexts = puzzleContent.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            if (legacyTexts[i] != null)
                legacyTexts[i].gameObject.SetActive(false);
        }

        Debug.Log($"[PuzzleTable] Free-form cleanup: hid {hiddenCount} direct legacy child object(s) under '{puzzleContent.name}'.");
    }

    private void HideLegacyTemplateVisualsRecursive(Transform root)
    {
        foreach (Transform child in root)
        {
            string n = child.name.Trim().ToLowerInvariant();
            if (n.StartsWith("box") || n.Contains("line"))
            {
                child.gameObject.SetActive(false);
            }
            HideLegacyTemplateVisualsRecursive(child);
        }
    }

    private void BuildFreeFormInputGuide()
    {
        if (!IsFreeFormCanvasMode()) return;

        if (freeFormGuideRoot != null)
            Destroy(freeFormGuideRoot);

        freeFormGuideRoot = new GameObject("FreeFormInputGuide");
        freeFormGuideRoot.transform.SetParent(transform, false);

        RectTransform rootRect = freeFormGuideRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CanvasGroup cg = freeFormGuideRoot.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        CreateInputLine("A", 0.10f);
        CreateInputLine("B", 0.18f);
        CreateInputLine("C", 0.26f);
        CreateOutputMarker();
    }

    private void CreateInputLine(string label, float x)
    {
        GameObject labelGO = new GameObject($"InputLabel_{label}");
        labelGO.transform.SetParent(freeFormGuideRoot.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(x - 0.02f, 0.73f);
        labelRect.anchorMax = new Vector2(x + 0.02f, 0.78f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 34;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        t.raycastTarget = false;

        GameObject lineGO = new GameObject($"InputLine_{label}");
        lineGO.transform.SetParent(freeFormGuideRoot.transform, false);
        RectTransform lineRect = lineGO.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(x - 0.001f, 0.20f);
        lineRect.anchorMax = new Vector2(x + 0.001f, 0.68f);
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;

        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        lineImg.raycastTarget = false;
    }

    private void CreateOutputMarker()
    {
        GameObject outGO = new GameObject("OutputLabel");
        outGO.transform.SetParent(freeFormGuideRoot.transform, false);
        RectTransform outRect = outGO.AddComponent<RectTransform>();
        outRect.anchorMin = new Vector2(0.78f, 0.43f);
        outRect.anchorMax = new Vector2(0.97f, 0.49f);
        outRect.offsetMin = Vector2.zero;
        outRect.offsetMax = Vector2.zero;

        TextMeshProUGUI outText = outGO.AddComponent<TextMeshProUGUI>();
        outText.text = "OUTPUT";
        outText.fontSize = 22;
        outText.fontStyle = FontStyles.Bold;
        outText.alignment = TextAlignmentOptions.Right;
        outText.textWrappingMode = TextWrappingModes.NoWrap;
        outText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        outText.raycastTarget = false;
    }
}
