using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Controls the chapter → level selection flow using the LevelSelection2.0 prefab.
/// Each chapter panel shows its levels with locked/unlocked states.
/// Attach to the LevelSelection2.0 prefab instance in the scene.
/// </summary>
public class LevelSelectionController : MonoBehaviour
{
    public static LevelSelectionController Instance { get; private set; }
    private const bool DevUnlockAllLevels = true;

    private TextMeshProUGUI globalChapterTitle;
    private TextMeshProUGUI levelLockWarningText;
    private Coroutine hideWarningCoroutine;

    [Header("Lock Visuals")]
    [Tooltip("Assign your medieval lock icon sprite here.")]
    [SerializeField] private Sprite lockedLevelIcon;
    [SerializeField] private Vector2 lockIconSize = new Vector2(64f, 64f);

    private static readonly string[] lockSpriteCandidatePaths =
    {
        "Assets/Models/lock.png",
        "Assets/Models/Lock.png",
        "Assets/Models/lock.PNG",
        "Assets/Models/Lock.PNG",
        "Assets/Models/lock.avif",
        "Assets/Models/Lock.avif"
    };

    [Header("Chapter Panels (auto-found from children)")]
    public GameObject chapter1Panel;
    public GameObject chapter2Panel;
    public GameObject chapter3Panel;
    public GameObject chapter4Panel;

    // Chapter → Level mapping
    // Chapter 1: Levels 1-4, Chapter 2: Levels 5-8, Chapter 3: Levels 9-12, Chapter 4: Levels 13-16
    private static readonly int[] chapterStartLevel = { 1, 5, 9, 13 };
    private static readonly int[] chapterLevelCount = { 4, 4, 4, 4 };

    // Temporary product request: keep Chapter 2 Level 5 and 6 locked.
    private static bool IsForceLockedLevel(int chapterNumber, int levelNumber)
    {
        if (DevUnlockAllLevels) return false;
        return chapterNumber == 2 && (levelNumber == 5 || levelNumber == 6);
    }

    void Awake()
    {
        Instance = this;
        EnsureLockedIconAssigned();
        AutoFindChapterPanels();
        CreateBackButton();
    }

    private void OnValidate()
    {
        EnsureLockedIconAssigned();
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
        // Chapter 3 and 4 have single unified maps — skip level panel and load directly.
        if (chapterNumber == 3)
        {
            Debug.Log("[LevelSelection] Chapter 3: loading Chapter3 scene directly.");
            if (LevelManager.Instance != null)
                LevelManager.Instance.LoadChapterScene("Chapter3");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("Chapter3");
            return;
        }

        if (chapterNumber == 4)
        {
            Debug.Log("[LevelSelection] Chapter 4: loading Chapter4 scene directly.");
            if (LevelManager.Instance != null)
                LevelManager.Instance.LoadChapterScene("Chapter4");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("Chapter4");
            return;
        }

        // Activate this panel
        gameObject.SetActive(true);

        // Hide all chapter sub-panels
        HideAllChapters();

        // Show the requested chapter
        GameObject chapterPanel = GetChapterPanel(chapterNumber);
        if (chapterPanel != null)
        {
            chapterPanel.SetActive(true);
            EnsureGlobalChapterTitle(chapterNumber);
            SetupChapterLevels(chapterNumber, chapterPanel);
            Debug.Log($"[LevelSelection] Showing Chapter {chapterNumber}");
        }
        else
        {
            Debug.LogWarning($"[LevelSelection] Chapter {chapterNumber} panel not found!");
        }
    }

