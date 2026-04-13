using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Attach to the TruthDoor in Level 7.
/// When the player presses E, shows the Level7 truth table panel with
/// Attempts/Submit/X UI at the top-right, matching other levels.
/// Called by SimpleGateCollector when E is pressed.
/// </summary>
public class TruthTableDisplay : MonoBehaviour
{


    /// <summary>True while the truth table panel is visible.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Display")]
    [Tooltip("Assign the Level prefab (e.g. Assets/Prefabs/Table/Table/Level7/Level7.prefab or Level8.prefab)")]
    public GameObject displayPanelPrefab;
    
    [Tooltip("Level number (7 or 8) to select correct answer key mappings")]
    public int levelNumber = 7;

    [Header("Attempts")]
    public int maxAttempts = 3;

    [Header("Chapter 3 Mode")]
    [Tooltip("When enabled, selects random Q panels and uses Yes/No evaluation from question text.")]
    public bool useChapter3QuestionMode = false;
    [Tooltip("How many random questions must be answered correctly per interaction.")]
    public int chapter3QuestionsToAnswer = 3;

    [Header("Door")]
    [Tooltip("Door transform to rotate open when the answer is correct. Defaults to this object.")]
    public Transform doorToOpen;
    public float openAngleY = -95f;
    public float openDuration = 1f;

    private GameObject _panelInstance; // the Level7 prefab instance
    private GameObject _canvasGO;      // the fullscreen overlay canvas wrapper
    private int _attemptsUsed = 0;
    private TextMeshProUGUI _attemptsText;
    private Button _submitButton;
    private TextMeshProUGUI _selectedValueText;
    private TextMeshProUGUI _selectedUnknownCell;
    private readonly List<TextMeshProUGUI> _unknownCells = new List<TextMeshProUGUI>();
    private Color _unknownDefaultColor = new Color(0.2f, 0.95f, 0.2f, 1f);
    private readonly Color _selectedBlinkColorA = new Color(0.35f, 1f, 0.35f, 1f);
    private readonly Color _selectedBlinkColorB = new Color(0.85f, 1f, 0.85f, 1f);
    private Transform _activeQuestionPanel;
    private GameObject _feedbackPanel;
    private TextMeshProUGUI _feedbackText;
    private Coroutine _hideFeedbackRoutine;
    private bool _solved;
    private bool _doorOpened;
    private Quaternion _doorClosedRotation;
    private Quaternion _doorOpenRotation;
    private int _chapter3SolvedCount;
    private int _chapter3QuestionIndex;
    private readonly List<Transform> _chapter3RoundQuestions = new List<Transform>();
    private bool? _chapter3SelectedAnswer;
    private Button _chapter3YesButton;
    private Button _chapter3NoButton;
    private static TruthTableDisplay _chapter3RuntimeDisplay;

