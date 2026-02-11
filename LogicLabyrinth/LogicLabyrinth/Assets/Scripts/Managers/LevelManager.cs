using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("UI Prefab")]
    public GameObject uiManagerPrefab;

    private int currentLevel = 1;
    private bool isPuzzleCompleted = false;
    private bool isLoadingGame = false;


    public PuzzleVariant currentLevelPuzzle;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);


            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDisable()
    {

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        isPuzzleCompleted = false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + scene.name + ", isLoadingGame: " + isLoadingGame);


        if (UIManager.Instance == null)
        {
            Debug.Log("UIManager.Instance is null - attempting to create/find it");


            UIManager.Instance = FindAnyObjectByType<UIManager>();


            if (UIManager.Instance == null && uiManagerPrefab != null)
            {
                Debug.Log("Creating UIManager from prefab");
                Instantiate(uiManagerPrefab);
            }


            if (UIManager.Instance == null)
            {
                UIManager.Instance = FindAnyObjectByType<UIManager>();
            }

            if (UIManager.Instance != null)
            {
                Debug.Log("UIManager found/created successfully");
            }
            else
            {
                Debug.LogError("Failed to find or create UIManager!");
            }
        }

        if (isLoadingGame)
        {

            if (UIManager.Instance != null)
            {
                Debug.Log("Calling ShowGameUI from OnSceneLoaded (game load)");
                UIManager.Instance.ShowGameUI();
            }
            else
            {
                Debug.LogError("Cannot show game UI - UIManager.Instance is still null!");
            }


            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && !player.activeInHierarchy)
            {
                player.SetActive(true);
                Debug.Log("Player manually enabled in level scene");
            }

            isLoadingGame = false;
        }

    }

    public void LoadLevel(int levelNumber)
    {
        currentLevel = levelNumber;


        if (!isLoadingGame)
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetInventory();
        }


        currentLevelPuzzle = null;

        isPuzzleCompleted = false;
        isLoadingGame = true;

        string sceneName = $"Level{levelNumber}";
        Debug.Log($"Loading {sceneName}, isLoadingGame set to: " + isLoadingGame);


        SceneManager.LoadScene(sceneName);
    }

    public void LoadNextLevel()
    {
        currentLevel++;
        Debug.Log($"Moving to Level {currentLevel}");

        if (currentLevel > 25)
        {
            GameComplete();
            return;
        }

        LoadLevel(currentLevel);
    }

    public void PuzzleCompleted()
    {
        if (!isPuzzleCompleted)
        {
            isPuzzleCompleted = true;
            Debug.Log($"Level {currentLevel} puzzle completed!");

            if (AccountManager.Instance != null)
                AccountManager.Instance.UnlockNextLevel();

            if (UIManager.Instance != null)
                UIManager.Instance.ShowPuzzleComplete();

            Invoke("LoadNextLevel", 3f);
        }
    }

    public void GameComplete()
    {
        Debug.Log("CONGRATULATIONS! You completed the game!");
        if (UIManager.Instance != null)
            UIManager.Instance.ShowGameComplete();

        Invoke("ReturnToMainMenu", 5f);
    }

    public void ReturnToMainMenu()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainMenu();
        }
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    public bool IsLoadingGame()
    {
        return isLoadingGame;
    }


    public void StartNewGame()
    {
        Debug.Log("STARTING NEW GAME - Resetting progress");


        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player != null)
        {
            player.unlockedLevels = 1;
            player.lastCompletedLevel = 0;
            player.collectedGates.Clear();
            player.andGatesCollected = 0;
            player.orGatesCollected = 0;
            player.notGatesCollected = 0;


            player.destroyedGates.Clear();

            AccountManager.Instance.SavePlayerProgress();
        }


        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ResetInventory();
        }


        isLoadingGame = false;

        LoadLevel(1);
    }


    public void ContinueGame()
    {
        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player != null)
        {
            Debug.Log($"CONTINUING GAME from Level {player.lastCompletedLevel + 1}");


            isLoadingGame = true;


            LoadLevel(player.lastCompletedLevel + 1);
        }
        else
        {
            Debug.LogWarning("No player logged in! Showing main menu.");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMainMenu();
            }
        }
    }

    public bool CanAccessLevel(int level)
    {
        var player = AccountManager.Instance?.GetCurrentPlayer();

        
        if (level == 2)
        {
            Debug.Log("🎯 PRESENTATION MODE: Level 2 unlocked!");
            return true;
        }

       
        return player != null && level <= player.unlockedLevels;
    }


    public void LoadLevelFromSelection(int level)
    {
        if (CanAccessLevel(level))
        {
            Debug.Log($"Loading Level {level} from selection");


            var player = AccountManager.Instance?.GetCurrentPlayer();
            if (player != null)
            {
                player.destroyedGates.Clear();
                Debug.Log("Cleared destroyedGates for fresh level start");
            }


            isLoadingGame = false;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetInventory();

            LoadLevel(level);
        }
        else
        {
            Debug.LogWarning($"Level {level} is locked!");
        }
    }
}