    /// <summary>
    /// Sets up all level buttons within a chapter panel.
    /// Wires onClick, shows locked/unlocked/completed states.
    /// </summary>
    private void SetupChapterLevels(int chapterNumber, GameObject chapterPanel)
    {
        int chapterIndex = chapterNumber - 1;
        int startLevel = chapterStartLevel[chapterIndex];
        int levelCount = chapterLevelCount[chapterIndex];

        // Use persisted progression so reopening the panel reflects saved state.
        int lastCompletedLevel = 0;
        var player = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;
        if (player != null)
        {
            lastCompletedLevel = Mathf.Max(0, player.lastCompletedLevel);
        }

        int nextPlayableLevel = DevUnlockAllLevels ? int.MaxValue : Mathf.Max(1, lastCompletedLevel + 1);

        HideChapterLocalTitles(chapterPanel);

        // Setup each level button
        for (int i = 0; i < levelCount; i++)
        {
            int levelNumber = startLevel + i;
            Transform problemTransform = ResolveLevelButtonTransform(chapterPanel.transform, i + 1, levelNumber);

            if (problemTransform == null)
            {
                Debug.LogWarning($"[LevelSelection] Level node not found for level {levelNumber} in Chapter {chapterNumber}");
                continue;
            }

            // Get or add Button component
            Button btn = problemTransform.GetComponent<Button>();
            if (btn == null)
                btn = problemTransform.gameObject.AddComponent<Button>();

            // Get the text label
            TextMeshProUGUI label = problemTransform.GetComponentInChildren<TextMeshProUGUI>();

            // Determine level state
            bool isForceLocked = IsForceLockedLevel(chapterNumber, levelNumber);
            bool isUnlocked = !isForceLocked && levelNumber <= nextPlayableLevel;
            bool isCompleted = levelNumber <= lastCompletedLevel;

            // Update visual state.
            // Keep prefab-authored look for both unlocked and locked levels (no gray tint).
            Image problemImage = problemTransform.GetComponent<Image>();
            if (problemImage != null)
                problemImage.color = Color.white;

            // Update label
            if (label != null)
            {
                // Keep clean label in play mode while preserving prefab styling.
                label.text = $"LEVEL {levelNumber}";
            }

            UpdateLockIcon(problemTransform, !isUnlocked);

            // Wire button
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;

            // Keep locked buttons from turning gray when non-interactable.
            ColorBlock colors = btn.colors;
            colors.disabledColor = Color.white;
            btn.colors = colors;

            int capturedLevel = levelNumber; // Capture for closure
            bool capturedIsForceLocked = isForceLocked;
            btn.onClick.AddListener(() => OnLevelButtonPressed(capturedLevel, nextPlayableLevel, capturedIsForceLocked));

            // Set button color transitions
            if (btn.targetGraphic == null && problemImage != null)
                btn.targetGraphic = problemImage;

            Debug.Log($"[LevelSelection] {problemTransform.name} → Level {levelNumber} (unlocked={isUnlocked}, completed={isCompleted})");
        }
    }

    private void OnLevelButtonPressed(int levelNumber, int nextPlayableLevel, bool isForceLocked)
    {
        if (isForceLocked)
        {
            ShowLockedLevelWarning(levelNumber, true);
            return;
        }

        if (levelNumber <= nextPlayableLevel)
        {
            OnLevelClicked(levelNumber);
            return;
        }

        ShowLockedLevelWarning(levelNumber, false);
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
        HideLockedLevelWarning();

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

    private Transform ResolveLevelButtonTransform(Transform chapterRoot, int problemIndex, int levelNumber)
    {
        if (chapterRoot == null) return null;

        string[] candidates =
        {
            $"Problem{problemIndex}",
            $"Problem {problemIndex}",
            $"Level{levelNumber}Button",
            $"Level {levelNumber}Button",
            $"Level{levelNumber}Button ",
            $"Level{levelNumber}",
            $"Level {levelNumber}"
        };

        foreach (string candidate in candidates)
        {
            Transform direct = chapterRoot.Find(candidate);
            if (direct != null) return direct;
        }

        foreach (string candidate in candidates)
        {
            Transform deep = FindDeepChildRecursive(chapterRoot, candidate);
            if (deep != null) return deep;
        }

        return null;
    }

    private void EnsureGlobalChapterTitle(int chapterNumber)
    {
        if (globalChapterTitle == null)
        {
            Transform existing = transform.Find("ChapterHeader");
            if (existing != null)
                globalChapterTitle = existing.GetComponent<TextMeshProUGUI>();

            if (globalChapterTitle == null)
            {
                GameObject titleGO = new GameObject("ChapterHeader", typeof(RectTransform));
                titleGO.transform.SetParent(transform, false);

                RectTransform rt = titleGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -22f);
                rt.sizeDelta = new Vector2(520f, 90f);

                globalChapterTitle = titleGO.AddComponent<TextMeshProUGUI>();
                globalChapterTitle.alignment = TextAlignmentOptions.Center;
                globalChapterTitle.fontStyle = FontStyles.Bold;
                globalChapterTitle.enableAutoSizing = true;
                globalChapterTitle.fontSizeMin = 24;
                globalChapterTitle.fontSizeMax = 64;

                // Reuse any existing TMP font from this panel for visual consistency.
                TextMeshProUGUI anyLabel = GetComponentInChildren<TextMeshProUGUI>(true);
                if (anyLabel != null)
                    globalChapterTitle.font = anyLabel.font;
            }
        }

        if (globalChapterTitle == null) return;

        globalChapterTitle.gameObject.SetActive(true);
        globalChapterTitle.text = $"CHAPTER {chapterNumber}";
        globalChapterTitle.color = new Color(0.85f, 0.82f, 0.6f, 1f);
    }

