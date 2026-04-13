using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the Level 1 tutorial sequence in two phases:
///
/// PHASE 1 — Cutscene panels (Cutscene1..4):
///   1. Cutscene1 — Full-screen black, text (click arrow →)
///   2. Cutscene2 — Full-screen black, text (click arrow →)
///   3. Cutscene3 — Camera look enabled, movement disabled. Press to continue.
///   4. Cutscene4 — Same. Press to continue →
///   5. Movement unlocked → inventory appears → Table highlighted
///
/// PHASE 2 — Table dialogue (when player opens puzzle table):
///   - Instruction banner inside scroll: "Insert the logic gates..."
///   - Inner dialogue auto-plays (2s each, no clicks):
///     "Is this… my homework?"  →  "No… this can't be happening…"  →  "Am I actually inside this…?"
///   - After dialogue ends, player interacts normally.
/// </summary>
public class CutsceneController : MonoBehaviour
{
    private enum TutorialQuestStage
    {
        OpenTable,
        Door,
        Candle,
        LogicGates,
        SolvePuzzle
    }

    [Header("Cutscene Prefab")]
    [Tooltip("The Cutscenes prefab from Assets/Cutscene/Cutscenes.prefab")]
    public GameObject cutscenePrefab;

    [Header("Dialogue Prefab")]
    [Tooltip("The Dialogue prefab from Assets/Cutscene/Dialogue.prefab")]
    public GameObject dialoguePrefab;

    [Header("Settings")]
    public bool onlyLevel1 = true;

    [Header("Dialogue Timing")]
    [Tooltip("How many seconds each inner dialogue line stays on screen")]
    public float dialogueDuration = 4f;

    [Header("Dialogue Visuals")]
    [Tooltip("Show the top instruction banner inside the puzzle scroll.")]
    public bool showInstructionBanner = false;
    [Tooltip("Show dark background panels behind bottom dialogue text.")]
    public bool showDialogueBackground = false;
    [Tooltip("Scale applied to puzzle board after table dialogue ends.")]
    public float postDialogueBoardScale = 0.94f;
    [Tooltip("How much to move the shrunken board upward after dialogue (UI pixels).")]
    public float postDialogueBoardYOffset = 56f;
    [Tooltip("Typewriter speed for post-dialogue text (characters per second).")]
    public float postDialogueTypeCharsPerSecond = 42f;

    // ── Static flags ──
    public static bool IsPlaying { get; private set; }
    public static bool CameraOnlyMode { get; private set; }
    /// <summary>True while the first-time table dialogue is running. Blocks puzzle close.</summary>
    public static bool IsTableDialogueActive { get; private set; }

    // ── Phase 1 ──
    private GameObject cutsceneInstance;
    private GameObject[] cutscenePanels;
    private int currentIndex = -1;
    private bool waitingForClick = false;
    private bool cutsceneActive = false;

    // ── Phase 2 ──
    private bool waitingForTableOpen = false;
    private GameObject dialogueInstance;
    private GameObject instructionBanner;
    private TextMeshProUGUI instructionTMP;
    private GameObject postDialogueBoardExtension;
    private TextMeshProUGUI postDialogueTextPrimary;
    private TextMeshProUGUI postDialogueTextSecondary;
    private Coroutine postDialogueMessageRoutine;
    private ParticleSystem postDialogueSparkles;

    // ── Phase 3 ──
    private bool waitingForTableClose = false;
    private bool doorTutorialStarted = false;
    private GameObject doorTutorialUI;
    private TutorialDoor trackedTutorialDoor;
    private TextMeshProUGUI doorQuestHeadingTMP;
    private TextMeshProUGUI openDoorQuestCheckboxTMP;
    private TextMeshProUGUI openDoorQuestTaskTMP;
    private TextMeshProUGUI findKeyQuestCheckboxTMP;
    private TextMeshProUGUI findKeyQuestTaskTMP;
    private TextMeshProUGUI finalQuestTaskTMP;
    private Coroutine doorQuestTypingRoutine;
    private TutorialQuestStage tutorialQuestStage = TutorialQuestStage.Door;
    private bool openDoorQuestCompleted = false;
    private bool findKeyQuestShown = false;
    private bool findKeyQuestCompleted = false;
    private bool candleCollectQuestCompleted = false;
    private bool candleEquipQuestCompleted = false;
    private bool candleEquippedOnce = false;
    private bool solvePuzzleQuestCompleted = false;
    private bool unlockExitDoorQuestCompleted = false;
    private bool introMovementTipsQueued = false;

    // Inner dialogue lines
    private static readonly string[] dialogueLines = new string[]
    {
        "Is this\u2026 my homework?",
        "No\u2026 this can\u2019t be happening\u2026",
        "Am I actually inside this\u2026?"
    };

    // Cached UI refs
    private GameObject gameUIRoot;
    private GameObject inventoryUIRoot;
    private Canvas levelCanvas;

    void Awake()
    {
        IsPlaying = false;
        CameraOnlyMode = false;
        IsTableDialogueActive = false;
        TutorialDoor.PlayerHasKey = false; // Reset key state on scene load
        TutorialDoor.TutorialKeyCollected = false;
        CollectibleCandle.IsEquipped = false;
    }

