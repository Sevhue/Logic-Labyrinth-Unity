using UnityEngine;
using UnityEngine.SceneManagement;
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

            // Clear mid-level save data
            player.savedPosX = 0f;
            player.savedPosY = 0f;
            player.savedPosZ = 0f;
            player.savedRotY = 0f;
            player.savedLevel = 0;

            player.destroyedGates.Clear();
            player.savedGateLayout = ""; // Force fresh random gate placement

            AccountManager.Instance.SavePlayerProgress();
            Debug.Log("New Game: Player data reset (including saved position & gate layout) and saved to Firebase");
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

                Debug.Log($"[ContinueGame] Setting isLoadingGame=true, shouldRestorePosition={shouldRestorePosition}, calling LoadLevel({levelToLoad})");
                isLoadingGame = true;
                LoadLevel(levelToLoad);
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
        GameObject playerGO = PauseMenuController.FindPlayerWithCharacterController();
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

        Debug.Log($"[LevelManager] TeleportPlayer: Actual position after set = ({playerGO.transform.position.x:F2},{playerGO.transform.position.y:F2},{playerGO.transform.position.z:F2})");
        return true;
    }

    public bool CanAccessLevel(int level)
    {
        var player = AccountManager.Instance?.GetCurrentPlayer();

        
        if (level == 2)
        {
            Debug.Log("PRESENTATION MODE: Level 2 unlocked!");
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