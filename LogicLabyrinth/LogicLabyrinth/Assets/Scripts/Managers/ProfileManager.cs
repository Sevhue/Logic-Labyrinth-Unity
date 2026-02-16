using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages profile picture selection and persistence.
/// Loads all images from Resources/ProfilePictures/ and lets the user pick one.
/// The chosen picture name is saved to PlayerData.profilePicture and synced to Firebase.
/// 
/// Attach this to a "ProfileManager" GameObject in the scene (or it will auto-create via Instance).
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    [Header("Profile Selection Panel (assign in Inspector)")]
    public GameObject profileSelectionPanel;

    [Header("Account Profile Panel References")]
    public Image profileDisplayImage;           // The "w" Image in AccountProfilePanel that shows selected pic
    public Button changeProfileButton;          // Button that opens the selection panel

    // Internal
    private List<string> availablePictureNames = new List<string>();
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private string currentSelectedName;       // The saved/committed profile picture
    private string pendingSelectedName;       // Preview-only — not yet saved
    private bool hasPendingChange = false;
    private const string DEFAULT_PROFILE = "image-removebg-preview";

    // Grid references (built dynamically)
    private Transform gridParent;
    private List<GameObject> gridItems = new List<GameObject>();

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
        // Auto-find ProfileSelectionPanel
        if (profileSelectionPanel == null && UIManager.Instance != null && UIManager.Instance.profileSelectionPanel != null)
        {
            profileSelectionPanel = UIManager.Instance.profileSelectionPanel;
        }
        if (profileSelectionPanel == null)
        {
            GameObject found = GameObject.Find("ProfileSelectionPanel");
            if (found != null) profileSelectionPanel = found;
        }

        // Auto-find the profile display image in AccountProfilePanel
        if (profileDisplayImage == null && UIManager.Instance != null && UIManager.Instance.accountProfilePanel != null)
        {
            Transform panel = UIManager.Instance.accountProfilePanel.transform;
            
            // Try "ProfilePicture" (direct child) first, then "Default" (nested in prefab)
            Transform picTransform = panel.Find("ProfilePicture");
            if (picTransform == null)
            {
                // Search recursively for "Default" (the profile picture in the prefab)
                picTransform = FindDeepChild(panel, "Default");
            }
            
            if (picTransform != null)
            {
                profileDisplayImage = picTransform.GetComponent<Image>();
                Debug.Log($"[ProfileManager] Auto-wired profileDisplayImage from {picTransform.name}.");

                // Also wire the button on it (add one if missing)
                changeProfileButton = picTransform.GetComponent<Button>();
                if (changeProfileButton == null)
                {
                    changeProfileButton = picTransform.gameObject.AddComponent<Button>();
                    Debug.Log("[ProfileManager] Added Button component to profile picture for selection.");
                }
            }
        }

        // Wire the change profile button to open selection
        if (changeProfileButton != null)
        {
            changeProfileButton.onClick.RemoveAllListeners();
            changeProfileButton.onClick.AddListener(() =>
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowProfileSelectionPanel();
                else
                    OpenSelectionPanel();
            });
            Debug.Log("[ProfileManager] Wired ChangeProfile button.");
        }

        // Wire the close button in the selection panel
        if (profileSelectionPanel != null)
        {
            Transform closeBtnTransform = profileSelectionPanel.transform.Find("CloseButton");
            if (closeBtnTransform != null)
            {
                Button closeBtn = closeBtnTransform.GetComponent<Button>();
                if (closeBtn != null)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(() =>
                    {
                        if (UIManager.Instance != null)
                            UIManager.Instance.HideProfileSelectionPanel();
                        else
                            CloseSelectionPanel();
                    });
                    Debug.Log("[ProfileManager] Wired CloseButton in ProfileSelectionPanel.");
                }
            }
        }
    }

    /// <summary>
    /// Reloads the current profile picture name from PlayerData. 
    /// Called by UIManager when showing the AccountProfilePanel.
    /// </summary>
    public void LoadCurrentProfileFromPlayer()
    {
        LoadCurrentProfilePicture();
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
            profileDisplayImage.color = Color.white; // Ensure it's visible
        }
        else
        {
            Debug.LogWarning($"[ProfileManager] Could not find sprite for '{displayName}', using default.");
            Sprite defaultSprite = GetSprite(DEFAULT_PROFILE);
            if (defaultSprite != null)
            {
                profileDisplayImage.sprite = defaultSprite;
                profileDisplayImage.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Returns a Sprite for the given profile picture name.
    /// </summary>
    public Sprite GetSprite(string pictureName)
    {
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
    //  SELECTION PANEL
    // ──────────────────────────────────────────

    /// <summary>
    /// Opens the profile picture selection panel.
    /// </summary>
    public void OpenSelectionPanel()
    {
        if (profileSelectionPanel == null)
        {
            Debug.LogError("[ProfileManager] profileSelectionPanel is not assigned!");
            return;
        }

        profileSelectionPanel.SetActive(true);
        BuildSelectionGrid();
    }

    /// <summary>
    /// Closes the profile selection panel without changing the picture.
    /// </summary>
    public void CloseSelectionPanel()
    {
        if (profileSelectionPanel != null)
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
        HighlightSelectedInGrid();
    }

    /// <summary>
    /// Commits the pending profile picture selection to PlayerData and Firebase.
    /// Called when the user presses the Save button.
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
                    AccountManager.Instance.SavePlayerProgress();
                    Debug.Log($"[ProfileManager] Profile picture saved to '{currentSelectedName}' and synced to Firebase.");
                }
            }
        }

        hasPendingChange = false;
        pendingSelectedName = null;
    }

    /// <summary>
    /// Discards the pending profile picture change and reverts the display to the saved one.
    /// Called when the user presses the Back button without saving.
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

    /// <summary>
    /// Builds the grid of selectable profile pictures inside the selection panel.
    /// </summary>
    private void BuildSelectionGrid()
    {
        // Find or create the grid parent
        if (gridParent == null)
        {
            Transform existing = profileSelectionPanel.transform.Find("Grid");
            if (existing != null)
            {
                gridParent = existing;
            }
            else
            {
                // Create a ScrollView + Grid layout
                CreateGridLayout();
            }
        }

        // Clear old items
        foreach (var item in gridItems)
        {
            if (item != null) Destroy(item);
        }
        gridItems.Clear();

        // Create one item per picture
        foreach (string picName in availablePictureNames)
        {
            CreateGridItem(picName);
        }

        HighlightSelectedInGrid();
    }

    private void CreateGridLayout()
    {
        // Create scroll view
        GameObject scrollViewGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(CanvasRenderer), typeof(Image));
        scrollViewGO.transform.SetParent(profileSelectionPanel.transform, false);
        scrollViewGO.layer = LayerMask.NameToLayer("UI");

        RectTransform scrollRT = scrollViewGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.05f, 0.15f);
        scrollRT.anchorMax = new Vector2(0.95f, 0.85f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        Image scrollBG = scrollViewGO.GetComponent<Image>();
        scrollBG.color = new Color(0.08f, 0.08f, 0.12f, 0.6f);

        // Viewport
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Mask));
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        viewportGO.layer = LayerMask.NameToLayer("UI");

        RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        Image viewportImg = viewportGO.GetComponent<Image>();
        viewportImg.color = Color.white;
        viewportGO.GetComponent<Mask>().showMaskGraphic = false;

        // Content (Grid)
        GameObject gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridGO.transform.SetParent(viewportGO.transform, false);
        gridGO.layer = LayerMask.NameToLayer("UI");

        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 1);
        gridRT.anchorMax = new Vector2(1, 1);
        gridRT.pivot = new Vector2(0.5f, 1f);
        gridRT.offsetMin = Vector2.zero;
        gridRT.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130, 130);
        grid.spacing = new Vector2(15, 15);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        ContentSizeFitter fitter = gridGO.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect
        ScrollRect scrollRect = scrollViewGO.GetComponent<ScrollRect>();
        scrollRect.content = gridRT;
        scrollRect.viewport = viewportRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        gridParent = gridGO.transform;
    }

    private void CreateGridItem(string pictureName)
    {
        // Container
        GameObject itemGO = new GameObject("ProfileItem_" + pictureName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        itemGO.transform.SetParent(gridParent, false);
        itemGO.layer = LayerMask.NameToLayer("UI");

        // Background/border
        Image itemBG = itemGO.GetComponent<Image>();
        itemBG.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        // Profile picture image (child)
        GameObject picGO = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        picGO.transform.SetParent(itemGO.transform, false);
        picGO.layer = LayerMask.NameToLayer("UI");

        RectTransform picRT = picGO.GetComponent<RectTransform>();
        picRT.anchorMin = new Vector2(0.05f, 0.05f);
        picRT.anchorMax = new Vector2(0.95f, 0.95f);
        picRT.offsetMin = Vector2.zero;
        picRT.offsetMax = Vector2.zero;

        Image picImage = picGO.GetComponent<Image>();
        Sprite sprite = GetSprite(pictureName);
        if (sprite != null)
        {
            picImage.sprite = sprite;
            picImage.preserveAspect = true;
        }

        // Name label (below the picture)
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(itemGO.transform, false);
        labelGO.layer = LayerMask.NameToLayer("UI");

        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 0.18f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelGO.GetComponent<TextMeshProUGUI>();
        // Show a clean display name
        string displayName = pictureName.Replace("image-removebg-preview", "Default")
                                        .Replace("-", " ")
                                        .Replace("_", " ");
        labelText.text = displayName;
        labelText.fontSize = 11;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 8;
        labelText.fontSizeMax = 14;

        // Button click
        Button btn = itemGO.GetComponent<Button>();
        string capturedName = pictureName; // Capture for closure
        btn.onClick.AddListener(() =>
        {
            SelectProfilePicture(capturedName);
            CloseSelectionPanel();
        });

        // Color tint transition
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.3f, 1f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.6f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.8f, 1f);
        colors.selectedColor = new Color(0.3f, 0.3f, 0.5f, 1f);
        btn.colors = colors;

        gridItems.Add(itemGO);
    }

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

    private void HighlightSelectedInGrid()
    {
        string activeName = (hasPendingChange && !string.IsNullOrEmpty(pendingSelectedName))
            ? pendingSelectedName
            : currentSelectedName;

        foreach (var item in gridItems)
        {
            if (item == null) continue;

            string itemName = item.name.Replace("ProfileItem_", "");
            Image bg = item.GetComponent<Image>();

            if (itemName == activeName)
            {
                // Highlight selected with gold border
                bg.color = new Color(1f, 0.84f, 0f, 1f); // Gold
            }
            else
            {
                bg.color = new Color(0.2f, 0.2f, 0.3f, 1f); // Normal dark
            }
        }
    }
}
