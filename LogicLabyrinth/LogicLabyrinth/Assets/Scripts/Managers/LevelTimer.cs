using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Singleton that tracks elapsed time for the current level.
/// Automatically starts when a level scene loads, pauses when
/// Time.timeScale == 0 (pause menu), and stops when <see cref="StopTimer"/>
/// is called (level complete).
///
/// Also creates a small HUD element (top-right corner) that shows the
/// elapsed time and the player's personal best for the current level.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }

    /// <summary>Elapsed seconds since the level started.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>True while the timer is actively counting.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>The level number currently being timed.</summary>
    public int CurrentLevel { get; private set; }

    // ── HUD ──
    private GameObject hudRoot;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI bestTimeText;
    private TMP_FontAsset medievalFont;

    // Colors (medieval palette)
    private static readonly Color goldText = new Color(0.84f, 0.75f, 0.50f, 1f);
    private static readonly Color creamText = new Color(0.95f, 0.90f, 0.75f, 1f);
    private static readonly Color greenGlow = new Color(0.3f, 1f, 0.5f, 1f);

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

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void Start()
    {
        // Handle the currently loaded scene immediately (important when pressing Play directly
        // from Level2/Level3 in the editor, where sceneLoaded may not fire for this first scene).
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void Update()
    {
        if (IsRunning)
        {
            // Use regular deltaTime so the timer pauses when the game is paused (timeScale=0)
            ElapsedSeconds += Time.deltaTime;

            // Update HUD
            if (timerText != null)
                timerText.text = FormatTime(ElapsedSeconds);
        }
    }

    // ═══════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Resets and starts the timer for the given level.
    /// Called automatically when a level scene loads.
    /// </summary>
    public void StartTimer(int levelNumber)
    {
        CurrentLevel = levelNumber;
        ElapsedSeconds = 0f;
        IsRunning = true;
        Debug.Log($"[LevelTimer] Timer STARTED for Level {levelNumber}");

        // Create/refresh HUD
        CreateTimerHUD();
        UpdateBestTimeDisplay(levelNumber);
    }

    /// <summary>
    /// Stops the timer. Call this when the level is completed (e.g. SuccessDoor opens).
    /// </summary>
    public void StopTimer()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Debug.Log($"[LevelTimer] Timer STOPPED for Level {CurrentLevel} — Elapsed: {FormatTime(ElapsedSeconds)}");

        // Flash the timer green to indicate completion
        if (timerText != null)
            timerText.color = greenGlow;
    }

    /// <summary>
    /// Stops the timer and records the time as a best time if it beats the previous record.
    /// Returns the elapsed time in seconds.
    /// </summary>
    public float StopAndRecordTime()
    {
        StopTimer();

        float completionTime = ElapsedSeconds;

        // Record the best time in AccountManager
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.RecordLevelTime(CurrentLevel, completionTime);
        }

        // Update the best time display
        UpdateBestTimeDisplay(CurrentLevel);

        return completionTime;
    }

    /// <summary>
    /// Pauses the timer (e.g. when the pause menu is opened).
    /// </summary>
    public void PauseTimer()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Resumes the timer (e.g. when the pause menu is closed).
    /// </summary>
    public void ResumeTimer()
    {
        IsRunning = true;
    }

    // ═══════════════════════════════════════════════
    //  FORMATTING
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Formats seconds into a human-readable string (e.g. "2:34.56" or "0:05.12").
    /// </summary>
    public static string FormatTime(float seconds)
    {
        if (seconds <= 0f) return "--:--";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float secs = seconds % 60f;
        return $"{minutes}:{secs:00.00}";
    }

    /// <summary>
    /// Formats seconds into a compact string for leaderboard display (e.g. "2:34").
    /// </summary>
    public static string FormatTimeCompact(float seconds)
    {
        if (seconds <= 0f) return "--:--";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes}:{secs:00}";
    }

    // ═══════════════════════════════════════════════
    //  SCENE MANAGEMENT
    // ═══════════════════════════════════════════════

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Auto-start timer when a level scene loads (skip Level 1 — tutorial)
        if (scene.name.StartsWith("Level") && int.TryParse(scene.name.Substring(5), out int levelNum))
        {
            if (levelNum <= 1)
            {
                // Level 1 is the tutorial — no timer
                Debug.Log("[LevelTimer] Level 1 (tutorial) — timer disabled.");
                DestroyTimerHUD();
                IsRunning = false;
                return;
            }
            StartTimer(levelNum);
        }
        else
        {
            // Not a level scene — stop timer and hide HUD
            if (IsRunning)
            {
                IsRunning = false;
                Debug.Log("[LevelTimer] Left level scene — timer stopped (not recorded).");
            }
            DestroyTimerHUD();
        }
    }

    // ═══════════════════════════════════════════════
    //  TIMER HUD
    // ═══════════════════════════════════════════════

    private void CreateTimerHUD()
    {
        // Destroy old HUD if it exists
        DestroyTimerHUD();

        LoadMedievalFont();

        // Create a screen-space overlay canvas
        hudRoot = new GameObject("LevelTimerHUD");
        hudRoot.transform.SetParent(transform, false);

        Canvas canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // Below pause menu, above game UI

        CanvasScaler scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Container (bottom-right corner)
        GameObject container = new GameObject("TimerContainer", typeof(RectTransform));
        container.transform.SetParent(hudRoot.transform, false);
        RectTransform containerRT = container.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(1f, 0f);
        containerRT.anchorMax = new Vector2(1f, 0f);
        containerRT.pivot = new Vector2(1f, 0f);
        containerRT.anchoredPosition = new Vector2(-20f, 85f); // Above the hotbar
        containerRT.sizeDelta = new Vector2(200f, 60f);

        // Semi-transparent background
        Image bg = container.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.04f, 0.02f, 0.6f);
        bg.raycastTarget = false;

        Outline outline = container.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.58f, 0.30f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Timer icon + text
        GameObject timerGO = new GameObject("TimerText", typeof(RectTransform));
        timerGO.transform.SetParent(container.transform, false);
        RectTransform timerRT = timerGO.GetComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(0f, 0.5f);
        timerRT.anchorMax = new Vector2(1f, 1f);
        timerRT.offsetMin = new Vector2(10f, 0f);
        timerRT.offsetMax = new Vector2(-10f, -3f);

        timerGO.AddComponent<CanvasRenderer>();
        timerText = timerGO.AddComponent<TextMeshProUGUI>();
        timerText.text = "0:00.00";
        timerText.fontSize = 22;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = creamText;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.raycastTarget = false;
        if (medievalFont != null) timerText.font = medievalFont;

        // Best time (smaller, below main timer)
        GameObject bestGO = new GameObject("BestTimeText", typeof(RectTransform));
        bestGO.transform.SetParent(container.transform, false);
        RectTransform bestRT = bestGO.GetComponent<RectTransform>();
        bestRT.anchorMin = new Vector2(0f, 0f);
        bestRT.anchorMax = new Vector2(1f, 0.5f);
        bestRT.offsetMin = new Vector2(10f, 3f);
        bestRT.offsetMax = new Vector2(-10f, 0f);

        bestGO.AddComponent<CanvasRenderer>();
        bestTimeText = bestGO.AddComponent<TextMeshProUGUI>();
        bestTimeText.text = "BEST: --:--";
        bestTimeText.fontSize = 12;
        bestTimeText.fontStyle = FontStyles.SmallCaps;
        bestTimeText.color = goldText;
        bestTimeText.alignment = TextAlignmentOptions.Center;
        bestTimeText.raycastTarget = false;
        if (medievalFont != null) bestTimeText.font = medievalFont;
    }

    private void DestroyTimerHUD()
    {
        if (hudRoot != null)
        {
            Destroy(hudRoot);
            hudRoot = null;
            timerText = null;
            bestTimeText = null;
        }
    }

    private void UpdateBestTimeDisplay(int level)
    {
        if (bestTimeText == null) return;

        float best = -1f;
        if (AccountManager.Instance != null)
            best = AccountManager.Instance.GetBestTime(level);

        if (best > 0f)
            bestTimeText.text = $"BEST: {FormatTime(best)}";
        else
            bestTimeText.text = "BEST: --:--";
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
