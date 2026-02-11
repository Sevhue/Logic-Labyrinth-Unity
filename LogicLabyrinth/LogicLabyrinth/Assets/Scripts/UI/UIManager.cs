using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Main Menu Panels")]
    public GameObject loggedOutPanel;
    public GameObject loggedInPanel;
    public GameObject loginPanel;
    public GameObject createAccountPanel;
    public GameObject optionsPanel;

   
    public GameObject mainLoginPanel;
    public GameObject completeProfilePanel;
    public GameObject accountProfilePanel;
    public GameObject leaderboardsPanel;
    public GameObject forgotPasswordPanel; 

    [Header("Confirmation Popups")]
    public GameObject exitPopup;
    public GameObject logoutPopup;
    public GameObject savePopup;

    [Header("Level Selection")]
    public GameObject levelSelectionPanel;

    [Header("In-Game UI")]
    public GameObject gameUI;
    public GameObject puzzleUI;
    public TextMeshProUGUI interactPrompt;
    public TextMeshProUGUI gateCountText;

    [Header("Puzzle Complete")]
    public GameObject puzzleCompletePanel;
    public GameObject gameCompletePanel;

    [Header("Cameras")]
    public Camera menuCamera;
    public Camera playerCamera;

    private GameObject playerObject;
    private bool isInitialized = false;

    
    private static UIManager _currentInstance;
    public static UIManager GetUIManager() => _currentInstance;
    public static bool IsUIManagerAvailable() => _currentInstance != null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _currentInstance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            isInitialized = true;
            Debug.Log("UIManager initialized successfully");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate UIManager found - destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        if (!gameObject.CompareTag("UIManager"))
        {
            Debug.LogWarning("UIManager GameObject should be tagged as 'UIManager' for better discovery");
        }
    }

    void Start()
    {
        if (!isInitialized) return;

        FindPlayerObject();
        AutoFindInteractPrompt();

        if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
        {
            ShowMainMenu();
        }
        else
        {
            ShowMainLoginPanel(); 
            Debug.Log("UIManager Start - Showing login panel (No user found)");
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (!isInitialized)
        {
            InitializeUIManager();
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _currentInstance = null;
            Debug.Log("UIManager destroyed and unregistered");
        }
    }

    private void InitializeUIManager()
    {
        if (isInitialized) return;

        if (Instance == null)
        {
            Instance = this;
            _currentInstance = this;
            DontDestroyOnLoad(gameObject);
            isInitialized = true;
            Debug.Log("UIManager initialized successfully");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate UIManager found - destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        if (!gameObject.CompareTag("UIManager"))
        {
            Debug.LogWarning("UIManager GameObject should be tagged as 'UIManager' for better discovery");
        }
    }

    
    void FindPlayerObject()
    {
        playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        if (playerObject == null)
        {
            
            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerObject = playerController.gameObject;
            }
        }

        if (playerObject != null)
        {
            Debug.Log($"Found player: {playerObject.name}");
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu" || currentScene == "SampleScene")
            {
                playerObject.SetActive(false);
                Debug.Log("Player disabled - in main menu scene");
            }
            else
            {
                playerObject.SetActive(true);
                Debug.Log("Player kept enabled - in level scene");
            }
        }
        else
        {
            Debug.LogWarning("Could not find player object in scene!");
        }
    }

    
    private void SetGameplayActive(bool gameplayActive)
    {
        if (menuCamera != null)
            menuCamera.gameObject.SetActive(!gameplayActive);

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(gameplayActive);

        if (playerObject != null)
        {
            playerObject.SetActive(gameplayActive);
        }

        Debug.Log(gameplayActive ? "Gameplay activated" : "Menu activated");
    }

    
    public void SetCursorState(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }


    public void ShowMainMenu()
    {
        HideAllPanels(); 

        bool isLoggedIn = AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null;

        if (isLoggedIn)
        {
            if (loggedInPanel != null)
            {
                loggedInPanel.SetActive(true);
                Debug.Log("UI: Switched to LoggedInPanel");
            }
        }
        else
        {
            if (loggedOutPanel != null)
            {
                loggedOutPanel.SetActive(true);
                Debug.Log("UI: Switched to LoggedOutPanel");
            }
        }

        SetCursorState(false); 
    }

    public void ShowLoginPanel()
    {
        HideAllPanels();
        if (loginPanel != null) loginPanel.SetActive(true);
        SetCursorState(false);
    }

    public void ShowCreateAccountPanel()
    {
        HideAllPanels();
        if (createAccountPanel != null) createAccountPanel.SetActive(true);
        SetCursorState(false);
    }

    public void ShowOptionsPanel()
    {
        HideAllPanels();
        if (optionsPanel != null) optionsPanel.SetActive(true);
        SetCursorState(false);
    }

    
    public void ShowMainLoginPanel()
    {
        HideAllPanels();
        if (mainLoginPanel != null) mainLoginPanel.SetActive(true);
        SetCursorState(false);
    }

    public void ShowCompleteProfilePanel()
    {
        HideAllPanels();
        if (completeProfilePanel != null) completeProfilePanel.SetActive(true);
        SetCursorState(false);
    }

    public void ShowAccountProfilePanel()
    {
        HideAllPanels();
        if (accountProfilePanel != null) accountProfilePanel.SetActive(true);
        SetCursorState(false);
    }

    public void ShowLeaderboardsPanel()
    {
        HideAllPanels();
        if (leaderboardsPanel != null) leaderboardsPanel.SetActive(true);
        SetCursorState(false);
    }

    
    public void ShowForgotPasswordPanel()
    {
        HideAllPanels();
        if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(true);
        SetCursorState(false);
    }

    
    public void ShowExitPopup()
    {
        if (exitPopup != null) exitPopup.SetActive(true);
    }

    public void ShowLogoutPopup()
    {
        if (logoutPopup != null) logoutPopup.SetActive(true);
    }

    public void ShowSavePopup()
    {
        if (savePopup != null) savePopup.SetActive(true);
    }

    public void HideAllPopups()
    {
        if (exitPopup != null) exitPopup.SetActive(false);
        if (logoutPopup != null) logoutPopup.SetActive(false);
        if (savePopup != null) savePopup.SetActive(false);
    }

    
    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowLevelSelection()
    {
        HideAllPanels();
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
        SetCursorState(false);
        Debug.Log("Showing LevelSelectionPanel");
    }

    public void ShowGameUI()
    {
        Debug.Log("ShowGameUI called");

        HideAllPanels();
        if (gameUI != null) gameUI.SetActive(true);
        SetCursorState(true);
        SetGameplayActive(true);
        UpdateInventoryDisplay();

        if (gateCountText != null)
        {
            var playerAccount = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;
            if (playerAccount != null)
            {
                gateCountText.text = $"Welcome {playerAccount.username}!\nLevel {playerAccount.lastCompletedLevel + 1}\nCollect logic gates to proceed!";
            }
            else
            {
                gateCountText.text = "Welcome!\nCollect logic gates to proceed!";
            }
        }
    }

    public void ShowPuzzleUI()
    {
        if (puzzleUI != null) puzzleUI.SetActive(true);
        SetCursorState(false);
    }

    public void HidePuzzleUI()
    {
        if (puzzleUI != null) puzzleUI.SetActive(false);
        SetCursorState(true);
    }

    
    public void ShowInteractPrompt(bool show, string message = "Press E to interact")
    {
        if (interactPrompt != null)
        {
            interactPrompt.gameObject.SetActive(show);
            if (show)
            {
                interactPrompt.text = message;
            }
        }
        else
        {
            Debug.LogWarning("Interact prompt is null! Message: " + message);
        }
    }

    
    public void UpdateInventoryDisplay()
    {
        if (InventoryManager.Instance != null && gateCountText != null)
        {
            int andCount = InventoryManager.Instance.GetGateCount("AND");
            int orCount = InventoryManager.Instance.GetGateCount("OR");
            int notCount = InventoryManager.Instance.GetGateCount("NOT");
            UpdateGateCounts(andCount, orCount, notCount);
        }
        else
        {
            Debug.LogWarning("Cannot update inventory - InventoryManager or gateCountText is null");
        }
    }

    public void UpdateGateCounts(int andCount, int orCount, int notCount)
    {
        if (gateCountText != null)
        {
            gateCountText.text = $"AND: {andCount} | OR: {orCount} | NOT: {notCount}";
        }

        
        Debug.Log($"INVENTORY UPDATE - AND: {andCount}, OR: {orCount}, NOT: {notCount}");
    }

    
    public void OnGateCollected(string gateType)
    {
        Debug.Log($"GATE COLLECTED: {gateType}");

        
        UpdateInventoryDisplay();

        
        if (gateCountText != null)
        {
            StartCoroutine(ShowCollectionMessage(gateType));
        }
    }

    
    private IEnumerator ShowCollectionMessage(string gateType)
    {
        if (gateCountText != null)
        {
            string originalText = gateCountText.text;
            Color originalColor = gateCountText.color;

            gateCountText.text = $"COLLECTED {gateType} GATE!";
            gateCountText.color = Color.green;

            yield return new WaitForSeconds(1.5f);

            
            UpdateInventoryDisplay();
            gateCountText.color = originalColor;
        }
    }

    
    public void ShowPuzzleComplete()
    {
        if (puzzleCompletePanel != null)
        {
            puzzleCompletePanel.SetActive(true);
            Invoke("HidePuzzleComplete", 2f);
        }
    }

    void HidePuzzleComplete()
    {
        if (puzzleCompletePanel != null)
        {
            puzzleCompletePanel.SetActive(false);
        }
    }

    public void ShowGameComplete()
    {
        if (gameCompletePanel != null)
        {
            gameCompletePanel.SetActive(true);
            SetCursorState(false);
        }
    }

    
    void HideAllPanels()
    {
        
        if (loggedOutPanel != null) loggedOutPanel.SetActive(false);
        if (loggedInPanel != null) loggedInPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (createAccountPanel != null) createAccountPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);

        
        if (mainLoginPanel != null) mainLoginPanel.SetActive(false);
        if (completeProfilePanel != null) completeProfilePanel.SetActive(false);
        if (accountProfilePanel != null) accountProfilePanel.SetActive(false);
        if (leaderboardsPanel != null) leaderboardsPanel.SetActive(false);
        if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false); 

        
        if (gameUI != null) gameUI.SetActive(false);
        if (puzzleUI != null) puzzleUI.SetActive(false);
        if (puzzleCompletePanel != null) puzzleCompletePanel.SetActive(false);
        if (gameCompletePanel != null) gameCompletePanel.SetActive(false);
    }

    
    void AutoFindInteractPrompt()
    {
        if (interactPrompt == null)
        {
            
            TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (TextMeshProUGUI text in allTexts)
            {
                if (text.name.Contains("Interact") || text.name.Contains("Prompt"))
                {
                    interactPrompt = text;
                    Debug.Log("Auto-found interact prompt: " + text.name);
                    break;
                }
            }

            if (interactPrompt == null && allTexts.Length > 0)
            {
                interactPrompt = allTexts[0];
                Debug.Log("Using first TextMeshPro as interact prompt: " + interactPrompt.name);
            }
        }
    }

    
    [ContextMenu("OnLoginButton")]
    public void OnLoginButton()
    {
        ShowLoginPanel();
    }

    [ContextMenu("OnCreateAccountButton")]
    public void OnCreateAccountButton()
    {
        ShowCreateAccountPanel();
    }

    [ContextMenu("OnOptionsButton")]
    public void OnOptionsButton()
    {
        ShowOptionsPanel();
    }

    [ContextMenu("OnExitButton")]
    public void OnExitButton()
    {
        ShowExitPopup();
    }

    [ContextMenu("OnBackButton")]
    public void OnBackButton()
    {
        ShowMainMenu();
    }

    [ContextMenu("OnNewGameButton")]
    public void OnNewGameButton()
    {
        ShowLevelSelection();
    }

    [ContextMenu("OnLoadGameButton")]
    public void OnLoadGameButton()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ContinueGame();
        }
        else
        {
            Debug.LogWarning("LevelManager not found!");
            ShowGameUI();
        }
    }

    [ContextMenu("OnLogoutButton")]
    public void OnLogoutButton()
    {
        ShowLogoutPopup();
    }

    
    [ContextMenu("OnAccountsProfileButton")]
    public void OnAccountsProfileButton()
    {
        ShowAccountProfilePanel();
    }

    [ContextMenu("OnLeaderboardsButton")]
    public void OnLeaderboardsButton()
    {
        ShowLeaderboardsPanel();
    }

    [ContextMenu("OnSaveButton")]
    public void OnSaveButton()
    {
        ShowSavePopup();
    }

    
    [ContextMenu("OnForgotPasswordButton")]
    public void OnForgotPasswordButton()
    {
        ShowForgotPasswordPanel();
    }

    public void OnLevelButtonClicked(int level)
    {
        Debug.Log($"Level {level} button clicked!");
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadLevelFromSelection(level);
        }
    }

    
    public static void SafeShowInteractPrompt(bool show, string message = "Press E to interact")
    {
        if (IsUIManagerAvailable())
        {
            _currentInstance.ShowInteractPrompt(show, message);
        }
        else
        {
            Debug.LogWarning("UIManager not available - cannot show interact prompt");
        }
    }

    public static void SafeUpdateInventoryDisplay()
    {
        if (IsUIManagerAvailable())
        {
            _currentInstance.UpdateInventoryDisplay();
        }
    }

   
    public static void SafeOnGateCollected(string gateType)
    {
        if (IsUIManagerAvailable())
        {
            _currentInstance.OnGateCollected(gateType);
        }
        else
        {
            Debug.Log($"Gate collected but UIManager not available: {gateType}");
        }
    }


    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("UIManager: Scene loaded - " + scene.name);
        FindPlayerObject();
        AutoFindInteractPrompt();

        if (scene.name.StartsWith("Level") || scene.name == "SampleScene")
        {
            if (gameUI == null) gameUI = GameObject.Find("GameUI");
            GameObject foundText = GameObject.Find("GateCountText");
            if (foundText != null)
            {
                gateCountText = foundText.GetComponent<TextMeshProUGUI>();
                Debug.Log("Successfully re-linked GateCountText!");
            }
            ShowGameUI();
        }
    }
    public void LinkWithGoogle(string email, string gid, string name)
    {
     
        AccountManager.Instance.LinkWithGoogle(email, gid, name);
        UIManager.Instance.ShowMainMenu();
    }

}