    private static readonly Regex Chapter3QuestionRegex = new Regex(
        @"Is the output of\s+([A-Za-z]+)\((\d)(?:\s*,\s*(\d))?\)\s+equal to\s+(\d)\?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuestionNameRegex = new Regex(@"^Q\d+$", RegexOptions.Compiled);
    private static readonly bool[] Chapter3AnswerKey = new bool[]
    {
        true,  true,  false, true,  false,
        true,  true,  false, false, true,
        true,  false, true,  true,  true,
        false, true,  true,  false, true,
        true,  true,  false, true,  false,
        true,  true,  false, false, false
    };

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────

    public static void OpenChapter3ForDoor(Transform doorTransform)
    {
        if (doorTransform == null)
            return;

        if (_chapter3RuntimeDisplay == null)
        {
            GameObject go = new GameObject("Chapter3TruthTableDisplayRuntime");
            _chapter3RuntimeDisplay = go.AddComponent<TruthTableDisplay>();
        }

        _chapter3RuntimeDisplay.ConfigureChapter3Runtime(doorTransform);
        _chapter3RuntimeDisplay.OpenDisplay();
    }

    public void OpenDisplay()
    {
        if (IsOpen) return;
        if (_solved && !useChapter3QuestionMode) return;

        if (displayPanelPrefab == null)
        {
            Debug.LogError("[TruthTableDisplay] displayPanelPrefab is not assigned!");
            return;
        }

        if (_canvasGO == null)
            BuildUI();

        _canvasGO.SetActive(true);

        if (useChapter3QuestionMode)
        {
            _solved = false;
            _attemptsUsed = 0;
            _chapter3SolvedCount = 0;
            _chapter3QuestionIndex = 0;
            _chapter3SelectedAnswer = null;
            PrepareChapter3Round();
            ShowChapter3QuestionAtCurrentIndex();
        }
        else
        {
            RandomlySelectOneQuestionPanel();
        }

        IsOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshAttemptsUI();
    }

    public void CloseDisplay()
    {
        if (!IsOpen) return;

        if (_canvasGO != null)
            _canvasGO.SetActive(false);

        IsOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        RestoreGameplayMouseLook();
    }

    private static void RestoreGameplayMouseLook()
    {
        StarterAssetsInputs inputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorInputForLook = true;
            inputs.cursorLocked = true;
            inputs.LookInput(Vector2.zero);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD
    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        bool sourceIsSceneObject = displayPanelPrefab.scene.IsValid();
        _canvasGO = sourceIsSceneObject ? displayPanelPrefab : Instantiate(displayPanelPrefab);
        _panelInstance = _canvasGO;
        RecursivelyEnable(_canvasGO);

        Canvas canvas = _canvasGO.GetComponent<Canvas>();
        if (canvas == null)
            canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = useChapter3QuestionMode ? 3000 : 500;

        CanvasScaler scaler = _canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (_canvasGO.GetComponent<GraphicRaycaster>() == null)
            _canvasGO.AddComponent<GraphicRaycaster>();

        if (doorToOpen == null)
            doorToOpen = transform;
        if (doorToOpen != null)
        {
            _doorClosedRotation = doorToOpen.localRotation;
            _doorOpenRotation = _doorClosedRotation * Quaternion.Euler(0f, openAngleY, 0f);
        }

        BuildControlsUI();
        if (!useChapter3QuestionMode)
        {
            BuildBinaryInputUI();
            BuildTutorialUI();
        }
        BuildFeedbackUI();
    }

    private void ConfigureChapter3Runtime(Transform doorTransform)
    {
        useChapter3QuestionMode = true;
        chapter3QuestionsToAnswer = Mathf.Max(1, chapter3QuestionsToAnswer);
        maxAttempts = 1;
        _doorOpened = false;
        _solved = false;
        doorToOpen = doorTransform;

        if (doorToOpen != null)
        {
            _doorClosedRotation = doorToOpen.localRotation;
            _doorOpenRotation = _doorClosedRotation * Quaternion.Euler(0f, openAngleY, 0f);
        }

        if (displayPanelPrefab == null)
        {
            displayPanelPrefab = FindChapter3QuestionSource();
        }
        else if (!HasChapter3QuestionContent(displayPanelPrefab.transform))
        {
            displayPanelPrefab = FindChapter3QuestionSource();
        }

        if (displayPanelPrefab == null)
        {
            Debug.LogError("[TruthTableDisplay] Could not find a valid Chapter3 question source with Q panels.");
        }
    }

    /// <summary>
    /// Adds the top-right strip: [Attempts: X/3]  [SUBMIT]  [X]
    /// Uses the same anchor positions as PuzzleTableController.BuildControlsUI().
    /// </summary>
    private void BuildControlsUI()
    {
        // ── Attempts label ─────────────────────────────────────────
        GameObject attGO = new GameObject("AttemptsLabel");
        attGO.transform.SetParent(_canvasGO.transform, false);

        RectTransform attRect = attGO.AddComponent<RectTransform>();
        attRect.anchorMin = new Vector2(0.60f, 0.91f);
        attRect.anchorMax = new Vector2(0.77f, 0.98f);
        attRect.offsetMin = Vector2.zero;
        attRect.offsetMax = Vector2.zero;

        _attemptsText = attGO.AddComponent<TextMeshProUGUI>();
        _attemptsText.fontSize = 20;
        _attemptsText.fontStyle = FontStyles.Bold;
        _attemptsText.alignment = TextAlignmentOptions.Center;
        _attemptsText.color = Color.white;
        _attemptsText.raycastTarget = false;

        // ── SUBMIT button ──────────────────────────────────────────
        GameObject submitGO = new GameObject("SubmitButton");
        submitGO.transform.SetParent(_canvasGO.transform, false);

        RectTransform submitRect = submitGO.AddComponent<RectTransform>();
        submitRect.anchorMin = new Vector2(0.78f, 0.91f);
        submitRect.anchorMax = new Vector2(0.92f, 0.98f);
        submitRect.offsetMin = Vector2.zero;
        submitRect.offsetMax = Vector2.zero;

        Image submitImg = submitGO.AddComponent<Image>();
        submitImg.color = new Color(0.15f, 0.4f, 0.15f, 0.95f);

        Outline submitOutline = submitGO.AddComponent<Outline>();
        submitOutline.effectColor = new Color(0.3f, 0.8f, 0.3f, 0.8f);
        submitOutline.effectDistance = new Vector2(2, -2);

        _submitButton = submitGO.AddComponent<Button>();
        _submitButton.targetGraphic = submitImg;
        _submitButton.onClick.AddListener(OnSubmit);

        ColorBlock sc = _submitButton.colors;
        sc.highlightedColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        sc.pressedColor = new Color(0.1f, 0.3f, 0.1f, 1f);
        _submitButton.colors = sc;

        AddLabel(submitGO, "SUBMIT", 16);

        // ── X (close) button ───────────────────────────────────────
        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(_canvasGO.transform, false);

        RectTransform closeRect = closeGO.AddComponent<RectTransform>();
        if (useChapter3QuestionMode)
        {
            // Chapter3: Submit is removed, so move X into the visible top-right action lane.
            closeRect.anchorMin = new Vector2(0.78f, 0.91f);
            closeRect.anchorMax = new Vector2(0.92f, 0.98f);
        }
        else
        {
            closeRect.anchorMin = new Vector2(0.93f, 0.91f);
            closeRect.anchorMax = new Vector2(0.99f, 0.98f);
        }
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;

        Image closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseDisplay);

        AddLabel(closeGO, "X", 18);

        if (useChapter3QuestionMode && _submitButton != null)
            _submitButton.gameObject.SetActive(false);
    }