    void Start()
    {
        if (onlyLevel1)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName != "Level1")
            {
                enabled = false;
                return;
            }
        }

        // Add TutorialDoor component early so door interaction works at all times
        EnsureTutorialDoorComponent();

        // Disable tutorial key at start so it can't be picked up before table quest
        DisableTutorialKeyAtStart();

        StartCoroutine(StartCutsceneDelayed());
    }

    /// <summary>
    /// Finds Door_Tutorial and ensures it has a TutorialDoor component.
    /// This lets the player interact with the door at any time (even before Phase 3).
    /// Phase 3 only adds the visual highlight.
    /// The TutorialDoor should already be on the door (added in the scene),
    /// but this is a safety net in case it's missing.
    /// </summary>
    private void EnsureTutorialDoorComponent()
    {
        // Search broadly for the door
        TutorialDoor existing = FindAnyObjectByType<TutorialDoor>();
        if (existing != null)
        {
            Debug.Log("[Cutscene] TutorialDoor already present on " + existing.gameObject.name);
            return;
        }

        // Fallback: find and add
        GameObject doorObj = GameObject.Find("Door_Tutorial");
        if (doorObj == null)
        {
            GameObject envGO = GameObject.Find("lvl1_NewEnvironment");
            if (envGO != null)
            {
                Transform doorT = envGO.transform.Find("Door_Tutorial");
                if (doorT != null) doorObj = doorT.gameObject;
            }
        }

        if (doorObj != null)
        {
            doorObj.AddComponent<TutorialDoor>();
            Debug.Log("[Cutscene] TutorialDoor component added to Door_Tutorial (fallback).");
        }
        else
        {
            Debug.LogWarning("[Cutscene] Could not find Door_Tutorial in scene!");
        }
    }

    private void DisableTutorialKeyAtStart()
    {
        CollectibleKey[] keys = FindObjectsByType<CollectibleKey>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < keys.Length; i++)
        {
            CollectibleKey key = keys[i];
            if (key == null || key.keyType != CollectibleKey.KeyType.Tutorial) continue;
            key.gameObject.SetActive(false);
            Debug.Log("[Cutscene] Tutorial key disabled at start.");
            return;
        }
    }

    void Update()
    {
        // ── Phase 1: Cutscene panels ──
        if (cutsceneActive && waitingForClick)
        {
            if (CameraOnlyMode)
            {
                // Panels 2+ ("Where am I?" etc.) — allow either keyboard or mouse click.
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                    Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                    Input.GetMouseButtonDown(0) ||
                    Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter))
                    AdvanceCutscene();
            }
            else
            {
                bool advance = Input.GetKeyDown(KeyCode.Space)
                             || Input.GetKeyDown(KeyCode.Return)
                             || Input.GetKeyDown(KeyCode.KeypadEnter);
                if (advance)
                    AdvanceCutscene();
            }
            KeepInventoryHidden();
            return;
        }

        // ── Phase 2: Waiting for table to open ──
        if (waitingForTableOpen && PuzzleTableController.IsOpen)
        {
            waitingForTableOpen = false;
            if (tutorialQuestStage == TutorialQuestStage.OpenTable)
                StartCoroutine(CompleteOpenTableAndStartDialogue());
            else
                StartCoroutine(RunTableDialogue());
        }

        // ── Phase 3: Waiting for table to close after dialogue ──
        if (waitingForTableClose && !PuzzleTableController.IsOpen)
        {
            waitingForTableClose = false;
            StartDoorTutorial();
        }

        // If the player grabs the tutorial key before opening the table,
        // keep the quest text in sync with that path to avoid stale guidance.
        if (tutorialQuestStage == TutorialQuestStage.OpenTable && TutorialDoor.TutorialKeyCollected)
        {
            if (doorQuestTypingRoutine != null)
            {
                StopCoroutine(doorQuestTypingRoutine);
                doorQuestTypingRoutine = null;
            }

            if (doorQuestHeadingTMP != null)
                doorQuestHeadingTMP.text = "TO DO";

            if (openDoorQuestTaskTMP != null)
            {
                openDoorQuestTaskTMP.text = "FIND THE KEY";
                openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);
            }

            if (openDoorQuestCheckboxTMP != null)
                openDoorQuestCheckboxTMP.text = "[v]";
        }

        if (doorTutorialStarted)
        {
            if (trackedTutorialDoor == null)
                trackedTutorialDoor = FindAnyObjectByType<TutorialDoor>();

            if (tutorialQuestStage == TutorialQuestStage.Door)
            {
                if (!findKeyQuestShown && TutorialDoor.TutorialKeyCollected)
                    ShowFindKeyQuest();
                else if (!findKeyQuestShown && trackedTutorialDoor != null && trackedTutorialDoor.HasTriedWhileLocked)
                    ShowFindKeyQuest();

                if (findKeyQuestShown && !findKeyQuestCompleted && TutorialDoor.TutorialKeyCollected)
                    CompleteFindKeyQuest();

                if (!openDoorQuestCompleted && trackedTutorialDoor != null && trackedTutorialDoor.IsDoorOpen)
                    CompleteOpenDoorQuest();

                if (openDoorQuestCompleted && findKeyQuestCompleted)
                    BeginCandleQuestStage();
            }
            else if (tutorialQuestStage == TutorialQuestStage.Candle)
            {
                bool hasCandle = InventoryManager.Instance != null && InventoryManager.Instance.HasCandle;
                if (!candleCollectQuestCompleted && hasCandle)
                    CompleteCandleCollectQuest();

                if (CollectibleCandle.IsEquipped)
                    candleEquippedOnce = true;

                if (!candleEquipQuestCompleted && candleEquippedOnce)
                    CompleteCandleEquipQuest();

                if (candleCollectQuestCompleted && candleEquipQuestCompleted)
                    BeginLogicGatesQuestStage();
            }
            else if (tutorialQuestStage == TutorialQuestStage.LogicGates)
            {
                if (InventoryManager.Instance != null)
                {
                    int total = InventoryManager.Instance.GetGateCount("AND")
                              + InventoryManager.Instance.GetGateCount("OR")
                              + InventoryManager.Instance.GetGateCount("NOT");
                    if (total >= 3)
                        BeginSolvePuzzleQuestStage();
                }
            }
            else if (tutorialQuestStage == TutorialQuestStage.SolvePuzzle)
            {
                if (!solvePuzzleQuestCompleted && IsPuzzleSolvedForQuest())
                    BeginUnlockExitDoorQuestStage();

                if (solvePuzzleQuestCompleted && !unlockExitDoorQuestCompleted && IsSuccessDoorOpenedForQuest())
                    BeginRunToExitQuestStage();
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  PHASE 1: CUTSCENE PANELS
    // ═══════════════════════════════════════════════

    private IEnumerator StartCutsceneDelayed()
    {
        yield return null; yield return null; yield return null;

        if (cutscenePrefab == null)
        {
            Debug.LogError("[Cutscene] No cutscenePrefab assigned!");
            yield break;
        }

        cutsceneInstance = Instantiate(cutscenePrefab);
        cutsceneInstance.name = "Cutscenes_Active";
        cutsceneInstance.SetActive(true);

        Canvas canvas = cutsceneInstance.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = 999;

        cutscenePanels = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            Transform t = cutsceneInstance.transform.Find($"Cutscene{i + 1}");
            if (t != null)
            {
                cutscenePanels[i] = t.gameObject;
                cutscenePanels[i].SetActive(false);
                WireArrowButton(cutscenePanels[i], i);
            }
        }

        HideGameUI();
        cutsceneActive = true;
        ShowCutscene(0);
    }

    private void HideGameUI()
    {
        var levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null) { gameUIRoot = levelUI.gameObject; gameUIRoot.SetActive(false); }

        if (GameInventoryUI.Instance != null)
        { inventoryUIRoot = GameInventoryUI.Instance.gameObject; inventoryUIRoot.SetActive(false); }

        GameObject lcGO = GameObject.Find("LevelCanvas");
        if (lcGO != null) { levelCanvas = lcGO.GetComponent<Canvas>(); if (levelCanvas != null) levelCanvas.enabled = false; }

        if (UIManager.Instance != null) UIManager.Instance.ShowInteractPrompt(false);
    }

    private void KeepInventoryHidden()
    {
        if (GameInventoryUI.Instance != null && GameInventoryUI.Instance.gameObject.activeSelf)
        { inventoryUIRoot = GameInventoryUI.Instance.gameObject; inventoryUIRoot.SetActive(false); }
    }

    private void ShowGameUI()
    {
        if (gameUIRoot != null) gameUIRoot.SetActive(true);
        if (inventoryUIRoot != null) inventoryUIRoot.SetActive(true);
        if (levelCanvas != null) levelCanvas.enabled = true;
    }

    private void WireArrowButton(GameObject panel, int panelIndex)
    {
        if (panelIndex >= 2) return;
        Button btn = null;
        Transform arrow = panel.transform.Find("Image");
        if (arrow != null)
        {
            btn = arrow.GetComponent<Button>() ?? arrow.gameObject.AddComponent<Button>();
            Image img = arrow.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }
        if (btn == null)
        {
            btn = panel.GetComponent<Button>() ?? panel.AddComponent<Button>();
            Image pi = panel.GetComponent<Image>();
            if (pi == null) { pi = panel.AddComponent<Image>(); pi.color = Color.clear; }
            pi.raycastTarget = true;
        }
        int ci = panelIndex;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => { if (waitingForClick) AdvanceCutscene(); });
    }

    private void ShowCutscene(int index)
    {
        if (index < 0 || index >= cutscenePanels.Length || cutscenePanels[index] == null)
        { EndCutscene(); return; }

        for (int i = 0; i < cutscenePanels.Length; i++)
            if (cutscenePanels[i] != null) cutscenePanels[i].SetActive(false);

        currentIndex = index;
        cutscenePanels[index].SetActive(true);
        waitingForClick = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowInteractPrompt(false);

        if (index <= 1)
        { IsPlaying = true; CameraOnlyMode = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else
        { IsPlaying = false; CameraOnlyMode = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        // Show movement tips during the "Where am I?" phase (panel index 2 onward), once.
        if (index >= 2 && !introMovementTipsQueued)
        {
            introMovementTipsQueued = true;
            StartCoroutine(ShowMovementTips());
        }
    }

    private void AdvanceCutscene()
    {
        if (!waitingForClick) return;
        waitingForClick = false;
        int next = currentIndex + 1;
        if (next < cutscenePanels.Length && cutscenePanels[next] != null)
            ShowCutscene(next);
        else
            EndCutscene();
    }

    private void EndCutscene()
    {
        Debug.Log("[Cutscene] Phase 1 complete. Waiting for table...");
        IsPlaying = false;
        CameraOnlyMode = false;
        cutsceneActive = false;

        if (cutsceneInstance != null) { Destroy(cutsceneInstance); cutsceneInstance = null; }

        ShowGameUI();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        HighlightTable();
        waitingForTableOpen = true;

        // Show "OPEN THE PUZZLE TABLE" quest immediately after cutscene.
        tutorialQuestStage = TutorialQuestStage.OpenTable;
        ShowDoorQuestTracker();   // FadeInDoorQuest will pick BeginOpenTableQuestStage via the stage check.
    }

    private IEnumerator ShowMovementTips()
    {
        // Keep these on unscaled time so they still sequence correctly even if time scale changes.
        yield return new WaitForSecondsRealtime(1.2f);
        TipOverlayUI.ShowTip("Use W, A, S, D to move around the labyrinth.", 5f, 40f);
        yield return new WaitForSecondsRealtime(5.4f);
        TipOverlayUI.ShowTip("Hold Shift to sprint.", 6f, 40f);
    }

    private void HighlightTable()
    {
        InteractiveTable table = FindAnyObjectByType<InteractiveTable>();
        if (table == null) return;
        TableHighlight h = table.GetComponent<TableHighlight>() ?? table.gameObject.AddComponent<TableHighlight>();
        h.StartHighlight();
    }

    // ═══════════════════════════════════════════════
    //  PHASE 2: TABLE DIALOGUE (auto-advance, 2s each)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Coroutine that runs the entire table dialogue sequence.
    /// Auto-advances every dialogueDuration seconds. No clicking needed.
    /// </summary>
    private IEnumerator RunTableDialogue()
    {
        Debug.Log("[Cutscene] Phase 2: Table opened! Starting inner dialogue...");
        IsTableDialogueActive = true;

        // Optional instruction banner inside the scroll.
        if (showInstructionBanner)
            CreateInstructionBanner();

        // ── Instantiate Dialogue prefab ──
        if (dialoguePrefab == null)
        {
            Debug.LogWarning("[Cutscene] No dialoguePrefab assigned!");
            yield break;
        }

        dialogueInstance = Instantiate(dialoguePrefab);
        dialogueInstance.name = "Dialogue_Active";
        dialogueInstance.SetActive(true);

        // Make it render on top of the puzzle UI
        Canvas dc = dialogueInstance.GetComponent<Canvas>();
        if (dc != null) dc.sortingOrder = 1001;

        // We'll create our own dialogue panels instead of using the prefab's panels directly,
        // since the prefab panels have TextMeshProUGUI (a Graphic) which conflicts with adding Image.
        // Hide all prefab children — we'll build clean panels as children of the dialogue Canvas.
        for (int i = 0; i < dialogueInstance.transform.childCount; i++)
            dialogueInstance.transform.GetChild(i).gameObject.SetActive(false);

        // Create 3 clean dialogue panels
        GameObject[] panels = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            // Container with background
            GameObject panel = new GameObject($"DialoguePanel{i + 1}");
            panel.transform.SetParent(dialogueInstance.transform, false);

            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.15f, 0.10f);
            panelRT.anchorMax = new Vector2(0.85f, 0.22f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            if (showDialogueBackground)
            {
                // Optional dark semi-transparent background
                Image bg = panel.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.03f, 0.02f, 0.85f);
                bg.raycastTarget = false;
            }

            // Text child
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(panel.transform, false);

            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(20f, 5f);
            textRT.offsetMax = new Vector2(-20f, -5f);

            TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.text = dialogueLines[i];
            txt.fontSize = 26;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Italic;
            txt.color = new Color(0.97f, 0.89f, 0.61f, 1f); // Gold text
            txt.enableWordWrapping = true;
            txt.raycastTarget = false;

            panel.SetActive(false);
            panels[i] = panel;
        }

        // ── Wait 5 seconds before starting the dialogue ──
        yield return new WaitForSecondsRealtime(5f);

        // ── Auto-advance through each dialogue line ──
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null) continue;

            // Hide all, show current
            for (int j = 0; j < panels.Length; j++)
                if (panels[j] != null) panels[j].SetActive(false);

            panels[i].SetActive(true);
            Debug.Log($"[Cutscene] Dialogue {i + 1}: \"{dialogueLines[i]}\"");

            yield return new WaitForSecondsRealtime(dialogueDuration);
        }

        // ── Done — clean up dialogue, keep instruction banner ──
        EndTableDialogue();
    }

    /// <summary>
    /// Creates a styled instruction banner inside the puzzle scroll area (top)
    /// with pulsing golden glow effect and larger text.
    /// </summary>
    private void CreateInstructionBanner()
    {
        PuzzleTableController ptc = FindAnyObjectByType<PuzzleTableController>();
        if (ptc == null) return;

        instructionBanner = new GameObject("TutorialBanner");
        instructionBanner.transform.SetParent(ptc.transform, false);

        // ── Background panel ──
        RectTransform bgRT = instructionBanner.AddComponent<RectTransform>();
        // Wide banner across the top of the scroll area
        bgRT.anchorMin = new Vector2(0.05f, 0.85f);
        bgRT.anchorMax = new Vector2(0.65f, 0.96f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        Image bgImg = instructionBanner.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.06f, 0.02f, 0.88f); // Dark parchment overlay
        bgImg.raycastTarget = false;

        // ── Gold border ──
        Outline outline = instructionBanner.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.7f, 0.35f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        // ── Text ──
        GameObject textObj = new GameObject("InstructionText");
        textObj.transform.SetParent(instructionBanner.transform, false);

        RectTransform txtRT = textObj.AddComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.04f, 0f);
        txtRT.anchorMax = new Vector2(0.96f, 1f);
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        instructionTMP = textObj.AddComponent<TextMeshProUGUI>();
        instructionTMP.text = "Insert the logic gates and solve the Boolean diagram to escape";
        instructionTMP.fontSize = 20;
        instructionTMP.alignment = TextAlignmentOptions.Center;
        instructionTMP.color = new Color(1f, 0.85f, 0.4f, 1f); // Golden
        instructionTMP.fontStyle = FontStyles.Bold | FontStyles.Italic;
        instructionTMP.enableWordWrapping = true;
        instructionTMP.raycastTarget = false;

        // Start the golden pulse coroutine
        StartCoroutine(PulseInstructionText());

        Debug.Log("[Cutscene] Instruction banner created inside scroll.");
    }

    private void EndTableDialogue()
    {
        Debug.Log("[Cutscene] Phase 2 complete! Player can now solve the puzzle.");
        IsTableDialogueActive = false;

        if (dialogueInstance != null)
        { Destroy(dialogueInstance); dialogueInstance = null; }

        ApplyPostDialoguePuzzleStyling();

        // Instruction banner stays — it's a child of PuzzleTableController,
        // so it auto-destroys when the puzzle UI closes.

        // Start watching for puzzle table to close → Phase 3
        waitingForTableClose = true;
    }

    private void ApplyPostDialoguePuzzleStyling()
    {
        PuzzleTableController ptc = FindAnyObjectByType<PuzzleTableController>();
        if (ptc == null) return;

        ptc.SetLegacyBoardScale(postDialogueBoardScale, true);
        ptc.SetLegacyBoardVerticalOffset(postDialogueBoardYOffset, true);
        CreatePostDialogueBoardExtension(ptc.transform);
    }

    private void CreatePostDialogueBoardExtension(Transform parent)
    {
        if (parent == null) return;

        // Global safety cleanup: remove stale extensions created by any duplicate flow.
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t != null && t.name == "PostDialogueBoardExtension")
                Destroy(t.gameObject);
        }

        // Safety cleanup: only one extension panel should exist at a time.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == "PostDialogueBoardExtension")
                Destroy(child.gameObject);
        }

        if (postDialogueBoardExtension != null)
            Destroy(postDialogueBoardExtension);

        postDialogueBoardExtension = new GameObject("PostDialogueBoardExtension");
        postDialogueBoardExtension.transform.SetParent(parent, false);

        RectTransform extRT = postDialogueBoardExtension.AddComponent<RectTransform>();
        extRT.anchorMin = new Vector2(0.16f, 0.14f);
        extRT.anchorMax = new Vector2(0.84f, 0.30f);
        extRT.offsetMin = Vector2.zero;
        extRT.offsetMax = Vector2.zero;

        Image bg = postDialogueBoardExtension.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.10f, 0.05f, 0.92f);
        bg.raycastTarget = false;

        Outline border = postDialogueBoardExtension.AddComponent<Outline>();
        border.effectColor = new Color(0.85f, 0.67f, 0.22f, 0.85f);
        border.effectDistance = new Vector2(2f, 2f);

        Shadow woodDepth = postDialogueBoardExtension.AddComponent<Shadow>();
        woodDepth.effectColor = new Color(0f, 0f, 0f, 0.55f);
        woodDepth.effectDistance = new Vector2(0f, -3f);

        CreatePostDialogueSparkles(postDialogueBoardExtension.transform);

        GameObject primaryGO = new GameObject("PrimaryText");
        primaryGO.transform.SetParent(postDialogueBoardExtension.transform, false);
        RectTransform pRT = primaryGO.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.05f, 0.20f);
        pRT.anchorMax = new Vector2(0.95f, 0.88f);
        pRT.offsetMin = Vector2.zero;
        pRT.offsetMax = Vector2.zero;

        postDialogueTextPrimary = primaryGO.AddComponent<TextMeshProUGUI>();
        postDialogueTextPrimary.text = string.Empty;
        postDialogueTextPrimary.fontSize = 28f;
        postDialogueTextPrimary.alignment = TextAlignmentOptions.Center;
        postDialogueTextPrimary.fontStyle = FontStyles.Bold;
        postDialogueTextPrimary.color = new Color(1f, 0.90f, 0.58f, 1f);
        postDialogueTextPrimary.enableWordWrapping = true;
        postDialogueTextPrimary.raycastTarget = false;

        Shadow primaryShadow = primaryGO.AddComponent<Shadow>();
        primaryShadow.effectColor = new Color(0.48f, 0.29f, 0.04f, 0.9f);
        primaryShadow.effectDistance = new Vector2(0f, -2f);

        GameObject secondaryGO = new GameObject("SecondaryText");
        secondaryGO.transform.SetParent(postDialogueBoardExtension.transform, false);
        RectTransform sRT = secondaryGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.05f, 0.20f);
        sRT.anchorMax = new Vector2(0.95f, 0.88f);
        sRT.offsetMin = Vector2.zero;
        sRT.offsetMax = Vector2.zero;

        postDialogueTextSecondary = secondaryGO.AddComponent<TextMeshProUGUI>();
        postDialogueTextSecondary.text = string.Empty;
        postDialogueTextSecondary.fontSize = 42f;
        postDialogueTextSecondary.alignment = TextAlignmentOptions.Center;
        postDialogueTextSecondary.fontStyle = FontStyles.Bold;
        postDialogueTextSecondary.color = new Color(1f, 0.82f, 0.34f, 1f);
        postDialogueTextSecondary.enableWordWrapping = true;
        postDialogueTextSecondary.raycastTarget = false;
        postDialogueTextSecondary.gameObject.SetActive(false);

        Shadow secondaryShadow = secondaryGO.AddComponent<Shadow>();
        secondaryShadow.effectColor = new Color(0.52f, 0.30f, 0.05f, 0.95f);
        secondaryShadow.effectDistance = new Vector2(0f, -3f);

        if (postDialogueMessageRoutine != null)
            StopCoroutine(postDialogueMessageRoutine);
        postDialogueMessageRoutine = StartCoroutine(RunPostDialogueMessageSequence());
    }

    private IEnumerator RunPostDialogueMessageSequence()
    {
        if (postDialogueTextPrimary == null || postDialogueTextSecondary == null)
            yield break;

        const string firstLine = "This is a Simplified Boolean Expression and you need to solve it's diagram";
        const string secondLine = "Find the required Logic gates inside the maze";

        postDialogueTextPrimary.gameObject.SetActive(true);
        postDialogueTextSecondary.gameObject.SetActive(false);

        yield return StartCoroutine(TypeSentence(postDialogueTextPrimary, firstLine, postDialogueTypeCharsPerSecond));
        if (postDialogueTextPrimary == null || postDialogueTextSecondary == null)
            yield break;

        yield return new WaitForSecondsRealtime(3f);
        if (postDialogueTextPrimary == null || postDialogueTextSecondary == null)
            yield break;

        // Keep a single active label to prevent any visual overlap race between two TMP objects.
        postDialogueTextSecondary.gameObject.SetActive(false);
        yield return StartCoroutine(TypeSentence(postDialogueTextPrimary, secondLine, postDialogueTypeCharsPerSecond * 1.2f));
    }

    private IEnumerator TypeSentence(TextMeshProUGUI target, string sentence, float charsPerSecond)
    {
        if (target == null) yield break;

        target.text = string.Empty;
        float cps = Mathf.Max(8f, charsPerSecond);
        float interval = 1f / cps;

        for (int i = 1; i <= sentence.Length; i++)
        {
            if (target == null) yield break;
            target.text = sentence.Substring(0, i) + "|";
            yield return new WaitForSecondsRealtime(interval);
        }

        if (target == null) yield break;
        target.text = sentence;
    }

    private void CreatePostDialogueSparkles(Transform parent)
    {
        if (postDialogueSparkles != null)
            Destroy(postDialogueSparkles.gameObject);

        GameObject sparkleGO = new GameObject("GoldSparkles");
        sparkleGO.transform.SetParent(parent, false);

        RectTransform rt = sparkleGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.08f);
        rt.anchorMax = new Vector2(0.98f, 0.92f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        postDialogueSparkles = sparkleGO.AddComponent<ParticleSystem>();
        var main = postDialogueSparkles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.93f, 0.62f, 0.95f),
            new Color(1f, 0.76f, 0.24f, 0.85f));
        main.maxParticles = 64;

        var emission = postDialogueSparkles.emission;
        emission.rateOverTime = 16f;

        var shape = postDialogueSparkles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(6.8f, 1.8f, 0.05f);

        var noise = postDialogueSparkles.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.55f;

        var col = postDialogueSparkles.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.70f), 0f),
                new GradientColorKey(new Color(1f, 0.76f, 0.24f), 0.6f),
                new GradientColorKey(new Color(0.95f, 0.62f, 0.15f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.2f),
                new GradientAlphaKey(0.85f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = postDialogueSparkles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.alignment = ParticleSystemRenderSpace.Local;
    }

    /// <summary>
    /// Pulses the instruction text between bright gold and dimmer gold
    /// to draw the player's attention.
    /// </summary>
    private IEnumerator PulseInstructionText()
    {
        if (instructionTMP == null) yield break;

        Color brightGold = new Color(1f, 0.92f, 0.55f, 1f);
        Color dimGold = new Color(0.75f, 0.55f, 0.18f, 0.65f);
        float speed = 2f;
        float timer = 0f;

        while (instructionTMP != null)
        {
            timer += Time.unscaledDeltaTime * speed;
            float t = (Mathf.Sin(timer) + 1f) / 2f; // 0..1 smooth oscillation
            instructionTMP.color = Color.Lerp(dimGold, brightGold, t);
            // No font size change — avoids layout glitch
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════
    //  PHASE 3: DOOR TUTORIAL (after first puzzle close)
    // ═══════════════════════════════════════════════

    private void StartDoorTutorial()
    {
        if (doorTutorialStarted) return;
        doorTutorialStarted = true;
        tutorialQuestStage = TutorialQuestStage.Door;
        openDoorQuestCompleted = false;
        findKeyQuestShown = false;
        findKeyQuestCompleted = false;
        candleCollectQuestCompleted = false;
        candleEquipQuestCompleted = false;
        candleEquippedOnce = false;
        solvePuzzleQuestCompleted = false;
        unlockExitDoorQuestCompleted = false;

        Debug.Log("[Cutscene] Phase 3: Puzzle closed. Starting door tutorial...");

        // Show persistent quest tracker on the left side.
        ShowDoorQuestTracker();

        // Find Door_Tutorial and add highlight + interaction
        GameObject doorObj = GameObject.Find("Door_Tutorial");
        if (doorObj == null)
        {
            // Search inside lvl1_NewEnvironment
            Transform env = null;
            GameObject envGO = GameObject.Find("lvl1_NewEnvironment");
            if (envGO != null) env = envGO.transform;
            if (env != null)
            {
                Transform doorT = env.Find("Door_Tutorial");
                if (doorT != null) doorObj = doorT.gameObject;
            }
        }

        if (doorObj != null)
        {
            TutorialDoor td = doorObj.GetComponent<TutorialDoor>();
            if (td == null) td = doorObj.AddComponent<TutorialDoor>();
            trackedTutorialDoor = td;
            td.StartHighlight();
            Debug.Log("[Cutscene] Door_Tutorial highlighted!");
        }
        else
        {
            Debug.LogWarning("[Cutscene] Could not find Door_Tutorial in scene!");
        }
    }

    private void ShowDoorQuestTracker()
    {
        if (doorTutorialUI != null)
            Destroy(doorTutorialUI);

        doorTutorialUI = new GameObject("DoorQuestTracker");
        Canvas canvas = doorTutorialUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;

        CanvasScaler scaler = doorTutorialUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        CanvasGroup rootGroup = doorTutorialUI.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;

        // Panel pinned on the far-left side
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(doorTutorialUI.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.02f, 0.62f);
        panelRT.anchorMax = new Vector2(0.27f, 0.85f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Intentionally no background image or border for this quest tracker.

        // TO DO heading
        GameObject headingGO = new GameObject("Heading");
        headingGO.transform.SetParent(panelGO.transform, false);
        RectTransform hRT = headingGO.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.08f, 0.72f);
        hRT.anchorMax = new Vector2(0.92f, 0.95f);
        hRT.offsetMin = Vector2.zero;
        hRT.offsetMax = Vector2.zero;

        doorQuestHeadingTMP = headingGO.AddComponent<TextMeshProUGUI>();
        doorQuestHeadingTMP.text = string.Empty;
        doorQuestHeadingTMP.fontSize = 28;
        doorQuestHeadingTMP.fontStyle = FontStyles.Bold;
        doorQuestHeadingTMP.alignment = TextAlignmentOptions.Left;
        doorQuestHeadingTMP.color = new Color(1f, 0.90f, 0.60f, 1f);
        doorQuestHeadingTMP.raycastTarget = false;

        // Checkbox text: [ ] or [x]
        GameObject checkboxGO = new GameObject("Checkbox");
        checkboxGO.transform.SetParent(panelGO.transform, false);
        RectTransform cRT = checkboxGO.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.08f, 0.28f);
        cRT.anchorMax = new Vector2(0.20f, 0.62f);
        cRT.offsetMin = Vector2.zero;
        cRT.offsetMax = Vector2.zero;

        openDoorQuestCheckboxTMP = checkboxGO.AddComponent<TextMeshProUGUI>();
        openDoorQuestCheckboxTMP.text = "[ ]";
        openDoorQuestCheckboxTMP.fontSize = 34;
        openDoorQuestCheckboxTMP.fontStyle = FontStyles.Bold;
        openDoorQuestCheckboxTMP.alignment = TextAlignmentOptions.Center;
        openDoorQuestCheckboxTMP.color = new Color(0.95f, 0.86f, 0.56f, 1f);
        openDoorQuestCheckboxTMP.raycastTarget = false;

        GameObject taskGO = new GameObject("Task");
        taskGO.transform.SetParent(panelGO.transform, false);
        RectTransform tRT = taskGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.22f, 0.26f);
        tRT.anchorMax = new Vector2(0.92f, 0.64f);
        tRT.offsetMin = Vector2.zero;
        tRT.offsetMax = Vector2.zero;

        openDoorQuestTaskTMP = taskGO.AddComponent<TextMeshProUGUI>();
        openDoorQuestTaskTMP.text = string.Empty;
        openDoorQuestTaskTMP.fontSize = 24;
        openDoorQuestTaskTMP.fontStyle = FontStyles.Bold;
        openDoorQuestTaskTMP.alignment = TextAlignmentOptions.Left;
        openDoorQuestTaskTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        openDoorQuestTaskTMP.enableWordWrapping = true;
        openDoorQuestTaskTMP.raycastTarget = false;

        // Quest row 2 (hidden until first locked attempt)
        GameObject checkbox2GO = new GameObject("Checkbox_FindKey");
        checkbox2GO.transform.SetParent(panelGO.transform, false);
        RectTransform c2RT = checkbox2GO.AddComponent<RectTransform>();
        c2RT.anchorMin = new Vector2(0.08f, 0.04f);
        c2RT.anchorMax = new Vector2(0.20f, 0.28f);
        c2RT.offsetMin = Vector2.zero;
        c2RT.offsetMax = Vector2.zero;

        findKeyQuestCheckboxTMP = checkbox2GO.AddComponent<TextMeshProUGUI>();
        findKeyQuestCheckboxTMP.text = "[ ]";
        findKeyQuestCheckboxTMP.fontSize = 34;
        findKeyQuestCheckboxTMP.fontStyle = FontStyles.Bold;
        findKeyQuestCheckboxTMP.alignment = TextAlignmentOptions.Center;
        findKeyQuestCheckboxTMP.color = new Color(0.95f, 0.86f, 0.56f, 1f);
        findKeyQuestCheckboxTMP.raycastTarget = false;
        findKeyQuestCheckboxTMP.gameObject.SetActive(false);

        GameObject task2GO = new GameObject("Task_FindKey");
        task2GO.transform.SetParent(panelGO.transform, false);
        RectTransform t2RT = task2GO.AddComponent<RectTransform>();
        t2RT.anchorMin = new Vector2(0.22f, 0.03f);
        t2RT.anchorMax = new Vector2(0.92f, 0.30f);
        t2RT.offsetMin = Vector2.zero;
        t2RT.offsetMax = Vector2.zero;

        findKeyQuestTaskTMP = task2GO.AddComponent<TextMeshProUGUI>();
        findKeyQuestTaskTMP.text = string.Empty;
        findKeyQuestTaskTMP.fontSize = 24;
        findKeyQuestTaskTMP.fontStyle = FontStyles.Bold;
        findKeyQuestTaskTMP.alignment = TextAlignmentOptions.Left;
        findKeyQuestTaskTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        findKeyQuestTaskTMP.enableWordWrapping = true;
        findKeyQuestTaskTMP.raycastTarget = false;
        findKeyQuestTaskTMP.gameObject.SetActive(false);

        GameObject finalTaskGO = new GameObject("FinalTask");
        finalTaskGO.transform.SetParent(panelGO.transform, false);
        RectTransform fRT = finalTaskGO.AddComponent<RectTransform>();
        fRT.anchorMin = new Vector2(0.08f, 0.03f);
        fRT.anchorMax = new Vector2(0.92f, 0.48f);
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;

        finalQuestTaskTMP = finalTaskGO.AddComponent<TextMeshProUGUI>();
        finalQuestTaskTMP.text = string.Empty;
        finalQuestTaskTMP.fontSize = 22;
        finalQuestTaskTMP.fontStyle = FontStyles.Bold;
        finalQuestTaskTMP.alignment = TextAlignmentOptions.Left;
        finalQuestTaskTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        finalQuestTaskTMP.enableWordWrapping = true;
        finalQuestTaskTMP.raycastTarget = false;
        finalQuestTaskTMP.gameObject.SetActive(false);

        StartCoroutine(FadeInDoorQuest(rootGroup));
    }

    private IEnumerator FadeInDoorQuest(CanvasGroup cg)
    {
        if (cg == null) yield break;

        float elapsed = 0f;
        const float fadeTime = 0.35f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / fadeTime);
            yield return null;
        }

        cg.alpha = 1f;

        if (doorQuestTypingRoutine != null)
            StopCoroutine(doorQuestTypingRoutine);

        if (tutorialQuestStage == TutorialQuestStage.OpenTable)
            doorQuestTypingRoutine = StartCoroutine(BeginOpenTableQuestStage());
        else
            doorQuestTypingRoutine = StartCoroutine(RunDoorQuestIntroTyping());
    }

    private IEnumerator BeginOpenTableQuestStage()
    {
        yield return StartCoroutine(TypeQuestText(doorQuestHeadingTMP, "TO DO", 34f));
        yield return new WaitForSeconds(0.08f);
        yield return StartCoroutine(TypeQuestText(openDoorQuestTaskTMP, "OPEN THE PUZZLE TABLE", 40f));
        doorQuestTypingRoutine = null;
    }

    private IEnumerator CompleteOpenTableAndStartDialogue()
    {
        // Stop any still-running typing.
        if (doorQuestTypingRoutine != null) { StopCoroutine(doorQuestTypingRoutine); doorQuestTypingRoutine = null; }

        // Check off the task.
        if (doorQuestHeadingTMP != null) doorQuestHeadingTMP.text = "TO DO";
        if (openDoorQuestTaskTMP != null)
        {
            openDoorQuestTaskTMP.text = "OPEN THE PUZZLE TABLE";
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);
        }
        if (openDoorQuestCheckboxTMP != null) openDoorQuestCheckboxTMP.text = "[v]";

        yield return new WaitForSeconds(1.2f);

        // Clear the tracker before table dialogue takes over.
        if (doorQuestHeadingTMP != null) doorQuestHeadingTMP.text = string.Empty;
        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        SetQuestRowVisible(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP, false);

        StartCoroutine(RunTableDialogue());
        
        // Enable the tutorial key after table dialogue finishes so it can't be picked up before.
        ActivateTutorialKeyAfterDelay(4f);
    }

    private void ActivateTutorialKeyAfterDelay(float delay)
    {
        StartCoroutine(ActivateTutorialKeyDelayedRoutine(delay));
    }

    private IEnumerator ActivateTutorialKeyDelayedRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        CollectibleKey[] keys = FindObjectsByType<CollectibleKey>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < keys.Length; i++)
        {
            CollectibleKey key = keys[i];
            if (key == null || key.keyType != CollectibleKey.KeyType.Tutorial) continue;
            key.gameObject.SetActive(true);
            yield break;
        }
    }

    private IEnumerator RunDoorQuestIntroTyping()
    {
        yield return StartCoroutine(TypeQuestText(doorQuestHeadingTMP, "TO DO", 34f));
        yield return new WaitForSeconds(0.08f);
        yield return StartCoroutine(TypeQuestText(openDoorQuestTaskTMP, "OPEN THE DOOR", 40f));
        doorQuestTypingRoutine = null;
    }

    private IEnumerator RunCandleQuestIntroTyping()
    {
        yield return StartCoroutine(TypeQuestText(openDoorQuestTaskTMP, "COLLECT THE CANDLE", 40f));
        doorQuestTypingRoutine = null;
    }

    private IEnumerator TypeQuestText(TextMeshProUGUI target, string sentence, float charsPerSecond)
    {
        if (target == null)
            yield break;

        target.text = string.Empty;
        float cps = Mathf.Max(10f, charsPerSecond);
        float interval = 1f / cps;

        for (int i = 1; i <= sentence.Length; i++)
        {
            if (target == null)
                yield break;

            target.text = sentence.Substring(0, i) + "|";
            yield return new WaitForSeconds(interval);
        }

        if (target == null)
            yield break;

        target.text = sentence;
    }

    private IEnumerator TypeQuestTextAndClear(TextMeshProUGUI target, string sentence, float charsPerSecond)
    {
        yield return StartCoroutine(TypeQuestText(target, sentence, charsPerSecond));
        doorQuestTypingRoutine = null;
    }

    private void SetQuestRowVisible(TextMeshProUGUI checkbox, TextMeshProUGUI task, bool visible)
    {
        if (checkbox != null)
            checkbox.gameObject.SetActive(visible);

        if (task != null)
            task.gameObject.SetActive(visible);
    }

    private void ResetQuestRow(TextMeshProUGUI checkbox, TextMeshProUGUI task)
    {
        if (checkbox != null)
        {
            checkbox.text = "[ ]";
            checkbox.color = new Color(0.95f, 0.86f, 0.56f, 1f);
        }

        if (task != null)
        {
            task.text = string.Empty;
            task.color = new Color(1f, 0.92f, 0.68f, 1f);
        }
    }

    private void ShowFindKeyQuest()
    {
        findKeyQuestShown = true;

        if (findKeyQuestCheckboxTMP != null)
            findKeyQuestCheckboxTMP.gameObject.SetActive(true);

        if (findKeyQuestTaskTMP != null)
        {
            findKeyQuestTaskTMP.gameObject.SetActive(true);
            if (doorQuestTypingRoutine != null)
                StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(findKeyQuestTaskTMP, "FIND THE KEY", 40f));
        }
    }

    private void CompleteFindKeyQuest()
    {
        findKeyQuestCompleted = true;

        if (findKeyQuestCheckboxTMP != null)
            findKeyQuestCheckboxTMP.text = "[v]";

        if (findKeyQuestTaskTMP != null)
            findKeyQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);
    }

    private void CompleteOpenDoorQuest()
    {
        openDoorQuestCompleted = true;

        if (openDoorQuestCheckboxTMP != null)
            openDoorQuestCheckboxTMP.text = "[v]";

        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);

        if (trackedTutorialDoor != null)
            trackedTutorialDoor.StopHighlight();
    }

    private void BeginCandleQuestStage()
    {
        tutorialQuestStage = TutorialQuestStage.Candle;

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        SetQuestRowVisible(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP, true);
        SetQuestRowVisible(findKeyQuestCheckboxTMP, findKeyQuestTaskTMP, false);
        ResetQuestRow(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP);
        ResetQuestRow(findKeyQuestCheckboxTMP, findKeyQuestTaskTMP);

        if (finalQuestTaskTMP != null)
        {
            finalQuestTaskTMP.text = string.Empty;
            finalQuestTaskTMP.gameObject.SetActive(false);
        }

        doorQuestTypingRoutine = StartCoroutine(RunCandleQuestIntroTyping());
    }

    private void CompleteCandleCollectQuest()
    {
        candleCollectQuestCompleted = true;

        if (openDoorQuestCheckboxTMP != null)
            openDoorQuestCheckboxTMP.text = "[v]";

        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);

        SetQuestRowVisible(findKeyQuestCheckboxTMP, findKeyQuestTaskTMP, true);
        if (findKeyQuestTaskTMP != null)
        {
            if (doorQuestTypingRoutine != null)
                StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(findKeyQuestTaskTMP, "PRESS 1 TO EQUIP THE CANDLE", 40f));
        }
    }

    private void CompleteCandleEquipQuest()
    {
        candleEquipQuestCompleted = true;

        if (findKeyQuestCheckboxTMP != null)
            findKeyQuestCheckboxTMP.text = "[v]";

        if (findKeyQuestTaskTMP != null)
            findKeyQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);
    }

    private void BeginLogicGatesQuestStage()
    {
        tutorialQuestStage = TutorialQuestStage.LogicGates;

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        SetQuestRowVisible(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP, true);
        SetQuestRowVisible(findKeyQuestCheckboxTMP, findKeyQuestTaskTMP, false);

        ResetQuestRow(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP);

        if (finalQuestTaskTMP != null)
        {
            finalQuestTaskTMP.text = string.Empty;
            finalQuestTaskTMP.gameObject.SetActive(false);
        }

        doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(openDoorQuestTaskTMP, "FIND THE LOGIC GATES SCATTERED AROUND THE LABYRINTH", 34f));

        // After the quest text finishes typing, hint the player about the journal.
        StartCoroutine(DelayedJournalHint());
    }

    private IEnumerator DelayedJournalHint()
    {
        // Wait for the quest text to finish typing (~2.5s) plus a small buffer.
        yield return new WaitForSeconds(4f);
        TipOverlayUI.ShowTip("Press J to open your Gate Journal.", 8f, 40f);
        GateJournal.EnsureInstance();
    }

    private void BeginSolvePuzzleQuestStage()
    {
        tutorialQuestStage = TutorialQuestStage.SolvePuzzle;
        solvePuzzleQuestCompleted = false;

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        // Check off the logic gates quest and show the new objective
        if (openDoorQuestCheckboxTMP != null)
            openDoorQuestCheckboxTMP.text = "[v]";

        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);

        // Small delay so the checkmark registers visually, then swap text
        StartCoroutine(ShowSolvePuzzleQuest());
    }

    private bool IsPuzzleSolvedForQuest()
    {
        InteractiveTable table = FindAnyObjectByType<InteractiveTable>();
        if (table != null && table.IsSolved)
            return true;

        PuzzleTableController controller = FindAnyObjectByType<PuzzleTableController>();
        return controller != null && controller.WasPuzzleSolved;
    }

    private bool IsSuccessDoorOpenedForQuest()
    {
        SuccessDoor successDoor = FindAnyObjectByType<SuccessDoor>();
        return successDoor != null && successDoor.IsDoorOpen;
    }

    private void BeginUnlockExitDoorQuestStage()
    {
        solvePuzzleQuestCompleted = true;
        unlockExitDoorQuestCompleted = false;

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        if (openDoorQuestCheckboxTMP != null)
            openDoorQuestCheckboxTMP.text = "[v]";

        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);

        StartCoroutine(ShowUnlockExitDoorQuest());
    }

    private IEnumerator ShowUnlockExitDoorQuest()
    {
        yield return new WaitForSeconds(1.2f);

        ResetQuestRow(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP);

        doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(
            openDoorQuestTaskTMP,
            "USE THIS KEY TO UNLOCK THE EXIT DOOR",
            34f));
    }

    private void BeginRunToExitQuestStage()
    {
        unlockExitDoorQuestCompleted = true;

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        if (openDoorQuestCheckboxTMP != null)
            openDoorQuestCheckboxTMP.text = "[v]";

        if (openDoorQuestTaskTMP != null)
            openDoorQuestTaskTMP.color = new Color(0.70f, 1f, 0.73f, 1f);

        StartCoroutine(ShowRunToExitQuest());
    }

    private IEnumerator ShowRunToExitQuest()
    {
        yield return new WaitForSeconds(1.2f);

        ResetQuestRow(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP);

        doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(
            openDoorQuestTaskTMP,
            "RUN TO THE EXIT TO REACH LEVEL 2",
            34f));
    }

    private IEnumerator ShowSolvePuzzleQuest()
    {
        yield return new WaitForSeconds(1.2f);

        ResetQuestRow(openDoorQuestCheckboxTMP, openDoorQuestTaskTMP);

        doorQuestTypingRoutine = StartCoroutine(TypeQuestTextAndClear(
            openDoorQuestTaskTMP,
            "RETURN TO THE PUZZLE TABLE AND SOLVE THE CIRCUIT",
            34f));
    }

    private IEnumerator FadeOutAndDestroyDoorQuestUI()
    {
        if (doorTutorialUI == null)
            yield break;

        yield return new WaitForSeconds(1.2f);

        if (doorTutorialUI == null)
            yield break;

        CanvasGroup cg = doorTutorialUI.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = doorTutorialUI.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        const float fadeTime = 0.35f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
            yield return null;
        }

        Destroy(doorTutorialUI);
        doorTutorialUI = null;
    }

    void OnDestroy()
    {
        if (postDialogueMessageRoutine != null)
        {
            StopCoroutine(postDialogueMessageRoutine);
            postDialogueMessageRoutine = null;
        }

        if (doorQuestTypingRoutine != null)
        {
            StopCoroutine(doorQuestTypingRoutine);
            doorQuestTypingRoutine = null;
        }

        IsPlaying = false;
        CameraOnlyMode = false;
        IsTableDialogueActive = false;
        if (instructionBanner != null) Destroy(instructionBanner);
        if (dialogueInstance != null) Destroy(dialogueInstance);
        if (doorTutorialUI != null) Destroy(doorTutorialUI);
    }
}
