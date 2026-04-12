using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Serializable wrapper for a single question's answer key.
/// Used by multi-question levels (Level 2, Level 4, etc.).
/// </summary>
[System.Serializable]
public class QuestionAnswerKey
{
    [Tooltip("The correct gate for each box slot in this question, in order (Box1, Box2, ...)")]
    public GateType[] answerKey;
}

/// <summary>
/// Attach to the table object in the scene.
/// When the player interacts (E key via Interactable, or mouse click),
/// it instantiates the UITable prefab and opens the puzzle.
///
/// For multi-question levels (Q1-Q5), ONE random question is picked.
/// The player only needs to solve that single question for the success key to spawn.
/// </summary>
public class InteractiveTable : MonoBehaviour
{
    public bool IsSolved => puzzleAlreadySolved;

    [Header("Puzzle UI")]
    [Tooltip("Assign the UITable prefab from Assets/Prefabs/Table/Table/UITable.prefab")]
    public GameObject puzzleUIPrefab;

    [Header("Answer Key (Single Question Levels)")]
    [Tooltip("The correct gate for each box slot, in order (Box1, Box2, Box3, ...). Used when questionAnswerKeys is empty.")]
    public GateType[] answerKey = new GateType[] {
        GateType.OR,   // Box1
        GateType.OR,   // Box2
        GateType.AND   // Box3
    };

    [Header("Answer Keys (Multi-Question Levels)")]
    [Tooltip("For levels with Q1-Q5 panels. Each entry is one question's answer key. One is picked at random.")]
    public QuestionAnswerKey[] questionAnswerKeys;

    [Header("Settings")]
    public int maxAttempts = 3;

    [Header("Success Key")]
    [Tooltip("The success_key GameObject in the scene. Hidden until the puzzle is solved.")]
    public GameObject successKeyObject;

    [Header("Visual Feedback")]
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer tableRenderer;

    // Runtime
    private GameObject puzzleUIInstance;
    private bool isPuzzleOpen;
    private bool puzzleAlreadySolved = false;
    private bool hasLockedQuestionSelection = false;
    private int lockedQuestionIndex = -1;
    private readonly HashSet<int> exhaustedQuestionIndices = new HashSet<int>();

    // Runtime answer keys loaded from AnswerKeyConfig
    private GateType[][] runtimeAnswerKeys;
    private CircuitQuestionData[] runtimeQuestions;
    private int currentLevel = 1;

    void Start()
    {
        tableRenderer = GetComponent<Renderer>();
        if (tableRenderer != null)
            originalMaterial = tableRenderer.material;

        ResolveSuccessKeyReference();

        // Always hide the success key at start
        if (successKeyObject != null)
        {
            successKeyObject.SetActive(false);
            Debug.Log("[InteractiveTable] Success key hidden until puzzle is solved.");
        }

        // Auto-configure answer keys from AnswerKeyConfig based on current level
        AutoConfigureAnswerKeys();
    }

    private void AutoConfigureAnswerKeys()
    {
        int level = 1;
        if (LevelManager.Instance != null)
            level = LevelManager.Instance.GetCurrentLevel();
        else
            level = GetLevelFromSceneName();

        if (level <= 1)
        {
            // If LevelManager falls back to 1, trust the loaded scene name when available.
            int sceneLevel = GetLevelFromSceneName();
            if (sceneLevel > 1)
                level = sceneLevel;
        }
        currentLevel = level;

        runtimeAnswerKeys = AnswerKeyConfig.GetAnswerKeys(level);
        runtimeQuestions = LevelExpressionConfig.GetQuestions(level);
        Debug.Log($"[InteractiveTable] Auto-configured {runtimeAnswerKeys.Length} question(s) for Level {level}");

        // Inspector questionAnswerKeys override runtime config if set
        if (questionAnswerKeys != null && questionAnswerKeys.Length > 0)
        {
            runtimeAnswerKeys = new GateType[questionAnswerKeys.Length][];
            for (int i = 0; i < questionAnswerKeys.Length; i++)
                runtimeAnswerKeys[i] = questionAnswerKeys[i].answerKey;
            Debug.Log($"[InteractiveTable] Inspector override: {runtimeAnswerKeys.Length} question(s).");
        }

        for (int q = 0; q < runtimeAnswerKeys.Length; q++)
        {
            string gates = string.Join(", ", runtimeAnswerKeys[q]);
            Debug.Log($"[InteractiveTable]   Q{q + 1}: {gates}");
        }
    }

