using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Globalization;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("UI Prefab")]
    public GameObject uiManagerPrefab;

    private int currentLevel = 1;
    private bool isPuzzleCompleted = false;
    private bool isLoadingGame = false;
    private bool shouldRestorePosition = false;
    private Coroutine outOfBoundsWatchdogRoutine;
    private Vector3 lastSafePlayerPosition = Vector3.zero;
    private float lastSafePlayerYaw = 0f;
    private float lastSafePlayerSampleTime = -999f;
    private float currentSceneVoidThreshold = float.NegativeInfinity;

    [Header("Player Safety")]
    [Tooltip("If player Y drops below this, auto-rescue to the last safe position.")]
    public float voidYThreshold = -60f;
    [Tooltip("Extreme hard limit for broken coordinates (very high on purpose; normal gameplay never reaches this).")]
    public float emergencyMaxAbsCoordinate = 100000f;
    [Tooltip("How often to check for out-of-bounds/fall events.")]
    public float safetyCheckInterval = 0.25f;
    [Tooltip("Enable automatic out-of-bounds rescue teleports. Disable for collision debugging to prevent forced position rewinds.")]
    public bool enableOutOfBoundsRescue = false;
    [Tooltip("Skip out-of-bounds rescue teleports in Level6 where large geometry and vertical layouts can cause false positives.")]
    public bool disableOutOfBoundsRescueInLevel6 = true;
    [Tooltip("Attach PlayerMotionDebugLogger at runtime in level scenes.")]
    public bool enableMotionDebugLogger = true;

    [Header("Invisible Blocker Hotfix")]
    [Tooltip("Disable likely invisible, non-trigger colliders in Level6 to remove unintended blocker volumes.")]
    public bool disableLevel6InvisibleBlockers = true;
    [Tooltip("Apply invisible-blocker cleanup in Chapter 2 levels (Level5-8).")]
    public bool disableChapter2InvisibleBlockers = true;
    [Tooltip("Apply invisible-blocker cleanup to all playable levels. Uses conservative name checks outside Level5-8.")]
    public bool disableAllLevelInvisibleBlockers = true;


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
        StopOutOfBoundsWatchdog();
    }

    void Start()
    {
        isPuzzleCompleted = false;

        // Bootstrap the LevelTimer singleton if not already present
        if (LevelTimer.Instance == null)
        {
            GameObject timerGO = new GameObject("LevelTimer");
            timerGO.AddComponent<LevelTimer>();
            Debug.Log("[LevelManager] Created LevelTimer singleton.");
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + scene.name + ", isLoadingGame: " + isLoadingGame);

        if (scene.name == "Level1")
            CleanupDuplicateLevel1Doors();

        if (scene.name.StartsWith("Level"))
            EnsureDungeonLightingManager();

        // ── Reset static key/candle flags on every level load ──
        // These statics can persist across scenes and even editor play sessions.
        if (scene.name.StartsWith("Level"))
        {
            TutorialDoor.PlayerHasKey = false;
            TutorialDoor.TutorialKeyCollected = false;
            SuccessDoor.PlayerHasSuccessKey = false;
            CollectibleCandle.IsEquipped = false;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.SetHasCandle(false);
            Debug.Log("[LevelManager] Reset key/candle flags for fresh level load.");

            // Reset cached rescue data per level load.
            lastSafePlayerPosition = Vector3.zero;
            lastSafePlayerYaw = 0f;
            lastSafePlayerSampleTime = -999f;
            currentSceneVoidThreshold = float.NegativeInfinity;
        }


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

            // Restore saved position if this is a Load Game with saved position
            if (shouldRestorePosition)
            {
                shouldRestorePosition = false;

                // ── INSTANT teleport (frame 0) — prevents the visible "pop" at spawn ──
                var pData = AccountManager.Instance?.GetCurrentPlayer();
                if (pData != null && pData.savedLevel > 0)
                {
                    Vector3 target = new Vector3(pData.savedPosX, pData.savedPosY, pData.savedPosZ);
                    if (target != Vector3.zero || pData.savedRotY != 0f)
                    {
                        Debug.Log($"[LevelManager] OnSceneLoaded: INSTANT teleport to ({target.x:F2},{target.y:F2},{target.z:F2})");
                        TeleportPlayer(target, pData.savedRotY);
                    }
                }

                // ── Delayed safety-net: verify the position stuck after scripts initialise ──
                Debug.Log("[LevelManager] OnSceneLoaded: Starting verification coroutine...");
                StartCoroutine(VerifyPlayerPositionAfterDelay());
            }
            else
            {
                Debug.Log("[LevelManager] OnSceneLoaded: shouldRestorePosition was FALSE — no position restore needed.");
            }

            isLoadingGame = false;

            // ── Belt-and-suspenders: sweep all gates after a short delay ──
            // Individual Interactable.Start() checks may miss gates if data arrives late.
            StartCoroutine(SweepDestroyedGatesAfterDelay());
        }

        if (scene.name.StartsWith("Level") && enableOutOfBoundsRescue)
            StartOutOfBoundsWatchdog();
        else
            StopOutOfBoundsWatchdog();

        if (scene.name.StartsWith("Level") && enableMotionDebugLogger)
            StartCoroutine(AttachMotionDebugLoggerNextFrame());

        bool shouldRunLevel6Fix = scene.name == "Level6" && disableLevel6InvisibleBlockers;
        bool shouldRunChapter2Fix = disableChapter2InvisibleBlockers && IsLevel5To8Scene(scene.name);
        bool shouldRunAllLevelFix = disableAllLevelInvisibleBlockers && IsAnyLevelScene(scene.name);
        if (shouldRunLevel6Fix || shouldRunChapter2Fix || shouldRunAllLevelFix)
            StartCoroutine(DisableLikelyInvisibleBlockersNextFrame());

    }

    private System.Collections.IEnumerator DisableLikelyInvisibleBlockersNextFrame()
    {
        // Wait one frame so scene bootstrap scripts can finish adding runtime objects.
        yield return null;

        Scene active = SceneManager.GetActiveScene();
        if (!IsAnyLevelScene(active.name))
            yield break;

        bool broadChapter2Mode = disableChapter2InvisibleBlockers && IsLevel5To8Scene(active.name);

        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        if (allColliders == null || allColliders.Length == 0)
            yield break;

        int disabledCount = 0;
        var disabledDetails = new System.Collections.Generic.List<string>();

        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider col = allColliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            if (!col.gameObject.activeInHierarchy)
                continue;

            // Keep any collider that is represented by visible geometry.
            if (HasAnyEnabledRendererNearby(col.transform))
                continue;

            // Keep known gameplay/self colliders that should never be stripped.
            if (col is CharacterController)
                continue;
            if (col.GetComponentInParent<StarterAssets.FirstPersonController>() != null)
                continue;
            if (col.CompareTag("Player"))
                continue;

            string lowerName = col.gameObject.name.ToLowerInvariant();
            bool explicitlySuspicious =
                lowerName.Contains("invisible") ||
                lowerName.Contains("block") ||
                lowerName.Contains("barrier") ||
                lowerName.Contains("boundary") ||
                lowerName.Contains("wall");

            // Chapter 2 uses broader cleanup for unnamed FBX blockers.
            // Other levels use conservative name-based cleanup to reduce risk of disabling intentional gameplay colliders.
            bool shouldDisable = broadChapter2Mode ? true : explicitlySuspicious;
            if (shouldDisable)
            {
                col.enabled = false;
                disabledCount++;

                if (disabledDetails.Count < 20)
                {
                    string path = GetHierarchyPath(col.transform);
                    disabledDetails.Add($"{col.GetType().Name} @ {path}");
                }
            }
        }

        string mode = broadChapter2Mode ? "broad" : "conservative";
        Debug.Log($"[LevelManager] Invisible-blocker hotfix ({mode}) disabled {disabledCount} collider(s) in {active.name}.");
        for (int i = 0; i < disabledDetails.Count; i++)
            Debug.Log($"[LevelManager]   disabled[{i + 1}]: {disabledDetails[i]}");
    }

    private static bool IsAnyLevelScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level");
    }

    private static bool IsLevel5To8Scene(string sceneName)
    {
        return sceneName == "Level5" || sceneName == "Level6" || sceneName == "Level7" || sceneName == "Level8";
    }

    private static bool HasAnyEnabledRendererNearby(Transform t)
    {
        if (t == null)
            return false;

        Renderer[] ownAndChildren = t.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < ownAndChildren.Length; i++)
        {
            Renderer r = ownAndChildren[i];
            if (r != null && r.enabled)
                return true;
        }

        Renderer[] parents = t.GetComponentsInParent<Renderer>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            Renderer r = parents[i];
            if (r != null && r.enabled)
                return true;
        }

        return false;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "<null>";

        string path = t.name;
        Transform cur = t.parent;
        while (cur != null)
        {
            path = cur.name + "/" + path;
            cur = cur.parent;
        }

        return path;
    }

    private System.Collections.IEnumerator AttachMotionDebugLoggerNextFrame()
    {
        // Let scene objects initialize first.
        yield return null;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject playerGO = FindActiveScenePlayerWithCharacterController(activeScene);
        if (playerGO == null)
            playerGO = PauseMenuController.FindPlayerWithCharacterController();

        if (playerGO == null)
        {
            Debug.LogWarning("[LevelManager] MotionDebug logger not attached: player not found.");
            yield break;
        }

        if (playerGO.GetComponent<PlayerMotionDebugLogger>() == null)
        {
            playerGO.AddComponent<PlayerMotionDebugLogger>();
            Debug.Log($"[LevelManager] MotionDebug logger attached to '{playerGO.name}'.");
        }
    }

    private void CleanupDuplicateLevel1Doors()
    {
        CleanupDuplicateNamedObjects("lvl1_NewEnvironment");
        CleanupDuplicateNamedObjects("Door_Tutorial");
        CleanupDuplicateNamedObjects("Door_Success");
    }

    private void EnsureDungeonLightingManager()
    {
        DungeonLightingManager existing = FindAnyObjectByType<DungeonLightingManager>();
        if (existing != null) return;

        GameObject go = new GameObject("DungeonLightingManager_Auto");
        go.AddComponent<DungeonLightingManager>();
        Debug.Log("[LevelManager] Auto-created DungeonLightingManager for level scene.");
    }

    private void CleanupDuplicateNamedObjects(string exactName)
    {
        if (string.IsNullOrWhiteSpace(exactName)) return;

        GameObject[] all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return;

        var matches = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null || !go.scene.IsValid()) continue;
            if (go.name == exactName)
                matches.Add(go);
        }

        if (matches.Count <= 1) return;

        // Keep the door closest to world origin and remove duplicates.
        // This gives deterministic behavior when duplicates are present.
        matches.Sort((a, b) =>
            a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude));

        for (int i = 1; i < matches.Count; i++)
        {
            if (matches[i] != null)
                Destroy(matches[i]);
        }

        Debug.LogWarning($"[LevelManager] Removed {matches.Count - 1} duplicate object(s) named '{exactName}' from Level1 at runtime.");
    }

    /// <summary>
    /// Post-load safety sweep: finds all Interactable objects whose gateID is in
    /// destroyedGates and destroys them.  Runs a few frames after level load.
    /// </summary>
    private System.Collections.IEnumerator SweepDestroyedGatesAfterDelay()
    {
        // Wait a few frames so all Interactable.Start() methods have generated their gateIDs.
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player == null || player.destroyedGates == null || player.destroyedGates.Count == 0)
        {
            Debug.Log("[LevelManager] SweepDestroyedGates: No destroyed gates to sweep.");
            yield break;
        }

        Debug.Log($"[LevelManager] SweepDestroyedGates: Checking for {player.destroyedGates.Count} collected gate(s)...");

        Interactable[] allGates = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        int destroyed = 0;

        foreach (Interactable gate in allGates)
        {
            if (gate == null) continue;

            // The gate's gateID was computed in its Start() — compare to destroyedGates list.
            if (player.destroyedGates.Contains(gate.gateID))
            {
                Debug.Log($"[LevelManager] SweepDestroyedGates: Destroying collected gate '{gate.gateID}'");
                Destroy(gate.gameObject);
                destroyed++;
            }
            else
            {
                // Also try an invariant-culture re-generated ID in case the gate was saved
                // with the old locale-dependent format before the fix.
                string invariantID = string.Format(CultureInfo.InvariantCulture,
                    "{0}_{1}_{2:F3}_{3:F3}_{4:F3}",
                    gate.gateType, gate.gameObject.name,
                    gate.transform.position.x, gate.transform.position.y, gate.transform.position.z);

                // Check if the OLD format (no decimal precision) matches anything in destroyedGates
                string legacyID = $"{gate.gateType}_{gate.gameObject.name}_{gate.transform.position.x}_{gate.transform.position.y}_{gate.transform.position.z}";

                if (player.destroyedGates.Contains(invariantID) || player.destroyedGates.Contains(legacyID))
                {
                    Debug.Log($"[LevelManager] SweepDestroyedGates: Destroying gate via legacy/invariant match '{gate.gateID}' (legacyID='{legacyID}')");
                    Destroy(gate.gameObject);
                    destroyed++;
                }
            }
        }

        Debug.Log($"[LevelManager] SweepDestroyedGates: Finished. {destroyed} gate(s) destroyed, {allGates.Length - destroyed} remaining.");
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

            // Stop the level timer and record the best time
            if (LevelTimer.Instance != null)
                LevelTimer.Instance.StopAndRecordTime();

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
            player.completedPuzzles.Clear(); // Reset puzzle completion so success keys don't show on new game
            player.andGatesCollected = 0;
            player.orGatesCollected = 0;
            player.notGatesCollected = 0;

            // Clear mid-level save data
            player.savedPosX = 0f;
            player.savedPosY = 0f;
            player.savedPosZ = 0f;
            player.savedRotY = 0f;
            player.savedLevel = 0;

            player.destroyedGates.Clear();
            player.savedGateLayout = ""; // Force fresh random gate placement

            AccountManager.Instance.SavePlayerProgress();
            Debug.Log("New Game: Player data reset (including saved position, gate layout, completed puzzles) and saved to Firebase");
        }


        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ResetInventory();
        }


        isLoadingGame = false;

        LoadLevel(1);
    }


    /// <summary>
    /// Load Game: Re-fetches the latest data from Firebase, syncs inventory, then loads the level.
    /// This ensures we always use the actual saved state, not stale in-memory data.
    /// </summary>
    public void ContinueGame()
    {
        Debug.Log("====== LOAD GAME START ======");
        Debug.Log("LOAD GAME: Fetching latest data from Firebase...");

        if (AccountManager.Instance == null)
        {
            Debug.LogWarning("No AccountManager! Showing main menu.");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMainMenu();
            return;
        }

        // Re-fetch from Firebase to ensure we have the absolute latest saved data
        AccountManager.Instance.RefreshPlayerDataFromFirebase((success) =>
        {
            if (!success)
            {
                // Fallback: try using in-memory data if Firebase fetch fails
                Debug.LogWarning("[ContinueGame] Firebase fetch FAILED — trying in-memory data...");
            }

            var player = AccountManager.Instance.GetCurrentPlayer();
            if (player != null)
            {
                Debug.Log($"[ContinueGame] ====== PLAYER DATA FROM FIREBASE ======");
                Debug.Log($"[ContinueGame] lastCompletedLevel={player.lastCompletedLevel}, unlockedLevels={player.unlockedLevels}");
                Debug.Log($"[ContinueGame] savedLevel={player.savedLevel}");
                Debug.Log($"[ContinueGame] savedPos=({player.savedPosX:F2},{player.savedPosY:F2},{player.savedPosZ:F2})");
                Debug.Log($"[ContinueGame] savedRotY={player.savedRotY:F1}");
                Debug.Log($"[ContinueGame] gates AND={player.andGatesCollected}, OR={player.orGatesCollected}, NOT={player.notGatesCollected}");
                Debug.Log($"[ContinueGame] destroyedGates count={player.destroyedGates.Count}");
                if (player.destroyedGates.Count > 0)
                {
                    foreach (var id in player.destroyedGates)
                        Debug.Log($"[ContinueGame]   destroyedGate: '{id}'");
                }

                // Sync InventoryManager from the FRESH Firebase data
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.SyncFromCloud(
                        player.andGatesCollected,
                        player.orGatesCollected,
                        player.notGatesCollected
                    );
                    Debug.Log("[ContinueGame] InventoryManager synced from fresh Firebase data.");
                }

                // Decide which level to load and whether to restore position
                int levelToLoad;

                if (player.savedLevel > 0)
                {
                    levelToLoad = player.savedLevel;
                    shouldRestorePosition = true;
                    Debug.Log($"[ContinueGame] >>> MID-LEVEL SAVE DETECTED! Loading Level {levelToLoad}, will restore to ({player.savedPosX:F2},{player.savedPosY:F2},{player.savedPosZ:F2})");
                }
                else
                {
                    levelToLoad = player.lastCompletedLevel + 1;
                    shouldRestorePosition = false;
                    Debug.Log($"[ContinueGame] >>> No mid-level save (savedLevel=0). Loading Level {levelToLoad} from spawn.");
                }

                Debug.Log($"[ContinueGame] Setting isLoadingGame=true, shouldRestorePosition={shouldRestorePosition}, starting transition to Level {levelToLoad}");
                isLoadingGame = true;
                StartCoroutine(LoadLevelWithSplash(levelToLoad));
            }
            else
            {
                Debug.LogWarning("[ContinueGame] No player logged in! Showing main menu.");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMainMenu();
                }
            }
        });
    }

    private System.Collections.IEnumerator LoadLevelWithSplash(int levelToLoad)
    {
        GameObject overlay = CreateLoadOverlay(levelToLoad);
        if (overlay != null)
        {
            Object.DontDestroyOnLoad(overlay);
            // Keep this short so load game stays responsive while still showing LEVEL X.
            yield return new WaitForSeconds(0.9f);
            overlay.AddComponent<FadeOverlayAutoDestroy>();
        }

        LoadLevel(levelToLoad);
    }

    private GameObject CreateLoadOverlay(int levelNumber)
    {
        GameObject root = new GameObject("LoadGame_FadeOverlay");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        root.AddComponent<GraphicRaycaster>();

        CanvasGroup cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = false;

        GameObject bg = new GameObject("BlackOverlay");
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.raycastTarget = true;

        GameObject textGO = new GameObject("LevelTransitionText");
        textGO.transform.SetParent(root.transform, false);
        RectTransform textRt = textGO.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.1f, 0.35f);
        textRt.anchorMax = new Vector2(0.9f, 0.65f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = $"LEVEL {levelNumber}";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 64;
        tmp.fontSizeMax = 180;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.95f, 0.87f, 0.62f, 1f);
        tmp.raycastTarget = false;

        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.1f, 0.05f, 0.95f);
        outline.effectDistance = new Vector2(4f, 4f);

        return root;
    }

    /// <summary>
    /// Lightweight verification coroutine.  The INSTANT teleport already happened in
    /// OnSceneLoaded (frame 0).  This waits a few frames for Start()/gravity/etc. to
    /// settle, then checks if the position drifted and re-teleports if necessary.
    /// </summary>
    private System.Collections.IEnumerator VerifyPlayerPositionAfterDelay()
    {
        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player == null || player.savedLevel <= 0)
        {
            Debug.LogWarning("[LevelManager] VerifyPosition: No saved data — skipping.");
            yield break;
        }

        Vector3 targetPos = new Vector3(player.savedPosX, player.savedPosY, player.savedPosZ);

        if (targetPos == Vector3.zero && player.savedRotY == 0f)
        {
            yield break; // No real save data
        }

        // Wait just 3 frames for Start() / first-Update() / gravity to settle
        yield return null;
        yield return null;
        yield return null;

        // Check if something moved the player away from the target
        for (int attempt = 0; attempt < 2; attempt++)
        {
            GameObject playerGO = PauseMenuController.FindPlayerWithCharacterController();
            if (playerGO == null) yield break;

            float drift = Vector3.Distance(playerGO.transform.position, targetPos);
            Debug.Log($"[LevelManager] VerifyPosition (attempt {attempt + 1}): actual=({playerGO.transform.position.x:F2},{playerGO.transform.position.y:F2},{playerGO.transform.position.z:F2}), drift={drift:F2}");

            if (drift < 3f)
            {
                Debug.Log("[LevelManager] Position restore CONFIRMED.");
                yield break; // All good
            }

            // Something overrode the position — re-teleport
            Debug.LogWarning($"[LevelManager] Position drifted {drift:F1}m — re-teleporting...");
            TeleportPlayer(targetPos, player.savedRotY);

            // Wait a couple frames before verifying again
            yield return null;
            yield return null;
        }
    }

    /// <summary>
    /// Teleports the player to the given position, temporarily disabling the CharacterController.
    /// Returns false if the Player GameObject was not found.
    /// 
    /// IMPORTANT: Uses FindPlayerWithCharacterController() to find the actual moving player
    /// object (PlayerCapsule), NOT the static parent (FirstPersonPlayer).
    /// </summary>
    private bool TeleportPlayer(Vector3 pos, float rotY)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject playerGO = FindActiveScenePlayerWithCharacterController(activeScene);
        if (playerGO == null)
            playerGO = PauseMenuController.FindPlayerWithCharacterController();
        if (playerGO == null)
        {
            Debug.LogWarning("[LevelManager] TeleportPlayer: Player with CharacterController not found!");
            return false;
        }

        Debug.Log($"[LevelManager] TeleportPlayer: '{playerGO.name}' FROM ({playerGO.transform.position.x:F2},{playerGO.transform.position.y:F2},{playerGO.transform.position.z:F2}) → TO ({pos.x:F2},{pos.y:F2},{pos.z:F2}) rotY={rotY:F1}");

        var cc = playerGO.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerGO.transform.position = pos;
        playerGO.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        // Force physics to acknowledge the new position before re-enabling
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        // Clear residual overlap/velocity side effects on the first frame after teleport.
        if (cc != null)
            cc.Move(Vector3.zero);

        lastSafePlayerPosition = pos;
        lastSafePlayerYaw = rotY;
        lastSafePlayerSampleTime = Time.time;

        Debug.Log($"[LevelManager] TeleportPlayer: Actual position after set = ({playerGO.transform.position.x:F2},{playerGO.transform.position.y:F2},{playerGO.transform.position.z:F2})");
        return true;
    }

    private void StartOutOfBoundsWatchdog()
    {
        StopOutOfBoundsWatchdog();
        NormalizeSafetySettings();
        InitializeSceneSafetyBounds();
        outOfBoundsWatchdogRoutine = StartCoroutine(OutOfBoundsWatchdog());
    }

    private void StopOutOfBoundsWatchdog()
    {
        if (outOfBoundsWatchdogRoutine != null)
        {
            StopCoroutine(outOfBoundsWatchdogRoutine);
            outOfBoundsWatchdogRoutine = null;
        }
    }

    private System.Collections.IEnumerator OutOfBoundsWatchdog()
    {
        // Wait a frame for scene/player initialization.
        yield return null;
        int invalidStreak = 0;

        while (true)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.name.StartsWith("Level"))
            {
                invalidStreak = 0;
                yield return new WaitForSeconds(safetyCheckInterval);
                continue;
            }

            if (disableOutOfBoundsRescueInLevel6 && active.name == "Level6")
            {
                invalidStreak = 0;
                yield return new WaitForSeconds(safetyCheckInterval);
                continue;
            }

            GameObject playerGO = FindActiveScenePlayerWithCharacterController(active);
            if (playerGO == null)
            {
                invalidStreak = 0;
                yield return new WaitForSeconds(safetyCheckInterval);
                continue;
            }

            Vector3 pos = playerGO.transform.position;
            float yThreshold = float.IsNegativeInfinity(currentSceneVoidThreshold) ? voidYThreshold : currentSceneVoidThreshold;
            bool hasNaN = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
            bool fellBelowThreshold = pos.y < yThreshold;
            bool invalid = hasNaN || fellBelowThreshold;

            if (invalid)
            {
                invalidStreak++;
                if (invalidStreak < 2)
                {
                    // Ignore one-off bad samples to prevent spurious teleports from transient physics spikes.
                    yield return new WaitForSeconds(safetyCheckInterval);
                    continue;
                }

                Vector3 rescuePos = lastSafePlayerPosition;
                float rescueYaw = lastSafePlayerYaw;
                bool hasRecentSafeSample =
                    rescuePos != Vector3.zero &&
                    (Time.time - lastSafePlayerSampleTime) <= 10f;

                if (!hasRecentSafeSample)
                {
                    // Fallback to spawn marker if we have no safe sample yet.
                    if (!TryGetSpawnFallback(out rescuePos, out rescueYaw))
                    {
                        rescuePos = new Vector3(0f, 2f, 0f);
                        rescueYaw = 0f;
                    }
                }

                string reason =
                    hasNaN ? "NaN coordinates" :
                    $"Y below threshold ({pos.y:F2} < {yThreshold:F2})";

                Debug.LogWarning($"[LevelManager] Out-of-bounds detected at ({pos.x:F2},{pos.y:F2},{pos.z:F2}) — reason: {reason} — rescuing player.");
                TeleportPlayer(rescuePos, rescueYaw);
                invalidStreak = 0;
            }
            else
            {
                invalidStreak = 0;
                // Cache a safe location while grounded (or vertically stable) to avoid stale rescues.
                CharacterController cc = playerGO.GetComponent<CharacterController>();
                bool groundedOrNoCC = (cc == null) || cc.isGrounded;
                float verticalSpeed = cc != null ? cc.velocity.y : 0f;
                bool verticallyStable = Mathf.Abs(verticalSpeed) < 4f;
                bool aboveSafetyMargin = pos.y > (yThreshold + 1.5f);

                if (aboveSafetyMargin && (groundedOrNoCC || verticallyStable))
                {
                    lastSafePlayerPosition = pos;
                    lastSafePlayerYaw = playerGO.transform.eulerAngles.y;
                    lastSafePlayerSampleTime = Time.time;
                }
            }

            yield return new WaitForSeconds(safetyCheckInterval);
        }
    }

    private bool TryGetSpawnFallback(out Vector3 spawnPos, out float spawnYaw)
    {
        spawnPos = Vector3.zero;
        spawnYaw = 0f;

        string[] preferredNames = new string[]
        {
            "PlayerSpawn",
            "PlayerSpawnPoint",
            "PlayerStart",
            "StartPoint",
            "RespawnPoint",
            "Respawn"
        };

        for (int i = 0; i < preferredNames.Length; i++)
        {
            GameObject spawn = GameObject.Find(preferredNames[i]);
            if (spawn == null) continue;

            spawnPos = spawn.transform.position + Vector3.up * 0.2f;
            spawnYaw = spawn.transform.eulerAngles.y;
            return true;
        }

        // Do not fallback to generic SpawnPoint names here; those are gate spawner markers.
        return false;
    }

    /// <summary>
    /// Finds the moving player capsule from the currently active scene only.
    /// Avoids grabbing stale/foreign Player-tagged objects from other loaded scenes.
    /// </summary>
    private GameObject FindActiveScenePlayerWithCharacterController(Scene activeScene)
    {
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return null;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            CharacterController[] ccs = roots[i].GetComponentsInChildren<CharacterController>(true);
            for (int j = 0; j < ccs.Length; j++)
            {
                CharacterController cc = ccs[j];
                if (cc == null) continue;
                if (cc.gameObject.scene != activeScene) continue;
                return cc.gameObject;
            }
        }

        return null;
    }

    private void InitializeSceneSafetyBounds()
    {
        currentSceneVoidThreshold = voidYThreshold;

        Scene active = SceneManager.GetActiveScene();
        if (TryGetSceneFloorMinY(active, out float minColliderY))
        {
            // Keep the rescue plane below all known solid colliders in this scene.
            float floorBasedThreshold = minColliderY - 8f;
            currentSceneVoidThreshold = Mathf.Min(currentSceneVoidThreshold, floorBasedThreshold);
        }

        if (TryGetSpawnFallback(out Vector3 spawnPos, out _))
        {
            // Use a scene-relative floor threshold to avoid false positives on deep maps (e.g. Level 6).
            currentSceneVoidThreshold = Mathf.Min(voidYThreshold, spawnPos.y - 25f);
            if (TryGetSceneFloorMinY(active, out minColliderY))
                currentSceneVoidThreshold = Mathf.Min(currentSceneVoidThreshold, minColliderY - 8f);

            Debug.Log($"[LevelManager] Safety bounds initialized. Scene={active.name}, SpawnY={spawnPos.y:F2}, MinColliderY={minColliderY:F2}, voidThreshold={currentSceneVoidThreshold:F2}");
        }
        else
        {
            string minColliderInfo = TryGetSceneFloorMinY(active, out minColliderY) ? minColliderY.ToString("F2") : "N/A";
            Debug.Log($"[LevelManager] Safety bounds initialized. Scene={active.name}, SpawnY=N/A, MinColliderY={minColliderInfo}, voidThreshold={currentSceneVoidThreshold:F2}");
        }
    }

    private void NormalizeSafetySettings()
    {
        // Guard against stale Inspector overrides from older scene versions.
        if (voidYThreshold > -20f)
        {
            Debug.LogWarning($"[LevelManager] voidYThreshold override ({voidYThreshold:F2}) is too high; forcing -60.");
            voidYThreshold = -60f;
        }

        if (emergencyMaxAbsCoordinate < 1000f)
        {
            Debug.LogWarning($"[LevelManager] emergencyMaxAbsCoordinate override ({emergencyMaxAbsCoordinate:F2}) is too low; forcing 100000.");
            emergencyMaxAbsCoordinate = 100000f;
        }

        if (safetyCheckInterval < 0.05f)
        {
            Debug.LogWarning($"[LevelManager] safetyCheckInterval override ({safetyCheckInterval:F3}) is too low; forcing 0.25.");
            safetyCheckInterval = 0.25f;
        }
    }

    private bool TryGetSceneFloorMinY(Scene scene, out float minY)
    {
        minY = float.PositiveInfinity;
        if (!scene.IsValid() || !scene.isLoaded) return false;

        bool found = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Collider[] colliders = roots[i].GetComponentsInChildren<Collider>(true);
            for (int j = 0; j < colliders.Length; j++)
            {
                Collider col = colliders[j];
                if (col == null || !col.enabled || col.isTrigger) continue;
                if (col.gameObject.scene != scene) continue;

                float y = col.bounds.min.y;
                if (float.IsNaN(y) || float.IsInfinity(y)) continue;

                if (y < minY)
                    minY = y;

                found = true;
            }
        }

        return found;
    }

    public bool CanAccessLevel(int level)
    {
        // DEV MODE: All levels unlocked for testing
        Debug.Log($"DEV MODE: Level {level} unlocked!");
        return true;
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
                player.savedGateLayout = ""; // Force fresh random gate placement
                Debug.Log("Cleared destroyedGates & gateLayout for fresh level start");
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