    private static void AddLabel(GameObject parent, string text, float size)
    {
        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(parent.transform, false);

        RectTransform lrt = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private void BuildBinaryInputUI()
    {
        GameObject panel = new GameObject("BinaryInputPanel");
        panel.transform.SetParent(_canvasGO.transform, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.16f, 0.26f);
        panelRT.anchorMax = new Vector2(0.22f, 0.64f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.85f, 0.66f, 0.25f, 0.9f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        _selectedValueText = CreateValueDisplay(panel.transform, "?", new Vector2(0.22f, 0.76f), new Vector2(0.78f, 0.94f),
            new Color(0.12f, 0.35f, 0.12f, 0.95f), new Color(0.3f, 0.95f, 0.3f, 1f));

        CreateValueButton(panel.transform, "OneButton", "1", new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.62f),
            new Color(0.45f, 0.34f, 0.12f, 0.95f), () => ApplySelectedValue("1"));

        CreateValueButton(panel.transform, "ZeroButton", "0", new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.32f),
            new Color(0.45f, 0.34f, 0.12f, 0.95f), () => ApplySelectedValue("0"));
    }

    private void BuildTutorialUI()
    {
        GameObject panel = new GameObject("TutorialPanel");
        panel.transform.SetParent(_canvasGO.transform, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.70f, 0.28f);
        panelRT.anchorMax = new Vector2(0.91f, 0.62f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.85f, 0.66f, 0.25f, 0.9f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GameObject titleGO = new GameObject("TutorialTitle");
        titleGO.transform.SetParent(panel.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.08f, 0.70f);
        titleRT.anchorMax = new Vector2(0.92f, 0.92f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        TextMeshProUGUI title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = "Tutorial";
        title.fontSize = 52f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.93f, 0.78f, 0.36f, 1f);
        title.raycastTarget = false;

        GameObject bodyGO = new GameObject("TutorialBody");
        bodyGO.transform.SetParent(panel.transform, false);
        RectTransform bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.10f, 0.14f);
        bodyRT.anchorMax = new Vector2(0.90f, 0.68f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        TextMeshProUGUI body = bodyGO.AddComponent<TextMeshProUGUI>();
        body.text = "Click the green ?\nand choose your\nanswer.";
        body.fontSize = 24f;
        body.alignment = TextAlignmentOptions.Center;
        body.color = new Color(0.93f, 0.78f, 0.36f, 1f);
        body.raycastTarget = false;
    }

    private void BuildFeedbackUI()
    {
        if (_feedbackPanel != null)
            Destroy(_feedbackPanel);

        _feedbackPanel = new GameObject("FeedbackPanel");
        _feedbackPanel.transform.SetParent(_canvasGO.transform, false);

        RectTransform fbRect = _feedbackPanel.AddComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.35f, 0.40f);
        fbRect.anchorMax = new Vector2(0.65f, 0.60f);
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        Image bg = _feedbackPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);

