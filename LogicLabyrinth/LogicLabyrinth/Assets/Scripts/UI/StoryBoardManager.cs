using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class StoryBoardManager : MonoBehaviour
{
    [Header("Chapter Buttons")]
    public Button chapter1Button;
    public Button chapter2Button;
    public Button chapter3Button;
    public Button chapter4Button;

    [Header("Locked Overlays")]
    public GameObject chapter2LockedOverlay;
    public GameObject chapter3LockedOverlay;
    public GameObject chapter4LockedOverlay;

    [Header("Warning Text")]
    public TextMeshProUGUI warningText;

    [Header("Back Button")]
    public Button backButton;

    // Updated chapter → level mapping:
    // Chapter 1 = Levels 1-4, Chapter 2 = Levels 5-8,
    // Chapter 3 = Levels 9-12, Chapter 4 = Levels 13-15
    private static readonly int[] chapterStartLevel = { 1, 5, 9, 13 };
    private static readonly int[] chapterLevelCount = { 4, 4, 4, 3 };

    private Coroutine hideWarningCoroutine;

    void OnEnable()
    {
        RefreshChapterStates();
        HideWarning();
    }

    void Start()
    {
        // Wire button listeners
        if (chapter1Button != null)
            chapter1Button.onClick.AddListener(OnChapter1Clicked);
        if (chapter2Button != null)
            chapter2Button.onClick.AddListener(OnChapter2Clicked);
        if (chapter3Button != null)
            chapter3Button.onClick.AddListener(OnChapter3Clicked);
        if (chapter4Button != null)
            chapter4Button.onClick.AddListener(OnChapter4Clicked);
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        RefreshChapterStates();
        HideWarning();
    }

    private void RefreshChapterStates()
    {
        int unlockedChapter = GetUnlockedChapter();

        // Chapter 1 is always unlocked
        SetChapterLocked(2, unlockedChapter < 2);
        SetChapterLocked(3, unlockedChapter < 3);
        SetChapterLocked(4, unlockedChapter < 4);
    }

    private int GetUnlockedChapter()
    {
        // DEV MODE: All chapters unlocked for testing
        return 4;
    }

    private void SetChapterLocked(int chapter, bool locked)
    {
        switch (chapter)
        {
            case 2:
                if (chapter2LockedOverlay != null) chapter2LockedOverlay.SetActive(locked);
                break;
            case 3:
                if (chapter3LockedOverlay != null) chapter3LockedOverlay.SetActive(locked);
                break;
            case 4:
                if (chapter4LockedOverlay != null) chapter4LockedOverlay.SetActive(locked);
                break;
        }
    }

    private bool IsChapterUnlocked(int chapter)
    {
        return GetUnlockedChapter() >= chapter;
    }

    // --- Button Handlers ---
    // Now opens the level selection panel for the chapter instead of directly loading a level.

    public void OnChapter1Clicked()
    {
        Debug.Log("Chapter 1 selected — showing level selection");
        OpenChapterLevelSelection(1);
    }

    public void OnChapter2Clicked()
    {
        if (IsChapterUnlocked(2))
        {
            Debug.Log("Chapter 2 selected — showing level selection");
            OpenChapterLevelSelection(2);
        }
        else
        {
            ShowLockedMessage("Finish Chapter 1 first!");
        }
    }

    public void OnChapter3Clicked()
    {
        if (IsChapterUnlocked(3))
        {
            Debug.Log("Chapter 3 selected — showing level selection");
            OpenChapterLevelSelection(3);
        }
        else
        {
            ShowLockedMessage("Finish Chapter 2 first!");
        }
    }

    public void OnChapter4Clicked()
    {
        if (IsChapterUnlocked(4))
        {
            Debug.Log("Chapter 4 selected — showing level selection");
            OpenChapterLevelSelection(4);
        }
        else
        {
            ShowLockedMessage("Finish Chapter 3 first!");
        }
    }

    /// <summary>
    /// Opens the LevelSelection2.0 panel for a specific chapter.
    /// </summary>
    private void OpenChapterLevelSelection(int chapterNumber)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLevelSelectionForChapter(chapterNumber);
        }
        else
        {
            Debug.LogWarning("[StoryBoard] UIManager not found!");
        }
    }

    private void ShowLockedMessage(string message)
    {
        Debug.Log(message);

        if (warningText != null)
        {
            warningText.text = message;
            warningText.color = Color.red;
            warningText.gameObject.SetActive(true);

            // Auto-hide after 2 seconds
            if (hideWarningCoroutine != null)
                StopCoroutine(hideWarningCoroutine);
            hideWarningCoroutine = StartCoroutine(HideWarningAfterDelay(2f));
        }
    }

    private IEnumerator HideWarningAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideWarning();
    }

    private void HideWarning()
    {
        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }
    }

    public void OnBackClicked()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainMenu();
        }
    }
}
