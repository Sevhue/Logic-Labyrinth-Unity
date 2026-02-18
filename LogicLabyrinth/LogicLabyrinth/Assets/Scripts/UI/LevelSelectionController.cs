using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the chapter → level selection flow using the LevelSelection2.0 prefab.
/// Each chapter panel shows its levels (Problem1, Problem2, etc.) with locked/unlocked states.
/// Attach to the LevelSelection2.0 prefab instance in the scene.
/// </summary>
public class LevelSelectionController : MonoBehaviour
{
    public static LevelSelectionController Instance { get; private set; }

    [Header("Chapter Panels (auto-found from children)")]
    public GameObject chapter1Panel;
    public GameObject chapter2Panel;
    public GameObject chapter3Panel;
    public GameObject chapter4Panel;

    // Chapter → Level mapping
    // Chapter 1: Levels 1-4, Chapter 2: Levels 5-8, Chapter 3: Levels 9-12, Chapter 4: Levels 13-16
    private static readonly int[] chapterStartLevel = { 1, 5, 9, 13 };
    private static readonly int[] chapterLevelCount = { 4, 4, 4, 4 };

    // Colors
    private readonly Color unlockedColor = new Color(0.85f, 0.75f, 0.55f, 1f); // Gold/parchment
    private readonly Color lockedColor = new Color(0.4f, 0.35f, 0.3f, 0.7f);   // Dim grey
    private readonly Color completedColor = new Color(0.4f, 0.7f, 0.3f, 1f);   // Green

    void Awake()
    {
        Instance = this;
        AutoFindChapterPanels();
        CreateBackButton();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Auto-find Chapter1, Chapter2, etc. from children if not assigned.
    /// </summary>
    private void AutoFindChapterPanels()
    {
        if (chapter1Panel == null) chapter1Panel = FindDeepChild("Chapter1");
        if (chapter2Panel == null) chapter2Panel = FindDeepChild("Chapter2");
        if (chapter3Panel == null) chapter3Panel = FindDeepChild("Chapter3");
        if (chapter4Panel == null) chapter4Panel = FindDeepChild("Chapter4");
    }

    /// <summary>
    /// Shows the level selection for a specific chapter (1-based).
    /// Called by StoryBoardManager when a chapter button is clicked.
    /// </summary>
    public void ShowChapter(int chapterNumber)
    {
        // Activate this panel
        gameObject.SetActive(true);

        // Hide all chapter sub-panels
        HideAllChapters();

        // Show the requested chapter
        GameObject chapterPanel = GetChapterPanel(chapterNumber);
        if (chapterPanel != null)
        {
            chapterPanel.SetActive(true);
            SetupChapterLevels(chapterNumber, chapterPanel);
            Debug.Log($"[LevelSelection] Showing Chapter {chapterNumber}");
        }
        else
        {
            Debug.LogWarning($"[LevelSelection] Chapter {chapterNumber} panel not found!");
        }
    }

    /// <summary>
    /// Sets up all level buttons (Problem1, Problem2, etc.) within a chapter panel.
    /// Wires onClick, shows locked/unlocked/completed states.
    /// </summary>
    private void SetupChapterLevels(int chapterNumber, GameObject chapterPanel)
    {
        int chapterIndex = chapterNumber - 1;
        int startLevel = chapterStartLevel[chapterIndex];
        int levelCount = chapterLevelCount[chapterIndex];

        // DEV MODE: All levels unlocked for testing
        int unlockedLevels = 99;
        int lastCompletedLevel = 0;

        // Update the chapter title text (first direct child Text (TMP))
        Transform titleTransform = chapterPanel.transform.Find("Text (TMP)");
        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = $"Chapter {chapterNumber}";
                titleText.color = new Color(0.25f, 0.2f, 0.12f, 1f); // Dark brown for parchment
            }
        }