        _feedbackText = new GameObject("FeedbackText").AddComponent<TextMeshProUGUI>();
        _feedbackText.transform.SetParent(_feedbackPanel.transform, false);

        RectTransform txtRect = _feedbackText.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        _feedbackText.alignment = TextAlignmentOptions.Center;
        _feedbackText.fontStyle = FontStyles.Bold;
        _feedbackText.fontSize = 80f;
        _feedbackText.raycastTarget = false;

        _feedbackPanel.SetActive(false);
    }

    private static TextMeshProUGUI CreateValueDisplay(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
    {
        GameObject go = new GameObject("SelectedValueDisplay");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGO = new GameObject("ValueText");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 58f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void CreateValueButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        AddLabel(go, label, 44f);
    }

    private void BindUnknownCells(Transform activeQuestionPanel)
    {
        _unknownCells.Clear();
        _selectedUnknownCell = null;
        _activeQuestionPanel = activeQuestionPanel;
        if (_selectedValueText != null)
            _selectedValueText.text = "?";

        if (activeQuestionPanel == null)
            return;

        foreach (TextMeshProUGUI tmp in activeQuestionPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null) continue;

            string value = tmp.text != null ? tmp.text.Trim() : string.Empty;
            if (value != "?") continue;

            _unknownCells.Add(tmp);
            _unknownDefaultColor = tmp.color;
            tmp.raycastTarget = true;

            Button btn = tmp.gameObject.GetComponent<Button>();
            if (btn == null)
                btn = tmp.gameObject.AddComponent<Button>();

            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = tmp;
            btn.onClick.RemoveAllListeners();
            TextMeshProUGUI captured = tmp;
            btn.onClick.AddListener(() => SelectUnknownCell(captured));
        }
    }

    private void SelectUnknownCell(TextMeshProUGUI cell)
    {
        if (cell == null) return;

        if (_selectedUnknownCell != null)
            _selectedUnknownCell.color = _unknownDefaultColor;

        _selectedUnknownCell = cell;
        _selectedUnknownCell.color = _selectedBlinkColorA;

        if (_selectedValueText != null)
            _selectedValueText.text = _selectedUnknownCell.text;
    }

    private void ApplySelectedValue(string value)
    {
        if (_selectedUnknownCell == null || string.IsNullOrEmpty(value))
            return;

        _selectedUnknownCell.text = value;

        if (_selectedValueText != null)
            _selectedValueText.text = value;
    }

    // ─────────────────────────────────────────────────────────────
    //  SUBMIT / GAME OVER
    // ─────────────────────────────────────────────────────────────

    private void OnSubmit()
    {
        if (useChapter3QuestionMode)
        {
            SubmitChapter3Answer();
            return;
        }

        if (_solved || _attemptsUsed >= maxAttempts) return;

        string qName = _activeQuestionPanel != null ? _activeQuestionPanel.name : string.Empty;
        List<string> expected = GetExpectedAnswers(qName);

        if (expected == null || expected.Count == 0)
        {
            ShowFeedback("No answer key for this question.", new Color(1f, 0.7f, 0.2f, 1f), 1.2f, 42f);
            return;
        }

        if (_unknownCells.Count != expected.Count)
        {
            ShowFeedback("Box count mismatch.", new Color(1f, 0.7f, 0.2f, 1f), 1.2f, 42f);
            return;
        }

        for (int i = 0; i < _unknownCells.Count; i++)
        {
            string value = _unknownCells[i] != null ? _unknownCells[i].text.Trim() : string.Empty;
            if (value != "0" && value != "1")
            {
                ShowFeedback("Fill all boxes!", new Color(1f, 0.8f, 0.2f, 1f), 1.0f, 46f);
                return;
            }
        }

        bool allCorrect = true;
        for (int i = 0; i < _unknownCells.Count; i++)
        {
            string actual = _unknownCells[i].text.Trim();
            if (actual != expected[i])
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            _solved = true;
            ShowFeedback("CORRECT!", new Color(0.2f, 1f, 0.3f, 1f), 0.9f, 70f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCorrectAnswerSound();
            OpenDoor();
            _submitButton.interactable = false;
            StartCoroutine(CloseAfterDelay(0.35f));
            return;
        }

        _attemptsUsed++;
        RefreshAttemptsUI();
        ShowFeedback("WRONG!", new Color(1f, 0.1f, 0.1f, 1f), 0.9f, 80f);
        ResetUnknownCellsToQuestionMarks();

        if (_attemptsUsed >= maxAttempts)
        {
            TriggerGameOver();
            return;
        }
    }

    private List<string> GetExpectedAnswers(string questionName)
    {
        if (levelNumber == 8)
        {
            switch (questionName)
            {
                case "Q1":
                    return new List<string> { "1", "1", "1", "1", "0", "0" };
                case "Q2":
                    return new List<string> { "1", "0", "0", "0", "1", "0" };
                case "Q3":
                    return new List<string> { "1", "1", "1", "1", "1", "1" };
                case "Q4":
                    return new List<string> { "1", "0", "0", "0", "0", "0" };
                case "Q5":
                    return new List<string> { "1", "1", "1", "1", "1", "0", "0" };
                default:
                    return null;
            }
        }

        // Default path stays Level 7 for backward compatibility.
        switch (questionName)
        {
            case "Q1":
                return new List<string> { "1", "1", "1", "0", "1", "0", "1", "0", "1", "0", "0" };
            case "Q2":
                return new List<string> { "1", "0", "0", "0", "0", "0", "0", "1", "1", "1", "1", "1" };
            case "Q3":
                return new List<string> { "0", "1", "1", "1", "1", "0", "0", "1", "1", "0", "0", "0" };
            case "Q4":
                return new List<string> { "1", "1", "1", "1", "0", "0" };
            case "Q5":
                return new List<string> { "0", "1", "0", "0", "1", "1" };
            default:
                return null;
        }
    }

    private void ResetUnknownCellsToQuestionMarks()
    {
        for (int i = 0; i < _unknownCells.Count; i++)
        {
            TextMeshProUGUI cell = _unknownCells[i];
            if (cell == null) continue;

            cell.text = "?";
            cell.color = _unknownDefaultColor;
        }

        _selectedUnknownCell = null;
        if (_selectedValueText != null)
            _selectedValueText.text = "?";
    }

    private void ShowFeedback(string message, Color color, float autoHideDelay, float fontSize)
    {
        if (_feedbackPanel == null || _feedbackText == null) return;

        if (_hideFeedbackRoutine != null)
            StopCoroutine(_hideFeedbackRoutine);

        _feedbackText.text = message;
        _feedbackText.color = color;
        _feedbackText.fontSize = fontSize;
        _feedbackPanel.SetActive(true);

        if (autoHideDelay > 0f)
            _hideFeedbackRoutine = StartCoroutine(HideFeedbackAfterDelay(autoHideDelay));
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_feedbackPanel != null)
            _feedbackPanel.SetActive(false);
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CloseDisplay();
    }

    private void OpenDoor()
    {
        if (_doorOpened || doorToOpen == null) return;
        _doorOpened = true;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUnlockDoorSound();
        StartCoroutine(AnimateDoorOpen());
    }

    private System.Collections.IEnumerator AnimateDoorOpen()
    {
        float t = 0f;
        Quaternion start = doorToOpen.localRotation;
        while (t < openDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, openDuration));
            doorToOpen.localRotation = Quaternion.Slerp(start, _doorOpenRotation, p);
            yield return null;
        }
        doorToOpen.localRotation = _doorOpenRotation;
    }

    private void TriggerGameOver()
    {
        if (IsOpen)
            CloseDisplay();

        // Mirror PuzzleTableController.DelayedGameOver(): apply lethal damage → death overlay + respawn.
        FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null && !fpc.IsDead)
        {
            float lethalDamage = Mathf.Max(1f, fpc.CurrentHealth);
            fpc.ApplyDamage(lethalDamage);
        }
    }

    private void RefreshAttemptsUI()
    {
        if (useChapter3QuestionMode)
        {
            int target = Mathf.Max(1, chapter3QuestionsToAnswer);
            int current = Mathf.Min(target, _chapter3SolvedCount + 1);
            if (_attemptsText != null)
                _attemptsText.text = $"Question: {current}/{target}";

            if (_submitButton != null)
                _submitButton.interactable = true;
            return;
        }

        int remaining = maxAttempts - _attemptsUsed;

        if (_attemptsText != null)
            _attemptsText.text = $"Attempts: {remaining}/{maxAttempts}";

        if (_submitButton != null)
            _submitButton.interactable = (remaining > 0);
    }

    // ─────────────────────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsOpen) return;

        UpdateSelectedCellBlink();

        bool esc = Input.GetKeyDown(KeyCode.Escape)
                || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        if (esc) CloseDisplay();
    }

    private void UpdateSelectedCellBlink()
    {
        if (_selectedUnknownCell == null)
            return;

        float t = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
        _selectedUnknownCell.color = Color.Lerp(_selectedBlinkColorA, _selectedBlinkColorB, t);
    }

    private static void RecursivelyEnable(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);

        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            if (child != null)
                RecursivelyEnable(child.gameObject);
        }
    }

    /// <summary>
    /// Find all Q1–Q5 descendants, randomly enable ONE, disable the rest.
    /// </summary>
    private void RandomlySelectOneQuestionPanel()
    {
        if (_panelInstance == null) return;

        List<Transform> questionPanels = new List<Transform>();
        foreach (Transform t in _panelInstance.GetComponentsInChildren<Transform>(true))
        {
            string n = t.gameObject.name;
            if (n == "Q1" || n == "Q2" || n == "Q3" || n == "Q4" || n == "Q5")
                questionPanels.Add(t);
        }

        if (questionPanels.Count == 0)
        {
            Debug.LogWarning("[TruthTableDisplay] No Q1-Q5 panels found in prefab.");
            return;
        }

        int selectedIndex = UnityEngine.Random.Range(0, questionPanels.Count);
        Transform selectedPanel = questionPanels[selectedIndex];
        for (int i = 0; i < questionPanels.Count; i++)
        {
            questionPanels[i].gameObject.SetActive(i == selectedIndex);
            Debug.Log($"[TruthTableDisplay] {questionPanels[i].name}: {(i == selectedIndex ? "SHOWN" : "hidden")}");
        }

        BindUnknownCells(selectedPanel);
    }

    private void PrepareChapter3Round()
    {
        _chapter3RoundQuestions.Clear();

        if (_panelInstance == null)
            return;

        List<Transform> allQuestions = new List<Transform>();
        foreach (Transform t in _panelInstance.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (QuestionNameRegex.IsMatch(t.name))
                allQuestions.Add(t);
        }

        if (allQuestions.Count == 0)
        {
            Debug.LogWarning("[TruthTableDisplay] Chapter 3 mode: No Q1..Qn panels found.");
            return;
        }

        for (int i = allQuestions.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Transform tmp = allQuestions[i];
            allQuestions[i] = allQuestions[j];
            allQuestions[j] = tmp;
        }

        int need = Mathf.Clamp(chapter3QuestionsToAnswer, 1, allQuestions.Count);
        for (int i = 0; i < need; i++)
            _chapter3RoundQuestions.Add(allQuestions[i]);

        Debug.Log($"[TruthTableDisplay] Chapter3 question pool found: {allQuestions.Count}, selected: {_chapter3RoundQuestions.Count}.");
    }

    private void ShowChapter3QuestionAtCurrentIndex()
    {
        if (_panelInstance == null)
            return;

        // Keep exactly one question panel active to avoid layered/duplicate visuals.
        foreach (Transform t in _panelInstance.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && QuestionNameRegex.IsMatch(t.name))
                t.gameObject.SetActive(false);
        }

        if (_chapter3QuestionIndex >= 0 && _chapter3QuestionIndex < _chapter3RoundQuestions.Count)
        {
            Transform selected = _chapter3RoundQuestions[_chapter3QuestionIndex];
            if (selected != null)
                selected.gameObject.SetActive(true);
        }

        _activeQuestionPanel = (_chapter3QuestionIndex >= 0 && _chapter3QuestionIndex < _chapter3RoundQuestions.Count)
            ? _chapter3RoundQuestions[_chapter3QuestionIndex]
            : null;

        _chapter3SelectedAnswer = null;
        BindChapter3YesNoButtons(_activeQuestionPanel);
        RefreshAttemptsUI();
    }

    private void BindChapter3YesNoButtons(Transform questionPanel)
    {
        _chapter3YesButton = null;
        _chapter3NoButton = null;

        if (questionPanel == null)
            return;

        foreach (Button b in questionPanel.GetComponentsInChildren<Button>(true))
        {
            if (b == null) continue;
            string n = b.gameObject.name.Trim();

            if (n.Equals("Yes", System.StringComparison.OrdinalIgnoreCase))
                _chapter3YesButton = b;
            else if (n.Equals("No", System.StringComparison.OrdinalIgnoreCase))
                _chapter3NoButton = b;
        }

        if (_chapter3YesButton != null)
        {
            _chapter3YesButton.onClick.RemoveAllListeners();
            _chapter3YesButton.onClick.AddListener(() =>
            {
                _chapter3SelectedAnswer = true;
                SubmitChapter3Answer();
            });
        }

        if (_chapter3NoButton != null)
        {
            _chapter3NoButton.onClick.RemoveAllListeners();
            _chapter3NoButton.onClick.AddListener(() =>
            {
                _chapter3SelectedAnswer = false;
                SubmitChapter3Answer();
            });
        }
    }

    private void SubmitChapter3Answer()
    {
        if (_activeQuestionPanel == null)
            return;

        if (!_chapter3SelectedAnswer.HasValue)
        {
            ShowFeedback("SELECT YES/NO", new Color(1f, 0.8f, 0.2f, 1f), 1.0f, 46f);
            return;
        }

        if (!TryGetChapter3ExpectedAnswer(_activeQuestionPanel, out bool expectedAnswer))
        {
            if (!TryEvaluateChapter3Statement(_activeQuestionPanel, out expectedAnswer))
            {
                ShowFeedback("INVALID QUESTION", new Color(1f, 0.7f, 0.2f, 1f), 1.0f, 42f);
                return;
            }
        }

        bool isCorrect = _chapter3SelectedAnswer.Value == expectedAnswer;
        if (!isCorrect)
        {
            ShowFeedback("WRONG!", new Color(1f, 0.1f, 0.1f, 1f), 0.9f, 80f);
            TriggerGameOver();
            return;
        }

        _chapter3SolvedCount++;
        if (_chapter3SolvedCount >= Mathf.Max(1, chapter3QuestionsToAnswer))
        {
            _solved = true;
            ShowFeedback("CORRECT!", new Color(0.2f, 1f, 0.3f, 1f), 0.9f, 70f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCorrectAnswerSound();
            OpenDoor();
            StartCoroutine(CloseAfterDelay(0.35f));
            return;
        }

        _chapter3QuestionIndex = Mathf.Min(_chapter3QuestionIndex + 1, _chapter3RoundQuestions.Count - 1);
        ShowFeedback("CORRECT", new Color(0.2f, 1f, 0.3f, 1f), 0.45f, 62f);
        ShowChapter3QuestionAtCurrentIndex();
    }

    private bool TryGetChapter3ExpectedAnswer(Transform questionPanel, out bool expectedAnswer)
    {
        expectedAnswer = false;
        if (questionPanel == null)
            return false;

        Match m = Regex.Match(questionPanel.name, @"^Q(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success)
            return false;

        if (!int.TryParse(m.Groups[1].Value, out int qIndex))
            return false;

        if (qIndex < 1 || qIndex > Chapter3AnswerKey.Length)
            return false;

        expectedAnswer = Chapter3AnswerKey[qIndex - 1];
        return true;
    }

    private bool TryEvaluateChapter3Statement(Transform questionPanel, out bool statementTrue)
    {
        statementTrue = false;
        if (questionPanel == null)
            return false;

        TextMeshProUGUI questionText = null;
        foreach (TextMeshProUGUI tmp in questionPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
            string t = tmp.text.Trim();
            if (t.StartsWith("Is the output of", System.StringComparison.OrdinalIgnoreCase))
            {
                questionText = tmp;
                break;
            }
        }

        if (questionText == null)
            return false;

        Match m = Chapter3QuestionRegex.Match(questionText.text.Trim());
        if (!m.Success)
            return false;

        string gate = m.Groups[1].Value.Trim().ToUpperInvariant();
        int a = int.Parse(m.Groups[2].Value);
        int b = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        int expected = int.Parse(m.Groups[4].Value);

        int output;
        switch (gate)
        {
            case "NOT":
                output = (a == 0) ? 1 : 0;
                break;
            case "AND":
                output = (a == 1 && b == 1) ? 1 : 0;
                break;
            case "OR":
                output = (a == 1 || b == 1) ? 1 : 0;
                break;
            case "NAND":
                output = (a == 1 && b == 1) ? 0 : 1;
                break;
            case "NOR":
                output = (a == 1 || b == 1) ? 0 : 1;
                break;
            case "XOR":
                output = (a != b) ? 1 : 0;
                break;
            default:
                return false;
        }

        statementTrue = (output == expected);
        return true;
    }

    private static GameObject FindChapter3QuestionSource()
    {
        GameObject named = GameObject.Find("Chapter3");
        if (named != null && HasChapter3QuestionContent(named.transform))
            return named;

        named = GameObject.Find("Chapter 3");
        if (named != null && HasChapter3QuestionContent(named.transform))
            return named;

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject best = null;
        int bestCount = 0;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;

            int questionCount = CountQuestionPanels(t);
            if (questionCount > bestCount && questionCount >= 5)
            {
                bestCount = questionCount;
                best = t.gameObject;
            }
        }

        if (best != null)
            Debug.Log($"[TruthTableDisplay] Auto-selected Chapter3 source '{best.name}' with {bestCount} Q panels.");

        return best;
    }

    private static bool HasChapter3QuestionContent(Transform root)
    {
        if (root == null)
            return false;

        return CountQuestionPanels(root) >= 5;
    }

    private static int CountQuestionPanels(Transform root)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && QuestionNameRegex.IsMatch(t.name))
                count++;
        }
        return count;
    }
}
