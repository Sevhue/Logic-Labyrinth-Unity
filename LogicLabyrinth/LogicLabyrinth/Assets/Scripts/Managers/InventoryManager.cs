using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public TMPro.TMP_Text andGateText;
    public TMPro.TMP_Text orGateText;
    public TMPro.TMP_Text notGateText;

    // Backward-compatible constant for older references.
    public const int MAX_GATES = 5;
    public const int MIN_GATE_CAPACITY = 5;

    private Dictionary<string, int> gateCounts = new Dictionary<string, int> { { "AND", 0 }, { "OR", 0 }, { "NOT", 0 } };

    /// <summary>Whether the player has collected a candle.</summary>
    public bool HasCandle { get; private set; } = false;

    [Header("Level 1 Hints")]
    [SerializeField] private bool enableDropGateHint = true;
    [SerializeField] private int dropGateHintThreshold = 3;
    [SerializeField] private float dropGateHintInitialDelay = 1.5f;

    [Header("Level 2 Hints")]
    [SerializeField] private bool enableTabCursorHint = true;
    [SerializeField] private float tabCursorHintInitialDelay = 1f;

    private static bool dropGateHintShownThisSession = false;
    private GameObject dropGateHintUI;
    private Coroutine dropGateHintRoutine;

    private bool tabCursorHintShownForCurrentLevel2Entry = false;
    private GameObject tabCursorHintUI;
    private Coroutine tabCursorHintRoutine;
    private float tabHintCheckCooldown = 0f;

    public void SetHasCandle(bool value)
    {
        HasCandle = value;
        Debug.Log($"[InventoryManager] HasCandle = {value}");
    }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        TryShowTabCursorHint(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (tabCursorHintShownForCurrentLevel2Entry || tabCursorHintRoutine != null) return;

        tabHintCheckCooldown -= Time.unscaledDeltaTime;
        if (tabHintCheckCooldown > 0f) return;
        tabHintCheckCooldown = 0.5f;

        TryShowTabCursorHint(SceneManager.GetActiveScene());
    }

    // ============================
    // CAPACITY
    // ============================

    public int GetTotalGateCount()
    {
        int total = 0;
        foreach (var kvp in gateCounts)
            total += kvp.Value;
        return total;
    }

    public bool IsInventoryFull()
    {
        return GetTotalGateCount() >= GetCurrentGateCapacity();
    }

    public int GetCurrentGateCapacity()
    {
        // Default minimum capacity
        int capacity = MIN_GATE_CAPACITY;

        int level = 1;
        if (LevelManager.Instance != null)
            level = LevelManager.Instance.GetCurrentLevel();

        if (level <= 1)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(sceneName) &&
                sceneName.StartsWith("Level") &&
                int.TryParse(sceneName.Substring(5), out int parsedLevel) &&
                parsedLevel > 1)
            {
                level = parsedLevel;
            }
        }

        GateType[][] answerSets = AnswerKeyConfig.GetAnswerKeys(level);
        if (answerSets != null)
        {
            for (int i = 0; i < answerSets.Length; i++)
            {
                if (answerSets[i] != null && answerSets[i].Length > capacity)
                    capacity = answerSets[i].Length;
            }
        }

        return capacity;
    }

    // ============================
    // ADD / REMOVE
    // ============================

    public void AddGate(string gateType)
    {
        string key = gateType.ToUpper();
        if (IsInventoryFull())
        {
            Debug.LogWarning($"[InventoryManager] Cannot add {key} — inventory is full ({GetTotalGateCount()}/{GetCurrentGateCapacity()}).");
            return;
        }

        if (gateCounts.ContainsKey(key))
        {
            gateCounts[key]++;

            if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
            {
                var player = AccountManager.Instance.GetCurrentPlayer();
                player.andGatesCollected = gateCounts["AND"];
                player.orGatesCollected = gateCounts["OR"];
                player.notGatesCollected = gateCounts["NOT"];

                StartCoroutine(SaveAfterFrame(gateType));
            }
            UpdateLocalUI();
            NotifyGateCollected(key);
            TryShowDropGateHint();
        }
    }

    /// <summary>
    /// Removes one gate of the given type from inventory.
    /// Returns true if successful, false if player has none of that type.
    /// </summary>
    public bool RemoveGate(string gateType)
    {
        string key = gateType.ToUpper();
        if (gateCounts.ContainsKey(key) && gateCounts[key] > 0)
        {
            gateCounts[key]--;

            // Sync to Firebase
            if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
            {
                var player = AccountManager.Instance.GetCurrentPlayer();
                player.andGatesCollected = gateCounts["AND"];
                player.orGatesCollected = gateCounts["OR"];
                player.notGatesCollected = gateCounts["NOT"];
                StartCoroutine(SaveAfterFrame(key));
            }

            UpdateLocalUI();

            // Notify UI about the change
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.RefreshFromInventory();

            Debug.Log($"[InventoryManager] Removed 1 {key} gate. Remaining: {gateCounts[key]}. Total: {GetTotalGateCount()}/{GetCurrentGateCapacity()}");
            return true;
        }
        Debug.LogWarning($"[InventoryManager] Cannot remove {key} gate — count is 0.");
        return false;
    }

    // ============================
    // UI
    // ============================

    public void UpdateLocalUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGateCounts(gateCounts["AND"], gateCounts["OR"], gateCounts["NOT"]);
        }

        if (GameInventoryUI.Instance != null)
        {
            GameInventoryUI.Instance.UpdateCounts(gateCounts["AND"], gateCounts["OR"], gateCounts["NOT"]);
        }
    }

    public void NotifyGateCollected(string gateType)
    {
        if (GameInventoryUI.Instance != null)
        {
            GameInventoryUI.Instance.OnGateCollected(gateType);
        }

        string sceneName = SceneManager.GetActiveScene().name;
        bool showGateCard = sceneName == "Level1" || sceneName == "Level2";

        // Show first-time tutorial card only in Level1/Level2.
        // In higher levels, unlock journal entry silently without opening the card.
        if (showGateCard)
        {
            GateTutorialCard.ShowCard(gateType);
        }
        else
        {
            string key = gateType.ToUpper();
            if (key == "AND" || key == "OR" || key == "NOT")
                GateTutorialCard.SeenGateTypes.Add(key);
        }

        // Make sure the J-key journal listener is alive
        GateJournal.EnsureInstance();
    }

    public int GetGateCount(string gateType)
    {
        string key = gateType.ToUpper();
        return gateCounts.ContainsKey(key) ? gateCounts[key] : 0;
    }

    public void ResetInventory()
    {
        gateCounts["AND"] = 0;
        gateCounts["OR"] = 0;
        gateCounts["NOT"] = 0;

        // Clear key flags so they don't carry over to the next level
        TutorialDoor.PlayerHasKey = false;
        SuccessDoor.PlayerHasSuccessKey = false;

        // Clear candle
        HasCandle = false;

    // Clear gate tutorial card tracking so cards show again on a new game
    GateTutorialCard.ResetSeenGates();

        // Sync zeroed counts to player data so a mid-level save won't restore old items
        if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
        {
            var player = AccountManager.Instance.GetCurrentPlayer();
            player.andGatesCollected = 0;
            player.orGatesCollected = 0;
            player.notGatesCollected = 0;
        }

        Debug.Log("[InventoryManager] Full inventory reset (gates, keys, candle).");

        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null)
        {
            levelUI.UpdateGateCount(0, 0, 0);
        }
        else
        {
            UIManager.SafeUpdateInventoryDisplay();
        }

        // Refresh the hotbar so key/candle slots also disappear
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();
    }

    private IEnumerator SaveAfterFrame(string gateType)
    {
        yield return new WaitForEndOfFrame();
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.SavePlayerProgress();
            Debug.Log($"Saved {gateType} gate change to Firebase");
        }
    }

    public void SyncFromCloud(int and, int or, int not)
    {
        gateCounts["AND"] = and;
        gateCounts["OR"] = or;
        gateCounts["NOT"] = not;
        UpdateLocalUI();
        TryShowDropGateHint();
        Debug.Log("InventoryManager: Synced from cloud data.");
    }

    private void TryShowDropGateHint()
    {
        if (!enableDropGateHint || dropGateHintShownThisSession) return;
        if (GetTotalGateCount() < Mathf.Max(1, dropGateHintThreshold)) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Level1") return;

        if (dropGateHintRoutine != null) return;
        dropGateHintShownThisSession = true;
        dropGateHintRoutine = StartCoroutine(ShowDropGateHintRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryShowTabCursorHint(scene);
    }

    private void TryShowTabCursorHint(Scene scene)
    {
        if (!enableTabCursorHint) return;

        if (scene.name != "Level2")
        {
            tabCursorHintShownForCurrentLevel2Entry = false;
            return;
        }

        if (tabCursorHintShownForCurrentLevel2Entry) return;
        if (tabCursorHintRoutine != null) return;

        tabCursorHintShownForCurrentLevel2Entry = true;
        tabCursorHintRoutine = StartCoroutine(ShowTabCursorHintRoutine());
        Debug.Log("[Hints] Level 2 TAB cursor hint queued.");
    }

    private IEnumerator ShowTabCursorHintRoutine()
    {
        if (tabCursorHintInitialDelay > 0f)
            yield return new WaitForSecondsRealtime(tabCursorHintInitialDelay);

        TipOverlayUI.ShowTip("Press Tab to toggle the mouse cursor.", 7f, 40f);

        tabCursorHintUI = null;
        tabCursorHintRoutine = null;
    }

    private IEnumerator ShowDropGateHintRoutine()
    {
        if (dropGateHintInitialDelay > 0f)
            yield return new WaitForSecondsRealtime(dropGateHintInitialDelay);

        TipOverlayUI.ShowTip("Press Q to drop logic gates.", 7f, 40f);

        dropGateHintUI = null;
        dropGateHintRoutine = null;
    }

    void OnDestroy()
    {
        if (dropGateHintRoutine != null)
            StopCoroutine(dropGateHintRoutine);

        if (dropGateHintUI != null)
            Destroy(dropGateHintUI);

        if (tabCursorHintRoutine != null)
            StopCoroutine(tabCursorHintRoutine);

        if (tabCursorHintUI != null)
            Destroy(tabCursorHintUI);

        if (Instance == this)
            Instance = null;
    }
}