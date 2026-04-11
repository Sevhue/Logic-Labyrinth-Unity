using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;
using System.Collections.Generic;

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
    [Tooltip("Assign the Level7 prefab from Assets/Prefabs/Table/Table/Level7/Level7.prefab")]
    public GameObject displayPanelPrefab;

    [Header("Attempts")]
    public int maxAttempts = 3;

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

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────

    public void OpenDisplay()
    {
        if (IsOpen) return;

        if (displayPanelPrefab == null)
        {
            Debug.LogError("[TruthTableDisplay] displayPanelPrefab is not assigned!");
            return;
        }

        if (_canvasGO == null)
            BuildUI();

        _canvasGO.SetActive(true);
        RandomlySelectOneQuestionPanel();
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
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD
    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Add Canvas directly to Level7 — this is the only approach that renders.
        // With the clean hierarchy (Level7 -> Background + Q1..Q5), no overlap occurs.
        _canvasGO = Instantiate(displayPanelPrefab);
        _panelInstance = _canvasGO;
        RecursivelyEnable(_canvasGO);

        Canvas canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGO.AddComponent<GraphicRaycaster>();

        BuildControlsUI();
        BuildBinaryInputUI();
        BuildTutorialUI();
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
        closeRect.anchorMin = new Vector2(0.93f, 0.91f);
        closeRect.anchorMax = new Vector2(0.99f, 0.98f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;

        Image closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseDisplay);

        AddLabel(closeGO, "X", 18);
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
        if (_attemptsUsed >= maxAttempts) return;

        _attemptsUsed++;
        CloseDisplay();

        if (_attemptsUsed >= maxAttempts)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
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

        int selectedIndex = Random.Range(0, questionPanels.Count);
        Transform selectedPanel = questionPanels[selectedIndex];
        for (int i = 0; i < questionPanels.Count; i++)
        {
            questionPanels[i].gameObject.SetActive(i == selectedIndex);
            Debug.Log($"[TruthTableDisplay] {questionPanels[i].name}: {(i == selectedIndex ? "SHOWN" : "hidden")}");
        }

        BindUnknownCells(selectedPanel);
    }
}
