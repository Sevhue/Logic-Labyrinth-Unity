using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Fetches all players from Firebase, ranks them by level progress,
/// and displays a medieval-themed leaderboard.
/// Attach to LeaderboardsPanel in the scene.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
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
    private Color highlightRow   = new Color(0.30f, 0.24f, 0.12f, 0.95f); // Current player
    private Color rankGold       = new Color(1f, 0.84f, 0f, 1f);
    private Color rankSilver     = new Color(0.75f, 0.75f, 0.75f, 1f);
    private Color rankBronze     = new Color(0.80f, 0.50f, 0.20f, 1f);

    // UI references (built at runtime)
    private Transform contentParent;
    private TextMeshProUGUI statusText;
    private GameObject loadingIndicator;
    private TMP_FontAsset medievalFont;

    // Profile picture cache
    private Dictionary<string, Sprite> profileSpriteCache = new Dictionary<string, Sprite>();
    private const string DEFAULT_PROFILE_PIC = "default";

    // Data
    private class LeaderboardEntry
    {
        public string username;
        public int lastCompletedLevel;
        public int puzzlesCompleted;
        public string profilePicture;
    }

    void OnEnable()
    {
        LoadProfilePictures();
        BuildUI();
        FetchLeaderboard();
    }

    void OnDisable()
    {
        // Clean up dynamic children so it rebuilds fresh next time
        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }
    }

    // ===================== UI BUILDING =====================

    private void BuildUI()
    {
        // Clear any existing children from the panel (except ones we'll rebuild)
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        LoadMedievalFont();

        RectTransform panelRT = GetComponent<RectTransform>();

        // Semi-transparent dark overlay background
        Image panelBG = GetComponent<Image>();
        if (panelBG != null) panelBG.color = new Color(0.05f, 0.04f, 0.02f, 0.85f);

        // ===== Center container (the "board") =====
        GameObject boardBorder = CreateUI("BoardBorder", transform);
        RectTransform boardBorderRT = boardBorder.GetComponent<RectTransform>();
        boardBorderRT.anchorMin = new Vector2(0.15f, 0.05f);
        boardBorderRT.anchorMax = new Vector2(0.85f, 0.92f);
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
        titleRT.anchoredPosition = new Vector2(0f, -8f);
        titleRT.sizeDelta = new Vector2(0f, 55f);
        titleGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "LEADERBOARD";
        titleTMP.fontSize = 32;
        titleTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        titleTMP.color = goldText;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.characterSpacing = 8f;
        if (medievalFont != null) titleTMP.font = medievalFont;

        // Gold underline below title
        GameObject underline = CreateUI("TitleUnderline", board.transform);
        RectTransform ulRT = underline.GetComponent<RectTransform>();
        ulRT.anchorMin = new Vector2(0.15f, 1f);
        ulRT.anchorMax = new Vector2(0.85f, 1f);
        ulRT.pivot = new Vector2(0.5f, 1f);
        ulRT.anchoredPosition = new Vector2(0f, -65f);
        ulRT.sizeDelta = new Vector2(0f, 2f);
        Image ulImg = underline.AddComponent<Image>();
        ulImg.color = goldBorderDim;

        // ===== Column headers =====
        GameObject headerRow = CreateUI("HeaderRow", board.transform);
        RectTransform headerRT = headerRow.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -72f);
        headerRT.sizeDelta = new Vector2(0f, 30f);

        HorizontalLayoutGroup headerHLG = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHLG.padding = new RectOffset(20, 20, 0, 0);
        headerHLG.spacing = 5f;
        headerHLG.childAlignment = TextAnchor.MiddleCenter;
        headerHLG.childForceExpandWidth = false;
        headerHLG.childForceExpandHeight = true;
        headerHLG.childControlWidth = false;
        headerHLG.childControlHeight = true;

        CreateHeaderCell(headerRow.transform, "#", 50f);
        CreateHeaderCell(headerRow.transform, "", 38f);  // Profile pic column (no header text)
        CreateHeaderCell(headerRow.transform, "PLAYER", 240f);
        CreateHeaderCell(headerRow.transform, "LEVEL", 100f);
        CreateHeaderCell(headerRow.transform, "PUZZLES", 100f);

        // ===== Scrollable content area =====
        GameObject scrollArea = CreateUI("ScrollArea", board.transform);
        RectTransform scrollRT = scrollArea.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(5f, 10f);
        scrollRT.offsetMax = new Vector2(-5f, -108f); // Below headers

        // ScrollRect
        ScrollRect sr = scrollArea.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // Mask
        Image scrollBG = scrollArea.AddComponent<Image>();
        scrollBG.color = Color.white; // alpha must be 1 for Mask to work
        Mask mask = scrollArea.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content container
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
        GameObject backGO = CreateUI("BackButton", board.transform);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0f, 0f);
        backRT.anchorMax = new Vector2(0f, 0f);
        backRT.pivot = new Vector2(0f, 0f);
        backRT.anchoredPosition = new Vector2(15f, 10f);
        backRT.sizeDelta = new Vector2(130f, 40f);

        Image backBG = backGO.AddComponent<Image>();
        backBG.color = new Color(0.14f, 0.11f, 0.06f, 0.95f);

        Button backBtn = backGO.AddComponent<Button>();
        ColorBlock cb = backBtn.colors;
        cb.highlightedColor = new Color(0.25f, 0.20f, 0.10f, 1f);
        cb.pressedColor = new Color(0.35f, 0.28f, 0.14f, 1f);
        backBtn.colors = cb;
        backBtn.onClick.AddListener(OnBackClicked);

        // Back button border
        Outline backOutline = backGO.AddComponent<Outline>();
        backOutline.effectColor = goldBorderDim;
        backOutline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject backTextGO = CreateUI("Text", backGO.transform);
        RectTransform backTextRT = backTextGO.GetComponent<RectTransform>();
        backTextRT.anchorMin = Vector2.zero;
        backTextRT.anchorMax = Vector2.one;
        backTextRT.offsetMin = Vector2.zero;
        backTextRT.offsetMax = Vector2.zero;
        backTextGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI backTMP = backTextGO.AddComponent<TextMeshProUGUI>();
        backTMP.text = "< BACK";
        backTMP.fontSize = 18;
        backTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        backTMP.color = goldText;
        backTMP.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) backTMP.font = medievalFont;

        // ===== Refresh button =====
        GameObject refreshGO = CreateUI("RefreshButton", board.transform);
        RectTransform refreshRT = refreshGO.GetComponent<RectTransform>();
        refreshRT.anchorMin = new Vector2(1f, 0f);
        refreshRT.anchorMax = new Vector2(1f, 0f);
        refreshRT.pivot = new Vector2(1f, 0f);
        refreshRT.anchoredPosition = new Vector2(-15f, 10f);
        refreshRT.sizeDelta = new Vector2(130f, 40f);

        Image refreshBG = refreshGO.AddComponent<Image>();
        refreshBG.color = new Color(0.14f, 0.11f, 0.06f, 0.95f);

        Button refreshBtn = refreshGO.AddComponent<Button>();
        ColorBlock rcb = refreshBtn.colors;
        rcb.highlightedColor = new Color(0.25f, 0.20f, 0.10f, 1f);
        rcb.pressedColor = new Color(0.35f, 0.28f, 0.14f, 1f);
        refreshBtn.colors = rcb;
        refreshBtn.onClick.AddListener(FetchLeaderboard);

        Outline refreshOutline = refreshGO.AddComponent<Outline>();
        refreshOutline.effectColor = goldBorderDim;
        refreshOutline.effectDistance = new Vector2(1.5f, 1.5f);

        GameObject refreshTextGO = CreateUI("Text", refreshGO.transform);
        RectTransform rTextRT = refreshTextGO.GetComponent<RectTransform>();
        rTextRT.anchorMin = Vector2.zero;
        rTextRT.anchorMax = Vector2.one;
        rTextRT.offsetMin = Vector2.zero;
        rTextRT.offsetMax = Vector2.zero;
        refreshTextGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI refreshTMP = refreshTextGO.AddComponent<TextMeshProUGUI>();
        refreshTMP.text = "REFRESH";
        refreshTMP.fontSize = 16;
        refreshTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        refreshTMP.color = goldText;
        refreshTMP.alignment = TextAlignmentOptions.Center;
        if (medievalFont != null) refreshTMP.font = medievalFont;
    }

    private void CreateHeaderCell(Transform parent, string text, float width)
    {
        GameObject cellGO = CreateUI($"Header_{text}", parent);
        cellGO.AddComponent<CanvasRenderer>();
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        TextMeshProUGUI tmp = cellGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        tmp.color = goldBorderDim;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 4f;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    // ===================== FIREBASE FETCH =====================

    private void FetchLeaderboard()
    {
        // Show loading state
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        if (statusText != null) statusText.text = "Fetching rankings...";

        // Clear existing entries
        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }

        DatabaseReference dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        // Read from "leaderboard" node (public, safe data only — no passwords/security answers)
        dbRef.Child("leaderboard").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[Leaderboard] Failed to fetch data: " + task.Exception);
                if (statusText != null) statusText.text = "Failed to load leaderboard.";
                return;
            }

            if (!task.Result.Exists || !task.Result.HasChildren)
            {
                if (statusText != null) statusText.text = "No players found yet. Play the game!";
                return;
            }

            List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

            foreach (DataSnapshot userSnap in task.Result.Children)
            {
                LeaderboardEntry entry = new LeaderboardEntry();

                // Username
                if (userSnap.HasChild("username"))
                    entry.username = userSnap.Child("username").Value?.ToString() ?? "Unknown";
                else
                    entry.username = "Unknown";

                // Level progress
                if (userSnap.HasChild("lastCompletedLevel"))
                    int.TryParse(userSnap.Child("lastCompletedLevel").Value?.ToString(), out entry.lastCompletedLevel);

                // Puzzles completed
                if (userSnap.HasChild("puzzlesCompleted"))
                    int.TryParse(userSnap.Child("puzzlesCompleted").Value?.ToString(), out entry.puzzlesCompleted);

                // Profile picture
                if (userSnap.HasChild("profilePicture"))
                    entry.profilePicture = userSnap.Child("profilePicture").Value?.ToString() ?? DEFAULT_PROFILE_PIC;
                else
                    entry.profilePicture = DEFAULT_PROFILE_PIC;

                entries.Add(entry);
            }

            // Sort: by level (desc), then by puzzles (desc), then by name (asc)
            entries = entries
                .OrderByDescending(e => e.lastCompletedLevel)
                .ThenByDescending(e => e.puzzlesCompleted)
                .ThenBy(e => e.username)
                .Take(maxEntries)
                .ToList();

            // Hide loading
            if (loadingIndicator != null) loadingIndicator.SetActive(false);

            // Populate rows
            PopulateLeaderboard(entries);

            Debug.Log($"[Leaderboard] Loaded {entries.Count} entries.");
        });
    }

    // ===================== POPULATE ROWS =====================

    private void PopulateLeaderboard(List<LeaderboardEntry> entries)
    {
        if (contentParent == null) return;

        // Get current player username to highlight their row
        string currentUsername = "";
        if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
            currentUsername = AccountManager.Instance.GetCurrentPlayer().username ?? "";

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            int rank = i + 1;
            bool isCurrentPlayer = !string.IsNullOrEmpty(currentUsername) &&
                                   entry.username.Equals(currentUsername, System.StringComparison.OrdinalIgnoreCase);

            CreateEntryRow(rank, entry, isCurrentPlayer, i % 2 == 0);
        }
    }

    private void CreateEntryRow(int rank, LeaderboardEntry entry, bool isCurrentPlayer, bool evenRow)
    {
        // Row container
        GameObject rowGO = CreateUI($"Row_{rank}", contentParent);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 44f;

        // Row background
        Image rowBG = rowGO.AddComponent<Image>();
        if (isCurrentPlayer)
            rowBG.color = highlightRow;
        else
            rowBG.color = evenRow ? rowEven : rowOdd;

        // Horizontal layout
        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 3, 3);
        hlg.spacing = 5f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Rank
        Color rankColor = creamText;
        string rankText = rank.ToString();
        if (rank == 1) { rankColor = rankGold; rankText = "* 1"; }
        else if (rank == 2) { rankColor = rankSilver; rankText = "2"; }
        else if (rank == 3) { rankColor = rankBronze; rankText = "3"; }

        CreateCell(rowGO.transform, rankText, 50f, rankColor, rank <= 3 ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center);

        // Profile picture
        CreateProfilePicCell(rowGO.transform, entry.profilePicture, 38f);

        // Username
        string displayName = entry.username;
        if (isCurrentPlayer) displayName += "  (You)";
        CreateCell(rowGO.transform, displayName, 240f, isCurrentPlayer ? goldText : creamText, isCurrentPlayer ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Left);

        // Level
        CreateCell(rowGO.transform, entry.lastCompletedLevel.ToString(), 100f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Puzzles
        CreateCell(rowGO.transform, entry.puzzlesCompleted.ToString(), 100f, creamText, FontStyles.Normal, TextAlignmentOptions.Center);

        // Gold left accent for top 3
        if (rank <= 3)
        {
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
    }

    /// <summary>
    /// Creates a profile picture cell in the leaderboard row.
    /// Shows a small circular-looking profile image with a gold border for top 3.
    /// </summary>
    private void CreateProfilePicCell(Transform parent, string pictureName, float size)
    {
        // Outer container with layout element
        GameObject cellGO = CreateUI("ProfilePic", parent);
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;

        // Border / background (acts as a frame)
        Image borderImg = cellGO.AddComponent<Image>();
        borderImg.color = goldBorderDim;

        // Inner picture
        GameObject picGO = CreateUI("Pic", cellGO.transform);
        RectTransform picRT = picGO.GetComponent<RectTransform>();
        picRT.anchorMin = Vector2.zero;
        picRT.anchorMax = Vector2.one;
        picRT.offsetMin = new Vector2(2f, 2f);  // 2px border
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
            picImg.color = new Color(0.3f, 0.25f, 0.15f, 1f); // Placeholder dark color
        }
    }

    private void CreateCell(Transform parent, string text, float width, Color color, FontStyles style, TextAlignmentOptions align)
    {
        GameObject cellGO = CreateUI("Cell", parent);
        cellGO.AddComponent<CanvasRenderer>();
        LayoutElement le = cellGO.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        TextMeshProUGUI tmp = cellGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (medievalFont != null) tmp.font = medievalFont;
    }

    // ===================== PROFILE PICTURES =====================

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
        // Treat empty, null, or old default name as the current default
        if (string.IsNullOrEmpty(pictureName) || pictureName == "image-removebg-preview")
            pictureName = DEFAULT_PROFILE_PIC;

        if (profileSpriteCache.ContainsKey(pictureName))
            return profileSpriteCache[pictureName];

        // Fallback to default silhouette
        if (profileSpriteCache.ContainsKey(DEFAULT_PROFILE_PIC))
            return profileSpriteCache[DEFAULT_PROFILE_PIC];

        return null;
    }

    // ===================== HELPERS =====================

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
