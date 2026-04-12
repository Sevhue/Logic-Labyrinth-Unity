using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages the in-game pause menu and the Options overlay.
///
/// Flow:
///   Gear icon / ESC  →  Pause prefab (Resume, Restart, Save, Options, Exit)
///   Pause > Options  →  Options prefab (Volume, Graphics)
///   Options > Back   →  Returns to Pause
///   MainLoginPanel > Options → Options prefab (from menu)
///   Options > Back   →  Returns to MainLoginPanel
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Header("Prefab References (also loaded from Resources as fallback)")]
    [Tooltip("The Pause prefab (Canvas with Resume/Restart/Save/Options/Exit)")]
    public GameObject pausePrefab;

    [Tooltip("The Options prefab (Canvas with Volume/Graphics)")]
    public GameObject settingsPrefab;

    [Tooltip("The Store prefab (Canvas). Loaded from Resources/Store as fallback.")]
    public GameObject storePrefab;

    [Header("Maya Checkout Backend")]
    [Tooltip("Your backend URL that proxies requests to Maya (never call Maya secret APIs directly from Unity).")]
    public string mayaBackendBaseUrl = "http://localhost:8787";

    [Tooltip("Where Maya should redirect after payment result. Backend should host these paths.")]
    public string mayaReturnBaseUrl = "http://localhost:8787/maya-return";

    [Tooltip("When backend is unreachable, allow a local sandbox fallback so store checkout can still proceed in development.")]
    public bool allowMayaOfflineFallback = false;

    [Tooltip("Allow manual completion button for Maya sandbox when status remains pending in development.")]
    public bool allowMayaSandboxManualConfirm = true;

    // Runtime instances
    private GameObject pauseInstance;
    private GameObject settingsInstance;
    private GameObject storeInstance;
    private GameObject storeButtonInstance;
    private TextMeshProUGUI storeDescriptionText;
    private Transform storeDescriptionRoot;
    private readonly Dictionary<string, GameObject> storeDescriptionPanels = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RectTransform> storeItemHoverRects = new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
    private string activeStoreHoverItemKey = null;
    private int storeHoverEventToken = 0;
    private string storeDefaultDescription = "";
    private readonly Dictionary<string, List<Graphic>> storeItemVisuals = new Dictionary<string, List<Graphic>>();
    private readonly Dictionary<string, decimal> storeItemPricesPhp = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        { "Lantern", 59m },
        { "Scanner", 74m },
        { "Adrenaline", 19m }
    };
    private readonly Dictionary<string, int> storeItemQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "Lantern", 1 },
        { "Scanner", 10 },
        { "Adrenaline", 5 }
    };

    private GameObject checkoutPanelInstance;
    private GameObject checkoutQrPopupInstance;
    private TextMeshProUGUI checkoutStatusText;
    private Button checkoutPayButton;
    private Button checkoutLoginButton;
    private Button checkoutOpenHostedButton;
    private Button checkoutManualSandboxButton;
    private string pendingCheckoutItemKey;
    private string pendingCheckoutReference;
    private string pendingCheckoutRedirectUrl;
    private string pendingCheckoutId;
    private bool pendingCheckoutGranted;
    private Coroutine checkoutAutoPollCoroutine;
    private bool checkoutInProgress = false;

    private const string OfflineCheckoutPrefix = "chk_offline_";

    [Serializable]
    private class MayaCreateCheckoutRequest
    {
        public MayaAmount totalAmount;
        public MayaBuyer buyer;
        public MayaRedirectUrls redirectUrl;
        public string requestReferenceNumber;
        public MayaItem[] items;
    }

    [Serializable]
    private class MayaAmount
    {
        public float value;
        public string currency;
    }

    [Serializable]
    private class MayaBuyer
    {
        public string firstName;
        public string lastName;
    }

    [Serializable]
    private class MayaRedirectUrls
    {
        public string success;
        public string failure;
        public string cancel;
    }

    [Serializable]
    private class MayaItem
    {
        public string name;
        public int quantity;
        public MayaAmount totalAmount;
    }

    [Serializable]
    private class MayaCreateCheckoutResponse
    {
        public string checkoutId;
        public string redirectUrl;
        public string status;
        public string message;
    }

    [Serializable]
    private class MayaCheckoutStatusResponse
    {
        public string checkoutId;
        public string status;
        public bool paid;
        public string message;
    }

    // Track where Options was opened from
    private enum SettingsOrigin { Pause, MainMenu }
    private SettingsOrigin settingsOrigin;

    // Quit-confirmation dialog
    private GameObject quitConfirmInstance;
    private enum QuitOrigin { PauseMenu, AppClose }
    private QuitOrigin quitOrigin = QuitOrigin.PauseMenu;
    private bool allowQuit = false; // set true right before Application.Quit() so OnWantsToQuit lets it through
    private GameObject restartConfirmInstance;

    private readonly Dictionary<string, string> storeItemDescriptions = new Dictionary<string, string>
    {
        { "Scanner", "Scanner: reveals useful puzzle clues and hidden interaction hints." },
        { "Lantern", "Lantern: improves visibility in dark areas to help exploration." },
        { "Adrenaline", "Adrenaline: temporary boost item for faster and safer movement." }
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure prefab references are valid
        EnsurePrefabs();

        // Hook into application quit so Alt+F4 / window close shows the save dialog
        Application.wantsToQuit += OnWantsToQuit;
    }

    void Update()
    {
        // ESC only works in gameplay scenes
        if (!IsGameplaySceneName(SceneManager.GetActiveScene().name)) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (storeInstance != null)
            {
                CloseStoreOverlay();
                return;
            }

            // If quit confirmation dialog is open, close it → back to Pause
            if (quitConfirmInstance != null)
            {
                OnQuitConfirm_Cancel();
                return;
            }

            if (restartConfirmInstance != null)
            {
                OnRestartConfirm_No();
                return;
            }

            // If Options overlay is open, close it → back to Pause
            if (settingsInstance != null)
            {
                CloseSettings();
                return;
            }

            if (IsPaused)
                Resume();
            else
                Pause();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Application.wantsToQuit -= OnWantsToQuit;
            Instance = null;
            IsPaused = false;
        }
    }

    /// <summary>
    /// Clean up pause state and re-wire UI when scenes load.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Always re-ensure prefabs after any scene load
        EnsurePrefabs();

        if (scene.name == "Main" || scene.name == "MainMenu")
        {
            // ── Guarantee clean state when returning to the menu ──
            IsPaused = false;
            Time.timeScale = 1f;
            HidePauseUI();
            CloseSettingsImmediate();

            // Ensure cursor is free for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Refresh player data from Firebase so Load Game has the freshest data
            if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
            {
                AccountManager.Instance.RefreshPlayerDataFromFirebase((success) =>
                {
                    if (success)
                        Debug.Log("[PauseMenu] Player data refreshed from Firebase on menu return.");
                    else
                        Debug.LogWarning("[PauseMenu] Firebase refresh failed on menu return (will retry on Load Game).");
                });
            }

            StartCoroutine(WireOptionsButtonDelayed());
            CleanupStoreHUDButton();
            CloseStoreOverlay();
        }
        else if (IsGameplaySceneName(scene.name))
        {
            StartCoroutine(EnsureStoreButtonDelayed());
        }
    }

    private System.Collections.IEnumerator WireOptionsButtonDelayed()
    {
        yield return null; // Wait one frame for scene objects to initialize
        WireMainLoginOptionsButton();
    }

    private System.Collections.IEnumerator EnsureStoreButtonDelayed()
    {
        // Wait one frame for gameplay HUD objects to initialize.
        yield return null;
        EnsureStoreHUDButton();
    }

    // ============================
    // ENSURE PREFAB REFERENCES
    // ============================

    /// <summary>
    /// Loads prefabs from Resources. Always uses Resources.Load as primary method
    /// to guarantee prefabs are available regardless of scene transitions.
    /// </summary>
    private void EnsurePrefabs()
    {
        // Always try to load from Resources — most reliable across scene transitions
        if (pausePrefab == null)
        {
            pausePrefab = Resources.Load<GameObject>("Pause");
            if (pausePrefab != null)
                Debug.Log("[PauseMenu] Loaded Pause prefab from Resources.");
            else
                Debug.LogError("[PauseMenu] FAILED to load Pause prefab! Ensure Assets/Resources/Pause.prefab exists.");
        }

        if (settingsPrefab == null)
        {
            settingsPrefab = Resources.Load<GameObject>("Options");
            if (settingsPrefab != null)
                Debug.Log("[PauseMenu] Loaded Options prefab from Resources.");
            else
                Debug.LogError("[PauseMenu] FAILED to load Options prefab! Ensure Assets/Resources/Options.prefab exists.");
        }

        if (storePrefab == null)
        {
#if UNITY_EDITOR
            // Prefer the editable prefab the user is modifying in Editor.
            storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Store/Store.prefab");
            if (storePrefab != null)
                Debug.Log("[PauseMenu] Loaded Store prefab from Assets/Store/Store.prefab (Editor).");
#endif

            if (storePrefab == null)
            {
                storePrefab = Resources.Load<GameObject>("Store");
                if (storePrefab != null)
                    Debug.Log("[PauseMenu] Loaded Store prefab from Resources.");
                else
                    Debug.LogWarning("[PauseMenu] Store prefab not found in Assets/Store or Resources/Store.");
            }
        }
    }

    private void EnsureStoreHUDButton()
    {
        if (!IsGameplaySceneName(SceneManager.GetActiveScene().name)) return;

        if (storeButtonInstance != null) return;

        EnsureEventSystem();

        Button pauseButton = FindTopRightPauseButton();
        if (pauseButton == null)
        {
            Debug.LogWarning("[PauseMenu] Could not find top-right pause button. Store button was not created.");
            return;
        }

        storeButtonInstance = Instantiate(pauseButton.gameObject, pauseButton.transform.parent);
        storeButtonInstance.name = "StoreButton_Runtime";

        RectTransform pauseRT = pauseButton.GetComponent<RectTransform>();
        RectTransform storeRT = storeButtonInstance.GetComponent<RectTransform>();
        if (pauseRT != null && storeRT != null)
        {
            // Position directly below pause button.
            storeRT.anchorMin = pauseRT.anchorMin;
            storeRT.anchorMax = pauseRT.anchorMax;
            storeRT.pivot = pauseRT.pivot;
            float yStep = Mathf.Max(60f, pauseRT.rect.height + 8f);
            storeRT.anchoredPosition = pauseRT.anchoredPosition + new Vector2(0f, -yStep);
            storeRT.sizeDelta = pauseRT.sizeDelta;
        }

        var btn = storeButtonInstance.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(ToggleStoreOverlay);
        }

        StylizeStoreHUDButton(storeButtonInstance);
    }

    private void StylizeStoreHUDButton(GameObject buttonGO)
    {
        if (buttonGO == null) return;

        // Hide cloned hamburger/icon children so we can render a clean store icon.
        for (int i = 0; i < buttonGO.transform.childCount; i++)
            buttonGO.transform.GetChild(i).gameObject.SetActive(false);

        // Keep a medieval gold tone consistent with current UI palette.
        Image bg = buttonGO.GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0.30f, 0.23f, 0.10f, 0.92f);

        // Create a centered cart + label using TMP text.
        GameObject labelGO = new GameObject("StoreLabel", typeof(RectTransform));
        labelGO.transform.SetParent(buttonGO.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.richText = true;
        label.font = TMP_Settings.defaultFontAsset;
        label.color = new Color(0.92f, 0.83f, 0.58f, 1f);
        label.text = "<size=32>\U0001F6D2</size>\n<size=11>STORE</size>";
    }

    private Button FindTopRightPauseButton()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        Button best = null;
        float bestScore = float.NegativeInfinity;

        foreach (var b in buttons)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            // Favor top-right anchored buttons (where pause icon lives).
            bool likelyTopRight = rt.anchorMin.x > 0.7f && rt.anchorMax.x > 0.7f &&
                                  rt.anchorMin.y > 0.7f && rt.anchorMax.y > 0.7f;
            if (!likelyTopRight) continue;

            float score = rt.anchorMin.x + rt.anchorMax.x + rt.anchorMin.y + rt.anchorMax.y;

            string n = b.name.ToLowerInvariant();
            if (n.Contains("pause") || n.Contains("menu") || n.Contains("option"))
                score += 1.5f;

            if (score > bestScore)
            {
                bestScore = score;
                best = b;
            }
        }

        return best;
    }

    public void ToggleStoreOverlay()
    {
        if (storeInstance != null)
        {
            CloseStoreOverlay();
            return;
        }

        // Don't open store while puzzle-related UI is active.
        if (PuzzleTableController.IsOpen || SwapGateUI.IsOpen) return;

        EnsurePrefabs();
        EnsureEventSystem();

        if (storePrefab == null)
        {
            Debug.LogError("[PauseMenu] Store prefab is null. Ensure Assets/Store/Store.prefab or Assets/Resources/Store.prefab exists.");
            return;
        }

#if UNITY_EDITOR
        Debug.Log($"[PauseMenu] Using Store prefab asset: {AssetDatabase.GetAssetPath(storePrefab)}");
#endif

        storeInstance = Instantiate(storePrefab);
        storeInstance.name = "StoreOverlay_Runtime";
        storeInstance.SetActive(true);

        Canvas canvas = storeInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 115;
        }

        if (storeInstance.GetComponent<GraphicRaycaster>() == null)
            storeInstance.AddComponent<GraphicRaycaster>();
        if (storeInstance.GetComponent<CanvasScaler>() == null)
            storeInstance.AddComponent<CanvasScaler>();

        // Prepare hover descriptions.
        SetupStoreDescriptionHover();
        EnsureStoreCloseButton();

        // Make sure player can interact with store UI.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPlayerInputEnabled(false);
    }

    private void CloseStoreOverlay()
    {
        CloseCheckoutPanel();

        if (storeInstance != null)
        {
            Destroy(storeInstance);
            storeInstance = null;
            storeDescriptionText = null;
            storeDescriptionRoot = null;
            storeDescriptionPanels.Clear();
            storeItemHoverRects.Clear();
            activeStoreHoverItemKey = null;
            storeHoverEventToken = 0;
            storeDefaultDescription = "";
            storeItemVisuals.Clear();
        }

        // Restore gameplay input only when no other blocking UI is open.
        if (!IsPaused && !PuzzleTableController.IsOpen && !SwapGateUI.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerInputEnabled(true);
        }
    }

    private void CleanupStoreHUDButton()
    {
        if (storeButtonInstance != null)
        {
            Destroy(storeButtonInstance);
            storeButtonInstance = null;
        }
    }

    private void SetupStoreDescriptionHover()
    {
        if (storeInstance == null) return;

        // Description root in prefab
        storeDescriptionRoot = DeepFind(storeInstance.transform, "Description");

        CacheStoreDescriptionPanels();

        // Optional text fallback if no panels are found
        if (storeDescriptionRoot != null)
        {
            var tmps = storeDescriptionRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmps != null && tmps.Length > 0)
            {
                TextMeshProUGUI best = tmps[0];
                foreach (var t in tmps)
                {
                    if (t != null && t.text != null && t.text.Length > best.text.Length)
                        best = t;
                }
                storeDescriptionText = best;
                storeDefaultDescription = storeDescriptionText.text;
            }
        }

        foreach (var kv in storeItemDescriptions)
        {
            Transform item = DeepFind(storeInstance.transform, kv.Key);
            if (item == null) continue;

            CacheStoreItemVisuals(kv.Key, item);
            WireStoreHoverTarget(item.gameObject, kv.Key, kv.Value);
            WireStorePurchaseTarget(item.gameObject, kv.Key);
        }
    }

    private void EnsureStoreCloseButton()
    {
        if (storeInstance == null) return;

        Transform existing = DeepFind(storeInstance.transform, "StoreCloseButton_Runtime");
        if (existing != null)
        {
            Button existingButton = existing.GetComponent<Button>();
            if (existingButton != null)
            {
                existingButton.onClick.RemoveAllListeners();
                existingButton.onClick.AddListener(CloseStoreOverlay);
            }
            return;
        }

        Transform panelTransform = DeepFind(storeInstance.transform, "Panel");
        RectTransform parentRect = panelTransform as RectTransform;
        if (parentRect == null)
            parentRect = storeInstance.GetComponent<RectTransform>();
        if (parentRect == null)
            return;

        GameObject closeButtonGO = new GameObject("StoreCloseButton_Runtime", typeof(RectTransform), typeof(Image), typeof(Button));
        closeButtonGO.transform.SetParent(parentRect, false);
        closeButtonGO.transform.SetAsLastSibling();

        RectTransform closeRT = closeButtonGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-12f, -12f);
        closeRT.sizeDelta = new Vector2(42f, 42f);

        Image closeBg = closeButtonGO.GetComponent<Image>();
        closeBg.color = new Color(0.25f, 0.12f, 0.08f, 0.95f);

        Button closeButton = closeButtonGO.GetComponent<Button>();
        closeButton.targetGraphic = closeBg;
        ColorBlock closeColors = closeButton.colors;
        closeColors.normalColor = closeBg.color;
        closeColors.highlightedColor = new Color(0.35f, 0.18f, 0.11f, 1f);
        closeColors.pressedColor = new Color(0.20f, 0.08f, 0.05f, 1f);
        closeColors.selectedColor = closeColors.highlightedColor;
        closeColors.disabledColor = new Color(0.15f, 0.10f, 0.08f, 0.5f);
        closeButton.colors = closeColors;
        closeButton.onClick.AddListener(CloseStoreOverlay);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(closeButtonGO.transform, false);

        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.text = "X";
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.87f, 0.60f, 1f);
        label.fontStyle = FontStyles.Bold;
    }

    private void AddCheckoutCloseButton(Transform parent)
    {
        if (parent == null) return;

        Transform existing = parent.Find("CheckoutCloseButton_Runtime");
        if (existing != null)
        {
            Button existingButton = existing.GetComponent<Button>();
            if (existingButton != null)
            {
                existingButton.onClick.RemoveAllListeners();
                existingButton.onClick.AddListener(CloseCheckoutPanel);
            }
            return;
        }

        GameObject closeButtonGO = new GameObject("CheckoutCloseButton_Runtime", typeof(RectTransform), typeof(Image), typeof(Button));
        closeButtonGO.transform.SetParent(parent, false);
        closeButtonGO.transform.SetAsLastSibling();

        RectTransform closeRT = closeButtonGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-10f, -10f);
        closeRT.sizeDelta = new Vector2(40f, 40f);

        Image closeBg = closeButtonGO.GetComponent<Image>();
        closeBg.color = new Color(0.20f, 0.10f, 0.08f, 0.95f);

        Button closeButton = closeButtonGO.GetComponent<Button>();
        closeButton.targetGraphic = closeBg;
        ColorBlock closeColors = closeButton.colors;
        closeColors.normalColor = closeBg.color;
        closeColors.highlightedColor = new Color(0.32f, 0.16f, 0.12f, 1f);
        closeColors.pressedColor = new Color(0.16f, 0.06f, 0.04f, 1f);
        closeColors.selectedColor = closeColors.highlightedColor;
        closeColors.disabledColor = new Color(0.13f, 0.08f, 0.07f, 0.5f);
        closeButton.colors = closeColors;
        closeButton.onClick.AddListener(CloseCheckoutPanel);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(closeButtonGO.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.text = "X";
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.88f, 0.60f, 1f);
        label.fontStyle = FontStyles.Bold;
    }

    private void WireStorePurchaseTarget(GameObject target, string itemKey)
    {
        if (target == null || string.IsNullOrWhiteSpace(itemKey)) return;

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.AddComponent<Button>();

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.93f, 0.70f, 1f);
        colors.pressedColor = new Color(0.98f, 0.84f, 0.52f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        button.onClick.AddListener(() => OpenMayaSandboxCheckout(itemKey));
    }

    private void OpenMayaSandboxCheckout(string itemKey)
    {
        if (checkoutInProgress) return;

        CloseCheckoutPanel();

        pendingCheckoutItemKey = itemKey;
        int quantity = GetStoreQuantity(itemKey);
        decimal price = GetStorePrice(itemKey);
        bool alreadyOwned = AccountManager.Instance != null && AccountManager.Instance.HasStoreItem(itemKey) && !itemKey.Equals("Adrenaline", StringComparison.OrdinalIgnoreCase);

        string reference = $"LL-{DateTime.UtcNow:HHmmssfff}";
        pendingCheckoutId = null;
        pendingCheckoutRedirectUrl = null;
        pendingCheckoutReference = reference;
        pendingCheckoutGranted = false;

        checkoutPanelInstance = CreateCheckoutBlock(
            storeInstance.transform,
            "MayaCheckoutSandbox",
            new Color(0f, 0f, 0f, 0.78f),
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);

        BuildMayaCheckoutPaymentScreen(itemKey, quantity, price, reference, alreadyOwned);
    }

    private void BuildMayaCheckoutPaymentScreen(string itemKey, int quantity, decimal price, string reference, bool alreadyOwned)
    {
        if (checkoutPanelInstance == null) return;

        ClearCheckoutChildren();

        GameObject shell = CreateCheckoutBlock(
            checkoutPanelInstance.transform,
            "CheckoutShell",
            new Color(0.06f, 0.08f, 0.11f, 1f),
            new Vector2(0.18f, 0.16f),
            new Vector2(0.82f, 0.84f),
            Vector2.zero,
            Vector2.zero);
        AddCheckoutCloseButton(shell.transform);

        TextMeshProUGUI title = CreateCheckoutText(shell.transform, "Maya Checkout", 48, FontStyles.Bold);
        title.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        SetAnchors(title.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f));

        TextMeshProUGUI line1 = CreateCheckoutText(shell.transform, itemKey, 36, FontStyles.Bold);
        line1.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        line1.alignment = TextAlignmentOptions.Center;
        SetAnchors(line1.rectTransform, new Vector2(0.10f, 0.60f), new Vector2(0.90f, 0.72f));

        TextMeshProUGUI line2 = CreateCheckoutText(shell.transform, $"Quantity: {quantity}", 24, FontStyles.Normal);
        line2.color = new Color(0.72f, 0.76f, 0.83f, 1f);
        line2.alignment = TextAlignmentOptions.Center;
        SetAnchors(line2.rectTransform, new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.60f));

        TextMeshProUGUI total = CreateCheckoutText(shell.transform, $"PHP {price:0.00}", 64, FontStyles.Bold);
        total.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        total.alignment = TextAlignmentOptions.Center;
        SetAnchors(total.rectTransform, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.50f));

        checkoutPayButton = CreateCheckoutButton(
            shell.transform,
            "Pay with Maya",
            new Vector2(0.18f, 0.15f),
            new Vector2(0.82f, 0.28f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.05f, 0.62f, 0.40f, 1f),
            Color.white,
            30f);

        checkoutStatusText = CreateCheckoutText(shell.transform, reference, 14, FontStyles.Normal);
        checkoutStatusText.color = new Color(0.50f, 0.54f, 0.60f, 1f);
        checkoutStatusText.alignment = TextAlignmentOptions.Center;
        SetAnchors(checkoutStatusText.rectTransform, new Vector2(0.10f, 0.06f), new Vector2(0.90f, 0.12f));

        if (alreadyOwned)
        {
            checkoutPayButton.interactable = false;
            if (checkoutStatusText != null)
            {
                checkoutStatusText.text = $"{itemKey} already owned";
                checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
            }
        }
        else
        {
            checkoutPayButton.onClick.AddListener(() => StartCoroutine(CreateMayaCheckoutAndOpen(itemKey, quantity, price, reference)));
        }

        checkoutOpenHostedButton = null;
        checkoutLoginButton = null;
    }

    private void ShowMayaPasskeyScreen(string itemKey, int quantity, decimal price, string reference)
    {
        if (checkoutPanelInstance == null) return;
        ClearCheckoutChildren();

        GameObject stage = CreateCheckoutBlock(
            checkoutPanelInstance.transform,
            "PasskeyStage",
            new Color(0.90f, 0.90f, 0.90f, 1f),
            new Vector2(0.05f, 0.06f),
            new Vector2(0.95f, 0.94f),
            Vector2.zero,
            Vector2.zero);
        AddCheckoutCloseButton(stage.transform);

        GameObject card = CreateCheckoutBlock(
            stage.transform,
            "PasskeyCard",
            new Color(0f, 0f, 0f, 0f),
            new Vector2(0.30f, 0.18f),
            new Vector2(0.70f, 0.84f),
            Vector2.zero,
            Vector2.zero);

        TextMeshProUGUI prompt = CreateCheckoutText(card.transform, "Provide your card's passkey below:", 20, FontStyles.Normal);
        prompt.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        prompt.alignment = TextAlignmentOptions.Left;
        SetAnchors(prompt.rectTransform, new Vector2(0f, 0.90f), new Vector2(1f, 1f));

        TextMeshProUGUI merchant = CreateCheckoutText(card.transform, "Merchant:\nDEV PORTAL", 40, FontStyles.Bold);
        merchant.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        merchant.alignment = TextAlignmentOptions.Left;
        SetAnchors(merchant.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.90f));

        TextMeshProUGUI amount = CreateCheckoutText(card.transform, $"Amount:\n{price:0.00} PHP", 40, FontStyles.Bold);
        amount.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        amount.alignment = TextAlignmentOptions.Left;
        SetAnchors(amount.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 0.68f));

        TextMeshProUGUI cardNumber = CreateCheckoutText(card.transform, "Card Number:\n4123 45XX XXXX 4443", 38, FontStyles.Bold);
        cardNumber.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        cardNumber.alignment = TextAlignmentOptions.Left;
        SetAnchors(cardNumber.rectTransform, new Vector2(0f, 0.24f), new Vector2(1f, 0.46f));

        GameObject passkeyField = CreateCheckoutBlock(
            card.transform,
            "PasskeyField",
            new Color(1f, 1f, 1f, 1f),
            new Vector2(0f, 0.12f),
            new Vector2(1f, 0.22f),
            Vector2.zero,
            Vector2.zero);
        TextMeshProUGUI passkeyPlaceholder = CreateCheckoutText(passkeyField.transform, "Passkey", 24, FontStyles.Normal);
        passkeyPlaceholder.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        passkeyPlaceholder.alignment = TextAlignmentOptions.Left;
        SetAnchors(passkeyPlaceholder.rectTransform, new Vector2(0.04f, 0.15f), new Vector2(0.40f, 0.85f));

        checkoutLoginButton = CreateCheckoutButton(
            card.transform,
            "Submit",
            new Vector2(0f, 0f),
            new Vector2(1f, 0.10f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.20f, 0.58f, 0.86f, 1f),
            Color.white,
            28f);
        checkoutLoginButton.onClick.AddListener(() => StartCoroutine(VerifyMayaCheckoutAndGrant(itemKey, quantity, price)));

        checkoutStatusText = null;
        checkoutPayButton = null;
        checkoutOpenHostedButton = null;
    }

    private void ShowMayaSuccessScreen(decimal price)
    {
        if (checkoutPanelInstance == null) return;
        ClearCheckoutChildren();

        GameObject shell = CreateCheckoutBlock(
            checkoutPanelInstance.transform,
            "SuccessShell",
            new Color(0.90f, 0.90f, 0.90f, 1f),
            new Vector2(0.05f, 0.06f),
            new Vector2(0.95f, 0.94f),
            Vector2.zero,
            Vector2.zero);
        AddCheckoutCloseButton(shell.transform);

        CreateCheckoutBlock(
            shell.transform,
            "SuccessDivider",
            new Color(0.84f, 0.84f, 0.84f, 1f),
            new Vector2(0.48f, 0.02f),
            new Vector2(0.52f, 0.98f),
            Vector2.zero,
            Vector2.zero);

        GameObject leftPane = CreateCheckoutBlock(
            shell.transform,
            "SuccessLeft",
            new Color(0f, 0f, 0f, 0f),
            new Vector2(0.04f, 0.05f),
            new Vector2(0.46f, 0.95f),
            Vector2.zero,
            Vector2.zero);

        GameObject rightPane = CreateCheckoutBlock(
            shell.transform,
            "SuccessRight",
            new Color(0f, 0f, 0f, 0f),
            new Vector2(0.54f, 0.05f),
            new Vector2(0.96f, 0.95f),
            Vector2.zero,
            Vector2.zero);

        TextMeshProUGUI successLabel = CreateCheckoutText(leftPane.transform, "Payment Successful", 30, FontStyles.Bold);
        successLabel.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        successLabel.alignment = TextAlignmentOptions.Center;
        SetAnchors(successLabel.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f));

        TextMeshProUGUI successAmount = CreateCheckoutText(leftPane.transform, $"PHP {price:0.00}", 78, FontStyles.Bold);
        successAmount.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        successAmount.alignment = TextAlignmentOptions.Center;
        SetAnchors(successAmount.rectTransform, new Vector2(0f, 0.70f), new Vector2(1f, 0.86f));

        TextMeshProUGUI merchant = CreateCheckoutText(leftPane.transform, "DEV PORTAL", 24, FontStyles.Normal);
        merchant.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        merchant.alignment = TextAlignmentOptions.Center;
        SetAnchors(merchant.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 0.72f));

        GameObject detailsCard = CreateCheckoutBlock(
            leftPane.transform,
            "TransactionDetails",
            new Color(0.93f, 0.93f, 0.94f, 1f),
            new Vector2(0.03f, 0.31f),
            new Vector2(0.97f, 0.62f),
            Vector2.zero,
            Vector2.zero);

        TextMeshProUGUI txTitle = CreateCheckoutText(detailsCard.transform, "TRANSACTION DETAILS", 18, FontStyles.Bold);
        txTitle.color = new Color(0.26f, 0.26f, 0.26f, 1f);
        txTitle.alignment = TextAlignmentOptions.Left;
        SetAnchors(txTitle.rectTransform, new Vector2(0.04f, 0.82f), new Vector2(0.80f, 0.96f));

        TextMeshProUGUI txLines = CreateCheckoutText(
            detailsCard.transform,
            "Transaction ID\nPayment Date\nPayment Method",
            18,
            FontStyles.Normal);
        txLines.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        txLines.alignment = TextAlignmentOptions.Left;
        SetAnchors(txLines.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.42f, 0.80f));

        TextMeshProUGUI txValues = CreateCheckoutText(
            detailsCard.transform,
            $"{Guid.NewGuid().ToString().Substring(0, 24)}\n{DateTime.Now:dd MMM yyyy}\nVISA 4443",
            18,
            FontStyles.Bold);
        txValues.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        txValues.alignment = TextAlignmentOptions.Right;
        SetAnchors(txValues.rectTransform, new Vector2(0.42f, 0.18f), new Vector2(0.94f, 0.80f));

        GameObject payCard = CreateCheckoutBlock(
            leftPane.transform,
            "PaymentDetails",
            new Color(0.93f, 0.93f, 0.94f, 1f),
            new Vector2(0.03f, 0.14f),
            new Vector2(0.97f, 0.28f),
            Vector2.zero,
            Vector2.zero);

        TextMeshProUGUI payDetails = CreateCheckoutText(payCard.transform, $"PAYMENT DETAILS\nTotal Amount Paid            PHP {price:0.00}", 18, FontStyles.Bold);
        payDetails.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        payDetails.alignment = TextAlignmentOptions.Left;
        SetAnchors(payDetails.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.95f, 0.92f));

        TextMeshProUGUI receiptMsg = CreateCheckoutText(rightPane.transform, "Conveniently receive your receipts by Email or SMS.", 34, FontStyles.Bold);
        receiptMsg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        receiptMsg.alignment = TextAlignmentOptions.Center;
        SetAnchors(receiptMsg.rectTransform, new Vector2(0.02f, 0.52f), new Vector2(0.98f, 0.72f));

        GameObject receiptInput = CreateCheckoutBlock(
            rightPane.transform,
            "ReceiptInput",
            Color.white,
            new Vector2(0.05f, 0.39f),
            new Vector2(0.80f, 0.48f),
            Vector2.zero,
            Vector2.zero);
        TextMeshProUGUI receiptPlaceholder = CreateCheckoutText(receiptInput.transform, "Email / PH Mobile", 24, FontStyles.Normal);
        receiptPlaceholder.color = new Color(0.62f, 0.62f, 0.62f, 1f);
        receiptPlaceholder.alignment = TextAlignmentOptions.Left;
        SetAnchors(receiptPlaceholder.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.92f, 0.88f));

        Button sendReceipt = CreateCheckoutButton(
            rightPane.transform,
            "->",
            new Vector2(0.80f, 0.39f),
            new Vector2(0.95f, 0.48f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.07f, 0.66f, 0.37f, 1f),
            Color.white,
            32f);
        sendReceipt.onClick.AddListener(CloseCheckoutPanel);

        TextMeshProUGUI mayaBusiness = CreateCheckoutText(rightPane.transform, "maya BUSINESS", 40, FontStyles.Bold);
        mayaBusiness.color = new Color(0.05f, 0.82f, 0.52f, 1f);
        mayaBusiness.alignment = TextAlignmentOptions.Center;
        SetAnchors(mayaBusiness.rectTransform, new Vector2(0.15f, 0.16f), new Vector2(0.85f, 0.24f));

        checkoutStatusText = null;
        checkoutPayButton = null;
        checkoutLoginButton = null;
        checkoutOpenHostedButton = null;
    }

    private void ClearCheckoutChildren()
    {
        if (checkoutPanelInstance == null) return;
        for (int i = checkoutPanelInstance.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(checkoutPanelInstance.transform.GetChild(i).gameObject);
        }
    }

    private GameObject CreateCheckoutBlock(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return go;
    }

    private TMPro.TextMeshProUGUI CreateCheckoutText(Transform parent, string text, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(0.95f, 0.90f, 0.72f, 1f);
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private Button CreateCheckoutButton(
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color backgroundColor,
        Color textColor,
        float fontSize)
    {
        GameObject btnGO = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image image = btnGO.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = btnGO.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.10f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.12f);
        button.colors = colors;

        TextMeshProUGUI txt = CreateCheckoutText(btnGO.transform, label, fontSize, FontStyles.Bold);
        txt.color = textColor;
        txt.alignment = TextAlignmentOptions.Center;
        SetAnchors(txt.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static bool ShouldUseOfflineFallback(string baseUrl, bool allowConfiguredFallback)
    {
        if (allowConfiguredFallback) return true;
        if (string.IsNullOrWhiteSpace(baseUrl)) return true;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
            return false;

        string host = uri.Host ?? string.Empty;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }

    private System.Collections.IEnumerator CreateMayaCheckoutAndOpen(string itemKey, int quantity, decimal price, string reference)
    {
        if (checkoutInProgress) yield break;
        checkoutInProgress = true;

        if (AccountManager.Instance == null || AccountManager.Instance.GetCurrentPlayer() == null)
        {
            checkoutInProgress = false;
            if (checkoutStatusText != null)
            {
                checkoutStatusText.text = "Please log in before starting checkout.";
                checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
            }
            yield break;
        }

        if (checkoutPayButton != null) checkoutPayButton.interactable = false;
        if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = false;

        if (checkoutStatusText != null)
        {
            checkoutStatusText.text = "Creating checkout via backend...";
            checkoutStatusText.color = new Color(0.70f, 0.84f, 1f, 1f);
        }

        string baseUrl = string.IsNullOrWhiteSpace(mayaBackendBaseUrl)
            ? "http://localhost:8787"
            : mayaBackendBaseUrl.TrimEnd('/');
        bool shouldUseOfflineFallback = ShouldUseOfflineFallback(baseUrl, allowMayaOfflineFallback);
        string createUrl = baseUrl + "/api/maya/create-checkout";

        string rrnParam = UnityWebRequest.EscapeURL(reference);
        string successUrl = mayaReturnBaseUrl.TrimEnd('/') + "/success?rrn=" + rrnParam;
        string failureUrl = mayaReturnBaseUrl.TrimEnd('/') + "/failure?rrn=" + rrnParam;
        string cancelUrl = mayaReturnBaseUrl.TrimEnd('/') + "/cancel?rrn=" + rrnParam;

        string firstName = "Test";
        string lastName = "User";
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player != null && !string.IsNullOrWhiteSpace(player.displayName))
        {
            string[] nameParts = player.displayName.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length > 0) firstName = nameParts[0];
            if (nameParts.Length > 1) lastName = nameParts[nameParts.Length - 1];
        }

        var req = new MayaCreateCheckoutRequest
        {
            totalAmount = new MayaAmount { value = (float)price, currency = "PHP" },
            buyer = new MayaBuyer { firstName = firstName, lastName = lastName },
            redirectUrl = new MayaRedirectUrls { success = successUrl, failure = failureUrl, cancel = cancelUrl },
            requestReferenceNumber = reference,
            items = new MayaItem[]
            {
                new MayaItem
                {
                    name = itemKey,
                    quantity = Mathf.Max(1, quantity),
                    totalAmount = new MayaAmount { value = (float)price, currency = "PHP" }
                }
            }
        };

        string payload = JsonUtility.ToJson(req);
        using (var request = new UnityWebRequest(createUrl, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] body = Encoding.UTF8.GetBytes(payload);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PauseMenu] Maya checkout create failed. Url={createUrl} Result={request.result} Code={request.responseCode} Error={request.error}");

                if (shouldUseOfflineFallback)
                {
                    EnterOfflineCheckoutFallback(itemKey, quantity, price, reference);
                    checkoutInProgress = false;
                    yield break;
                }

                if (checkoutStatusText != null)
                {
                    string statusCode = request.responseCode > 0 ? request.responseCode.ToString() : "no-response";
                    checkoutStatusText.text = $"Checkout create failed ({statusCode}).\\nBackend: {baseUrl}";
                    checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
                }

                if (checkoutPayButton != null) checkoutPayButton.interactable = true;
                if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
                checkoutInProgress = false;
                yield break;
            }

            string json = request.downloadHandler.text;
            MayaCreateCheckoutResponse response = null;
            try { response = JsonUtility.FromJson<MayaCreateCheckoutResponse>(json); }
            catch { }

            if (response == null || string.IsNullOrWhiteSpace(response.redirectUrl) || string.IsNullOrWhiteSpace(response.checkoutId))
            {
                if (shouldUseOfflineFallback)
                {
                    Debug.LogWarning("[PauseMenu] Maya create-checkout returned invalid payload. Entering offline checkout fallback.");
                    EnterOfflineCheckoutFallback(itemKey, quantity, price, reference);
                    checkoutInProgress = false;
                    yield break;
                }

                if (checkoutStatusText != null)
                {
                    checkoutStatusText.text = "Invalid create-checkout response from backend.";
                    checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
                }

                if (checkoutPayButton != null) checkoutPayButton.interactable = true;
                if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
                checkoutInProgress = false;
                yield break;
            }

            pendingCheckoutId = response.checkoutId;
            pendingCheckoutRedirectUrl = NormalizeMayaHostedCheckoutUrl(response.redirectUrl, response.checkoutId);

            Application.OpenURL(pendingCheckoutRedirectUrl);
            ShowAwaitingVerificationScreen(itemKey, quantity, price, reference);
            checkoutInProgress = false;
        }
    }

    private static string NormalizeMayaHostedCheckoutUrl(string redirectUrl, string checkoutId)
    {
        string id = string.IsNullOrWhiteSpace(checkoutId) ? "" : checkoutId.Trim();

        if (string.IsNullOrWhiteSpace(redirectUrl))
            return string.IsNullOrWhiteSpace(id)
                ? ""
                : $"https://payments-web-sandbox.maya.ph/v2/checkout?id={UnityWebRequest.EscapeURL(id)}";

        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri uri))
            return redirectUrl;

        string path = uri.AbsolutePath ?? "";
        if (path.IndexOf("/v2/checkout/result", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string finalId = id;
            if (string.IsNullOrWhiteSpace(finalId))
            {
                string q = uri.Query ?? "";
                int idx = q.IndexOf("id=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int start = idx + 3;
                    int end = q.IndexOf('&', start);
                    finalId = end >= 0 ? q.Substring(start, end - start) : q.Substring(start);
                }
            }

            if (!string.IsNullOrWhiteSpace(finalId))
                return $"{uri.Scheme}://{uri.Host}/v2/checkout?id={UnityWebRequest.EscapeURL(finalId)}";
        }

        return redirectUrl;
    }

    private void EnterOfflineCheckoutFallback(string itemKey, int quantity, decimal price, string reference)
    {
        pendingCheckoutId = OfflineCheckoutPrefix + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        pendingCheckoutRedirectUrl = null;

        if (checkoutStatusText != null)
        {
            checkoutStatusText.text = "Backend unavailable. Running local sandbox confirmation mode.";
            checkoutStatusText.color = new Color(1f, 0.88f, 0.55f, 1f);
        }

        ShowAwaitingVerificationScreen(itemKey, quantity, price, reference);
    }

    private bool IsOfflineCheckoutFlow()
    {
        return !string.IsNullOrWhiteSpace(pendingCheckoutId) && pendingCheckoutId.StartsWith(OfflineCheckoutPrefix, StringComparison.Ordinal);
    }

    private bool IsSandboxHostedCheckout()
    {
        return !string.IsNullOrWhiteSpace(pendingCheckoutRedirectUrl)
            && pendingCheckoutRedirectUrl.IndexOf("payments-web-sandbox.maya.ph", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool CanUseSandboxManualConfirm()
    {
        return allowMayaSandboxManualConfirm && (IsOfflineCheckoutFlow() || IsSandboxHostedCheckout());
    }

    private void CompleteSandboxCheckoutManually(string itemKey, int quantity, decimal price)
    {
        GrantPendingStoreItem(itemKey, quantity);

        if (checkoutAutoPollCoroutine != null)
        {
            StopCoroutine(checkoutAutoPollCoroutine);
            checkoutAutoPollCoroutine = null;
        }

        checkoutInProgress = false;
        ShowMayaSuccessScreen(price);
    }

    private void GrantPendingStoreItem(string itemKey, int quantity)
    {
        if (pendingCheckoutGranted) return;
        pendingCheckoutGranted = true;
        AccountManager.Instance.GrantStoreItem(itemKey, quantity);
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();
    }

    private void ShowAwaitingVerificationScreen(string itemKey, int quantity, decimal price, string reference)
    {
        if (checkoutPanelInstance == null) return;
        ClearCheckoutChildren();

        GameObject stage = CreateCheckoutBlock(
            checkoutPanelInstance.transform,
            "AwaitVerificationStage",
            new Color(0.08f, 0.10f, 0.13f, 1f),
            new Vector2(0.12f, 0.20f),
            new Vector2(0.88f, 0.80f),
            Vector2.zero,
            Vector2.zero);
        AddCheckoutCloseButton(stage.transform);

        TextMeshProUGUI title = CreateCheckoutText(stage.transform, "Processing Payment", 42, FontStyles.Bold);
        title.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.95f));

        checkoutStatusText = CreateCheckoutText(
            stage.transform,
            "Complete your payment in the browser.\nWe will automatically confirm payment and deliver your item.",
            24,
            FontStyles.Normal);
        checkoutStatusText.color = new Color(0.75f, 0.80f, 0.88f, 1f);
        checkoutStatusText.alignment = TextAlignmentOptions.Center;
        SetAnchors(checkoutStatusText.rectTransform, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.72f));

        checkoutOpenHostedButton = null;
        if (!string.IsNullOrWhiteSpace(pendingCheckoutRedirectUrl))
        {
            checkoutOpenHostedButton = CreateCheckoutButton(
                stage.transform,
                "Open Hosted Checkout Again",
                new Vector2(0.16f, 0.32f),
                new Vector2(0.84f, 0.44f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.14f, 0.31f, 0.62f, 1f),
                Color.white,
                22f);
            checkoutOpenHostedButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrWhiteSpace(pendingCheckoutRedirectUrl))
                    Application.OpenURL(pendingCheckoutRedirectUrl);
            });
        }

        checkoutLoginButton = CreateCheckoutButton(
            stage.transform,
            "Verify Now",
            new Vector2(0.16f, string.IsNullOrWhiteSpace(pendingCheckoutRedirectUrl) ? 0.24f : 0.16f),
            new Vector2(0.84f, string.IsNullOrWhiteSpace(pendingCheckoutRedirectUrl) ? 0.30f : 0.20f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.07f, 0.66f, 0.37f, 1f),
            Color.white,
            24f);
        checkoutLoginButton.onClick.AddListener(() => StartCoroutine(VerifyMayaCheckoutAndGrant(itemKey, quantity, price)));

        checkoutManualSandboxButton = null;
        if (CanUseSandboxManualConfirm())
        {
            checkoutManualSandboxButton = CreateCheckoutButton(
                stage.transform,
                "Complete in Sandbox",
                new Vector2(0.16f, 0.08f),
                new Vector2(0.84f, 0.12f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.80f, 0.58f, 0.12f, 1f),
                Color.white,
                19f);
            checkoutManualSandboxButton.onClick.AddListener(() => CompleteSandboxCheckoutManually(itemKey, quantity, price));
        }

        checkoutPayButton = null;

        if (checkoutAutoPollCoroutine != null)
            StopCoroutine(checkoutAutoPollCoroutine);
        checkoutAutoPollCoroutine = StartCoroutine(AutoVerifyMayaCheckoutAndGrant(itemKey, quantity, price));
    }

    private System.Collections.IEnumerator AutoVerifyMayaCheckoutAndGrant(string itemKey, int quantity, decimal price)
    {
        if (IsOfflineCheckoutFlow())
        {
            if (checkoutStatusText != null)
            {
                checkoutStatusText.text = "Offline sandbox mode active. Completing checkout locally...";
                checkoutStatusText.color = new Color(0.78f, 0.86f, 0.96f, 1f);
            }

            yield return new WaitForSecondsRealtime(1.5f);
            GrantPendingStoreItem(itemKey, quantity);
            checkoutInProgress = false;
            checkoutAutoPollCoroutine = null;
            ShowMayaSuccessScreen(price);
            yield break;
        }

        float elapsed = 0f;
        const float maxSeconds = 180f;

        while (checkoutPanelInstance != null && !string.IsNullOrWhiteSpace(pendingCheckoutId) && elapsed < maxSeconds)
        {
            string baseUrl = string.IsNullOrWhiteSpace(mayaBackendBaseUrl)
                ? "http://localhost:8787"
                : mayaBackendBaseUrl.TrimEnd('/');
            string statusUrl = baseUrl + "/api/maya/checkout-status/" + UnityWebRequest.EscapeURL(pendingCheckoutId);

            using (var request = UnityWebRequest.Get(statusUrl))
            {
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    MayaCheckoutStatusResponse statusResp = null;
                    try { statusResp = JsonUtility.FromJson<MayaCheckoutStatusResponse>(json); }
                    catch { }

                    if (statusResp != null && statusResp.paid)
                    {
                        GrantPendingStoreItem(itemKey, quantity);

                        checkoutInProgress = false;
                        checkoutAutoPollCoroutine = null;
                        ShowMayaSuccessScreen(price);
                        yield break;
                    }

                    if (checkoutStatusText != null && statusResp != null)
                    {
                        string statusValue = string.IsNullOrWhiteSpace(statusResp.status) ? "PENDING" : statusResp.status;
                        checkoutStatusText.text = $"Waiting for payment confirmation...\nCurrent status: {statusValue}";
                        checkoutStatusText.color = new Color(0.78f, 0.86f, 0.96f, 1f);
                    }
                }
            }

            yield return new WaitForSecondsRealtime(2f);
            elapsed += 2f;
        }

        if (checkoutStatusText != null && checkoutPanelInstance != null)
        {
            checkoutStatusText.text = "Still waiting for confirmation. You can click Verify Now to check immediately.";
            checkoutStatusText.color = new Color(1f, 0.88f, 0.55f, 1f);
        }

        checkoutAutoPollCoroutine = null;
    }

    private System.Collections.IEnumerator VerifyMayaCheckoutAndGrant(string itemKey, int quantity, decimal price)
    {
        if (checkoutInProgress) yield break;
        checkoutInProgress = true;

        if (checkoutLoginButton != null) checkoutLoginButton.interactable = false;
        if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = false;
        if (checkoutManualSandboxButton != null) checkoutManualSandboxButton.interactable = false;

        if (checkoutStatusText != null)
        {
            checkoutStatusText.text = "Verifying payment status from backend...";
            checkoutStatusText.color = new Color(0.70f, 0.84f, 1f, 1f);
        }

        if (string.IsNullOrWhiteSpace(pendingCheckoutId))
        {
            if (checkoutStatusText != null)
            {
                checkoutStatusText.text = "Missing checkoutId. Please create checkout again.";
                checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
            }
            checkoutInProgress = false;
            if (checkoutLoginButton != null) checkoutLoginButton.interactable = true;
            if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
            if (checkoutManualSandboxButton != null) checkoutManualSandboxButton.interactable = true;
            yield break;
        }

        if (IsOfflineCheckoutFlow())
        {
            if (checkoutStatusText != null)
            {
                checkoutStatusText.text = "Offline sandbox mode: payment confirmed locally.";
                checkoutStatusText.color = new Color(0.78f, 0.96f, 0.78f, 1f);
            }

            GrantPendingStoreItem(itemKey, quantity);
            if (checkoutAutoPollCoroutine != null)
            {
                StopCoroutine(checkoutAutoPollCoroutine);
                checkoutAutoPollCoroutine = null;
            }

            checkoutInProgress = false;
            ShowMayaSuccessScreen(price);
            yield break;
        }

        string baseUrl = string.IsNullOrWhiteSpace(mayaBackendBaseUrl)
            ? "http://localhost:8787"
            : mayaBackendBaseUrl.TrimEnd('/');
        string statusUrl = baseUrl + "/api/maya/checkout-status/" + UnityWebRequest.EscapeURL(pendingCheckoutId);

        using (var request = UnityWebRequest.Get(statusUrl))
        {
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (checkoutStatusText != null)
                {
                    checkoutStatusText.text = "Status check failed. Please verify backend and try again.";
                    checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
                }
                checkoutInProgress = false;
                if (checkoutLoginButton != null) checkoutLoginButton.interactable = true;
                if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
                if (checkoutManualSandboxButton != null) checkoutManualSandboxButton.interactable = true;
                yield break;
            }

            string json = request.downloadHandler.text;
            MayaCheckoutStatusResponse statusResp = null;
            try { statusResp = JsonUtility.FromJson<MayaCheckoutStatusResponse>(json); }
            catch { }

            if (statusResp == null)
            {
                if (checkoutStatusText != null)
                {
                    checkoutStatusText.text = "Could not parse status response from backend.";
                    checkoutStatusText.color = new Color(1f, 0.55f, 0.42f, 1f);
                }
                checkoutInProgress = false;
                if (checkoutLoginButton != null) checkoutLoginButton.interactable = true;
                if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
                if (checkoutManualSandboxButton != null) checkoutManualSandboxButton.interactable = true;
                yield break;
            }

            if (!statusResp.paid)
            {
                if (checkoutStatusText != null)
                {
                    string statusValue = string.IsNullOrWhiteSpace(statusResp.status) ? "PENDING" : statusResp.status;
                    checkoutStatusText.text = CanUseSandboxManualConfirm()
                        ? "Payment not completed yet. Current status: " + statusValue + "\nIf already paid in sandbox, press Complete in Sandbox."
                        : "Payment not completed yet. Current status: " + statusValue;
                    checkoutStatusText.color = new Color(1f, 0.88f, 0.55f, 1f);
                }
                checkoutInProgress = false;
                if (checkoutLoginButton != null) checkoutLoginButton.interactable = true;
                if (checkoutOpenHostedButton != null) checkoutOpenHostedButton.interactable = true;
                if (checkoutManualSandboxButton != null) checkoutManualSandboxButton.interactable = true;
                yield break;
            }

            GrantPendingStoreItem(itemKey, quantity);

            if (checkoutAutoPollCoroutine != null)
            {
                StopCoroutine(checkoutAutoPollCoroutine);
                checkoutAutoPollCoroutine = null;
            }

            checkoutInProgress = false;
            ShowMayaSuccessScreen(price);
        }
    }

    private void CloseCheckoutPanel()
    {
        checkoutInProgress = false;
        pendingCheckoutItemKey = null;
        checkoutStatusText = null;
        checkoutPayButton = null;
        checkoutLoginButton = null;
        checkoutOpenHostedButton = null;
        checkoutManualSandboxButton = null;
        pendingCheckoutReference = null;
        pendingCheckoutGranted = false;
        pendingCheckoutRedirectUrl = null;
        pendingCheckoutId = null;

        if (checkoutAutoPollCoroutine != null)
        {
            StopCoroutine(checkoutAutoPollCoroutine);
            checkoutAutoPollCoroutine = null;
        }

        if (checkoutQrPopupInstance != null)
        {
            Destroy(checkoutQrPopupInstance);
            checkoutQrPopupInstance = null;
        }

        if (checkoutPanelInstance != null)
        {
            Destroy(checkoutPanelInstance);
            checkoutPanelInstance = null;
        }
    }

    private decimal GetStorePrice(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey)) return 0m;
        return storeItemPricesPhp.TryGetValue(itemKey, out var value) ? value : 0m;
    }

    private int GetStoreQuantity(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey)) return 1;
        return storeItemQuantities.TryGetValue(itemKey, out var value) ? Mathf.Max(1, value) : 1;
    }

    private void CacheStoreDescriptionPanels()
    {
        storeDescriptionPanels.Clear();
        if (storeDescriptionRoot == null) return;

        foreach (var key in storeItemDescriptions.Keys)
        {
            Transform panel = DeepFind(storeDescriptionRoot, key);
            if (panel != null)
                storeDescriptionPanels[key] = panel.gameObject;
        }

        Debug.Log($"[PauseMenu] Store description panels found: {storeDescriptionPanels.Count}");

        // Hide all description panels by default; hover will show one.
        foreach (var p in storeDescriptionPanels.Values)
            if (p != null) p.SetActive(false);
    }

    private void ShowStoreDescriptionForItem(string itemKey, string text)
    {
        // Freeze selected description while hovering this item.
        activeStoreHoverItemKey = itemKey;

        bool shownPanel = false;
        foreach (var kv in storeDescriptionPanels)
        {
            if (kv.Value == null) continue;
            bool isMatch = string.Equals(kv.Key, itemKey, StringComparison.OrdinalIgnoreCase);
            kv.Value.SetActive(isMatch);
            if (isMatch) shownPanel = true;
        }

        // Fallback for prefabs that use a single text field instead of per-item panels.
        if (!shownPanel)
            ShowStoreDescription(text);
    }

    private void ResetStoreDescription()
    {
        foreach (var kv in storeDescriptionPanels)
            if (kv.Value != null) kv.Value.SetActive(false);

        if (storeDescriptionText != null)
            storeDescriptionText.text = storeDefaultDescription;
    }

    private void ShowStoreDescription(string text)
    {
        if (storeDescriptionText == null) return;
        if (!storeDescriptionText.gameObject.activeSelf)
            storeDescriptionText.gameObject.SetActive(true);
        if (!storeDescriptionText.enabled)
            storeDescriptionText.enabled = true;
        storeDescriptionText.text = text;
    }

    private void CacheStoreItemVisuals(string itemKey, Transform itemRoot)
    {
        if (itemRoot == null) return;
        if (storeItemVisuals.ContainsKey(itemKey)) return;

        var visuals = new List<Graphic>();
        var rootGraphic = itemRoot.GetComponent<Graphic>();
        var allGraphics = itemRoot.GetComponentsInChildren<Graphic>(true);
        foreach (var g in allGraphics)
        {
            if (g == null) continue;
            if (rootGraphic != null && g == rootGraphic) continue;
            visuals.Add(g);
        }
        storeItemVisuals[itemKey] = visuals;
    }

    private bool IsPointerOverAnyStoreItem()
    {
        foreach (var kv in storeItemHoverRects)
        {
            RectTransform rt = kv.Value;
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null))
                return true;
        }
        return false;
    }

    private void OnStoreItemHoverEnter(string itemKey, string description)
    {
        storeHoverEventToken++;
        ShowStoreDescriptionForItem(itemKey, description);
    }

    private void OnStoreItemHoverExit(string itemKey)
    {
        int token = ++storeHoverEventToken;
        StartCoroutine(HandleStoreHoverExit(token));
    }

    private System.Collections.IEnumerator HandleStoreHoverExit(int token)
    {
        // Wait one frame so enter-from-adjacent-item can fire first.
        yield return null;

        if (storeInstance == null) yield break;
        if (token != storeHoverEventToken) yield break;

        // If pointer is still over any item card, keep the current description frozen.
        if (IsPointerOverAnyStoreItem()) yield break;

        activeStoreHoverItemKey = null;
        ResetStoreDescription();
    }

    private void WireStoreHoverTarget(GameObject target, string itemKey, string description)
    {
        if (target == null) return;

        var graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            var hitbox = target.AddComponent<Image>();
            hitbox.color = new Color(1f, 1f, 1f, 0f);
            hitbox.raycastTarget = true;
        }
        else
        {
            graphic.raycastTarget = true;
        }

        var rt = target.GetComponent<RectTransform>();
        if (rt != null)
            storeItemHoverRects[itemKey] = rt;

        var childGraphics = target.GetComponentsInChildren<Graphic>(true);
        foreach (var g in childGraphics)
        {
            if (g == null) continue;
            if (g.gameObject == target) continue;
            g.raycastTarget = false;
        }

        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener((_) => OnStoreItemHoverEnter(itemKey, description));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener((_) => OnStoreItemHoverExit(itemKey));
        trigger.triggers.Add(exit);
    }

    // ============================
    // WIRE OPTIONS BUTTON ON MAIN MENU
    // ============================

    private void WireMainLoginOptionsButton()
    {
        GameObject optionsGO = null;

        // Find MainLoginPanel > OptionsButton
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = DeepFind(root.transform, "OptionsButton");
            if (found != null)
            {
                if (found.parent != null && found.parent.name == "MainLoginPanel")
                {
                    optionsGO = found.gameObject;
                    break;
                }
            }
        }

        if (optionsGO != null)
        {
            Button btn = optionsGO.GetComponent<Button>();
            if (btn != null)
            {
                // Replace entire onClick event (removes persistent + runtime listeners)
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(OpenSettingsFromMainMenu);
                Debug.Log("[PauseMenu] MainLoginPanel OptionsButton wired.");
            }
        }
    }

    // ============================
    // PAUSE / RESUME
    // ============================

    public void Pause()
    {
        if (IsPaused) return;

        // Don't open pause if puzzle or swap UI is already open
        if (PuzzleTableController.IsOpen || SwapGateUI.IsOpen) return;

        IsPaused = true;
        Time.timeScale = 0f;

        // CRITICAL: Ensure Input System processes events in Dynamic Update
        // so the UI can receive clicks while timeScale == 0
        // (FixedUpdate stops when timeScale == 0, breaking UI input)
        EnsureInputSystemDynamicUpdate();

        // Disable player input so it doesn't consume mouse/keyboard events
        SetPlayerInputEnabled(false);

        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Ensure EventSystem exists for UI interaction
        EnsureEventSystem();

        // Instantiate and show the Pause prefab
        ShowPauseUI();

        Debug.Log("[PauseMenu] Game Paused");
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        // Destroy all overlays
        HideQuitConfirmation();
        HideRestartConfirmation();
        HidePauseUI();

        // Also close Options if open
        CloseSettingsImmediate();

        // Lock cursor back
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player input
        SetPlayerInputEnabled(true);

        Debug.Log("[PauseMenu] Game Resumed");
    }

    // ============================
    // RESTART LEVEL
    // ============================

    public void RestartLevel()
    {
        ShowRestartConfirmation();
    }

    private void RestartLevelConfirmed()
    {
        Debug.Log("[PauseMenu] Restarting level...");

        IsPaused = false;
        Time.timeScale = 1f;
        HidePauseUI();
        CloseSettingsImmediate();
        SetPlayerInputEnabled(true);

        // Reset inventory
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ResetInventory();

        // Clear destroyed gates list so they respawn, and clear saved position
        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player != null)
        {
            player.destroyedGates.Clear();
            player.collectedGates.Clear();
            player.andGatesCollected = 0;
            player.orGatesCollected = 0;
            player.notGatesCollected = 0;
            player.savedGateLayout = ""; // Force fresh random gate placement on restart

            // Clear mid-level save so restart puts player at spawn point
            player.savedPosX = 0f;
            player.savedPosY = 0f;
            player.savedPosZ = 0f;
            player.savedRotY = 0f;
            player.savedLevel = 0;
        }

        // Reload the current level
        if (LevelManager.Instance != null)
        {
            int level = LevelManager.Instance.GetCurrentLevel();
            LevelManager.Instance.LoadLevel(level);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void ShowRestartConfirmation()
    {
        if (restartConfirmInstance != null) return;

        if (pauseInstance != null)
            pauseInstance.SetActive(false);

        restartConfirmInstance = new GameObject("RestartConfirmDialog");

        Canvas canvas = restartConfirmInstance.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;

        var scaler = restartConfirmInstance.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        restartConfirmInstance.AddComponent<GraphicRaycaster>();

        GameObject dimGO = CreateDialogUIObject("Dim", restartConfirmInstance.transform);
        RectTransform dimRT = dimGO.GetComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        Image dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        dimImg.raycastTarget = true;

        GameObject panelGO = CreateDialogUIObject("Panel", restartConfirmInstance.transform);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.30f, 0.32f);
        panelRT.anchorMax = new Vector2(0.70f, 0.68f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.08f, 0.06f, 0.03f, 0.96f);

        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.75f, 0.58f, 0.22f, 0.9f);
        panelOutline.effectDistance = new Vector2(3f, 3f);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        GameObject titleGO = CreateDialogUIObject("Title", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.70f);
        titleRT.anchorMax = new Vector2(0.95f, 0.93f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        titleGO.AddComponent<CanvasRenderer>();

        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "RESTART LEVEL";
        titleTMP.fontSize = 32;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.95f, 0.82f, 0.35f, 1f);
        if (font != null) titleTMP.font = font;

        GameObject msgGO = CreateDialogUIObject("Message", panelGO.transform);
        RectTransform msgRT = msgGO.GetComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.08f, 0.44f);
        msgRT.anchorMax = new Vector2(0.92f, 0.70f);
        msgRT.offsetMin = Vector2.zero;
        msgRT.offsetMax = Vector2.zero;
        msgGO.AddComponent<CanvasRenderer>();

        TextMeshProUGUI msgTMP = msgGO.AddComponent<TextMeshProUGUI>();
        msgTMP.text = "Are you sure you want to restart this level?\nUnsaved progress in this run will be lost.";
        msgTMP.fontSize = 21;
        msgTMP.alignment = TextAlignmentOptions.Center;
        msgTMP.color = new Color(0.88f, 0.82f, 0.65f, 1f);
        msgTMP.enableWordWrapping = true;
        if (font != null) msgTMP.font = font;

        float btnY0 = 0.10f, btnY1 = 0.34f;

        CreateDialogButton(panelGO.transform, "Yes, Restart", font,
            new Vector2(0.08f, btnY0), new Vector2(0.46f, btnY1),
            new Color(0.65f, 0.22f, 0.18f, 1f),
            () => OnRestartConfirm_Yes());

        CreateDialogButton(panelGO.transform, "No", font,
            new Vector2(0.54f, btnY0), new Vector2(0.92f, btnY1),
            new Color(0.35f, 0.30f, 0.18f, 1f),
            () => OnRestartConfirm_No());

        Debug.Log("[PauseMenu] Restart confirmation dialog shown.");
    }

    private void HideRestartConfirmation()
    {
        if (restartConfirmInstance != null)
        {
            Destroy(restartConfirmInstance);
            restartConfirmInstance = null;
        }
    }

    private void OnRestartConfirm_Yes()
    {
        HideRestartConfirmation();
        RestartLevelConfirmed();
    }

    private void OnRestartConfirm_No()
    {
        HideRestartConfirmation();
        if (pauseInstance != null)
            pauseInstance.SetActive(true);
    }

    // ============================
    // SAVE GAME
    // ============================

    public void SaveGame()
    {
        SaveGameInternal(null);
    }

    /// <summary>
    /// Save game with an optional callback that fires after Firebase write completes.
    /// </summary>
    private void SaveGameInternal(System.Action<bool> onComplete)
    {
        Debug.Log("[PauseMenu] ====== SAVE GAME START ======");

        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player == null)
        {
            Debug.LogWarning("[PauseMenu] No player data to save! (AccountManager or currentPlayer is null)");
            ShowSaveFeedback(false);
            onComplete?.Invoke(false);
            return;
        }

        // Find the player GameObject that has the CharacterController (the one that actually moves).
        // IMPORTANT: There are multiple objects tagged "Player" in level scenes — the parent 
        // (FirstPersonPlayer) stays at the spawn point forever, while the child (PlayerCapsule)
        // with the CharacterController is the one that actually moves with the player.
        GameObject playerGO = FindPlayerWithCharacterController();
        if (playerGO != null)
        {
            Vector3 pos = playerGO.transform.position;
            float rotY = playerGO.transform.eulerAngles.y;

            player.savedPosX = pos.x;
            player.savedPosY = pos.y;
            player.savedPosZ = pos.z;
            player.savedRotY = rotY;

            Debug.Log($"[PauseMenu] Captured player position: ({pos.x:F2},{pos.y:F2},{pos.z:F2}), rotY={rotY:F1}");
            Debug.Log($"[PauseMenu] Player GO name='{playerGO.name}', active={playerGO.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[PauseMenu] Player GameObject with CharacterController NOT FOUND! Position will NOT be saved!");
        }

        // Save current level
        if (LevelManager.Instance != null)
        {
            player.savedLevel = LevelManager.Instance.GetCurrentLevel();
            Debug.Log($"[PauseMenu] savedLevel set to: {player.savedLevel} (from LevelManager.GetCurrentLevel)");
        }
        else
        {
            Debug.LogError("[PauseMenu] LevelManager.Instance is null! savedLevel NOT updated (currently {player.savedLevel})");
        }

        // Sync inventory counts
        if (InventoryManager.Instance != null)
        {
            player.andGatesCollected = InventoryManager.Instance.GetGateCount("AND");
            player.orGatesCollected = InventoryManager.Instance.GetGateCount("OR");
            player.notGatesCollected = InventoryManager.Instance.GetGateCount("NOT");
        }

        Debug.Log($"[PauseMenu] About to write to Firebase: savedPosX={player.savedPosX:F2}, savedPosY={player.savedPosY:F2}, savedPosZ={player.savedPosZ:F2}, savedRotY={player.savedRotY:F1}, savedLevel={player.savedLevel}");
        Debug.Log($"[PauseMenu] destroyedGates count: {player.destroyedGates.Count}");
        if (player.destroyedGates.Count > 0)
        {
            foreach (var id in player.destroyedGates)
                Debug.Log($"[PauseMenu]   destroyedGate: '{id}'");
        }

        // Save to Firebase — wait for completion before showing feedback
        AccountManager.Instance.SavePlayerProgress((success) =>
        {
            ShowSaveFeedback(success);

            if (success)
            {
                Debug.Log($"[PauseMenu] ====== SAVE GAME SUCCESS ======");
                Debug.Log($"[PauseMenu] Firebase confirmed write: Pos=({player.savedPosX:F2},{player.savedPosY:F2},{player.savedPosZ:F2}), Level={player.savedLevel}");
            }
            else
            {
                Debug.LogError("[PauseMenu] ====== SAVE GAME FAILED ======");
            }

            onComplete?.Invoke(success);
        });
    }

    // ============================
    // EXIT TO MENU (→ LoggedInPanel)
    // ============================

    public void ExitToMenu()
    {
        Debug.Log("[PauseMenu] Exiting to main menu (LoggedInPanel)...");

        // Save before exiting — wait for Firebase to confirm before switching scene
        SaveGameInternal((success) =>
        {
            if (success)
                Debug.Log("[PauseMenu] Save confirmed before exit — Load Game will restore from this save.");
            else
                Debug.LogWarning("[PauseMenu] Save may not have completed, exiting anyway.");

            // ── Clean up all pause / gameplay state ──
            IsPaused = false;
            Time.timeScale = 1f;
            HidePauseUI();
            CloseSettingsImmediate();

            // DO NOT call SetPlayerInputEnabled(true) here — that sets cursorLocked=true
            // which causes StarterAssetsInputs.OnApplicationFocus to re-lock the cursor.
            // Instead, disable PlayerInput and force cursorLocked=false so the cursor stays free.
            EnsureCursorUnlockedForMenu();

            // Reset inventory so the menu starts clean
            // (ContinueGame re-syncs from Firebase when the player clicks Load Game)
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetInventory();

            // Load main menu scene.
            // UIManager.OnSceneLoaded("Main") → ShowMainMenu() → loggedInPanel
            SceneManager.LoadScene("Main");
        });
    }

    // ============================
    // PAUSE UI (instantiate / destroy)
    // ============================

    private void ShowPauseUI()
    {
        if (pauseInstance != null)
        {
            Debug.Log("[PauseMenu] pauseInstance already exists, skipping.");
            return;
        }

        EnsurePrefabs();

        if (pausePrefab == null)
        {
            Debug.LogError("[PauseMenu] pausePrefab is NULL — cannot show pause menu!");
            return;
        }

        Debug.Log($"[PauseMenu] Instantiating Pause prefab: {pausePrefab.name}");
        pauseInstance = Instantiate(pausePrefab);
        pauseInstance.name = "PauseMenu_Runtime";

        // Make sure root is active
        pauseInstance.SetActive(true);

        // Activate the inner PausePanel child (starts inactive in the prefab)
        Transform panel = pauseInstance.transform.Find("PausePanel");
        if (panel != null)
        {
            panel.gameObject.SetActive(true);

            // Use a subtle neutral dim overlay instead of the heavy brown fullscreen sprite.
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = null;
                panelImage.color = new Color(0f, 0f, 0f, 0.22f);
            }

            Debug.Log("[PauseMenu] PausePanel child activated.");
        }
        else
        {
            // Fallback: try to activate all children
            Debug.LogWarning("[PauseMenu] PausePanel child not found, activating all children.");
            foreach (Transform child in pauseInstance.transform)
            {
                child.gameObject.SetActive(true);
            }
        }

        // Ensure Canvas is set up properly
        Canvas canvas = pauseInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            Debug.Log($"[PauseMenu] Canvas renderMode={canvas.renderMode}, sortOrder={canvas.sortingOrder}");
        }
        else
        {
            Debug.LogError("[PauseMenu] No Canvas component on pause prefab root!");
        }

        // Wire buttons
        WirePauseButtons();

        Debug.Log("[PauseMenu] Pause UI shown successfully.");
    }

    private void HidePauseUI()
    {
        if (pauseInstance != null)
        {
            Destroy(pauseInstance);
            pauseInstance = null;
            Debug.Log("[PauseMenu] Pause UI destroyed.");
        }
    }

    /// <summary>
    /// Wires Resume/Restart/Save/Options/Exit buttons on the instantiated Pause UI.
    /// </summary>
    private void WirePauseButtons()
    {
        if (pauseInstance == null) return;

        WireChildButton(pauseInstance, "Resume", Resume);
        WireChildButton(pauseInstance, "Restart", RestartLevel);
        WireChildButton(pauseInstance, "Save", SaveGame);
        WireChildButton(pauseInstance, "OPTIONS", OpenSettingsFromPause);
        WireChildButton(pauseInstance, "Exit", () => ShowQuitConfirmation(QuitOrigin.PauseMenu));
    }

    private void WireChildButton(GameObject root, string childName, UnityEngine.Events.UnityAction action)
    {
        Transform child = DeepFind(root.transform, childName);
        if (child != null)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(action);
                Debug.Log($"[PauseMenu] Wired button: {childName} (interactable={btn.interactable})");
            }
        }
        else
        {
            Debug.LogWarning($"[PauseMenu] Button '{childName}' not found in pause UI.");
        }
    }

    // ============================
    // OPTIONS OVERLAY
    // ============================

    private void OpenSettingsFromPause()
    {
        settingsOrigin = SettingsOrigin.Pause;

        // Hide Pause UI (but keep game paused)
        if (pauseInstance != null)
            pauseInstance.SetActive(false);

        ShowSettingsOverlay();
    }

    public void OpenSettingsFromMainMenu()
    {
        settingsOrigin = SettingsOrigin.MainMenu;
        EnsureEventSystem();
        ShowSettingsOverlay();
    }

    private void ShowSettingsOverlay()
    {
        if (settingsInstance != null) return;

        EnsurePrefabs();

        if (settingsPrefab == null)
        {
            Debug.LogError("[PauseMenu] settingsPrefab (Options) is null!");
            return;
        }

        settingsInstance = Instantiate(settingsPrefab);
        settingsInstance.SetActive(true);
        settingsInstance.name = "OptionsOverlay_Runtime";

        // Ensure Canvas is set up properly (ScreenSpaceOverlay, high sortOrder)
        Canvas canvas = settingsInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; // Above the Pause UI (100)
            Debug.Log($"[PauseMenu] Options Canvas renderMode={canvas.renderMode}, sortOrder={canvas.sortingOrder}");
        }

        // Ensure GraphicRaycaster exists
        if (settingsInstance.GetComponent<GraphicRaycaster>() == null)
            settingsInstance.AddComponent<GraphicRaycaster>();

        // Wire BackButton
        WireSettingsBackButton();

        // Wire Volume toggles (Music, SFX)
        WireVolumeSliders();

        // Wire Quality buttons (Low, Medium, High, Ultra)
        WireQualityButtons();

        Debug.Log("[PauseMenu] Options overlay opened.");
    }

    // ---- Quality tooltip ----
    private GameObject qualityTooltip;
    private TextMeshProUGUI qualityTooltipText;

    /// <summary>
    /// Creates and wires themed Music/SFX volume sliders in the existing Options prefab panel.
    /// </summary>
    private void WireVolumeSliders()
    {
        if (settingsInstance == null) return;

        Transform panelRoot = DeepFind(settingsInstance.transform, "Panel") ?? settingsInstance.transform;
        if (panelRoot == null) return;
        Transform existingVolumeRoot = DeepFind(panelRoot, "VolumeControlsRoot");
        if (existingVolumeRoot != null)
            Destroy(existingVolumeRoot.gameObject);

        GameObject volumeRootGO = new GameObject("VolumeControlsRoot", typeof(RectTransform));
        Transform volumeRoot = volumeRootGO.transform;
        volumeRoot.SetParent(panelRoot, false);
        if (volumeRoot.parent == null) return;

        RectTransform rootRT = volumeRootGO.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.13f, 0.54f);
        rootRT.anchorMax = new Vector2(0.87f, 0.74f);
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        CreateVolumeRow(volumeRoot, "Music", new Vector2(0f, 0.52f), new Vector2(1f, 1f),
            AudioManager.Instance != null ? AudioManager.Instance.GetMusicVolume() : 1f,
            value =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetMusicVolume(value);
                }
            });

        CreateVolumeRow(volumeRoot, "SFX", new Vector2(0f, 0f), new Vector2(1f, 0.46f),
            AudioManager.Instance != null ? AudioManager.Instance.GetSFXVolume() : 1f,
            value =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetSFXVolume(value);
                }
            });

        Debug.Log("[PauseMenu] Volume sliders wired successfully.");
    }

    private void CreateVolumeRow(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject($"{label}VolumeRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = anchorMin;
        rowRT.anchorMax = anchorMax;
        rowRT.offsetMin = Vector2.zero;
        rowRT.offsetMax = Vector2.zero;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(row.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(0.30f, 1f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label.ToUpperInvariant();
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.90f, 0.82f, 0.56f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(row.transform, false);
        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.34f, 0.2f);
        sliderRT.anchorMax = new Vector2(1f, 0.8f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        Slider slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(initialValue);

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bg = bgGO.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.12f, 0.07f, 0.95f);

        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0f);
        fillAreaRT.anchorMax = new Vector2(1f, 1f);
        fillAreaRT.offsetMin = new Vector2(8f, 6f);
        fillAreaRT.offsetMax = new Vector2(-16f, -6f);

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fill = fillGO.GetComponent<Image>();
        fill.color = new Color(0.76f, 0.60f, 0.20f, 0.95f);

        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = new Vector2(0f, 0f);
        handleAreaRT.anchorMax = new Vector2(1f, 1f);
        handleAreaRT.offsetMin = new Vector2(8f, 0f);
        handleAreaRT.offsetMax = new Vector2(-8f, 0f);

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(10f, 20f);
        Image handle = handleGO.GetComponent<Image>();
        handle.color = new Color(0.95f, 0.85f, 0.55f, 1f);

        slider.targetGraphic = handle;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = false;

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(onChanged);
    }

    /// <summary>
    /// Wires the Low / Medium / High / Ultra quality buttons, highlights the active one,
    /// and attaches hover tooltips that describe each preset.
    /// </summary>
    private void WireQualityButtons()
    {
        if (settingsInstance == null) return;

        // Ensure QualitySettingsManager exists
        if (QualitySettingsManager.Instance == null)
        {
            GameObject qmGO = new GameObject("QualitySettingsManager");
            qmGO.AddComponent<QualitySettingsManager>();
            Debug.Log("[PauseMenu] Created QualitySettingsManager at runtime.");
        }

        // Build the tooltip panel once
        CreateQualityTooltip();

        WireQualityButton("Low",    QualitySettingsManager.QualityPreset.Low);
        WireQualityButton("Medium", QualitySettingsManager.QualityPreset.Medium);
        WireQualityButton("High",   QualitySettingsManager.QualityPreset.High);
        WireQualityButton("Ultra",  QualitySettingsManager.QualityPreset.Ultra);

        // Highlight the currently active button
        HighlightActiveQualityButton();
    }

    private void WireQualityButton(string buttonName, QualitySettingsManager.QualityPreset preset)
    {
        Transform btnTransform = DeepFind(settingsInstance.transform, buttonName);
        if (btnTransform == null)
        {
            Debug.LogWarning($"[PauseMenu] Quality button '{buttonName}' not found in Options UI!");
            return;
        }

        Button btn = btnTransform.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogWarning($"[PauseMenu] '{buttonName}' has no Button component!");
            return;
        }

        btn.interactable = true;
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(() =>
        {
            QualitySettingsManager.Instance.SetQuality(preset);
            HighlightActiveQualityButton();
            Debug.Log($"[PauseMenu] Quality set to {preset}");
        });

        // --- Hover tooltip ---
        AddHoverTooltip(btnTransform.gameObject, preset);
    }

    /// <summary>
    /// Visually highlights the currently active quality button and dims the rest.
    /// </summary>
    private void HighlightActiveQualityButton()
    {
        if (settingsInstance == null || QualitySettingsManager.Instance == null) return;

        var current = QualitySettingsManager.Instance.CurrentPreset;
        string[] buttonNames = { "Low", "Medium", "High", "Ultra" };

        for (int i = 0; i < buttonNames.Length; i++)
        {
            Transform btnTransform = DeepFind(settingsInstance.transform, buttonNames[i]);
            if (btnTransform == null) continue;

            Button btn = btnTransform.GetComponent<Button>();
            if (btn == null) continue;

            Image img = btn.GetComponent<Image>();
            if (img == null) continue;

            bool isActive = (int)current == i;

            // Active button gets a bright highlight, others get a dimmed look
            if (isActive)
            {
                // Bright green/teal highlight
                img.color = new Color(0.3f, 0.85f, 0.5f, 1f);
            }
            else
            {
                // Default dimmed color
                img.color = new Color(1f, 1f, 1f, 0.6f);
            }

            // Also update the button's text color for clarity
            TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null)
            {
                txt.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }
    }

    // ============================
    // QUALITY TOOLTIP
    // ============================

    /// <summary>
    /// Builds a floating tooltip panel that lives inside the Options overlay canvas.
    /// Hidden by default; shown on hover over a quality button.
    /// </summary>
    private void CreateQualityTooltip()
    {
        if (settingsInstance == null) return;

        // Find the panel that holds the quality buttons so we can parent the tooltip nearby
        Transform panel = DeepFind(settingsInstance.transform, "Panel");
        Transform tooltipParent = panel != null ? panel : settingsInstance.transform;

        // --- Container ---
        qualityTooltip = new GameObject("QualityTooltip");
        qualityTooltip.transform.SetParent(tooltipParent, false);

        RectTransform rt = qualityTooltip.AddComponent<RectTransform>();
        // Anchor to bottom-center of the panel
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -8f);
        rt.sizeDelta = new Vector2(0f, 120f); // full width of panel, 120px tall

        // Background image
        Image bg = qualityTooltip.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // --- Text ---
        GameObject textGO = new GameObject("TooltipText");
        textGO.transform.SetParent(qualityTooltip.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 8f);   // left, bottom padding
        textRT.offsetMax = new Vector2(-12f, -8f);  // right, top padding

        qualityTooltipText = textGO.AddComponent<TextMeshProUGUI>();
        qualityTooltipText.fontSize = 14;
        qualityTooltipText.color = new Color(0.9f, 0.92f, 0.85f, 1f);
        qualityTooltipText.alignment = TMPro.TextAlignmentOptions.TopLeft;
        qualityTooltipText.enableWordWrapping = true;
        qualityTooltipText.overflowMode = TMPro.TextOverflowModes.Truncate;
        qualityTooltipText.text = "";

        // Start hidden
        qualityTooltip.SetActive(false);
    }

    /// <summary>
    /// Adds PointerEnter / PointerExit events to a quality button so
    /// the tooltip appears on hover.
    /// </summary>
    private void AddHoverTooltip(GameObject buttonGO, QualitySettingsManager.QualityPreset preset)
    {
        // Get or add an EventTrigger
        UnityEngine.EventSystems.EventTrigger trigger = buttonGO.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
            trigger = buttonGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // --- Pointer Enter ---
        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((_) => ShowQualityTooltip(preset));
        trigger.triggers.Add(enterEntry);

        // --- Pointer Exit ---
        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((_) => HideQualityTooltip());
        trigger.triggers.Add(exitEntry);
    }

    private void ShowQualityTooltip(QualitySettingsManager.QualityPreset preset)
    {
        if (qualityTooltip == null || qualityTooltipText == null) return;

        qualityTooltipText.text = GetQualityDescription(preset);
        qualityTooltip.SetActive(true);
    }

    private void HideQualityTooltip()
    {
        if (qualityTooltip != null)
            qualityTooltip.SetActive(false);
    }

    /// <summary>
    /// Returns a human-readable description of what each quality preset does.
    /// </summary>
    private string GetQualityDescription(QualitySettingsManager.QualityPreset preset)
    {
        switch (preset)
        {
            case QualitySettingsManager.QualityPreset.Low:
                return "<b>LOW</b>  —  Best for older / low-end PCs\n" +
                       "• Shadows: <color=#FF6B6B>Off</color>\n" +
                       "• Textures: Half resolution\n" +
                       "• Anti-Aliasing: None\n" +
                       "• Effects: Minimal  |  Target: 60 FPS";

            case QualitySettingsManager.QualityPreset.Medium:
                return "<b>MEDIUM</b>  —  Balanced performance & visuals\n" +
                       "• Shadows: <color=#FFD93D>Hard</color>  (40m range)\n" +
                       "• Textures: Full resolution\n" +
                       "• Anti-Aliasing: None\n" +
                       "• Effects: Standard  |  VSync On";

            case QualitySettingsManager.QualityPreset.High:
                return "<b>HIGH</b>  —  Great visuals, smooth gameplay\n" +
                       "• Shadows: <color=#6BCB77>Soft</color>  (70m range)\n" +
                       "• Textures: Full + Anisotropic filtering\n" +
                       "• Anti-Aliasing: 2x MSAA\n" +
                       "• Effects: Particles, reflections  |  VSync On";

            case QualitySettingsManager.QualityPreset.Ultra:
                return "<b>ULTRA</b>  —  Maximum quality (needs a good GPU)\n" +
                       "• Shadows: <color=#4D96FF>Soft + 4 cascades</color>  (120m range)\n" +
                       "• Textures: Full + Forced Anisotropic\n" +
                       "• Anti-Aliasing: 4x MSAA\n" +
                       "• Effects: All on  |  Capped at 120 FPS";

            default:
                return "";
        }
    }

    private void WireSettingsBackButton()
    {
        if (settingsInstance == null) return;

        Transform backBtn = DeepFind(settingsInstance.transform, "BackButton");
        if (backBtn != null)
        {
            Button btn = backBtn.GetComponent<Button>();
            if (btn != null)
            {
                RectTransform backRect = backBtn as RectTransform;
                if (backRect != null)
                {
                    RectTransform panelRect = DeepFind(settingsInstance.transform, "Panel") as RectTransform;
                    if (panelRect != null && backRect.parent != panelRect)
                        backRect.SetParent(panelRect, false);

                    backRect.anchorMin = new Vector2(1f, 1f);
                    backRect.anchorMax = new Vector2(1f, 1f);
                    backRect.pivot = new Vector2(1f, 1f);
                    backRect.anchoredPosition = new Vector2(-10f, -10f);
                    backRect.sizeDelta = new Vector2(54f, 40f);
                }

                // Hide any existing text labels under BackButton.
                TextMeshProUGUI[] tmpLabels = backBtn.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < tmpLabels.Length; i++)
                    tmpLabels[i].enabled = false;

                Text[] legacyLabels = backBtn.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < legacyLabels.Length; i++)
                    legacyLabels[i].enabled = false;

                // Remove sprite that visually contains "< BACK" and render this as a plain close button.
                Image image = backBtn.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = null;
                    image.color = new Color(0.35f, 0.12f, 0.12f, 0.9f);
                }

                Transform xLabel = backBtn.Find("RuntimeXLabel");
                if (xLabel == null)
                {
                    GameObject xLabelGO = new GameObject("RuntimeXLabel");
                    xLabelGO.transform.SetParent(backBtn, false);

                    RectTransform xRect = xLabelGO.AddComponent<RectTransform>();
                    xRect.anchorMin = Vector2.zero;
                    xRect.anchorMax = Vector2.one;
                    xRect.offsetMin = Vector2.zero;
                    xRect.offsetMax = Vector2.zero;

                    TextMeshProUGUI xText = xLabelGO.AddComponent<TextMeshProUGUI>();
                    xText.text = "X";
                    xText.fontSize = 28f;
                    xText.fontStyle = FontStyles.Bold;
                    xText.alignment = TextAlignmentOptions.Center;
                    xText.color = Color.white;
                    xText.raycastTarget = false;
                }

                btn.interactable = true;
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(CloseSettings);
                Debug.Log("[PauseMenu] Wired Settings BackButton.");
            }
        }
        else
        {
            Debug.LogWarning("[PauseMenu] BackButton not found in Options UI!");
        }
    }

    /// <summary>
    /// Closes Options and returns to where it was opened from.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsInstance != null)
        {
            Destroy(settingsInstance);
            settingsInstance = null;
        }

        if (settingsOrigin == SettingsOrigin.Pause)
        {
            // Return to Pause panel
            if (pauseInstance != null)
                pauseInstance.SetActive(true);
        }
        else if (settingsOrigin == SettingsOrigin.MainMenu)
        {
            // Return to MainLoginPanel
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMainLoginPanel();
        }

        Debug.Log("[PauseMenu] Options overlay closed.");
    }

    private void CloseSettingsImmediate()
    {
        if (settingsInstance != null)
        {
            Destroy(settingsInstance);
            settingsInstance = null;
        }
    }

    // ============================
    // QUIT CONFIRMATION DIALOG
    // ============================

    /// <summary>
    /// Shows a medieval-themed "Save before quitting?" dialog with three options.
    /// Behaviour adapts depending on whether the player clicked Exit in the pause menu
    /// or tried to close the application (Alt+F4, window X).
    /// </summary>
    private void ShowQuitConfirmation(QuitOrigin origin)
    {
        if (quitConfirmInstance != null) return; // Already showing

        quitOrigin = origin;

        // Hide the pause panel while the dialog is up
        if (pauseInstance != null)
            pauseInstance.SetActive(false);

        quitConfirmInstance = new GameObject("QuitConfirmDialog");

        // ── Canvas ──
        Canvas canvas = quitConfirmInstance.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above pause (100)

        var scaler = quitConfirmInstance.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        quitConfirmInstance.AddComponent<GraphicRaycaster>();

        // ── Dim overlay ──
        GameObject dimGO = CreateDialogUIObject("Dim", quitConfirmInstance.transform);
        RectTransform dimRT = dimGO.GetComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        Image dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        dimImg.raycastTarget = true;

        // ── Panel ──
        GameObject panelGO = CreateDialogUIObject("Panel", quitConfirmInstance.transform);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.25f, 0.28f);
        panelRT.anchorMax = new Vector2(0.75f, 0.72f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.08f, 0.06f, 0.03f, 0.96f);

        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.75f, 0.58f, 0.22f, 0.9f);
        panelOutline.effectDistance = new Vector2(3f, 3f);

        // Load medieval font
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // ── Title ──
        bool isAppClose = (origin == QuitOrigin.AppClose);
        string titleText = isAppClose ? "QUIT GAME" : "LEAVING DUNGEON";
        string messageText = isAppClose
            ? "Would you like to save your progress\nbefore closing the game?"
            : "Would you like to save your progress\nbefore returning to the main menu?";

        GameObject titleGO = CreateDialogUIObject("Title", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.72f);
        titleRT.anchorMax = new Vector2(0.95f, 0.95f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        titleGO.AddComponent<CanvasRenderer>();

        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = titleText;
        titleTMP.fontSize = 34;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.95f, 0.82f, 0.35f, 1f);
        if (font != null) titleTMP.font = font;

        // ── Message ──
        GameObject msgGO = CreateDialogUIObject("Message", panelGO.transform);
        RectTransform msgRT = msgGO.GetComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.08f, 0.42f);
        msgRT.anchorMax = new Vector2(0.92f, 0.72f);
        msgRT.offsetMin = Vector2.zero;
        msgRT.offsetMax = Vector2.zero;
        msgGO.AddComponent<CanvasRenderer>();

        TextMeshProUGUI msgTMP = msgGO.AddComponent<TextMeshProUGUI>();
        msgTMP.text = messageText;
        msgTMP.fontSize = 22;
        msgTMP.alignment = TextAlignmentOptions.Center;
        msgTMP.color = new Color(0.88f, 0.82f, 0.65f, 1f);
        msgTMP.enableWordWrapping = true;
        if (font != null) msgTMP.font = font;

        // ── Buttons row ──
        float btnY0 = 0.08f, btnY1 = 0.35f;

        // Save & Quit
        CreateDialogButton(panelGO.transform, "Save & Quit", font,
            new Vector2(0.05f, btnY0), new Vector2(0.35f, btnY1),
            new Color(0.22f, 0.55f, 0.28f, 1f), // green
            () => OnQuitConfirm_SaveAndQuit());

        // Quit Without Saving
        CreateDialogButton(panelGO.transform, "Don't Save", font,
            new Vector2(0.37f, btnY0), new Vector2(0.63f, btnY1),
            new Color(0.65f, 0.22f, 0.18f, 1f), // red
            () => OnQuitConfirm_QuitNoSave());

        // Cancel
        CreateDialogButton(panelGO.transform, "Cancel", font,
            new Vector2(0.65f, btnY0), new Vector2(0.95f, btnY1),
            new Color(0.35f, 0.30f, 0.18f, 1f), // neutral brown
            () => OnQuitConfirm_Cancel());

        Debug.Log($"[PauseMenu] Quit confirmation dialog shown (origin={origin}).");
    }

    private void HideQuitConfirmation()
    {
        if (quitConfirmInstance != null)
        {
            Destroy(quitConfirmInstance);
            quitConfirmInstance = null;
        }
    }

    private void OnQuitConfirm_SaveAndQuit()
    {
        Debug.Log("[PauseMenu] Player chose: Save & Quit");
        HideQuitConfirmation();

        if (quitOrigin == QuitOrigin.AppClose)
        {
            // Save then close the application entirely
            SaveGameInternal((success) =>
            {
                Debug.Log(success
                    ? "[PauseMenu] Save confirmed — quitting application."
                    : "[PauseMenu] Save may have failed — quitting anyway.");
                allowQuit = true;
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            });
        }
        else
        {
            ExitToMenu(); // Saves then loads main menu
        }
    }

    private void OnQuitConfirm_QuitNoSave()
    {
        Debug.Log("[PauseMenu] Player chose: Quit Without Saving");
        HideQuitConfirmation();

        if (quitOrigin == QuitOrigin.AppClose)
        {
            // Close the application without saving
            allowQuit = true;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        else
        {
            // Return to main menu without saving
            IsPaused = false;
            Time.timeScale = 1f;
            HidePauseUI();
            CloseSettingsImmediate();
            EnsureCursorUnlockedForMenu();

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetInventory();

            SceneManager.LoadScene("Main");
        }
    }

    private void OnQuitConfirm_Cancel()
    {
        Debug.Log("[PauseMenu] Player chose: Cancel");
        HideQuitConfirmation();

        if (quitOrigin == QuitOrigin.AppClose)
        {
            // They cancelled the app-close — just show the pause menu again
            if (pauseInstance != null)
                pauseInstance.SetActive(true);
        }
        else
        {
            // Show the pause panel again
            if (pauseInstance != null)
                pauseInstance.SetActive(true);
        }
    }

    // ── Dialog UI helpers ──

    private GameObject CreateDialogUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private void CreateDialogButton(Transform parent, string label, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = CreateDialogUIObject($"Btn_{label}", parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image btnBG = btnGO.AddComponent<Image>();
        btnBG.color = bgColor;

        Outline btnOutline = btnGO.AddComponent<Outline>();
        btnOutline.effectColor = new Color(bgColor.r + 0.15f, bgColor.g + 0.15f, bgColor.b + 0.08f, 0.7f);
        btnOutline.effectDistance = new Vector2(1.5f, 1.5f);

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBG;

        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = new Color(
            Mathf.Min(bgColor.r + 0.12f, 1f),
            Mathf.Min(bgColor.g + 0.12f, 1f),
            Mathf.Min(bgColor.b + 0.08f, 1f), 1f);
        colors.pressedColor = new Color(
            Mathf.Max(bgColor.r - 0.08f, 0f),
            Mathf.Max(bgColor.g - 0.08f, 0f),
            Mathf.Max(bgColor.b - 0.04f, 0f), 1f);
        btn.colors = colors;

        btn.onClick.AddListener(onClick);

        // Label text
        GameObject textGO = CreateDialogUIObject("Label", btnGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(4f, 4f);
        textRT.offsetMax = new Vector2(-4f, -4f);
        textGO.AddComponent<CanvasRenderer>();

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.92f, 0.82f, 1f);
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
    }

    // ============================
    // ALT+F4 / FORCE-CLOSE INTERCEPT
    // ============================

    /// <summary>
    /// Called when the player tries to quit the application (Alt+F4, window X, etc.)
    /// Cancels the quit and shows the save-before-quit confirmation dialog instead.
    /// The player must choose Save&Quit, Don't Save, or Cancel before the app exits.
    /// </summary>
    private bool OnWantsToQuit()
    {
        // If a dialog button already set allowQuit, let the application close now
        if (allowQuit)
            return true;

        // Allow quit immediately from the main menu — no save needed
        string sceneName = SceneManager.GetActiveScene().name;
        if (!IsGameplaySceneName(sceneName))
            return true;

        // If the dialog is already up, keep blocking — let the player decide
        if (quitConfirmInstance != null)
            return false;

        Debug.Log("[PauseMenu] Alt+F4 / window close intercepted — showing quit confirmation.");

        // Pause the game if not already paused
        if (!IsPaused)
            Pause();

        // Hide the normal pause panel — the confirmation dialog takes over
        if (pauseInstance != null)
            pauseInstance.SetActive(false);

        ShowQuitConfirmation(QuitOrigin.AppClose);

        // Block the quit — the dialog buttons will call Application.Quit() if the player confirms
        return false;
    }

    // ============================
    // SAVE FEEDBACK
    // ============================

    private void ShowSaveFeedback(bool success)
    {
        if (pauseInstance == null) return;

        Transform panel = DeepFind(pauseInstance.transform, "PausePanel");
        if (panel == null) panel = pauseInstance.transform;

        GameObject feedbackGO = new GameObject("SaveFeedback", typeof(RectTransform));
        feedbackGO.transform.SetParent(panel, false);
        feedbackGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = feedbackGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.02f);
        rt.anchorMax = new Vector2(0.9f, 0.12f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        feedbackGO.AddComponent<CanvasRenderer>();
        var tmp = feedbackGO.AddComponent<TMPro.TextMeshProUGUI>();

        if (success)
        {
            // Show the saved position so the user can confirm it's correct
            var player = AccountManager.Instance?.GetCurrentPlayer();
            if (player != null)
                tmp.text = $"GAME SAVED!\nPos: ({player.savedPosX:F1}, {player.savedPosY:F1}, {player.savedPosZ:F1}) Level {player.savedLevel}";
            else
                tmp.text = "GAME SAVED!";
        }
        else
        {
            tmp.text = "SAVE FAILED! Check connection.";
        }

        tmp.fontSize = 18;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = success ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        var medievalFont = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (medievalFont == null)
            medievalFont = Resources.Load<TMPro.TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (medievalFont != null)
            tmp.font = medievalFont;

        StartCoroutine(DestroyAfterUnscaledTime(feedbackGO, 3f));
    }

    private static bool IsGameplaySceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
               (sceneName.StartsWith("Level") || sceneName == "Chapter3" || sceneName == "Chapter4");
    }

    private System.Collections.IEnumerator DestroyAfterUnscaledTime(GameObject go, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ============================
    // PLAYER INPUT MANAGEMENT
    // ============================

    /// <summary>
    /// Disables or enables the PlayerInput component on the player GameObject.
    /// When disabled, the Input System stops routing device events through PlayerInput,
    /// allowing the InputSystemUIInputModule (EventSystem) to receive mouse clicks for UI.
    /// Also updates StarterAssetsInputs.cursorLocked to prevent OnApplicationFocus re-locking.
    /// </summary>
    private void SetPlayerInputEnabled(bool enabled)
    {
        GameObject playerGO = FindPlayerWithCharacterController();
        if (playerGO == null) return;

        // Check root first, then children
        PlayerInput playerInput = playerGO.GetComponentInChildren<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = enabled;
            Debug.Log($"[PauseMenu] PlayerInput {(enabled ? "enabled" : "disabled")}");
        }

        // Also manage StarterAssetsInputs cursor lock setting
        var starterInputs = playerGO.GetComponentInChildren<StarterAssets.StarterAssetsInputs>();
        if (starterInputs != null)
        {
            starterInputs.cursorLocked = enabled;
        }
    }

    /// <summary>
    /// Ensures the cursor is unlocked for menu use.
    /// Disables PlayerInput and sets cursorLocked=false so OnApplicationFocus won't re-lock.
    /// </summary>
    private void EnsureCursorUnlockedForMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameObject playerGO = FindPlayerWithCharacterController();
        if (playerGO != null)
        {
            var starterInputs = playerGO.GetComponentInChildren<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
                starterInputs.cursorLocked = false;

            PlayerInput playerInput = playerGO.GetComponentInChildren<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;
        }
    }

    // ============================
    // INPUT SYSTEM & EVENT SYSTEM
    // ============================

    /// <summary>
    /// The Input System must process events in DynamicUpdate (Update) mode
    /// for UI to work when Time.timeScale == 0.
    /// FixedUpdate stops running when timeScale is 0, which would block all input.
    /// </summary>
    private void EnsureInputSystemDynamicUpdate()
    {
#if ENABLE_INPUT_SYSTEM
        if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
        {
            Debug.Log("[PauseMenu] Switching Input System to ProcessEventsInDynamicUpdate for pause UI.");
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        }
#endif
    }

    /// <summary>
    /// Ensures an active EventSystem exists in the scene.
    /// Without one, no UI elements can receive clicks/input.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        Debug.LogWarning("[PauseMenu] No EventSystem found — creating one.");
        GameObject esGO = new GameObject("EventSystem_Runtime");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
    }

    // ============================
    // UTILITY
    // ============================

    /// <summary>
    /// Finds the Player-tagged GameObject that has a CharacterController component.
    /// 
    /// Level scenes have multiple objects tagged "Player":
    ///   - FirstPersonPlayer (parent, only Transform) — stays at spawn position forever
    ///   - PlayerCapsule (child, has CharacterController) — actually moves with the player
    /// 
    /// Using plain FindGameObjectWithTag("Player") may return the parent, giving
    /// the wrong (spawn) position. This method guarantees we get the one that moves.
    /// </summary>
    public static GameObject FindPlayerWithCharacterController()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("[FindPlayerCC] No objects with tag 'Player' found.");
            return null;
        }

        // Prefer the one that has a CharacterController (the one that actually moves)
        foreach (GameObject go in players)
        {
            if (go.GetComponent<CharacterController>() != null)
            {
                Debug.Log($"[FindPlayerCC] Found player with CharacterController: '{go.name}' at ({go.transform.position.x:F2},{go.transform.position.y:F2},{go.transform.position.z:F2})");
                return go;
            }
        }

        // Fallback: check children of tagged objects for CC
        foreach (GameObject go in players)
        {
            CharacterController cc = go.GetComponentInChildren<CharacterController>();
            if (cc != null)
            {
                Debug.Log($"[FindPlayerCC] Found CharacterController on child '{cc.gameObject.name}' of '{go.name}'");
                return cc.gameObject;
            }
        }

        // Last resort: return first tagged object
        Debug.LogWarning($"[FindPlayerCC] No CharacterController found on any Player-tagged object. Using '{players[0].name}' as fallback.");
        return players[0];
    }

    private Transform DeepFind(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform result = DeepFind(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    private void HideStoreItemVisuals(string itemKey)
    {
        if (!storeItemVisuals.TryGetValue(itemKey, out var visuals)) return;
        foreach (var g in visuals)
        {
            if (g == null) continue;
            if (storeDescriptionText != null && g == storeDescriptionText) continue;
            g.enabled = false;
        }
    }

    private void ShowAllStoreItemVisuals()
    {
        foreach (var pair in storeItemVisuals)
        {
            var visuals = pair.Value;
            if (visuals == null) continue;
            foreach (var g in visuals)
            {
                if (g == null) continue;
                if (storeDescriptionText != null && g == storeDescriptionText) continue;
                g.enabled = true;
            }
        }
    }
}
