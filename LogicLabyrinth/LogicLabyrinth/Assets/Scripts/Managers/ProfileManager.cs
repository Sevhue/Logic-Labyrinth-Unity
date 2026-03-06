using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages profile picture selection and persistence.
/// Loads all images from Resources/ProfilePictures/ and lets the user pick one.
/// The chosen picture name is saved to PlayerData.profilePicture and synced to Firebase.
/// 
/// Uses the ProfilePicture prefab (Assets/Profile/ProfilePicture.prefab) as the selection UI.
/// The prefab is instantiated at runtime when the user clicks on their profile picture
/// in the AccountProfile panel.
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    [Header("Profile Selection Panel (assign in Inspector or auto-found)")]
    public GameObject profileSelectionPanel;

    [Header("ProfilePicture Prefab (auto-loaded from Assets/Profile/)")]
    public GameObject profilePicturePrefab;

    [Header("Account Profile Panel References")]
    public Image profileDisplayImage;           // The "Default" Image in AccountProfilePanel that shows selected pic
    public Button changeProfileButton;          // Button that opens the selection panel

    // Internal
    private List<string> availablePictureNames = new List<string>();
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private string currentSelectedName;       // The saved/committed profile picture
    private string pendingSelectedName;       // Preview-only — not yet saved
    private bool hasPendingChange = false;
    private const string DEFAULT_PROFILE = "default";

    // The instantiated ProfilePicture panel
    private GameObject profilePictureInstance;
    private ProfilePictureSelector selectorComponent;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        LoadAllProfileTextures();
        LoadCurrentProfilePicture();
        AutoWireReferences();
    }

    // ──────────────────────────────────────────
    //  LOADING
    // ──────────────────────────────────────────

    private void LoadAllProfileTextures()
    {
        Texture2D[] textures = Resources.LoadAll<Texture2D>("ProfilePictures");
        availablePictureNames.Clear();
        textureCache.Clear();

        foreach (Texture2D tex in textures)
        {
            availablePictureNames.Add(tex.name);
            textureCache[tex.name] = tex;
        }

        Debug.Log($"[ProfileManager] Loaded {availablePictureNames.Count} profile pictures from Resources.");
    }

    /// <summary>
    /// Gets the current profile picture name from PlayerData (or default).
    /// </summary>
    private void LoadCurrentProfilePicture()
    {
        currentSelectedName = DEFAULT_PROFILE;

        if (AccountManager.Instance != null)
        {
            var player = AccountManager.Instance.GetCurrentPlayer();
            if (player != null && !string.IsNullOrEmpty(player.profilePicture))
            {
                currentSelectedName = player.profilePicture;
            }
        }

        RefreshProfileDisplay();
    }

    /// <summary>
    /// Auto-find and wire references if not set in Inspector.
    /// </summary>
    private void AutoWireReferences()
    {
        // Try to find the ProfilePicture prefab in the scene hierarchy first
        if (profileSelectionPanel == null)
        {
            // Check if the ProfilePicture is already instantiated in the scene
            ProfilePictureSelector existingSelector = FindAnyObjectByType<ProfilePictureSelector>(FindObjectsInactive.Include);
            if (existingSelector != null)
            {
                profileSelectionPanel = existingSelector.gameObject;
                selectorComponent = existingSelector;
            }
        }

        // Also check UIManager's reference
        if (profileSelectionPanel == null && UIManager.Instance != null && UIManager.Instance.profileSelectionPanel != null)
        {
            profileSelectionPanel = UIManager.Instance.profileSelectionPanel;
        }

        // Try to find it by name in the scene
        if (profileSelectionPanel == null)
        {
            GameObject found = GameObject.Find("ProfilePicture");
            if (found == null) found = GameObject.Find("ProfileSelectionPanel");
            if (found != null) profileSelectionPanel = found;
        }

        // Auto-find the profile display image in AccountProfilePanel
        if (profileDisplayImage == null && UIManager.Instance != null && UIManager.Instance.accountProfilePanel != null)
        {
            Transform panel = UIManager.Instance.accountProfilePanel.transform;
            
            // Search recursively for "Default" (the profile picture image in the AccountProfile prefab)
            Transform picTransform = FindDeepChild(panel, "Default");
            
            if (picTransform != null)
            {
                profileDisplayImage = picTransform.GetComponent<Image>();
                Debug.Log($"[ProfileManager] Auto-wired profileDisplayImage from '{picTransform.name}'.");

                // Also wire the button on it (add one if missing)
                changeProfileButton = picTransform.GetComponent<Button>();
                if (changeProfileButton == null)
                {
                    changeProfileButton = picTransform.gameObject.AddComponent<Button>();
                    Debug.Log("[ProfileManager] Added Button component to profile picture for selection.");
                }
            }
        }

        // Wire the change profile button to open the ProfilePicture prefab
        if (changeProfileButton != null)
        {
            changeProfileButton.onClick.RemoveAllListeners();
            changeProfileButton.onClick.AddListener(OpenSelectionPanel);
            Debug.Log("[ProfileManager] Wired ChangeProfile button → OpenSelectionPanel.");
        }
    }

    /// <summary>
    /// Reloads the current profile picture name from PlayerData. 
    /// Called by UIManager when showing the AccountProfilePanel.
    /// </summary>
    public void LoadCurrentProfileFromPlayer()
    {
        LoadCurrentProfilePicture();
        AutoWireReferences(); // Re-wire in case the panel was rebuilt
    }

    // ──────────────────────────────────────────
    //  PUBLIC API
    // ──────────────────────────────────────────

    /// <summary>
    /// Call this when the AccountProfilePanel is shown to refresh the display.
    /// </summary>
    public void RefreshProfileDisplay()
    {
        if (profileDisplayImage == null) return;

        // Show pending preview if available, otherwise the saved one
        string displayName = (hasPendingChange && !string.IsNullOrEmpty(pendingSelectedName))
            ? pendingSelectedName
            : currentSelectedName;

        Sprite sprite = GetSprite(displayName);
        if (sprite != null)
        {
            profileDisplayImage.sprite = sprite;
            profileDisplayImage.preserveAspect = true;
            profileDisplayImage.color = Color.white; // Ensure it's visible
        }
        else
        {
            Debug.LogWarning($"[ProfileManager] Could not find sprite for '{displayName}', using default.");
            Sprite defaultSprite = GetSprite(DEFAULT_PROFILE);
            if (defaultSprite != null)
            {
                profileDisplayImage.sprite = defaultSprite;
                profileDisplayImage.preserveAspect = true;
                profileDisplayImage.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Returns a Sprite for the given profile picture name.
    /// </summary>
    public Sprite GetSprite(string pictureName)
    {
        if (string.IsNullOrEmpty(pictureName)) return null;

        if (textureCache.ContainsKey(pictureName))
        {
            Texture2D tex = textureCache[pictureName];
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    /// <summary>
    /// Returns the current profile picture name.
    /// </summary>
    public string GetCurrentProfilePictureName()
    {
        return currentSelectedName;
    }

    /// <summary>
    /// Returns a Sprite for the currently selected profile.
    /// </summary>
    public Sprite GetCurrentProfileSprite()
    {
        return GetSprite(currentSelectedName);
    }

    // ──────────────────────────────────────────
    //  SELECTION PANEL (ProfilePicture Prefab)
    // ──────────────────────────────────────────

    /// <summary>
    /// Opens the profile picture selection panel.
    /// Uses the ProfilePicture prefab if available, otherwise falls back to dynamic grid.
    /// </summary>
    public void OpenSelectionPanel()
    {
        // Try to instantiate or show the ProfilePicture prefab
        if (EnsureProfilePicturePrefab())
        {
            profilePictureInstance.SetActive(true);

            // Initialize the selector with the current profile picture
            if (selectorComponent != null)
                selectorComponent.Initialize(currentSelectedName);

            Debug.Log("[ProfileManager] Opened ProfilePicture prefab panel.");
            return;
        }

        // Fallback: use the old profileSelectionPanel
        if (profileSelectionPanel != null)
        {
            profileSelectionPanel.SetActive(true);
            Debug.Log("[ProfileManager] Opened fallback selection panel.");
        }
        else
        {
            Debug.LogError("[ProfileManager] No profile selection panel available!");
        }
    }

    /// <summary>
    /// Ensures the ProfilePicture prefab instance exists. 
    /// Returns true if successfully found/instantiated.
    /// </summary>
    private bool EnsureProfilePicturePrefab()
    {
        // Already have a valid instance
        if (profilePictureInstance != null)
        {
            if (selectorComponent == null)
                selectorComponent = profilePictureInstance.GetComponent<ProfilePictureSelector>();
            return selectorComponent != null;
        }

        // Check if one already exists in the scene (inactive)
        ProfilePictureSelector[] allSelectors = FindObjectsByType<ProfilePictureSelector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sel in allSelectors)
        {
            profilePictureInstance = sel.gameObject;
            selectorComponent = sel;
            return true;
        }

        // Try to find it by name in the scene
        GameObject found = null;
        // Search all root objects including inactive ones
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = FindDeepChild(root.transform, "ProfilePicture");
            if (t != null && t.GetComponent<Canvas>() != null)
            {
                found = t.gameObject;
                break;
            }
        }

        if (found != null)
        {
            profilePictureInstance = found;
            selectorComponent = found.GetComponent<ProfilePictureSelector>();
            if (selectorComponent == null)
                selectorComponent = found.AddComponent<ProfilePictureSelector>();
            return true;
        }

        // Try to load and instantiate the prefab
        if (profilePicturePrefab == null)
        {
            // Try loading from a known path
            profilePicturePrefab = Resources.Load<GameObject>("ProfilePicture");
        }

        if (profilePicturePrefab != null)
        {
            profilePictureInstance = Instantiate(profilePicturePrefab);
            profilePictureInstance.name = "ProfilePicture";
            DontDestroyOnLoad(profilePictureInstance);

            selectorComponent = profilePictureInstance.GetComponent<ProfilePictureSelector>();
            if (selectorComponent == null)
                selectorComponent = profilePictureInstance.AddComponent<ProfilePictureSelector>();

            Debug.Log("[ProfileManager] Instantiated ProfilePicture prefab from Resources.");
            return true;
        }

        // Last resort: try to find the prefab object in the scene as an inactive panel
        // (The prefab might have been placed manually in the scene hierarchy by the user)
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in allCanvases)
        {
            if (canvas.gameObject.name == "ProfilePicture")
            {
                profilePictureInstance = canvas.gameObject;
                selectorComponent = profilePictureInstance.GetComponent<ProfilePictureSelector>();
                if (selectorComponent == null)
                    selectorComponent = profilePictureInstance.AddComponent<ProfilePictureSelector>();
                return true;
            }
        }

        Debug.LogWarning("[ProfileManager] Could not find or instantiate ProfilePicture prefab.");
        return false;
    }

    /// <summary>
    /// Closes the profile selection panel without changing the picture.
    /// </summary>
    public void CloseSelectionPanel()
    {
        if (profilePictureInstance != null)
            profilePictureInstance.SetActive(false);
        else if (profileSelectionPanel != null)
            profileSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// Called when a profile picture is clicked in the selection grid.
    /// This only sets a PREVIEW — the change is NOT saved until SavePendingChanges() is called.
    /// </summary>
    public void SelectProfilePicture(string pictureName)
    {
        pendingSelectedName = pictureName;
        hasPendingChange = true;

        Debug.Log($"[ProfileManager] Profile picture preview set to '{pictureName}' (not saved yet).");

        RefreshProfileDisplay();
    }

    /// <summary>
    /// Commits the pending profile picture selection to PlayerData and Firebase.
    /// Called when the user presses the Save/Confirm button.
    /// </summary>
    public void SavePendingChanges()
    {
        if (hasPendingChange && !string.IsNullOrEmpty(pendingSelectedName))
        {
            currentSelectedName = pendingSelectedName;

            if (AccountManager.Instance != null)
            {
                var player = AccountManager.Instance.GetCurrentPlayer();
                if (player != null)
                {
                    player.profilePicture = currentSelectedName;
                    AccountManager.Instance.SavePlayerProgress(success =>
                    {
                        if (success)
                        {
                            Debug.Log($"[ProfileManager] Profile picture saved to '{currentSelectedName}' and cloud sync completed.");
                        }
                        else
                        {
                            Debug.LogWarning($"[ProfileManager] Profile picture saved locally to '{currentSelectedName}', but cloud sync did not complete (no authenticated Firebase session).");
                        }
                    });
                }
            }
        }

        hasPendingChange = false;
        pendingSelectedName = null;

        // Refresh the display on the AccountProfile panel
        RefreshProfileDisplay();
    }

    /// <summary>
    /// Discards the pending profile picture change and reverts the display to the saved one.
    /// Called when the user presses the Back/Close button without saving.
    /// </summary>
    public void RevertPendingChanges()
    {
        if (hasPendingChange)
        {
            Debug.Log($"[ProfileManager] Reverting pending profile picture change. Keeping '{currentSelectedName}'.");
        }

        hasPendingChange = false;
        pendingSelectedName = null;
        RefreshProfileDisplay();
    }

    // ──────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────

    /// <summary>
    /// Recursively finds a child transform by name.
    /// </summary>
    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