    private void ShowLockedLevelWarning(int levelNumber, bool isForceLocked)
    {
        EnsureLockedLevelWarningText();
        if (levelLockWarningText == null) return;

        if (isForceLocked)
        {
            levelLockWarningText.text = $"Level {levelNumber} is currently locked.";
        }
        else
        {
            int requiredLevel = Mathf.Max(1, levelNumber - 1);
            levelLockWarningText.text = $"Finish Level {requiredLevel} first.";
        }
        levelLockWarningText.gameObject.SetActive(true);

        if (hideWarningCoroutine != null)
            StopCoroutine(hideWarningCoroutine);

        hideWarningCoroutine = StartCoroutine(HideLockedLevelWarningAfterDelay(2f));
    }

    private IEnumerator HideLockedLevelWarningAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideLockedLevelWarning();
        hideWarningCoroutine = null;
    }

    private void HideLockedLevelWarning()
    {
        if (levelLockWarningText != null)
            levelLockWarningText.gameObject.SetActive(false);
    }

    private void EnsureLockedLevelWarningText()
    {
        if (levelLockWarningText != null) return;

        Transform existing = transform.Find("LockedLevelWarning");
        if (existing != null)
            levelLockWarningText = existing.GetComponent<TextMeshProUGUI>();

        if (levelLockWarningText != null) return;

        GameObject warningGO = new GameObject("LockedLevelWarning", typeof(RectTransform));
        warningGO.transform.SetParent(transform, false);

        RectTransform rt = warningGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(520f, 56f);

        levelLockWarningText = warningGO.AddComponent<TextMeshProUGUI>();
        levelLockWarningText.alignment = TextAlignmentOptions.Center;
        levelLockWarningText.enableAutoSizing = true;
        levelLockWarningText.fontSizeMin = 16;
        levelLockWarningText.fontSizeMax = 30;
        levelLockWarningText.color = new Color(0.95f, 0.78f, 0.5f, 1f);
        levelLockWarningText.fontStyle = FontStyles.Bold;
        levelLockWarningText.text = string.Empty;
        levelLockWarningText.gameObject.SetActive(false);

        TextMeshProUGUI anyLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        if (anyLabel != null)
            levelLockWarningText.font = anyLabel.font;
    }

    private static void HideChapterLocalTitles(GameObject chapterPanel)
    {
        if (chapterPanel == null) return;

        // Hide legacy per-chapter title texts to avoid faint duplicate title behind the global header.
        foreach (Transform child in chapterPanel.transform)
        {
            if (child == null) continue;
            TextMeshProUGUI localTitle = child.GetComponent<TextMeshProUGUI>();
            if (localTitle != null)
                localTitle.gameObject.SetActive(false);
        }
    }

    private void UpdateLockIcon(Transform problemTransform, bool showLocked)
    {
        if (problemTransform == null) return;

        Transform iconRoot = problemTransform.Find("LockIcon");
        Transform fallbackRoot = problemTransform.Find("LockFallback");
        Image iconImage;

        if (iconRoot == null)
        {
            GameObject iconGO = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(problemTransform, false);
            iconRoot = iconGO.transform;

            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = lockIconSize;

            iconImage = iconGO.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }
        else
        {
            iconImage = iconRoot.GetComponent<Image>();
            if (iconImage == null)
                iconImage = iconRoot.gameObject.AddComponent<Image>();
        }

        iconImage.sprite = lockedLevelIcon;
        bool useSpriteIcon = showLocked && lockedLevelIcon != null;
        iconImage.enabled = useSpriteIcon;

        // If no sprite icon is available, render a lock shape using UI Images.
        if (fallbackRoot == null)
        {
            GameObject rootGO = new GameObject("LockFallback", typeof(RectTransform));
            rootGO.transform.SetParent(problemTransform, false);
            fallbackRoot = rootGO.transform;

            RectTransform rootRT = rootGO.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = Vector2.zero;
            rootRT.sizeDelta = lockIconSize;

            // Shackle
            GameObject shackleGO = new GameObject("Shackle", typeof(RectTransform), typeof(Image));
            shackleGO.transform.SetParent(rootGO.transform, false);
            RectTransform shackleRT = shackleGO.GetComponent<RectTransform>();
            shackleRT.anchorMin = new Vector2(0.5f, 0.5f);
            shackleRT.anchorMax = new Vector2(0.5f, 0.5f);
            shackleRT.pivot = new Vector2(0.5f, 0.5f);
            shackleRT.anchoredPosition = new Vector2(0f, 12f);
            shackleRT.sizeDelta = new Vector2(24f, 22f);

            Image shackleImage = shackleGO.GetComponent<Image>();
            shackleImage.type = Image.Type.Simple;
            shackleImage.color = new Color(0.18f, 0.12f, 0.07f, 0.95f);

            // Body
            GameObject bodyGO = new GameObject("Body", typeof(RectTransform), typeof(Image));
            bodyGO.transform.SetParent(rootGO.transform, false);
            RectTransform bodyRT = bodyGO.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRT.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRT.pivot = new Vector2(0.5f, 0.5f);
            bodyRT.anchoredPosition = new Vector2(0f, -4f);
            bodyRT.sizeDelta = new Vector2(34f, 24f);

            Image bodyImage = bodyGO.GetComponent<Image>();
            bodyImage.type = Image.Type.Simple;
            bodyImage.color = new Color(0.4f, 0.25f, 0.11f, 0.95f);

            // Keyhole
            GameObject keyholeGO = new GameObject("Keyhole", typeof(RectTransform), typeof(Image));
            keyholeGO.transform.SetParent(bodyGO.transform, false);
            RectTransform keyholeRT = keyholeGO.GetComponent<RectTransform>();
            keyholeRT.anchorMin = new Vector2(0.5f, 0.5f);
            keyholeRT.anchorMax = new Vector2(0.5f, 0.5f);
            keyholeRT.pivot = new Vector2(0.5f, 0.5f);
            keyholeRT.anchoredPosition = new Vector2(0f, -1f);
            keyholeRT.sizeDelta = new Vector2(5f, 10f);

            Image keyholeImage = keyholeGO.GetComponent<Image>();
            keyholeImage.type = Image.Type.Simple;
            keyholeImage.color = new Color(0.08f, 0.05f, 0.03f, 1f);
        }

        bool useGlyphFallback = showLocked && lockedLevelIcon == null;
        fallbackRoot.gameObject.SetActive(useGlyphFallback);

        // Only show image object when sprite icon is available.
        iconRoot.gameObject.SetActive(useSpriteIcon);
    }


    private void EnsureLockedIconAssigned()
    {
        if (lockedLevelIcon != null) return;

#if UNITY_EDITOR
        foreach (string candidatePath in lockSpriteCandidatePaths)
        {
            Sprite candidateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(candidatePath);
            if (candidateSprite != null)
            {
                lockedLevelIcon = candidateSprite;
                EditorUtility.SetDirty(this);
                break;
            }
        }
#endif
    }
}
