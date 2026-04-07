using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Fetches all players from the Firebase "leaderboard" node, ranks them by
/// their best completion times, and displays a medieval-themed leaderboard.
///
/// The leaderboard has two views:
///   • GLOBAL  — ranks players by total best time across all completed levels
///   • PER-LEVEL — shows fastest times for a specific level
///
/// Keeps profile picture and display name from the original design.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    private const string RealtimeDatabaseUrl = "https://logiclabyrinth-auth-default-rtdb.asia-southeast1.firebasedatabase.app";

    [Header("Max entries to display")]
    public int maxEntries = 20;

    // --- Colors (medieval palette) ---
    private Color goldText       = new Color(0.84f, 0.75f, 0.50f, 1f);
    private Color creamText      = new Color(0.95f, 0.90f, 0.75f, 1f);
    private Color goldBorder     = new Color(0.72f, 0.58f, 0.30f, 1f);
    private Color goldBorderDim  = new Color(0.50f, 0.40f, 0.22f, 0.6f);
    private Color darkBrown      = new Color(0.12f, 0.09f, 0.05f, 0.95f);
    private Color rowEven        = new Color(0.14f, 0.11f, 0.06f, 0.90f);
    private Color rowOdd         = new Color(0.18f, 0.14f, 0.08f, 0.90f);
    private Color highlightRow   = new Color(0.30f, 0.24f, 0.12f, 0.95f);
    private Color rankGold       = new Color(1f, 0.84f, 0f, 1f);
    private Color rankSilver     = new Color(0.75f, 0.75f, 0.75f, 1f);
    private Color rankBronze     = new Color(0.80f, 0.50f, 0.20f, 1f);
    private Color tabActive      = new Color(0.25f, 0.20f, 0.10f, 1f);
    private Color tabInactive    = new Color(0.14f, 0.11f, 0.06f, 0.90f);

    // UI references (built at runtime)
    private Transform contentParent;
    private TextMeshProUGUI statusText;
    private GameObject loadingIndicator;
    private TMP_FontAsset medievalFont;
    private GameObject headerRow;

    // Tab buttons
    private Button globalTabBtn;
    private Button levelTabBtn;
    private TextMeshProUGUI globalTabText;
    private TextMeshProUGUI levelTabText;
    private Image globalTabBG;
    private Image levelTabBG;

    // Level selector (dropdown-like buttons)
    private GameObject levelSelectorRow;
    private int selectedLevel = 2;   // Start at Level 2 (Level 1 is tutorial)
    private int minLevel = 2;        // Level 1 is tutorial, not ranked
    private int maxLevel = 25;

    // Profile picture cache
    private Dictionary<string, Sprite> profileSpriteCache = new Dictionary<string, Sprite>();
    private const string DEFAULT_PROFILE_PIC = "default";

    // Data
    private class LeaderboardEntry
    {
        public string uid;
        public string username;
        public int lastCompletedLevel;
        public int puzzlesCompleted;
        public string profilePicture;
        public Dictionary<int, float> bestTimes = new Dictionary<int, float>();
        public Dictionary<int, int> attemptCounts = new Dictionary<int, int>();
        public float totalBestTime;
        public float fastestLevelTime;
        public int levelsCompleted;
        public float totalPlayedSeconds;
        public long updatedAtUnixMs;
    }

    private List<LeaderboardEntry> allEntries = new List<LeaderboardEntry>();
    private bool showingGlobal = true;
    private bool isFetchInProgress;
    private bool queuedRefresh;
    private bool permissionDeniedLockout;
    private DatabaseReference leaderboardRef;
    private bool realtimeListenerAttached;

    void OnEnable()
    {
        permissionDeniedLockout = false;
        queuedRefresh = false;
        isFetchInProgress = false;

        LoadProfilePictures();
        BuildUI();
        AttachRealtimeListener();
        FetchLeaderboard();
    }

    void OnDisable()
    {
        DetachRealtimeListener();

        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }
    }

    void OnDestroy()
    {
        DetachRealtimeListener();
    }

    // ===================== UI BUILDING =====================

    private void BuildUI()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        LoadMedievalFont();

        Image panelBG = GetComponent<Image>();
        if (panelBG != null) panelBG.color = new Color(0.05f, 0.04f, 0.02f, 0.85f);

        // ===== Center container (the "board") =====
        GameObject boardBorder = CreateUI("BoardBorder", transform);
        RectTransform boardBorderRT = boardBorder.GetComponent<RectTransform>();
        boardBorderRT.anchorMin = new Vector2(0.1f, 0.03f);
        boardBorderRT.anchorMax = new Vector2(0.9f, 0.95f);
        boardBorderRT.offsetMin = Vector2.zero;
        boardBorderRT.offsetMax = Vector2.zero;
        Image boardBorderImg = boardBorder.AddComponent<Image>();
        boardBorderImg.color = goldBorder;

        GameObject board = CreateUI("Board", boardBorder.transform);
        RectTransform boardRT = board.GetComponent<RectTransform>();
        boardRT.anchorMin = Vector2.zero;
        boardRT.anchorMax = Vector2.one;
        boardRT.offsetMin = new Vector2(2f, 2f);
        boardRT.offsetMax = new Vector2(-2f, -2f);
        Image boardImg = board.AddComponent<Image>();
        boardImg.color = darkBrown;

        // ===== Title =====
        GameObject titleGO = CreateUI("Title", board.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -6f);
        titleRT.sizeDelta = new Vector2(0f, 45f);
        titleGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "LEADERBOARD";
        titleTMP.fontSize = 30;
        titleTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        titleTMP.color = goldText;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.characterSpacing = 8f;
        if (medievalFont != null) titleTMP.font = medievalFont;

        // Gold underline
        GameObject underline = CreateUI("TitleUnderline", board.transform);
        RectTransform ulRT = underline.GetComponent<RectTransform>();
        ulRT.anchorMin = new Vector2(0.15f, 1f);
        ulRT.anchorMax = new Vector2(0.85f, 1f);
        ulRT.pivot = new Vector2(0.5f, 1f);
        ulRT.anchoredPosition = new Vector2(0f, -54f);
        ulRT.sizeDelta = new Vector2(0f, 2f);
        Image ulImg = underline.AddComponent<Image>();
        ulImg.color = goldBorderDim;

        // ===== Tab buttons (Global / Per Level) =====
        float tabY = -60f;
        float tabH = 30f;

        // Global tab
        GameObject globalTab = CreateTabButton(board.transform, "GLOBAL", 0.15f, 0.48f, tabY, tabH);
        globalTabBtn = globalTab.GetComponent<Button>();
        globalTabBG = globalTab.GetComponent<Image>();
        globalTabText = globalTab.GetComponentInChildren<TextMeshProUGUI>();
        globalTabBtn.onClick.AddListener(() => SwitchTab(true));

        // Per-Level tab
        GameObject levelTab = CreateTabButton(board.transform, "PER LEVEL", 0.52f, 0.85f, tabY, tabH);
        levelTabBtn = levelTab.GetComponent<Button>();
        levelTabBG = levelTab.GetComponent<Image>();
        levelTabText = levelTab.GetComponentInChildren<TextMeshProUGUI>();
        levelTabBtn.onClick.AddListener(() => SwitchTab(false));

        // ===== Level selector row (hidden in global view) =====
        levelSelectorRow = CreateUI("LevelSelector", board.transform);
        RectTransform lsRT = levelSelectorRow.GetComponent<RectTransform>();
        lsRT.anchorMin = new Vector2(0.05f, 1f);
        lsRT.anchorMax = new Vector2(0.95f, 1f);
        lsRT.pivot = new Vector2(0.5f, 1f);
        lsRT.anchoredPosition = new Vector2(0f, tabY - tabH - 4f);
        lsRT.sizeDelta = new Vector2(0f, 28f);

        HorizontalLayoutGroup lsHLG = levelSelectorRow.AddComponent<HorizontalLayoutGroup>();
        lsHLG.spacing = 3f;
        lsHLG.childAlignment = TextAnchor.MiddleCenter;
        lsHLG.childForceExpandWidth = true;
        lsHLG.childForceExpandHeight = true;
        lsHLG.childControlWidth = true;
        lsHLG.childControlHeight = true;

        // Left arrow
        CreateLevelNavButton(levelSelectorRow.transform, "<", () => ChangeSelectedLevel(-1));

        // Level number display
        GameObject levelNumGO = CreateUI("LevelNum", levelSelectorRow.transform);
        levelNumGO.AddComponent<CanvasRenderer>();
        LayoutElement lnLE = levelNumGO.AddComponent<LayoutElement>();
        lnLE.flexibleWidth = 3f;
        TextMeshProUGUI levelNumTMP = levelNumGO.AddComponent<TextMeshProUGUI>();
        levelNumTMP.text = $"LEVEL {selectedLevel}";
        levelNumTMP.fontSize = 16;
        levelNumTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        levelNumTMP.color = goldText;
        levelNumTMP.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) levelNumTMP.font = medievalFont;

        // Right arrow
        CreateLevelNavButton(levelSelectorRow.transform, ">", () => ChangeSelectedLevel(1));

        levelSelectorRow.SetActive(false); // Hidden in global view

        // ===== Column headers =====
        float headerY = tabY - tabH - 8f;
        headerRow = CreateUI("HeaderRow", board.transform);
        RectTransform headerRT = headerRow.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, headerY);
        headerRT.sizeDelta = new Vector2(0f, 28f);

        HorizontalLayoutGroup headerHLG = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHLG.padding = new RectOffset(15, 15, 0, 0);
        headerHLG.spacing = 4f;
        headerHLG.childAlignment = TextAnchor.MiddleCenter;
        headerHLG.childForceExpandWidth = false;
        headerHLG.childForceExpandHeight = true;
        headerHLG.childControlWidth = false;
        headerHLG.childControlHeight = true;

        BuildHeaderCells(true); // Global by default

        // ===== Scrollable content area =====
        GameObject scrollArea = CreateUI("ScrollArea", board.transform);
        RectTransform scrollRT = scrollArea.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(5f, 55f);
        scrollRT.offsetMax = new Vector2(-5f, headerY - 30f);

        ScrollRect sr = scrollArea.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        Image scrollBG = scrollArea.AddComponent<Image>();
        scrollBG.color = Color.white;
        Mask mask = scrollArea.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUI("Content", scrollArea.transform);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRT;
        contentParent = content.transform;

        // ===== Loading text =====
        loadingIndicator = CreateUI("Loading", board.transform);
        RectTransform loadingRT = loadingIndicator.GetComponent<RectTransform>();
        loadingRT.anchorMin = new Vector2(0.2f, 0.3f);
        loadingRT.anchorMax = new Vector2(0.8f, 0.6f);
        loadingRT.offsetMin = Vector2.zero;
        loadingRT.offsetMax = Vector2.zero;
        loadingIndicator.AddComponent<CanvasRenderer>();
        statusText = loadingIndicator.AddComponent<TextMeshProUGUI>();
        statusText.text = "Fetching rankings...";
        statusText.fontSize = 20;
        statusText.color = goldBorderDim;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontStyle = FontStyles.Italic;
        if (medievalFont != null) statusText.font = medievalFont;

        // ===== Back button =====
        CreateFooterButton(board.transform, "< BACK", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(15f, 10f), OnBackClicked);

        // ===== Refresh button =====
        CreateFooterButton(board.transform, "REFRESH", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 10f), FetchLeaderboard);

        // Initial tab styling
        UpdateTabVisuals();
    }

    private void BuildHeaderCells(bool global)
    {
        // Clear existing header cells
        foreach (Transform child in headerRow.transform)
            Destroy(child.gameObject);

        CreateHeaderCell(headerRow.transform, "#", 40f);
        CreateHeaderCell(headerRow.transform, "", 36f);  // Profile pic column
        CreateHeaderCell(headerRow.transform, "PLAYER", 180f);

        if (global)
        {
            CreateHeaderCell(headerRow.transform, "LEVELS", 70f);
            CreateHeaderCell(headerRow.transform, "BEST TIME", 100f);
            CreateHeaderCell(headerRow.transform, "FASTEST", 90f);
        }
        else
        {
            CreateHeaderCell(headerRow.transform, "BEST TIME", 120f);
            CreateHeaderCell(headerRow.transform, "ATTEMPTS", 90f);
        }
    }

    private GameObject CreateTabButton(Transform parent, string text, float xMin, float xMax, float y, float h)
    {
        GameObject tabGO = CreateUI($"Tab_{text}", parent);
        RectTransform tabRT = tabGO.GetComponent<RectTransform>();
        tabRT.anchorMin = new Vector2(xMin, 1f);
        tabRT.anchorMax = new Vector2(xMax, 1f);
        tabRT.pivot = new Vector2(0.5f, 1f);
        tabRT.anchoredPosition = new Vector2(0f, y);
        tabRT.sizeDelta = new Vector2(0f, h);

        Image bg = tabGO.AddComponent<Image>();
        bg.color = tabInactive;

        Button btn = tabGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.30f, 0.24f, 0.12f, 1f);
        cb.pressedColor = new Color(0.35f, 0.28f, 0.14f, 1f);
        btn.colors = cb;

        Outline outline = tabGO.AddComponent<Outline>();
        outline.effectColor = goldBorderDim;
        outline.effectDistance = new Vector2(1f, 1f);

        GameObject textGO = CreateUI("Text", tabGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        tmp.color = goldText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 3f;
        if (medievalFont != null) tmp.font = medievalFont;

        return tabGO;
    }

    private void CreateLevelNavButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = CreateUI($"NavBtn_{text}", parent);
        btnGO.AddComponent<CanvasRenderer>();
        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        Image bg = btnGO.AddComponent<Image>();
        bg.color = tabInactive;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = tabActive;
        cb.pressedColor = new Color(0.35f, 0.28f, 0.14f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        Outline outline = btnGO.AddComponent<Outline>();
        outline.effectColor = goldBorderDim;
        outline.effectDistance = new Vector2(1f, 1f);

        GameObject textGO = CreateUI("Text", btnGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = goldText;
        tmp.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    private void CreateFooterButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = CreateUI($"Btn_{text}", parent);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = anchorMin;
        btnRT.anchorMax = anchorMax;
        btnRT.pivot = anchorMin;
        btnRT.anchoredPosition = anchoredPos;
        btnRT.sizeDelta = new Vector2(120f, 36f);

        Image bg = btnGO.AddComponent<Image>();
        bg.color = tabInactive;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = tabActive;
        cb.pressedColor = new Color(0.35f, 0.28f, 0.14f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        Outline outline = btnGO.AddComponent<Outline>();
        outline.effectColor = goldBorderDim;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject textGO = CreateUI("Text", btnGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        tmp.color = goldText;
        tmp.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    private void CreateHeaderCell(Transform parent, string text, float width)
    {
        GameObject cellGO = CreateUI($"Header_{text}", parent);
        cellGO.AddComponent<CanvasRenderer>();
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        TextMeshProUGUI tmp = cellGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12;
        tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        tmp.color = goldBorderDim;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 3f;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    // ===================== TAB SWITCHING =====================

    private void SwitchTab(bool global)
    {
        showingGlobal = global;
        UpdateTabVisuals();
        BuildHeaderCells(global);
        levelSelectorRow.SetActive(!global);

        // Adjust header position based on whether level selector is shown
        RectTransform headerRT = headerRow.GetComponent<RectTransform>();
        if (!global)
            headerRT.anchoredPosition = new Vector2(0f, -124f);
        else
            headerRT.anchoredPosition = new Vector2(0f, -98f);

        DisplayCurrentView();
    }

    private void UpdateTabVisuals()
    {
        if (globalTabBG != null) globalTabBG.color = showingGlobal ? tabActive : tabInactive;
        if (levelTabBG != null) levelTabBG.color = !showingGlobal ? tabActive : tabInactive;
        if (globalTabText != null) globalTabText.color = showingGlobal ? rankGold : goldText;
        if (levelTabText != null) levelTabText.color = !showingGlobal ? rankGold : goldText;
    }

    private void ChangeSelectedLevel(int delta)
    {
        selectedLevel = Mathf.Clamp(selectedLevel + delta, minLevel, maxLevel);

        // Update the level number display
        TextMeshProUGUI levelNumTMP = levelSelectorRow.transform.Find("LevelNum")?.GetComponent<TextMeshProUGUI>();
        if (levelNumTMP != null) levelNumTMP.text = $"LEVEL {selectedLevel}";

        DisplayCurrentView();
    }

    // ===================== FIREBASE FETCH =====================

    private void FetchLeaderboard()
    {
        FetchLeaderboard(true);
    }

    private void FetchLeaderboard(bool showLoading)
    {
        if (permissionDeniedLockout)
        {
            if (showLoading)
                ShowPermissionDeniedState();
            return;
        }

        if (isFetchInProgress)
        {
            queuedRefresh = true;
            return;
        }

        isFetchInProgress = true;

        if (showLoading && loadingIndicator != null) loadingIndicator.SetActive(true);
        if (showLoading && statusText != null) statusText.text = "Fetching rankings...";

        if (showLoading && contentParent != null)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }

        DatabaseReference dbRef = FirebaseDatabase.GetInstance(RealtimeDatabaseUrl).RootReference;
        dbRef.Child("leaderboard").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            isFetchInProgress = false;

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[Leaderboard] Failed to fetch data: " + task.Exception);
                bool permissionDenied = IsPermissionDenied(task.Exception);
                if (permissionDenied)
                {
                    permissionDeniedLockout = true;
                    DetachRealtimeListener();
                    ShowPermissionDeniedState();
                }
                else if (showLoading && statusText != null)
                {
                    statusText.text = "Failed to load leaderboard.";
                }
                RunQueuedRefreshIfNeeded();
                return;
            }

            ApplySnapshot(task.Result, showLoading);
            RunQueuedRefreshIfNeeded();
        });
    }

    private void AttachRealtimeListener()
    {
        DetachRealtimeListener();
        leaderboardRef = FirebaseDatabase.GetInstance(RealtimeDatabaseUrl).RootReference.Child("leaderboard");
        leaderboardRef.ValueChanged += OnLeaderboardValueChanged;
        realtimeListenerAttached = true;
    }

    private void DetachRealtimeListener()
    {
        if (!realtimeListenerAttached || leaderboardRef == null) return;
        leaderboardRef.ValueChanged -= OnLeaderboardValueChanged;
        realtimeListenerAttached = false;
        leaderboardRef = null;
    }

    private void OnLeaderboardValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (permissionDeniedLockout) return;

        if (args.DatabaseError != null)
        {
            Debug.LogError("[Leaderboard] Realtime listener failed: " + args.DatabaseError.Message);
            if (IsPermissionDenied(args.DatabaseError.Message))
            {
                permissionDeniedLockout = true;
                DetachRealtimeListener();
                ShowPermissionDeniedState();
            }
            return;
        }

        ApplySnapshot(args.Snapshot, false);
    }

    private void ApplySnapshot(DataSnapshot snapshot, bool showEmptyStatus)
    {
        if (snapshot == null || !snapshot.Exists || !snapshot.HasChildren)
        {
            allEntries.Clear();
            if (showEmptyStatus && statusText != null)
                statusText.text = "No players found yet. Play the game!";
            if (loadingIndicator != null)
                loadingIndicator.SetActive(showEmptyStatus);
            DisplayCurrentView();
            return;
        }

        allEntries.Clear();

        foreach (DataSnapshot userSnap in snapshot.Children)
        {
            LeaderboardEntry entry = new LeaderboardEntry();
            entry.uid = userSnap.Key;

            if (userSnap.HasChild("username"))
                entry.username = userSnap.Child("username").Value?.ToString() ?? "Unknown";
            else
                entry.username = "Unknown";

            if (userSnap.HasChild("lastCompletedLevel"))
                int.TryParse(userSnap.Child("lastCompletedLevel").Value?.ToString(), out entry.lastCompletedLevel);

            if (userSnap.HasChild("puzzlesCompleted"))
                int.TryParse(userSnap.Child("puzzlesCompleted").Value?.ToString(), out entry.puzzlesCompleted);

            if (userSnap.HasChild("profilePicture"))
                entry.profilePicture = userSnap.Child("profilePicture").Value?.ToString() ?? DEFAULT_PROFILE_PIC;
            else
                entry.profilePicture = DEFAULT_PROFILE_PIC;

            if (userSnap.HasChild("bestLevelTimes"))
            {
                string timesStr = userSnap.Child("bestLevelTimes").Value?.ToString() ?? "";
                entry.bestTimes = AccountManager.ParseBestTimes(timesStr);
            }

            if (userSnap.HasChild("levelAttemptCounts"))
            {
                string attemptsStr = userSnap.Child("levelAttemptCounts").Value?.ToString() ?? "";
                entry.attemptCounts = AccountManager.ParseAttemptCounts(attemptsStr);
            }

            if (userSnap.HasChild("totalBestTime"))
                float.TryParse(userSnap.Child("totalBestTime").Value?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out entry.totalBestTime);
            else
                entry.totalBestTime = -1f;

            if (userSnap.HasChild("fastestLevelTime"))
                float.TryParse(userSnap.Child("fastestLevelTime").Value?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out entry.fastestLevelTime);
            else
                entry.fastestLevelTime = -1f;

            if (userSnap.HasChild("levelsCompleted"))
                int.TryParse(userSnap.Child("levelsCompleted").Value?.ToString(), out entry.levelsCompleted);

            if (entry.levelsCompleted == 0 && entry.lastCompletedLevel > 0)
                entry.levelsCompleted = entry.lastCompletedLevel;

            if (userSnap.HasChild("totalPlayedSeconds"))
                float.TryParse(userSnap.Child("totalPlayedSeconds").Value?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out entry.totalPlayedSeconds);

            if (userSnap.HasChild("updatedAtUnixMs"))
                long.TryParse(userSnap.Child("updatedAtUnixMs").Value?.ToString(), out entry.updatedAtUnixMs);

            // Keep this client's own avatar current even when cloud sync is temporarily unavailable.
            string localPic = ResolveLocalProfilePictureOverride(entry);
            if (!string.IsNullOrWhiteSpace(localPic))
                entry.profilePicture = localPic;

            allEntries.Add(entry);
        }

        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        DisplayCurrentView();
        Debug.Log($"[Leaderboard] Loaded {allEntries.Count} entries.");
    }

    private void RunQueuedRefreshIfNeeded()
    {
        if (permissionDeniedLockout)
        {
            queuedRefresh = false;
            return;
        }

        if (!queuedRefresh) return;
        queuedRefresh = false;
        FetchLeaderboard(false);
    }

    private bool IsPermissionDenied(System.Exception ex)
    {
        if (ex == null) return false;
        string msg = ex.ToString();
        return IsPermissionDenied(msg);
    }

    private bool IsPermissionDenied(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.IndexOf("permission denied", System.StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("does not have permission", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ShowPermissionDeniedState()
    {
        allEntries.Clear();
        TryAddLocalFallbackEntry();

        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        if (statusText != null)
        {
            statusText.text = "Leaderboard blocked by Firebase Rules.\nLogin again or allow read access to /leaderboard.";
        }

        DisplayCurrentView();
    }

    private void TryAddLocalFallbackEntry()
    {
        if (AccountManager.Instance == null) return;
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player == null) return;

        var entry = new LeaderboardEntry();
        entry.uid = GetCurrentUid();
        entry.username = !string.IsNullOrWhiteSpace(player.displayName)
            ? player.displayName
            : (player.username ?? "You");
        entry.lastCompletedLevel = player.lastCompletedLevel;
        entry.puzzlesCompleted = player.completedPuzzles != null ? player.completedPuzzles.Count : 0;
        entry.profilePicture = string.IsNullOrWhiteSpace(player.profilePicture) ? DEFAULT_PROFILE_PIC : player.profilePicture;
        entry.bestTimes = AccountManager.ParseBestTimes(player.bestLevelTimes ?? "");
        entry.attemptCounts = AccountManager.ParseAttemptCounts(player.levelAttemptCounts ?? "");

        float totalBest = 0f;
        float fastest = -1f;
        foreach (var kvp in entry.bestTimes)
        {
            totalBest += kvp.Value;
            if (fastest < 0f || kvp.Value < fastest)
                fastest = kvp.Value;
        }
        entry.totalBestTime = entry.bestTimes.Count > 0 ? totalBest : -1f;
        entry.fastestLevelTime = fastest;
        entry.levelsCompleted = entry.bestTimes.Count > 0 ? entry.bestTimes.Count : player.lastCompletedLevel;
        entry.totalPlayedSeconds = player.totalPlayedSeconds;
        entry.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        allEntries.Add(entry);
    }

    private string ResolveLocalProfilePictureOverride(LeaderboardEntry entry)
    {
        if (entry == null || AccountManager.Instance == null) return null;
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player == null) return null;

        string currentUid = GetCurrentUid();
        if (!string.IsNullOrWhiteSpace(currentUid) && currentUid == entry.uid)
            return player.profilePicture;

        // Fallback match when session is local/offline and UID is unavailable.
        string entryName = (entry.username ?? string.Empty).Trim();
        string localDisplay = (player.displayName ?? string.Empty).Trim();
        string localUser = (player.username ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(entryName) &&
            (entryName.Equals(localDisplay, System.StringComparison.OrdinalIgnoreCase) ||
             entryName.Equals(localUser, System.StringComparison.OrdinalIgnoreCase)))
        {
            return player.profilePicture;
        }

        return null;
    }

    // ===================== DISPLAY =====================

    private void DisplayCurrentView()
    {
        if (contentParent == null) return;

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        if (showingGlobal)
            DisplayGlobalRanking();
        else
            DisplayLevelRanking();
    }

    private void DisplayGlobalRanking()
    {
        var deduped = BuildDedupedEntries();

        // Sort by: most levels completed (desc), then lowest total best time (asc), then name
        var sorted = deduped
            .OrderByDescending(e => e.levelsCompleted)
            .ThenBy(e => e.totalBestTime > 0 ? e.totalBestTime : float.MaxValue)
            .ThenBy(e => e.username)
            .Take(maxEntries)
            .ToList();

        string currentUid = GetCurrentUid();
        var currentPlayer = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;

        for (int i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];
            int rank = i + 1;
            bool isCurrent = IsCurrentPlayerEntry(entry, currentUid, currentPlayer);
            CreateGlobalRow(rank, entry, isCurrent, i % 2 == 0);
        }

        if (sorted.Count == 0 && statusText != null)
        {
            statusText.text = "No rankings yet. Complete a level!";
            loadingIndicator?.SetActive(true);
        }
    }

    private void DisplayLevelRanking()
    {
        var deduped = BuildDedupedEntries();

        // Filter entries that have a time for the selected level
        var withTime = deduped
            .Where(e => e.bestTimes.ContainsKey(selectedLevel))
            .OrderBy(e => e.bestTimes[selectedLevel])
            .ThenBy(e => e.username)
            .Take(maxEntries)
            .ToList();

        string currentUid = GetCurrentUid();
        var currentPlayer = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;

        for (int i = 0; i < withTime.Count; i++)
        {
            var entry = withTime[i];
            int rank = i + 1;
            bool isCurrent = IsCurrentPlayerEntry(entry, currentUid, currentPlayer);
            CreateLevelRow(rank, entry, isCurrent, i % 2 == 0);
        }

        if (withTime.Count == 0)
        {
            // Show a "no times" message in the content area
            GameObject msgGO = CreateUI("NoTimes", contentParent);
            msgGO.AddComponent<CanvasRenderer>();
            LayoutElement le = msgGO.AddComponent<LayoutElement>();
            le.preferredHeight = 60f;
            TextMeshProUGUI tmp = msgGO.AddComponent<TextMeshProUGUI>();
            tmp.text = $"No times recorded for Level {selectedLevel} yet.";
            tmp.fontSize = 16;
            tmp.color = goldBorderDim;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Italic;
            if (medievalFont != null) tmp.font = medievalFont;
        }
    }

    // ===================== ROW CREATION =====================

    private List<LeaderboardEntry> BuildDedupedEntries()
    {
        var byIdentity = new Dictionary<string, LeaderboardEntry>();

        foreach (var entry in allEntries)
        {
            if (entry == null) continue;

            string key = BuildIdentityKey(entry);
            if (string.IsNullOrWhiteSpace(key)) continue;

            if (!byIdentity.TryGetValue(key, out var existing) || IsBetterEntryCandidate(entry, existing))
                byIdentity[key] = entry;
        }

        return byIdentity.Values.ToList();
    }

    private string BuildIdentityKey(LeaderboardEntry entry)
    {
        string nameKey = NormalizeIdentityText(entry != null ? entry.username : null);
        if (!string.IsNullOrWhiteSpace(nameKey))
            return "name:" + nameKey;

        string uidKey = NormalizeIdentityText(entry != null ? entry.uid : null);
        if (!string.IsNullOrWhiteSpace(uidKey))
            return "uid:" + uidKey;

        return string.Empty;
    }

    private bool IsBetterEntryCandidate(LeaderboardEntry candidate, LeaderboardEntry existing)
    {
        if (candidate == null) return false;
        if (existing == null) return true;

        // Prefer the newest write first so profile changes from fallback/public paths are visible.
        if (candidate.updatedAtUnixMs != existing.updatedAtUnixMs)
            return candidate.updatedAtUnixMs > existing.updatedAtUnixMs;

        bool candidateIsPublic = IsPublicFallbackId(candidate.uid);
        bool existingIsPublic = IsPublicFallbackId(existing.uid);
        if (candidateIsPublic != existingIsPublic)
            return !candidateIsPublic;

        if (candidate.levelsCompleted != existing.levelsCompleted)
            return candidate.levelsCompleted > existing.levelsCompleted;

        if (candidate.totalBestTime > 0f || existing.totalBestTime > 0f)
        {
            float candidateTime = candidate.totalBestTime > 0f ? candidate.totalBestTime : float.MaxValue;
            float existingTime = existing.totalBestTime > 0f ? existing.totalBestTime : float.MaxValue;
            if (candidateTime != existingTime)
                return candidateTime < existingTime;
        }

        if (candidate.lastCompletedLevel != existing.lastCompletedLevel)
            return candidate.lastCompletedLevel > existing.lastCompletedLevel;

        return false;
    }

    private bool IsCurrentPlayerEntry(LeaderboardEntry entry, string currentUid, AccountManager.PlayerData currentPlayer)
    {
        if (entry == null || currentPlayer == null) return false;

        if (!string.IsNullOrWhiteSpace(currentUid) && entry.uid == currentUid)
            return true;

        string normalizedEntryName = NormalizeIdentityText(entry.username);
        string normalizedDisplay = NormalizeIdentityText(currentPlayer.displayName);
        string normalizedUser = NormalizeIdentityText(currentPlayer.username);

        if (!string.IsNullOrWhiteSpace(normalizedEntryName) &&
            (normalizedEntryName == normalizedDisplay || normalizedEntryName == normalizedUser))
        {
            return true;
        }

        if (IsPublicFallbackId(entry.uid))
        {
            string normalizedPublicUid = NormalizeIdentityText(entry.uid.Substring("public_".Length));
            if (!string.IsNullOrWhiteSpace(normalizedPublicUid) &&
                (normalizedPublicUid == normalizedDisplay || normalizedPublicUid == normalizedUser))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPublicFallbackId(string uid)
    {
        return !string.IsNullOrWhiteSpace(uid)
            && uid.StartsWith("public_", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIdentityText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string trimmed = raw.Trim().ToLowerInvariant();
        var chars = trimmed.Where(System.Char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private void CreateGlobalRow(int rank, LeaderboardEntry entry, bool isCurrentPlayer, bool evenRow)
    {
        GameObject rowGO = CreateRowBase(rank, entry, isCurrentPlayer, evenRow);
        HorizontalLayoutGroup hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(15, 15, 3, 3);

        // Rank
        Color rankColor = GetRankColor(rank);
        string rankText = GetRankText(rank);
        CreateCell(rowGO.transform, rankText, 40f, rankColor, rank <= 3 ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center);

        // Profile pic
        CreateProfilePicCell(rowGO.transform, entry.profilePicture, 36f);

        // Name
        string displayName = entry.username;
        if (isCurrentPlayer) displayName += "  (You)";
        CreateCell(rowGO.transform, displayName, 180f, isCurrentPlayer ? goldText : creamText, isCurrentPlayer ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Left);

        // Levels completed
        CreateCell(rowGO.transform, entry.levelsCompleted.ToString(), 70f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Total best time
        string totalTimeStr = entry.totalBestTime > 0 ? LevelTimer.FormatTime(entry.totalBestTime) : "--:--";
        CreateCell(rowGO.transform, totalTimeStr, 100f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Fastest single level
        string fastestStr = entry.fastestLevelTime > 0 ? LevelTimer.FormatTime(entry.fastestLevelTime) : "--:--";
        CreateCell(rowGO.transform, fastestStr, 90f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Gold accent for top 3
        AddTopRankAccent(rowGO, rank, rankColor);
    }

    private void CreateLevelRow(int rank, LeaderboardEntry entry, bool isCurrentPlayer, bool evenRow)
    {
        GameObject rowGO = CreateRowBase(rank, entry, isCurrentPlayer, evenRow);
        HorizontalLayoutGroup hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(15, 15, 3, 3);

        // Rank
        Color rankColor = GetRankColor(rank);
        string rankText = GetRankText(rank);
        CreateCell(rowGO.transform, rankText, 40f, rankColor, rank <= 3 ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center);

        // Profile pic
        CreateProfilePicCell(rowGO.transform, entry.profilePicture, 36f);

        // Name
        string displayName = entry.username;
        if (isCurrentPlayer) displayName += "  (You)";
        CreateCell(rowGO.transform, displayName, 180f, isCurrentPlayer ? goldText : creamText, isCurrentPlayer ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Left);

        // Best time for this level
        float levelTime = entry.bestTimes.ContainsKey(selectedLevel) ? entry.bestTimes[selectedLevel] : -1f;
        string timeStr = levelTime > 0 ? LevelTimer.FormatTime(levelTime) : "--:--";
        CreateCell(rowGO.transform, timeStr, 120f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Attempts for this level
        int attempts = entry.attemptCounts.ContainsKey(selectedLevel) ? entry.attemptCounts[selectedLevel] : 0;
        string attemptsText = attempts > 0 ? attempts.ToString() : "--";
        CreateCell(rowGO.transform, attemptsText, 90f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        AddTopRankAccent(rowGO, rank, rankColor);
    }

    private GameObject CreateRowBase(int rank, LeaderboardEntry entry, bool isCurrentPlayer, bool evenRow)
    {
        GameObject rowGO = CreateUI($"Row_{rank}", contentParent);
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 42f;

        Image rowBG = rowGO.AddComponent<Image>();
        rowBG.color = isCurrentPlayer ? highlightRow : (evenRow ? rowEven : rowOdd);

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        return rowGO;
    }

    private void AddTopRankAccent(GameObject rowGO, int rank, Color rankColor)
    {
        if (rank > 3) return;

        GameObject accent = CreateUI("Accent", rowGO.transform);
        accent.transform.SetAsFirstSibling();
        RectTransform acRT = accent.GetComponent<RectTransform>();
        acRT.anchorMin = new Vector2(0f, 0f);
        acRT.anchorMax = new Vector2(0f, 1f);
        acRT.pivot = new Vector2(0f, 0.5f);
        acRT.anchoredPosition = Vector2.zero;
        acRT.sizeDelta = new Vector2(3f, 0f);
        Image acImg = accent.AddComponent<Image>();
        acImg.color = rankColor;
    }

    private Color GetRankColor(int rank)
    {
        if (rank == 1) return rankGold;
        if (rank == 2) return rankSilver;
        if (rank == 3) return rankBronze;
        return creamText;
    }

    private string GetRankText(int rank)
    {
        if (rank == 1) return "* 1";
        return rank.ToString();
    }

    private void CreateCell(Transform parent, string text, float width, Color color, FontStyles style, TextAlignmentOptions align)
    {
        GameObject cellGO = CreateUI("Cell", parent);
        cellGO.AddComponent<CanvasRenderer>();
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        TextMeshProUGUI tmp = cellGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    // ===================== PROFILE PICTURES =====================

    private void CreateProfilePicCell(Transform parent, string pictureName, float size)
    {
        GameObject cellGO = CreateUI("ProfilePic", parent);
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;

        Image borderImg = cellGO.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        GameObject picGO = CreateUI("Pic", cellGO.transform);
        RectTransform picRT = picGO.GetComponent<RectTransform>();
        picRT.anchorMin = Vector2.zero;
        picRT.anchorMax = Vector2.one;
        picRT.offsetMin = new Vector2(2f, 2f);
        picRT.offsetMax = new Vector2(-2f, -2f);

        Image picImg = picGO.AddComponent<Image>();
        Sprite sprite = GetProfileSprite(pictureName);
        if (sprite != null)
        {
            picImg.sprite = sprite;
            picImg.preserveAspect = true;
        }
        else
        {
            picImg.color = new Color(0.3f, 0.25f, 0.15f, 1f);
        }
    }

    private void LoadProfilePictures()
    {
        profileSpriteCache.Clear();
        Texture2D[] textures = Resources.LoadAll<Texture2D>("ProfilePictures");
        foreach (Texture2D tex in textures)
        {
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            profileSpriteCache[tex.name] = sprite;
        }
        Debug.Log($"[Leaderboard] Loaded {profileSpriteCache.Count} profile pictures.");
    }

    private Sprite GetProfileSprite(string pictureName)
    {
        if (string.IsNullOrEmpty(pictureName) || pictureName == "image-removebg-preview")
            pictureName = DEFAULT_PROFILE_PIC;

        if (profileSpriteCache.ContainsKey(pictureName))
            return profileSpriteCache[pictureName];

        if (profileSpriteCache.ContainsKey(DEFAULT_PROFILE_PIC))
            return profileSpriteCache[DEFAULT_PROFILE_PIC];

        return null;
    }

    // ===================== HELPERS =====================

    private string GetCurrentUid()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        return auth.CurrentUser != null ? auth.CurrentUser.UserId : "";
    }

    private void OnBackClicked()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
    }

    private GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private void LoadMedievalFont()
    {
        medievalFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (medievalFont == null)
            medievalFont = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");
        if (medievalFont == null)
            medievalFont = TMP_Settings.defaultFontAsset;
    }
}