        // Setup each Problem button
        for (int i = 0; i < levelCount; i++)
        {
            int levelNumber = startLevel + i;
            string problemName = $"Problem{i + 1}";
            Transform problemTransform = chapterPanel.transform.Find(problemName);

            if (problemTransform == null)
            {
                Debug.LogWarning($"[LevelSelection] {problemName} not found in Chapter {chapterNumber}");
                continue;
            }

            // Get or add Button component
            Button btn = problemTransform.GetComponent<Button>();
            if (btn == null)
                btn = problemTransform.gameObject.AddComponent<Button>();

            // Get the text label
            TextMeshProUGUI label = problemTransform.GetComponentInChildren<TextMeshProUGUI>();

            // Determine level state
            bool isUnlocked = levelNumber <= unlockedLevels;
            bool isCompleted = levelNumber <= lastCompletedLevel;

            // Update visual state
            Image problemImage = problemTransform.GetComponent<Image>();
            if (problemImage != null)
            {
                if (isCompleted)
                    problemImage.color = completedColor;
                else if (isUnlocked)
                    problemImage.color = unlockedColor;
                else
                    problemImage.color = lockedColor;
            }

            // Update label
            if (label != null)
            {
                if (isCompleted)
                    label.text = $"Level {levelNumber} ✓";
                else if (isUnlocked)
                    label.text = $"Level {levelNumber}";
                else
                    label.text = $"Level {levelNumber} 🔒";

                // Dark text for parchment background
                label.color = isUnlocked ? new Color(0.2f, 0.15f, 0.1f, 1f) : new Color(0.5f, 0.45f, 0.4f, 0.7f);
            }

            // Wire button
            btn.onClick.RemoveAllListeners();
            btn.interactable = isUnlocked;

            if (isUnlocked)
            {
                int capturedLevel = levelNumber; // Capture for closure
                btn.onClick.AddListener(() => OnLevelClicked(capturedLevel));
            }

            // Set button color transitions
            if (btn.targetGraphic == null && problemImage != null)
                btn.targetGraphic = problemImage;

            Debug.Log($"[LevelSelection] {problemName} → Level {levelNumber} (unlocked={isUnlocked}, completed={isCompleted})");
        }
    }

    /// <summary>
    /// Called when a level button is clicked.
    /// </summary>
    private void OnLevelClicked(int levelNumber)
    {
        Debug.Log($"[LevelSelection] Level {levelNumber} selected!");

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadLevelFromSelection(levelNumber);
        }
        else
        {
            Debug.LogWarning("[LevelSelection] LevelManager not found! Loading scene directly.");
            UnityEngine.SceneManagement.SceneManager.LoadScene($"Level{levelNumber}");
        }
    }

    /// <summary>
    /// Goes back to the StoryBoardPanel (chapter selection).
    /// </summary>
    public void GoBack()
    {
        gameObject.SetActive(false);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowStoryBoardPanel();
        }
    }

    // ─────────── Back Button (created at runtime) ───────────

    private void CreateBackButton()
    {
        // Check if a back button already exists
        Transform existing = FindDeepChildRecursive(transform, "BackButton");
        if (existing != null) return;

        // Create back button GO — positioned inside the image, bottom-left area
        GameObject backBtnGO = new GameObject("BackButton", typeof(RectTransform));
        backBtnGO.transform.SetParent(transform, false);

        RectTransform rt = backBtnGO.GetComponent<RectTransform>();
        // Anchor to bottom-left but with enough padding to be inside the scroll image
        rt.anchorMin = new Vector2(0.02f, 0.05f);
        rt.anchorMax = new Vector2(0.12f, 0.12f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Background image — semi-transparent dark for contrast on the image
        Image btnImg = backBtnGO.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.1f, 0.07f, 0.85f); // Dark brown overlay

        // Button component
        Button btn = backBtnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(GoBack);

        // Hover/press colors
        var colors = btn.colors;
        colors.normalColor = new Color(0.15f, 0.1f, 0.07f, 0.85f);
        colors.highlightedColor = new Color(0.3f, 0.22f, 0.15f, 0.95f);
        colors.pressedColor = new Color(0.1f, 0.07f, 0.04f, 1f);
        colors.selectedColor = new Color(0.15f, 0.1f, 0.07f, 0.85f);
        btn.colors = colors;

        // Gold outline for visibility
        Outline outline = backBtnGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.6f, 0.35f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Text child
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(backBtnGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "← BACK";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.82f, 0.65f, 1f); // Warm gold text
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = 20;

        Debug.Log("[LevelSelection] Created Back button (inside image area)");
    }

    // ─────────── Helpers ───────────

    private void HideAllChapters()
    {
        if (chapter1Panel != null) chapter1Panel.SetActive(false);
        if (chapter2Panel != null) chapter2Panel.SetActive(false);
        if (chapter3Panel != null) chapter3Panel.SetActive(false);
        if (chapter4Panel != null) chapter4Panel.SetActive(false);
    }

    private GameObject GetChapterPanel(int chapter)
    {
        switch (chapter)
        {
            case 1: return chapter1Panel;
            case 2: return chapter2Panel;
            case 3: return chapter3Panel;
            case 4: return chapter4Panel;
            default: return null;
        }
    }

    private GameObject FindDeepChild(string childName)
    {
        Transform found = FindDeepChildRecursive(transform, childName);
        return found != null ? found.gameObject : null;
    }

    private Transform FindDeepChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
