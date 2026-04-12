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
    [Header("Sign Up Panel 1 (Credentials)")]
    public GameObject credentialsPanel; //Panel 1
    public TMP_InputField signUpUsername;
    public TMP_InputField signUpPassword;
    public TMP_InputField signUpConfirmPassword;
    public Button continueButton;         // "Continue" button on Panel 1

    [Header("Security Question Panel (Details)")]
    public GameObject detailsPanel; //Panel 2  
    public TMP_InputField securityAnswer;
    public TMP_Dropdown genderDropdown;
    public TMP_Dropdown ageDropdown;
    public Toggle termsToggle;
    public ScrollRect termsScrollRect;    // Assign the ScrollRect for Terms & Conditions
    public Button confirmSignUpButton;    // The "Confirm" / final sign-up button
    public TMP_Text feedbackText;

    // Runtime state for terms scrolling
    private bool hasScrolledToBottom = false;
    private GameObject termsPopupOverlay; // Generated at runtime

    [Header("Confirmation Popups")]
    public GameObject exitPopup;
    public GameObject logoutPopup;
    public GameObject savePopup;

    [Header("Level Selection")]
    public GameObject levelSelectionPanel;
    public GameObject levelSelectionPanel2; // New LevelSelection2.0 panel with chapter sub-panels

    [Header("StoryBoard")]
    public GameObject storyBoardPanel;

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

    [Header("Validation Popup")]
    public GameObject validationPopup; 
    public TMP_Text validationMessageText;

    [Header("Profile Selection")]
    public GameObject profileSelectionPanel;

    private GameObject playerObject;
    private bool isInitialized = false;
    private System.Action validationConfirmAction;


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
            isInitialized = true;
            Debug.Log("UIManager initialized successfully");
        }
        else if (Instance != this)
        {
            // ── Replace the stale DontDestroyOnLoad instance with THIS fresh scene instance ──
            // Buttons in the scene have persistent onClick listeners referencing THIS UIManager.
            // If we destroyed THIS and kept the old one, those buttons would be dead.
            Debug.Log("[UIManager] Replacing stale DontDestroyOnLoad instance with fresh scene instance.");

            UIManager oldInstance = Instance;

            // Prevent the old instance from firing OnSceneLoaded with stale references
            SceneManager.sceneLoaded -= oldInstance.OnSceneLoaded;

            // This becomes the new singleton
            Instance = this;
            _currentInstance = this;
            isInitialized = true;
            DontDestroyOnLoad(gameObject);

            // Destroy the old instance's GameObject
            Destroy(oldInstance.gameObject);

            Debug.Log("[UIManager] Old instance destroyed. Fresh instance is now the singleton.");
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
        WireLogoutButtonToPopup();
        WireLoggedInPanelButtons();
        WireLoginPanelButtons();

        string currentScene = SceneManager.GetActiveScene().name;
        if (IsGameplaySceneName(currentScene))
        {
            ShowGameUI();
            return;
        }

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
            Debug.Log("UIManager initialized successfully (from InitializeUIManager)");
        }
        else if (Instance != this)
        {
            // Same swap logic as Awake — keep fresh, destroy stale
            Debug.Log("[UIManager] Duplicate in InitializeUIManager — replacing old instance.");
            UIManager oldInstance = Instance;
            SceneManager.sceneLoaded -= oldInstance.OnSceneLoaded;
            Instance = this;
            _currentInstance = this;
            isInitialized = true;
            DontDestroyOnLoad(gameObject);
            Destroy(oldInstance.gameObject);
        }

        if (!gameObject.CompareTag("UIManager"))
        {
            Debug.LogWarning("UIManager GameObject should be tagged as 'UIManager' for better discovery");
        }
    }


    /// <summary>
    /// Copies every non-null Inspector reference from a freshly-created (duplicate) UIManager
    /// so the surviving DontDestroyOnLoad instance has valid references to the new scene's objects.
    /// </summary>
    private void StealSceneReferences(UIManager source)
    {
        // Main Menu Panels
        if (source.loggedOutPanel != null) loggedOutPanel = source.loggedOutPanel;
        if (source.loggedInPanel != null) loggedInPanel = source.loggedInPanel;
        if (source.loginPanel != null) loginPanel = source.loginPanel;
        if (source.createAccountPanel != null) createAccountPanel = source.createAccountPanel;
        if (source.optionsPanel != null) optionsPanel = source.optionsPanel;
        if (source.mainLoginPanel != null) mainLoginPanel = source.mainLoginPanel;
        if (source.completeProfilePanel != null) completeProfilePanel = source.completeProfilePanel;
        if (source.accountProfilePanel != null) accountProfilePanel = source.accountProfilePanel;
        if (source.leaderboardsPanel != null) leaderboardsPanel = source.leaderboardsPanel;
        if (source.forgotPasswordPanel != null) forgotPasswordPanel = source.forgotPasswordPanel;

        // Sign Up panels
        if (source.credentialsPanel != null) credentialsPanel = source.credentialsPanel;
        if (source.signUpUsername != null) signUpUsername = source.signUpUsername;
        if (source.signUpPassword != null) signUpPassword = source.signUpPassword;
        if (source.signUpConfirmPassword != null) signUpConfirmPassword = source.signUpConfirmPassword;
        if (source.continueButton != null) continueButton = source.continueButton;

        // Security Question Panel
        if (source.detailsPanel != null) detailsPanel = source.detailsPanel;
        if (source.securityAnswer != null) securityAnswer = source.securityAnswer;
        if (source.genderDropdown != null) genderDropdown = source.genderDropdown;
        if (source.ageDropdown != null) ageDropdown = source.ageDropdown;
        if (source.termsToggle != null) termsToggle = source.termsToggle;
        if (source.termsScrollRect != null) termsScrollRect = source.termsScrollRect;
        if (source.confirmSignUpButton != null) confirmSignUpButton = source.confirmSignUpButton;
        if (source.feedbackText != null) feedbackText = source.feedbackText;

        // Confirmation Popups
        if (source.exitPopup != null) exitPopup = source.exitPopup;
        if (source.logoutPopup != null) logoutPopup = source.logoutPopup;
        if (source.savePopup != null) savePopup = source.savePopup;

        // Level Selection
        if (source.levelSelectionPanel != null) levelSelectionPanel = source.levelSelectionPanel;
        if (source.levelSelectionPanel2 != null) levelSelectionPanel2 = source.levelSelectionPanel2;

        // StoryBoard
        if (source.storyBoardPanel != null) storyBoardPanel = source.storyBoardPanel;

        // In-Game UI
        if (source.gameUI != null) gameUI = source.gameUI;
        if (source.puzzleUI != null) puzzleUI = source.puzzleUI;
        if (source.interactPrompt != null) interactPrompt = source.interactPrompt;
        if (source.gateCountText != null) gateCountText = source.gateCountText;

        // Puzzle Complete
        if (source.puzzleCompletePanel != null) puzzleCompletePanel = source.puzzleCompletePanel;
        if (source.gameCompletePanel != null) gameCompletePanel = source.gameCompletePanel;

        // Cameras
        if (source.menuCamera != null) menuCamera = source.menuCamera;
        if (source.playerCamera != null) playerCamera = source.playerCamera;

        // Validation Popup
        if (source.validationPopup != null) validationPopup = source.validationPopup;
        if (source.validationMessageText != null) validationMessageText = source.validationMessageText;

        // Profile Selection
        if (source.profileSelectionPanel != null) profileSelectionPanel = source.profileSelectionPanel;

        Debug.Log($"[UIManager] Scene references transferred. loggedInPanel={loggedInPanel != null}, menuCamera={menuCamera != null}, mainLoginPanel={mainLoginPanel != null}");
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
            bool isLevelScene = currentScene.StartsWith("Level") || currentScene == "Chapter3" || currentScene == "Chapter4";

            if (isLevelScene)
            {
                playerObject.SetActive(true);
                Debug.Log("Player enabled - in level scene");
            }
            else
            {
                playerObject.SetActive(false);
                Debug.Log("Player disabled - in menu scene: " + currentScene);
            }
        }
        else
        {
            Debug.LogWarning("Could not find player object in scene!");
        }
    }

    private static bool IsGameplaySceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
               (sceneName.StartsWith("Level") || sceneName == "Chapter3" || sceneName == "Chapter4");
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
        SetGameplayActive(false);

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

        // Always start on the credentials panel (Panel 1)
        if (credentialsPanel != null) credentialsPanel.SetActive(true);
        if (detailsPanel != null) detailsPanel.SetActive(false);

        // Clear previous input
        if (signUpUsername != null) signUpUsername.text = "";
        if (signUpPassword != null) signUpPassword.text = "";
        if (signUpConfirmPassword != null) signUpConfirmPassword.text = "";

        // Wire the Continue button to go to Panel 2
        // NOTE: Replace the ENTIRE onClick event to also clear Inspector-wired persistent listeners
        if (continueButton != null)
        {
            continueButton.onClick = new Button.ButtonClickedEvent();
            continueButton.onClick.AddListener(GoToNextSignUpPanel);
        }

        // NOTE: The Confirm button is handled by CreateAccountPanel.cs — do NOT add a duplicate listener here.

        // Reset terms state
        hasScrolledToBottom = false;

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
        SetGameplayActive(false);

        if (mainLoginPanel != null)
        {
            mainLoginPanel.SetActive(true);
            Debug.Log("UIManager: Main Login Panel Activated");
        }
        else
        {
            Debug.LogError("UIManager: mainLoginPanel is MISSING in Inspector!");
        }

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
        if (accountProfilePanel != null)
        {
            accountProfilePanel.SetActive(true);
            WireAccountProfilePanelButtons();
            PopulateAccountProfileFields();
        }
        SetCursorState(false);

        // Refresh the profile picture display
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.LoadCurrentProfileFromPlayer();
            ProfileManager.Instance.RefreshProfileDisplay();
        }
    }

    /// <summary>
    /// Populates the Name, Email, Age, Gender fields in the AccountProfilePanel
    /// from the current player's data.
    /// The prefab structure is:  Profile / Name / Placeholder (TMP)  &amp;  Name / Input (TMP)
    /// We write the value into the Placeholder TMP text and also enable + set the Input TMP.
    /// </summary>
    private void PopulateAccountProfileFields()
    {
        if (accountProfilePanel == null || AccountManager.Instance == null) return;
        var p = AccountManager.Instance.GetCurrentPlayer();
        if (p == null) return;

        // Helper: sets the text in the correct child TMP under a named container.
        // The AccountProfile prefab has two overlapping text children per field:
        //   Placeholder (TMP) — semi-transparent hint text ("Name", "Email", etc.)
        //   Input       (TMP) — the actual value (disabled by default)
        // We must only show ONE of them to avoid overlapping text.
        void SetProfileField(string containerName, string value)
        {
            Transform container = FindChildRecursive(accountProfilePanel.transform, containerName);
            if (container == null) return;

            bool hasValue = !string.IsNullOrWhiteSpace(value);
            Transform phT = container.Find("Placeholder");
            Transform inputT = container.Find("Input");

            if (hasValue)
            {
                // Hide placeholder, show Input with the real value
                if (phT != null) phT.gameObject.SetActive(false);
                if (inputT != null)
                {
                    inputT.gameObject.SetActive(true);
                    TextMeshProUGUI inputTMP = inputT.GetComponent<TextMeshProUGUI>();
                    if (inputTMP != null)
                    {
                        inputTMP.text = value;
                        inputTMP.enabled = true;
                    }
                }
            }
            else
            {
                // No value — show the placeholder, hide Input
                if (phT != null) phT.gameObject.SetActive(true);
                if (inputT != null) inputT.gameObject.SetActive(false);
            }

            // Some prefab variants nest TMP_InputField deeper than the container.
            TMP_InputField[] inputFields = container.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var field in inputFields)
                field.text = hasValue ? value : "";
        }

        // Gather values
        string displayName = !string.IsNullOrWhiteSpace(p.displayName) ? p.displayName : p.username;
        string email = !string.IsNullOrWhiteSpace(p.googleEmail)
            ? p.googleEmail
            : (p.username + "@logic.com");

        SetProfileField("Name", displayName);
        SetProfileField("Email", email);
        SetProfileField("Age", p.age);
        SetProfileField("Gender", p.gender);

        // ── Stats section (Boarder) ──
        // The "Boarder" child has 4 Text (TMP) children for: Maze Depth, level, Puzzle Solved, count
        // We'll search for specific text content and update the value texts
        Transform boarder = FindChildRecursive(accountProfilePanel.transform, "Boarder");
        if (boarder != null)
        {
            TextMeshProUGUI[] statTexts = boarder.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in statTexts)
            {
                string lower = txt.text.Trim().ToLower();

                // Update the "level" text (below "Maze Depth")
                if (lower == "level" || lower.StartsWith("level"))
                {
                    txt.text = $"Level {p.unlockedLevels}";
                }
                // Update the puzzles count (below "Puzzle Solved")
                else if (lower == "0" || (int.TryParse(lower, out _) && !lower.Contains(":")))
                {
                    int puzzleCount = p.completedPuzzles != null ? p.completedPuzzles.Count : 0;
                    txt.text = puzzleCount.ToString();
                }
            }
        }

        Debug.Log($"[UIManager] Populated AccountProfilePanel: name='{displayName}', email='{email}', gender='{p.gender}', age='{p.age}'");
    }

    /// <summary>
    /// Wires the BackButton (and other buttons) in AccountProfilePanel at runtime.
    /// </summary>
    private void WireAccountProfilePanelButtons()
    {
        if (accountProfilePanel == null) return;

        // Wire the Back button — revert pending changes and go back
        // The prefab uses "back" as the name, but some scenes may use "BackButton"
        Button backBtn = FindButtonRecursive(accountProfilePanel.transform, "back");
        if (backBtn == null)
            backBtn = FindButtonRecursive(accountProfilePanel.transform, "BackButton");
        if (backBtn != null)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(() =>
            {
                // Revert any unsaved profile picture change before leaving
                if (ProfileManager.Instance != null)
                    ProfileManager.Instance.RevertPendingChanges();

                ShowMainMenu();
            });
            Debug.Log("[UIManager] Wired AccountProfilePanel BackButton (revert + ShowMainMenu).");
        }
        else
        {
            Debug.LogWarning("[UIManager] BackButton not found in AccountProfilePanel hierarchy!");
        }

        // Wire the Save button — it's "Gender (1)" in the prefab hierarchy
        WireSaveButton();
    }

    /// <summary>
    /// Finds the Save button in the AccountProfilePanel (named "Gender (1)" in the prefab)
    /// and wires it to commit pending profile changes (including text fields).
    /// </summary>
    private void WireSaveButton()
    {
        if (accountProfilePanel == null) return;

        // The save button is "Gender (1)" inside Profile — find it recursively
        Transform saveTransform = FindChildRecursive(accountProfilePanel.transform, "Gender (1)");
        if (saveTransform == null)
        {
            Debug.LogWarning("[UIManager] Save button ('Gender (1)') not found in AccountProfilePanel!");
            return;
        }

        // Add Button component if missing
        Button saveBtn = saveTransform.GetComponent<Button>();
        if (saveBtn == null)
        {
            saveBtn = saveTransform.gameObject.AddComponent<Button>();
            // Set the target graphic so we get visual feedback
            Image img = saveTransform.GetComponent<Image>();
            if (img != null) saveBtn.targetGraphic = img;
        }

        saveBtn.onClick.RemoveAllListeners();
        saveBtn.onClick.AddListener(() =>
        {
            // 1. Save the pending profile picture change
            if (ProfileManager.Instance != null)
                ProfileManager.Instance.SavePendingChanges();

            // 2. Read text fields from the profile panel and persist them
            SaveProfileFieldsToAccountManager();

            Debug.Log("[UIManager] Save button clicked — profile changes committed.");
        });

        // Give it a nice color tint transition
        saveBtn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = saveBtn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.7f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.5f, 1f);
        saveBtn.colors = colors;

        Debug.Log("[UIManager] Wired Save button in AccountProfilePanel.");
    }

    /// <summary>
    /// Reads the current text from profile field containers (Name, Email, Age, Gender)
    /// and writes them back to AccountManager's current player, then saves to Firebase.
    /// </summary>
    private void SaveProfileFieldsToAccountManager()
    {
        if (accountProfilePanel == null || AccountManager.Instance == null) return;
        var p = AccountManager.Instance.GetCurrentPlayer();
        if (p == null) return;

        // Helper: read text from the "Input" child (our populate function writes there)
        string ReadProfileField(string containerName)
        {
            Transform container = FindChildRecursive(accountProfilePanel.transform, containerName);
            if (container == null) return null;

            // Prefer TMP_InputField text from any nested layout variant.
            TMP_InputField[] inputFields = container.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var field in inputFields)
            {
                if (!string.IsNullOrWhiteSpace(field.text))
                    return field.text;
            }

            // Read from the Input TMP child (active when a value is set)
            Transform inputT = container.Find("Input");
            if (inputT != null && inputT.gameObject.activeSelf)
            {
                TextMeshProUGUI inputTMP = inputT.GetComponent<TextMeshProUGUI>();
                if (inputTMP != null && !string.IsNullOrWhiteSpace(inputTMP.text))
                    return inputTMP.text;
            }

            // Fallback: read Placeholder (if Input is hidden, value wasn't set)
            Transform phT = container.Find("Placeholder");
            if (phT != null && phT.gameObject.activeSelf)
            {
                TextMeshProUGUI phTMP = phT.GetComponent<TextMeshProUGUI>();
                if (phTMP != null) return phTMP.text;
            }
            return null;
        }

        string newName = ReadProfileField("Name");
        string newEmail = ReadProfileField("Email");
        string newAge = ReadProfileField("Age");
        string newGender = ReadProfileField("Gender");

        bool changed = false;

        // Only update if the text is different from the placeholder default
        if (!string.IsNullOrWhiteSpace(newName) && newName != "Name" && newName != p.displayName)
        {
            p.displayName = newName;
            changed = true;
            Debug.Log($"[UIManager] Updated displayName → '{newName}'");
        }
        if (!string.IsNullOrWhiteSpace(newEmail) && newEmail != "Email" && newEmail != p.googleEmail)
        {
            p.googleEmail = newEmail;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(newAge) && newAge != "Age" && newAge != p.age)
        {
            p.age = newAge;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(newGender) && newGender != "Gender" && newGender != p.gender)
        {
            p.gender = newGender;
            changed = true;
        }

        if (changed)
        {
            AccountManager.Instance.SavePlayerProgress(success =>
            {
                if (!success)
                {
                    Debug.LogWarning("[UIManager] Profile save failed.");
                    return;
                }

                // Keep this screen in sync immediately after save.
                PopulateAccountProfileFields();
                if (ProfileManager.Instance != null)
                {
                    ProfileManager.Instance.LoadCurrentProfileFromPlayer();
                    ProfileManager.Instance.RefreshProfileDisplay();
                }

                Debug.Log("[UIManager] Profile fields saved to Firebase.");
            });
        }
    }

    /// <summary>
    /// Recursively searches for a child transform by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Recursively searches for a Button component on a child with the given name.
    /// </summary>
    private Button FindButtonRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null) return btn;
            }
            Button found = FindButtonRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    public void ShowProfileSelectionPanel()
    {
        // ProfileManager now handles showing the ProfilePicture prefab directly
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.OpenSelectionPanel();
        }
        else if (profileSelectionPanel != null)
        {
            profileSelectionPanel.SetActive(true);
        }
    }

    public void HideProfileSelectionPanel()
    {
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.CloseSelectionPanel();
        }
        else if (profileSelectionPanel != null)
        {
            profileSelectionPanel.SetActive(false);
        }
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
        if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);
        ForgotPasswordRuntimePanel.Show();
        SetCursorState(false);
    }


    public void ShowExitPopup()
    {
        if (exitPopup != null) exitPopup.SetActive(true);
    }

    public void ShowLogoutPopup()
    {
        if (logoutPopup != null)
        {
            logoutPopup.SetActive(true);
            WireLogoutPopupButtons();
        }
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

    /// <summary>
    /// Rewires the LogoutButton in LoggedInPanel so it shows the confirmation popup
    /// instead of logging out directly.
    /// </summary>
    private void WireLogoutButtonToPopup()
    {
        if (loggedInPanel == null) return;

        // Find the LogoutButton in LoggedInPanel
        Transform logoutBtnTransform = loggedInPanel.transform.Find("LogoutButton");
        if (logoutBtnTransform == null)
        {
            Debug.LogWarning("[UIManager] LogoutButton not found in LoggedInPanel.");
            return;
        }

        Button logoutBtn = logoutBtnTransform.GetComponent<Button>();
        if (logoutBtn != null)
        {
            logoutBtn.onClick.RemoveAllListeners();
            logoutBtn.onClick.AddListener(OnLogoutButton);
            Debug.Log("[UIManager] Wired LogoutButton to show confirmation popup.");
        }
    }

    /// <summary>
    /// Wires all buttons in LoggedInPanel that have no persistent onClick listeners.
    /// </summary>
    private void WireLoggedInPanelButtons()
    {
        if (loggedInPanel == null) return;

        // Wire AccountsProfileButton → ShowAccountProfilePanel
        WireButtonByName(loggedInPanel, "AccountsProfileButton", ShowAccountProfilePanel, "ShowAccountProfilePanel");

        // Wire NewGameButton → ShowStoryBoardPanel (new game starts with the story)
        WireButtonByName(loggedInPanel, "NewGameButton", ShowStoryBoardPanel, "ShowStoryBoardPanel");

        // Wire ContinueButton → ShowLevelSelection (continue from where they left off)
        WireButtonByName(loggedInPanel, "ContinueButton", ShowLevelSelection, "ShowLevelSelection");

        // Wire LeaderboardsButton → ShowLeaderboardsPanel
        WireButtonByName(loggedInPanel, "LeaderboardsButton", ShowLeaderboardsPanel, "ShowLeaderboardsPanel");

    }

    /// <summary>
    /// Wires buttons inside the LoginFormPanel (loginPanel) at runtime.
    /// ForgotPasswordButton lives inside LoginFormPanel > BG, not LoggedInPanel.
    /// </summary>
    private void WireLoginPanelButtons()
    {
        if (loginPanel == null) return;

        Button forgotBtn = FindButtonRecursive(loginPanel.transform, "ForgotPasswordButton");
        if (forgotBtn != null)
        {
            if (forgotBtn.onClick.GetPersistentEventCount() == 0)
            {
                forgotBtn.onClick.RemoveAllListeners();
                forgotBtn.onClick.AddListener(ShowForgotPasswordPanel);
                Debug.Log("[UIManager] Wired ForgotPasswordButton → ShowForgotPasswordPanel");
            }
        }
        else
        {
            Debug.LogWarning("[UIManager] ForgotPasswordButton not found anywhere under loginPanel.");
        }
    }

    /// <summary>
    /// Helper to wire a button by its GameObject name inside a parent panel.
    /// Only wires if the button has no existing persistent listeners.
    /// </summary>
    private void WireButtonByName(GameObject parent, string buttonName, UnityEngine.Events.UnityAction action, string actionName)
    {
        Transform btnTransform = parent.transform.Find(buttonName);
        if (btnTransform == null)
        {
            Debug.LogWarning($"[UIManager] {buttonName} not found in {parent.name}.");
            return;
        }

        Button btn = btnTransform.GetComponent<Button>();
        if (btn != null)
        {
            // Only wire if there are no persistent listeners
            if (btn.onClick.GetPersistentEventCount() == 0)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(action);
                Debug.Log($"[UIManager] Wired {buttonName} → {actionName}");
            }
        }
    }

    /// <summary>
    /// Wires the YES/NO buttons inside the LogoutPopup at runtime,
    /// since they have no persistent onClick listeners set in the Inspector.
    /// </summary>
    private void WireLogoutPopupButtons()
    {
        if (logoutPopup == null) return;

        Button[] buttons = logoutPopup.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt == null) continue;

            string buttonText = txt.text.Trim().ToUpper();
            btn.onClick.RemoveAllListeners();

            if (buttonText == "YES")
            {
                btn.onClick.AddListener(ConfirmLogout);
                Debug.Log("[UIManager] Wired LogoutPopup YES button.");
            }
            else if (buttonText == "NO")
            {
                btn.onClick.AddListener(CancelLogout);
                Debug.Log("[UIManager] Wired LogoutPopup NO button.");
            }
        }
    }

    /// <summary>
    /// Called when user confirms logout from the LogoutPopup.
    /// Signs out of Firebase, clears data, and returns to MainLoginPanel.
    /// </summary>
    public void ConfirmLogout()
    {
        HideAllPopups();
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.Logout();
            // AccountManager.Logout() calls ShowMainLoginPanel() internally
        }
        else
        {
            // Fallback: go to main login panel directly
            ShowMainLoginPanel();
        }
        Debug.Log("[UIManager] Logout confirmed.");
    }

    /// <summary>
    /// Called when user cancels the logout from the LogoutPopup.
    /// Simply hides the popup.
    /// </summary>
    public void CancelLogout()
    {
        HideAllPopups();
        Debug.Log("[UIManager] Logout cancelled.");
    }


    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowLevelSelection()
    {
        // Now redirects to StoryBoardPanel (chapter selection) so users pick a chapter first
        HideAllPanels();
        if (storyBoardPanel != null)
        {
            storyBoardPanel.SetActive(true);
            Debug.Log("Showing StoryBoardPanel (chapter selection) for level selection");
        }
        else if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(true);
            Debug.Log("Fallback: Showing old LevelSelectionPanel");
        }
        SetCursorState(false);
    }

    /// <summary>
    /// Shows the level selection panel for a specific chapter.
    /// Called by StoryBoardManager when a chapter button is clicked.
    /// </summary>
    public void ShowLevelSelectionForChapter(int chapterNumber)
    {
        HideAllPanels();

        if (LevelSelectionController.Instance != null)
        {
            LevelSelectionController.Instance.ShowChapter(chapterNumber);
        }
        else if (levelSelectionPanel2 != null)
        {
            // Fallback: activate the panel and let the controller auto-find chapters
            levelSelectionPanel2.SetActive(true);
            var controller = levelSelectionPanel2.GetComponent<LevelSelectionController>();
            if (controller != null)
                controller.ShowChapter(chapterNumber);
        }
        else
        {
            Debug.LogWarning("[UIManager] LevelSelectionPanel2 not found! Cannot show chapter levels.");
        }

        SetCursorState(false);
        Debug.Log($"Showing Level Selection for Chapter {chapterNumber}");
    }

    public void ShowStoryBoardPanel()
    {
        HideAllPanels();
        if (storyBoardPanel != null)
        {
            storyBoardPanel.SetActive(true);
            Debug.Log("Showing StoryBoardPanel");
        }
        else
        {
            Debug.LogError("UIManager: storyBoardPanel is MISSING in Inspector!");
        }
        SetCursorState(false);
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
        // Note: This can happen on initial scene load when components initialize
        // in different order. The GameInventoryUI handles the in-level display.
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
        if (levelSelectionPanel2 != null) levelSelectionPanel2.SetActive(false);
        if (storyBoardPanel != null) storyBoardPanel.SetActive(false);

        if (mainLoginPanel != null) mainLoginPanel.SetActive(false);
        if (completeProfilePanel != null) completeProfilePanel.SetActive(false);
        if (accountProfilePanel != null) accountProfilePanel.SetActive(false);
        if (leaderboardsPanel != null) leaderboardsPanel.SetActive(false);
        if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);

        // Sign-up sub-panels
        if (credentialsPanel != null) credentialsPanel.SetActive(false);
        if (detailsPanel != null) detailsPanel.SetActive(false);

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);

        // Also hide the ProfilePicture selector panel
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.CloseSelectionPanel();

        if (gameUI != null) gameUI.SetActive(false);
        if (puzzleUI != null) puzzleUI.SetActive(false);
        if (puzzleCompletePanel != null) puzzleCompletePanel.SetActive(false);
        if (gameCompletePanel != null) gameCompletePanel.SetActive(false);

        // Clean up terms popup and listeners
        CloseTermsPopup();
        if (termsToggle != null)
            termsToggle.onValueChanged.RemoveListener(OnTermsToggleChanged);
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
        // Redirect to PauseMenuController's Settings flow
        if (PauseMenuController.Instance != null)
            PauseMenuController.Instance.OpenSettingsFromMainMenu();
        else
            ShowOptionsPanel(); // fallback
    }

    [ContextMenu("OnExitButton")]
    public void OnExitButton()
    {
        ShowExitPopup();
    }

    public void OnBackButton()
    {
        ShowMainLoginPanel();
    }

    [ContextMenu("OnNewGameButton")]
    public void OnNewGameButton()
    {
        ShowStoryBoardPanel();
    }

    [ContextMenu("OnLoadGameButton")]
    public void OnLoadGameButton()
    {
        var player = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;
        bool hasSavedGame = player != null && (player.savedLevel > 0 || player.lastCompletedLevel > 0);

        if (!hasSavedGame)
        {
            ShowValidationMessage("NO SAVED GAME FOUND. START A NEW GAME FIRST.");
            return;
        }

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

    private void ShowValidationMessage(string message)
    {
        ShowValidationMessage(message, null);
    }

    private void ShowValidationMessage(string message, System.Action onConfirm)
    {
        validationConfirmAction = onConfirm;

        if (validationPopup != null && validationMessageText != null)
        {
            StyleValidationPopup();
            validationMessageText.text = message;
            validationPopup.SetActive(true);
            return;
        }

        Debug.LogWarning($"[UIManager] Validation popup unavailable. Message: {message}");
    }

    private void StyleValidationPopup()
    {
        if (validationPopup == null || validationMessageText == null)
            return;

        Image overlayImage = validationPopup.GetComponent<Image>();
        if (overlayImage != null)
            overlayImage.color = new Color(0.02f, 0.04f, 0.05f, 0.68f);

        Image panelImage = FindLargestChildImage(validationPopup.transform);
        Vector2 panelCenter = new Vector2(0f, 8f);
        Vector2 panelSize = new Vector2(560f, 320f);

        if (panelImage != null)
        {
            RectTransform panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = panelCenter;
            panelRect.sizeDelta = panelSize;

            panelImage.color = new Color(0.19f, 0.13f, 0.07f, 0.94f);

            Outline panelOutline = GetOrAddComponent<Outline>(panelImage.gameObject);
            panelOutline.effectColor = new Color(0.75f, 0.62f, 0.29f, 0.7f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            Shadow panelShadow = GetOrAddComponent<Shadow>(panelImage.gameObject);
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            panelShadow.effectDistance = new Vector2(0f, -8f);
        }

        TMP_Text titleText = FindDescendantText(validationPopup.transform, "ValidationTitleText");
        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.localScale = Vector3.one;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = panelCenter + new Vector2(0f, 106f);
            titleRect.sizeDelta = new Vector2(panelSize.x - 120f, 46f);
            titleText.text = "NOTICE";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 36f;
            titleText.color = new Color(0.88f, 0.76f, 0.42f, 1f);
        }

        // Reparent message into the same panel space as the title so X=0 is the true panel centre
        RectTransform messageRect = validationMessageText.rectTransform;
        Transform messageParent = (titleText != null) ? titleText.transform.parent : validationPopup.transform;
        messageRect.SetParent(messageParent, false);
        messageRect.localScale = Vector3.one;
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, 8f);
        messageRect.sizeDelta = new Vector2(panelSize.x - 120f, 96f);
        validationMessageText.alignment = TextAlignmentOptions.Center;
        validationMessageText.fontSize = 24f;
        validationMessageText.color = new Color(0.96f, 0.93f, 0.85f, 1f);
        validationMessageText.enableWordWrapping = true;
        validationMessageText.margin = new Vector4(40f, 0f, 40f, 0f);

        Button confirmButton = validationPopup.GetComponentInChildren<Button>(true);
        if (confirmButton != null)
        {
            RectTransform buttonRect = confirmButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.localScale = Vector3.one;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = panelCenter + new Vector2(0f, -108f);
                buttonRect.sizeDelta = new Vector2(170f, 46f);
            }

            Image buttonImage = confirmButton.targetGraphic as Image;
            if (buttonImage != null)
            {
                buttonImage.color = new Color(0.28f, 0.20f, 0.09f, 0.98f);

                Outline buttonOutline = GetOrAddComponent<Outline>(buttonImage.gameObject);
                buttonOutline.effectColor = new Color(0.78f, 0.66f, 0.31f, 0.9f);
                buttonOutline.effectDistance = new Vector2(1f, -1f);
            }

            ColorBlock colors = confirmButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.82f, 1f);
            colors.pressedColor = new Color(0.92f, 0.84f, 0.55f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);
            colors.fadeDuration = 0.08f;
            confirmButton.colors = colors;

            TMP_Text buttonText = confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = "OKAY";
                buttonText.alignment = TextAlignmentOptions.Center;
                buttonText.fontSize = 24f;
                buttonText.color = new Color(0.9f, 0.82f, 0.49f, 1f);
            }

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnValidationConfirmButtonClicked);
        }
    }

    private void OnValidationConfirmButtonClicked()
    {
        System.Action action = validationConfirmAction;
        validationConfirmAction = null;
        CloseValidationPopup();
        action?.Invoke();
    }

    private static Image FindLargestChildImage(Transform root)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image best = null;
        float bestArea = -1f;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.transform == root)
                continue;

            RectTransform rect = image.rectTransform;
            float area = Mathf.Abs(rect.rect.width * rect.rect.height);
            if (area > bestArea)
            {
                best = image;
                bestArea = area;
            }
        }

        return best;
    }

    private static TMP_Text FindDescendantText(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                return child.GetComponent<TMP_Text>();
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
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

        bool isGameplayScene = scene.name.StartsWith("Level") || scene.name == "SampleScene" || scene.name == "Chapter3" || scene.name == "Chapter4";

        if (isGameplayScene)
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
        else if (scene.name == "Main" || scene.name == "MainMenu")
        {
            // Returning to main menu - deactivate gameplay
            SetGameplayActive(false);
            ShowMainMenu();

            // Extra safety: ensure cursor is free for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    public void LinkWithGoogle(string email, string gid, string name)
    {
        if (AccountManager.Instance == null)
        {
            Debug.LogWarning("[UIManager] AccountManager not found. Cannot link Google account.");
            UpdateFeedback("Login service unavailable. Please retry.", Color.red);
            return;
        }

        AccountManager.Instance.LinkWithGoogle(email, gid, name);
        ShowMainMenu();
    }

    public void OnSignUpEnterPressed(string dummy)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            GoToNextSignUpPanel();
        }
    }
    public void GoToNextSignUpPanel()
    {
        // Only run when step 1 (credentialsPanel) is actually visible.
        // Prevents stale navigation when CreateAccountPanel already advanced the flow.
        if (credentialsPanel != null && !credentialsPanel.activeInHierarchy) return;

        // SAFETY CHECK
        if (validationMessageText == null || validationPopup == null)
        {
            Debug.LogError("UIManager Error: Drag the Validation objects in the Inspector!");
            return;
        }

        // 1. Check fields are filled
        if (string.IsNullOrEmpty(signUpUsername?.text?.Trim()) || string.IsNullOrEmpty(signUpPassword?.text))
        {
            validationMessageText.text = "PLEASE FILL IN ALL FIELDS!";
            validationPopup.SetActive(true);
            return;
        }

        // 2. Validate username (length, characters, profanity)
        string usernameError = SignUpValidator.ValidateUsername(signUpUsername.text.Trim());
        if (!string.IsNullOrEmpty(usernameError))
        {
            validationMessageText.text = usernameError;
            validationPopup.SetActive(true);
            return;
        }

        // 3. Validate password (8-20 chars, letters+numbers only, no specials)
        string passwordError = SignUpValidator.ValidatePassword(signUpPassword.text);
        if (!string.IsNullOrEmpty(passwordError))
        {
            validationMessageText.text = passwordError;
            validationPopup.SetActive(true);
            return;
        }

        // 4. Check confirm password is filled
        if (signUpConfirmPassword == null || string.IsNullOrEmpty(signUpConfirmPassword.text))
        {
            validationMessageText.text = "PLEASE CONFIRM YOUR PASSWORD!";
            validationPopup.SetActive(true);
            return;
        }

        // 5. Check passwords match
        if (signUpPassword.text != signUpConfirmPassword.text)
        {
            validationMessageText.text = "PASSWORDS DO NOT MATCH!";
            validationPopup.SetActive(true);
            return;
        }

        // 6. All good — go to Security Question panel (Panel 2)
        if (credentialsPanel != null) credentialsPanel.SetActive(false);
        if (detailsPanel != null) detailsPanel.SetActive(true);

        // Hide the terms scroll area by default — opened via clickable link
        if (termsScrollRect != null)
            termsScrollRect.gameObject.SetActive(false);

        // Reset terms state for this new attempt
        hasScrolledToBottom = false;
        if (termsToggle != null)
        {
            termsToggle.isOn = false;
            termsToggle.interactable = false; // Can't check until scrolled to bottom
        }
        if (confirmSignUpButton != null)
        {
            confirmSignUpButton.interactable = false; // Can't confirm until terms accepted
        }

        // Clear previous security answer
        if (securityAnswer != null) securityAnswer.text = "";

        // Make the toggle label an underlined clickable link
        SetupTermsLink();

        // Hook up terms toggle listener
        if (termsToggle != null)
        {
            termsToggle.onValueChanged.RemoveListener(OnTermsToggleChanged);
            termsToggle.onValueChanged.AddListener(OnTermsToggleChanged);
        }

        Debug.Log("[UIManager] Moved to Security Question panel");
    }

    /// <summary>
    /// Sets up ALL label children of the toggle as clickable "Terms and Conditions" links.
    /// Handles the original "Label" and any duplicates (Label (1), Label (2), etc.)
    /// </summary>
    private void SetupTermsLink()
    {
        if (termsToggle == null) return;

        // Loop through ALL children of the toggle to find every Label
        for (int i = 0; i < termsToggle.transform.childCount; i++)
        {
            Transform child = termsToggle.transform.GetChild(i);

            // Match "Label", "Label (1)", "Label (2)", etc.
            if (!child.name.StartsWith("Label")) continue;

            // Add a click handler (or reuse existing Button)
            Button linkBtn = child.GetComponent<Button>();
            if (linkBtn == null)
                linkBtn = child.gameObject.AddComponent<Button>();

            linkBtn.onClick.RemoveAllListeners();
            linkBtn.onClick.AddListener(ShowTermsPopup);

            // Make sure the label is raycast-able
            var graphic = child.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }
    }

    /// <summary>
    /// Shows the Terms & Conditions as a centered popup overlay.
    /// </summary>
    public void ShowTermsPopup()
    {
        // Destroy any existing popup
        if (termsPopupOverlay != null)
            Destroy(termsPopupOverlay);

        // Create overlay
        termsPopupOverlay = new GameObject("TermsPopupOverlay", typeof(RectTransform), typeof(CanvasRenderer));

        // Parent it to the createAccountPanel canvas so it renders on top
        Canvas parentCanvas = null;
        if (createAccountPanel != null)
            parentCanvas = createAccountPanel.GetComponentInChildren<Canvas>();
        if (parentCanvas == null && detailsPanel != null)
            parentCanvas = detailsPanel.GetComponent<Canvas>();

        if (parentCanvas != null)
            termsPopupOverlay.transform.SetParent(parentCanvas.transform, false);
        else if (detailsPanel != null)
            termsPopupOverlay.transform.SetParent(detailsPanel.transform, false);

        RectTransform overlayRT = termsPopupOverlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Semi-transparent background
        Image overlayBg = termsPopupOverlay.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0.7f);
        overlayBg.raycastTarget = true;

        // ── Popup panel (slim, centered) ──
        GameObject popup = new GameObject("TermsPanel", typeof(RectTransform));
        popup.transform.SetParent(termsPopupOverlay.transform, false);

        RectTransform popupRT = popup.GetComponent<RectTransform>();
        popupRT.anchorMin = new Vector2(0.15f, 0.08f);
        popupRT.anchorMax = new Vector2(0.85f, 0.92f);
        popupRT.offsetMin = Vector2.zero;
        popupRT.offsetMax = Vector2.zero;

        Image popupBg = popup.AddComponent<Image>();
        popupBg.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);

        // ── Title bar ──
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform));
        titleBar.transform.SetParent(popup.transform, false);
        RectTransform titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0, 1);
        titleBarRT.anchorMax = new Vector2(1, 1);
        titleBarRT.pivot = new Vector2(0.5f, 1);
        titleBarRT.sizeDelta = new Vector2(0, 40);

        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = new Color(0.2f, 0.15f, 0.1f, 1f);

        // Title text
        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(titleBar.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(10, 0);
        titleRT.offsetMax = new Vector2(-40, 0);

        TMP_Text titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "TERMS AND CONDITIONS";
        titleTxt.fontSize = 18;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
        titleTxt.color = new Color(0.9f, 0.8f, 0.5f);

        // ── Close button (X) ──
        GameObject closeGO = new GameObject("CloseBtn", typeof(RectTransform));
        closeGO.transform.SetParent(titleBar.transform, false);
        RectTransform closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 0);
        closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 0.5f);
        closeRT.sizeDelta = new Vector2(40, 0);

        TMP_Text closeTxt = closeGO.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 20;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = new Color(1f, 0.4f, 0.4f);
        closeTxt.raycastTarget = true;

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseTermsPopup);

        // ── Scroll area (below title bar) ──
        GameObject scrollArea = new GameObject("ScrollArea", typeof(RectTransform));
        scrollArea.transform.SetParent(popup.transform, false);

        RectTransform scrollAreaRT = scrollArea.GetComponent<RectTransform>();
        scrollAreaRT.anchorMin = new Vector2(0, 0);
        scrollAreaRT.anchorMax = new Vector2(1, 1);
        scrollAreaRT.offsetMin = new Vector2(5, 5);
        scrollAreaRT.offsetMax = new Vector2(-5, -40); // below title bar

        Image scrollBg = scrollArea.AddComponent<Image>();
        scrollBg.color = new Color(0.1f, 0.08f, 0.06f, 0.8f);

        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        scrollArea.AddComponent<Mask>().showMaskGraphic = true;

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(scrollArea.transform, false);

        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = new Vector2(5, 0);
        contentRT.offsetMax = new Vector2(-15, 0);

        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 4;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Body text
        GameObject bodyGO = new GameObject("TermsBody", typeof(RectTransform));
        bodyGO.transform.SetParent(content.transform, false);
        TMP_Text bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 13;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.color = new Color(0.85f, 0.8f, 0.7f);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.text =
            "<b>Terms and Conditions for Logic Labyrinth</b>\n\n" +
            "These Terms and Conditions (\u201cTerms\u201d) govern the use of the Logic Labyrinth desktop application (\u201cthe Game\u201d), an educational game designed to enhance learning and understanding of digital logic concepts. By installing or using the Game, users agree to comply with the following terms.\n\n" +

            "<b>1. Acceptance of Terms</b>\nBy accessing, installing, or playing Logic Labyrinth, you (\u201cUser\u201d or \u201cPlayer\u201d) agree to be bound by these Terms. If you do not agree, you must not install or use the Game.\n\n" +

            "<b>2. Ownership and License</b>\n\n" +
            "<b>2.1 License Grant</b>\nThe Logic Labyrinth development team grants you a limited, non-exclusive, non-transferable license to install and use the Game for personal, non-commercial, educational purposes only.\n\n" +
            "<b>2.2 Ownership</b>\nAll rights, title, and interest in the Game\u2014including its source code, design, graphics, content, and materials\u2014remain the property of the Logic Labyrinth development team. Unauthorized reproduction, distribution, or modification of the Game is strictly prohibited.\n\n" +
            "<b>2.3 Platform Limitation</b>\nThe Game is designed for Windows 10/11 (64-bit) desktop and laptop devices only. It may not function correctly on other operating systems or platforms.\n\n" +

            "<b>3. User Accounts and Data</b>\n\n" +
            "<b>3.1 Account Creation</b>\nSome features of the Game may require user registration. You are responsible for keeping your login credentials secure and for all activities performed under your account.\n\n" +
            "<b>3.2 Data Storage</b>\nThe Game may store player progress, level completion, and scores locally or through a database connection. The development team strives to maintain data integrity but does not guarantee that data will always be available or error-free.\n\n" +
            "<b>3.3 Age Restriction</b>\nThe Game is recommended for users aged 13 and above (PG rating). By using the Game, you confirm that you meet this requirement or have parental/guardian consent.\n\n" +

            "<b>4. In-Game Purchases</b>\n\n" +
            "<b>4.1 Free-to-Play Access</b>\nAll core features of Logic Labyrinth are free to ensure accessibility for students and educators.\n\n" +
            "<b>4.2 Optional Purchases</b>\nThe Game may include optional in-game purchases such as stronger flashlights, additional health, or extra hints to assist players during gameplay. These features are entirely optional and do not affect the main learning experience.\n\n" +
            "<b>4.3 Payment Methods</b>\nPayments for optional items can be made securely through supported digital payment platforms such as GCash and Maya. All payments are one-time and non-refundable, except as required by applicable laws.\n\n" +

            "<b>5. User Responsibilities</b>\nUsers agree not to:\n\n" +
            "\u2022 Use the Game for unlawful purposes.\n" +
            "\u2022 Attempt to modify, hack, or exploit the Game\u2019s systems or data.\n" +
            "\u2022 Share or distribute unauthorized copies of the Game.\n" +
            "\u2022 Engage in cheating or unfair manipulation of game features.\n\n" +
            "Failure to comply may result in the suspension or termination of access to the Game.\n\n" +

            "<b>6. Limitations and Disclaimers</b>\n\n" +
            "<b>6.1 Academic Nature</b>\nLogic Labyrinth is an academic project developed for educational use. The team provides limited maintenance and technical support within the project\u2019s defined duration.\n\n" +
            "<b>6.2 System Limitations</b>\n" +
            "\u2022 The Game is single-player only and currently does not support multiplayer functions.\n" +
            "\u2022 Leaderboard data may not be updated in real time and may refresh periodically (e.g., every two weeks).\n" +
            "\u2022 The Game requires a local or hosted database connection (MySQL) for saving progress.\n" +
            "\u2022 Visuals and performance may vary depending on device specifications.\n\n" +
            "<b>6.3 Disclaimer of Warranties</b>\nThe Game is provided \u201cas is\u201d without any express or implied warranties. The Logic Labyrinth team does not guarantee uninterrupted or error-free operation and shall not be held liable for data loss or system malfunctions.\n\n" +

            "<b>7. Support and Updates</b>\nThe team provides up to two months of post-deployment support for bug fixes, troubleshooting, and user guidance. After this period, support will be on a best-effort basis and is not guaranteed.\n\nUpdates or improvements may be released at the discretion of the development team.\n\n" +

            "<b>8. Amendments</b>\nThe Logic Labyrinth team reserves the right to update or modify these Terms at any time. Significant changes will be communicated through appropriate channels. Continued use of the Game after revisions constitutes acceptance of the new Terms.\n\n" +

            "<b>9. Termination</b>\nThe development team may suspend or terminate access to the Game at any time for users who violate these Terms or misuse the system.\n\n" +

            "<b>10. Contact Information</b>\nFor inquiries, feedback, or technical support, users may contact the Logic Labyrinth development team through their official communication channels as indicated in the project documentation.\n\n" +

            "<b>Acknowledgment</b>\n<b>By scrolling to the bottom and checking the checkbox, you confirm that you have read, understood, and agreed to these Terms and Conditions.</b>";

        scrollRect.content = contentRT;

        // Scrollbar
        GameObject scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform));
        scrollbarGO.transform.SetParent(scrollArea.transform, false);

        RectTransform scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1, 0);
        scrollbarRT.anchorMax = new Vector2(1, 1);
        scrollbarRT.pivot = new Vector2(1, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(8, 0);
        scrollbarRT.anchoredPosition = new Vector2(4, 0);

        Image scrollbarBgI = scrollbarGO.AddComponent<Image>();
        scrollbarBgI.color = new Color(0.2f, 0.18f, 0.15f, 0.5f);

        Scrollbar scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject handleArea = new GameObject("SlidingArea", typeof(RectTransform));
        handleArea.transform.SetParent(scrollbarGO.transform, false);
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = Vector2.zero;
        haRT.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.7f, 0.6f, 0.4f, 0.8f);

        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;
        scrollRect.verticalScrollbar = scrollbar;

        // Hook up scroll listener for "scrolled to bottom" detection
        scrollRect.onValueChanged.AddListener(OnTermsScrollChanged);

        Debug.Log("[UIManager] Terms popup opened");
    }

    /// <summary>
    /// Closes the terms popup overlay.
    /// </summary>
    public void CloseTermsPopup()
    {
        if (termsPopupOverlay != null)
        {
            Destroy(termsPopupOverlay);
            termsPopupOverlay = null;
        }
        Debug.Log("[UIManager] Terms popup closed");
    }

    /// <summary>
    /// Called when the Terms & Conditions scroll position changes.
    /// Enables the toggle once the user scrolls to the bottom.
    /// </summary>
    private void OnTermsScrollChanged(Vector2 scrollPos)
    {
        // scrollPos.y == 0 means scrolled to the bottom (Unity ScrollRect: 1=top, 0=bottom)
        if (scrollPos.y <= 0.05f)
        {
            hasScrolledToBottom = true;
            if (termsToggle != null)
            {
                termsToggle.interactable = true;
            }
            Debug.Log("[UIManager] Terms scrolled to bottom — checkbox enabled");
        }
    }

    /// <summary>
    /// Called when the Terms & Conditions toggle is changed.
    /// Enables/disables the Confirm button.
    /// </summary>
    private void OnTermsToggleChanged(bool isOn)
    {
        if (confirmSignUpButton != null)
        {
            confirmSignUpButton.interactable = isOn;
        }
        Debug.Log($"[UIManager] Terms toggle changed: {isOn} — Confirm button {(isOn ? "enabled" : "disabled")}");
    }

    public void ExecuteFinalSignUp()
    {
        // 1. Re-validate username and password (in case user went back and changed them)
        if (signUpUsername != null)
        {
            string usernameError = SignUpValidator.ValidateUsername(signUpUsername.text.Trim());
            if (!string.IsNullOrEmpty(usernameError))
            {
                UpdateFeedback(usernameError, Color.red);
                return;
            }
        }

        if (signUpPassword != null)
        {
            string passwordError = SignUpValidator.ValidatePassword(signUpPassword.text);
            if (!string.IsNullOrEmpty(passwordError))
            {
                UpdateFeedback(passwordError, Color.red);
                return;
            }
        }

        // 2. Must accept terms
        if (termsToggle != null && !termsToggle.isOn)
        {
            UpdateFeedback("Please accept the Terms & Conditions!", Color.red);
            return;
        }

        // 3. Must have scrolled to bottom first
        if (!hasScrolledToBottom)
        {
            UpdateFeedback("Please read the Terms & Conditions first!", Color.red);
            return;
        }

        // 4. Security answer required
        if (securityAnswer != null && string.IsNullOrEmpty(securityAnswer.text.Trim()))
        {
            UpdateFeedback("Please fill in your security answer!", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(signUpUsername.text))
        {
            UpdateFeedback("Please enter a username.", Color.red);
            if (confirmSignUpButton != null)
                confirmSignUpButton.interactable = true;
            return;
        }

        string cleanUsername = signUpUsername.text.Trim();
        string emailForAuth = cleanUsername + "@logic.com";
        string gender = genderDropdown != null ? genderDropdown.options[genderDropdown.value].text : "Not specified";
        string age = ageDropdown != null ? ageDropdown.options[ageDropdown.value].text : "Not specified";

        // Disable confirm button to prevent double-clicks
        if (confirmSignUpButton != null)
            confirmSignUpButton.interactable = false;

        if (AccountManager.Instance == null)
        {
            UpdateFeedback("Account service unavailable. Please retry.", Color.red);
            if (confirmSignUpButton != null)
                confirmSignUpButton.interactable = true;
            return;
        }

        AccountManager.Instance.CreateFullAccount(
            emailForAuth, 
            signUpPassword.text,
            securityAnswer.text,
            gender,
            age,
            (success) => {
                if (success)
                {
                    UpdateFeedback("ACCOUNT WAS SUCCESSFULLY CREATED", Color.green);
                    // Go to the LoggedIn panel after a short delay
                    Invoke("ShowMainMenu", 2f);
                }
                else
                {
                    UpdateFeedback("Signup Failed! Try again.", Color.red);
                    // Re-enable confirm button so they can retry
                    if (confirmSignUpButton != null)
                        confirmSignUpButton.interactable = true;
                }
            });
    }
    public void CloseValidationPopup()
    {
        // Itatago ang popup pagka-click ng OKAY sa loob nito
        if (validationPopup != null)
        {
            validationPopup.SetActive(false);
        }
    }

    private void UpdateFeedback(string message, Color color)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = color;
            Invoke("ClearFeedback", 3f);
        }
        Debug.Log(message);
    }
    private void ClearFeedback()
    {
        feedbackText.text = "";
    }
}