    private int GetLevelFromSceneName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) &&
            sceneName.StartsWith("Level") &&
            int.TryParse(sceneName.Substring(5), out int parsed))
        {
            return parsed;
        }

        return 1;
    }

    private const float MAX_INTERACT_DISTANCE = 5f;

    private bool IsInteractionBlocked()
    {
        if (CutsceneController.IsPlaying || CutsceneController.CameraOnlyMode)
            return true;
        if (PauseMenuController.IsPaused)
            return true;

        Camera cam = Camera.main;
        if (cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist > MAX_INTERACT_DISTANCE)
                return true;
        }
        return false;
    }

    void OnMouseEnter()
    {
        if (IsInteractionBlocked()) return;
        if (tableRenderer != null && highlightMaterial != null && !isPuzzleOpen)
            tableRenderer.material = highlightMaterial;
    }

    void OnMouseExit()
    {
        if (tableRenderer != null && originalMaterial != null && !isPuzzleOpen)
            tableRenderer.material = originalMaterial;
    }

    void OnMouseDown()
    {
        if (IsInteractionBlocked()) return;
        if (!isPuzzleOpen && !PuzzleTableController.IsOpen)
            OpenPuzzleInterface();
    }

    public void OpenPuzzleInterface()
    {
        if (isPuzzleOpen) return;

        if (puzzleAlreadySolved)
        {
            Debug.Log("[InteractiveTable] Puzzle already solved.");
            return;
        }

        if (puzzleUIPrefab == null)
        {
            Debug.LogError("[InteractiveTable] puzzleUIPrefab is not assigned!");
            return;
        }

        // Keep one question locked per table instance for the current play session.
        // Reopening the table should not reshuffle the question.
        int questionIndex;
        if (hasLockedQuestionSelection &&
            lockedQuestionIndex >= 0 &&
            lockedQuestionIndex < runtimeAnswerKeys.Length)
        {
            questionIndex = lockedQuestionIndex;
        }
        else
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < runtimeAnswerKeys.Length; i++)
            {
                if (!exhaustedQuestionIndices.Contains(i))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                exhaustedQuestionIndices.Clear();
                for (int i = 0; i < runtimeAnswerKeys.Length; i++)
                    candidates.Add(i);
            }

            questionIndex = candidates[Random.Range(0, candidates.Count)];
            lockedQuestionIndex = questionIndex;
            hasLockedQuestionSelection = true;
        }
        GateType[] selectedAnswerKey = runtimeAnswerKeys[questionIndex];

        Debug.Log($"[InteractiveTable] Randomly selected Q{questionIndex + 1}/{runtimeAnswerKeys.Length} " +
                  $"with {selectedAnswerKey.Length} gate slots");

        // Hide interact prompt
        var levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null)
            levelUI.HideInteractPrompt();
        if (UIManager.Instance != null)
            UIManager.Instance.ShowInteractPrompt(false);

        // Hide inventory bar while puzzle is open
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.gameObject.SetActive(false);

        EnsureEventSystem();

        // Reuse existing puzzle UI instance after manual close (ESC/X) so attempts persist.
        if (puzzleUIInstance == null)
            puzzleUIInstance = Instantiate(puzzleUIPrefab);
        puzzleUIInstance.SetActive(true);

        EnsurePuzzleUIInfrastructure(puzzleUIInstance);

        PuzzleTableController controller = puzzleUIInstance.GetComponent<PuzzleTableController>();
        if (controller == null)
            controller = puzzleUIInstance.AddComponent<PuzzleTableController>();

        controller.maxAttempts = maxAttempts;
        controller.answerKey = selectedAnswerKey;
        controller.currentLevelNumber = currentLevel;
        controller.selectedQuestionExpression = string.Empty;
        controller.requiredAnd = 0;
        controller.requiredOr = 0;
        controller.requiredNot = 0;

        if (runtimeAnswerKeys.Length > 1)
        {
            // Multi-question: tell controller which Q panel to show
            controller.selectedQuestionIndex = questionIndex;
            controller.SetQuestionNumber(questionIndex + 1, runtimeAnswerKeys.Length);
        }
        else
        {
            controller.selectedQuestionIndex = -1;
        }

        if (runtimeQuestions != null && questionIndex >= 0 && questionIndex < runtimeQuestions.Length)
        {
            CircuitQuestionData questionData = runtimeQuestions[questionIndex];
            if (questionData != null)
            {
                controller.selectedQuestionExpression = questionData.expression;
                controller.requiredAnd = questionData.requiredAnd;
                controller.requiredOr = questionData.requiredOr;
                controller.requiredNot = questionData.requiredNot;
            }
        }

        controller.OpenPuzzle();

        isPuzzleOpen = true;
        StartCoroutine(WatchForPuzzleClose());
    }

    /// <summary>
    /// Supports both full UITable prefabs and level panel-only prefabs by ensuring
    /// the instantiated root can render as an overlay UI.
    /// </summary>
    private void EnsurePuzzleUIInfrastructure(GameObject root)
    {
        if (root == null) return;

        Canvas puzzleCanvas = root.GetComponent<Canvas>();
        if (puzzleCanvas == null)
        {
            puzzleCanvas = root.AddComponent<Canvas>();
            puzzleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            puzzleCanvas.pixelPerfect = false;
        }
        puzzleCanvas.sortingOrder = 500;

        if (root.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Debug.Log("[InteractiveTable] Created EventSystem for UI interactions.");
        }
    }

    private IEnumerator WatchForPuzzleClose()
    {
        yield return null;

        PuzzleTableController controller = puzzleUIInstance != null
            ? puzzleUIInstance.GetComponent<PuzzleTableController>()
            : null;

        while (puzzleUIInstance != null && puzzleUIInstance.activeSelf)
        {
            yield return null;
        }

        isPuzzleOpen = false;

        bool wasSolved = (controller != null && controller.WasPuzzleSolved);
        bool wasGameOver = (controller != null && controller.WasGameOver);

        if (!wasSolved && wasGameOver)
        {
            if (lockedQuestionIndex >= 0)
                exhaustedQuestionIndices.Add(lockedQuestionIndex);

            // After game over, unlock selection so next open uses a remaining question.
            hasLockedQuestionSelection = false;
            lockedQuestionIndex = -1;
        }

        if (wasSolved)
        {
            puzzleAlreadySolved = true;

            bool autoAdvanceLevel = (currentLevel == 5 || currentLevel == 6);

            if (autoAdvanceLevel)
            {
                Debug.Log($"[InteractiveTable] Puzzle solved on Level {currentLevel}. Auto-advancing to next level.");
                if (LevelManager.Instance != null)
                    LevelManager.Instance.PuzzleCompleted();
            }
            else
            {
                if (successKeyObject == null)
                    ResolveSuccessKeyReference();

                if (successKeyObject != null)
                {
                    successKeyObject.SetActive(true);
                    Debug.Log("[InteractiveTable] Puzzle solved! Success key spawned.");
                }
                else
                {
                    Debug.LogWarning("[InteractiveTable] Puzzle solved, but no success key is assigned/found in scene.");
                }
            }
        }

        // Restore inventory bar
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.gameObject.SetActive(true);

        // Clean up only after terminal states; keep instance for manual close/reopen attempt continuity.
        if (puzzleUIInstance != null && (wasSolved || wasGameOver))
        {
            Destroy(puzzleUIInstance);
            puzzleUIInstance = null;
        }

        if (tableRenderer != null && originalMaterial != null)
            tableRenderer.material = originalMaterial;
    }

    private void ResolveSuccessKeyReference()
    {
        if (successKeyObject != null) return;

        CollectibleKey[] keys = FindObjectsByType<CollectibleKey>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < keys.Length; i++)
        {
            CollectibleKey key = keys[i];
            if (key == null) continue;
            if (key.gameObject.scene != gameObject.scene) continue;
            if (key.keyType != CollectibleKey.KeyType.Success) continue;
            successKeyObject = key.gameObject;
            break;
        }

        if (successKeyObject == null)
        {
            successKeyObject = GameObject.Find("success_key");
            if (successKeyObject == null) successKeyObject = GameObject.Find("Success_Key");
            if (successKeyObject == null) successKeyObject = GameObject.Find("SuccessKey");
        }

        if (successKeyObject != null)
            Debug.Log($"[InteractiveTable] Auto-resolved success key: {successKeyObject.name}");
    }
}