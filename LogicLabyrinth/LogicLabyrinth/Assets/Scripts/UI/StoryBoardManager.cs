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

    // Levels per chapter: Chapter 1 = Levels 1-5, Chapter 2 = 6-10, etc.
    private const int LEVELS_PER_CHAPTER = 5;
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

        // Chapter 1 is always unlocked for a new game
        SetChapterLocked(2, unlockedChapter < 2);
        SetChapterLocked(3, unlockedChapter < 3);
        SetChapterLocked(4, unlockedChapter < 4);
    }

    private int GetUnlockedChapter()
    {
        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player == null)
            return 1; // Default: only chapter 1

        // Determine which chapter the player has unlocked based on their progress
        // Chapter 1 = Levels 1-5, Chapter 2 = Levels 6-10, etc.
        int unlockedLevels = player.unlockedLevels;

        if (unlockedLevels > LEVELS_PER_CHAPTER * 3) return 4;
        if (unlockedLevels > LEVELS_PER_CHAPTER * 2) return 3;
        if (unlockedLevels > LEVELS_PER_CHAPTER * 1) return 2;
        return 1;
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

    public void OnChapter1Clicked()
    {
        Debug.Log("Chapter 1 selected - Loading Level 1");

        // Reset progress for new game
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.StartNewGame();
        }
        else
        {
            Debug.LogWarning("LevelManager not found! Loading Level1 scene directly.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Level1");
        }
    }

    public void OnChapter2Clicked()
    {
        if (IsChapterUnlocked(2))
        {
            Debug.Log("Chapter 2 selected - Loading Level 6");
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadLevelFromSelection(LEVELS_PER_CHAPTER + 1);
            }
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
            Debug.Log("Chapter 3 selected - Loading Level 11");
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadLevelFromSelection(LEVELS_PER_CHAPTER * 2 + 1);
            }
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
            Debug.Log("Chapter 4 selected - Loading Level 16");
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadLevelFromSelection(LEVELS_PER_CHAPTER * 3 + 1);
            }
        }
        else
        {
            ShowLockedMessage("Finish Chapter 3 first!");
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
