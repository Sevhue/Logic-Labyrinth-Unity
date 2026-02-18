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
    private bool tableDialogueActive = false;
    private bool tableDialogueFinished = false;
    private GameObject dialogueInstance;
    private GameObject instructionBanner;
    private TextMeshProUGUI instructionTMP;

    // ── Phase 3 ──
    private bool waitingForTableClose = false;
    private bool doorTutorialStarted = false;
    private GameObject doorTutorialUI;

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

    void Update()
    {
        // ── Phase 1: Cutscene panels ──
        if (cutsceneActive && waitingForClick)
        {
            bool advance = Input.GetKeyDown(KeyCode.Space)
                         || Input.GetKeyDown(KeyCode.Return)
                         || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (CameraOnlyMode)
                advance = advance || Input.GetMouseButtonDown(0);
            if (advance)
                AdvanceCutscene();
            KeepInventoryHidden();
            return;
        }

        // ── Phase 2: Waiting for table to open ──
        if (waitingForTableOpen && PuzzleTableController.IsOpen)
        {
            waitingForTableOpen = false;
            StartCoroutine(RunTableDialogue());
        }

        // ── Phase 3: Waiting for table to close after dialogue ──
        if (waitingForTableClose && !PuzzleTableController.IsOpen)
        {
            waitingForTableClose = false;
            StartDoorTutorial();
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
        tableDialogueActive = true;
        IsTableDialogueActive = true;

        // ── Create instruction banner inside the scroll ──
        CreateInstructionBanner();

        // ── Instantiate Dialogue prefab ──
        if (dialoguePrefab == null)
        {
            Debug.LogWarning("[Cutscene] No dialoguePrefab assigned!");
            tableDialogueActive = false;
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
            panelRT.anchorMin = new Vector2(0.15f, 0.06f);
            panelRT.anchorMax = new Vector2(0.85f, 0.18f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // Dark semi-transparent background
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.02f, 0.85f);
            bg.raycastTarget = false;

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
        tableDialogueActive = false;
        IsTableDialogueActive = false;
        tableDialogueFinished = true;

        if (dialogueInstance != null)
        { Destroy(dialogueInstance); dialogueInstance = null; }

        // Instruction banner stays — it's a child of PuzzleTableController,
        // so it auto-destroys when the puzzle UI closes.

        // Start watching for puzzle table to close → Phase 3
        waitingForTableClose = true;
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

        Debug.Log("[Cutscene] Phase 3: Puzzle closed. Starting door tutorial...");

        // Show tutorial notification top-right
        StartCoroutine(ShowDoorTutorialNotification());

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
            td.StartHighlight();
            Debug.Log("[Cutscene] Door_Tutorial highlighted!");
        }
        else
        {
            Debug.LogWarning("[Cutscene] Could not find Door_Tutorial in scene!");
        }
    }

    private IEnumerator ShowDoorTutorialNotification()
    {
        // Create an overlay Canvas for the tutorial notification
        doorTutorialUI = new GameObject("DoorTutorialNotification");
        Canvas canvas = doorTutorialUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;

        CanvasScaler scaler = doorTutorialUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // ── Panel (top-right) ──
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(doorTutorialUI.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.55f, 0.82f);
        panelRT.anchorMax = new Vector2(0.98f, 0.96f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.05f, 0.02f, 0.9f);
        bg.raycastTarget = false;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.7f, 0.35f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        // ── Main text (full panel, no label) ──
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(15f, 8f);
        textRT.offsetMax = new Vector2(-15f, -8f);

        TextMeshProUGUI textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.text = "Open the door and explore the labyrinth to find the logic gates";
        textTMP.fontSize = 20;
        textTMP.alignment = TextAlignmentOptions.Center;
        textTMP.color = new Color(1f, 0.9f, 0.6f, 1f);
        textTMP.fontStyle = FontStyles.Italic;
        textTMP.enableWordWrapping = true;
        textTMP.raycastTarget = false;

        // ── Slide-in animation from right ──
        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        Vector2 originalMin = panelRT.anchorMin;
        Vector2 originalMax = panelRT.anchorMax;
        // Start off-screen to the right
        panelRT.anchorMin = new Vector2(1.05f, originalMin.y);
        panelRT.anchorMax = new Vector2(1.48f, originalMax.y);

        // Animate in
        float slideTime = 0.5f;
        float elapsed = 0f;
        while (elapsed < slideTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideTime;
            t = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic
            panelRT.anchorMin = Vector2.Lerp(new Vector2(1.05f, originalMin.y), originalMin, t);
            panelRT.anchorMax = Vector2.Lerp(new Vector2(1.48f, originalMax.y), originalMax, t);
            cg.alpha = t;
            yield return null;
        }
        panelRT.anchorMin = originalMin;
        panelRT.anchorMax = originalMax;
        cg.alpha = 1f;

        // Pulse the text for attention
        float pulseTimer = 0f;
        float displayTime = 8f; // Show for 8 seconds
        float displayElapsed = 0f;
        Color brightGold = new Color(1f, 0.95f, 0.65f, 1f);
        Color dimGold = new Color(0.85f, 0.7f, 0.35f, 0.8f);

        while (displayElapsed < displayTime)
        {
            displayElapsed += Time.deltaTime;
            pulseTimer += Time.deltaTime * 2.5f;
            float pt = (Mathf.Sin(pulseTimer) + 1f) / 2f;
            textTMP.color = Color.Lerp(dimGold, brightGold, pt);
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        float fadeTime = 0.5f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - (elapsed / fadeTime);
            yield return null;
        }

        Destroy(doorTutorialUI);
        doorTutorialUI = null;
    }

    void OnDestroy()
    {
        IsPlaying = false;
        CameraOnlyMode = false;
        IsTableDialogueActive = false;
        if (instructionBanner != null) Destroy(instructionBanner);
        if (dialogueInstance != null) Destroy(dialogueInstance);
        if (doorTutorialUI != null) Destroy(doorTutorialUI);
    }
}
