using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public TMPro.TMP_Text andGateText;
    public TMPro.TMP_Text orGateText;
    public TMPro.TMP_Text notGateText;

    public const int MAX_GATES = 5;

    private Dictionary<string, int> gateCounts = new Dictionary<string, int> { { "AND", 0 }, { "OR", 0 }, { "NOT", 0 } };

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
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
        return GetTotalGateCount() >= MAX_GATES;
    }

    // ============================
    // ADD / REMOVE
    // ============================

    public void AddGate(string gateType)
    {
        string key = gateType.ToUpper();
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

            Debug.Log($"[InventoryManager] Removed 1 {key} gate. Remaining: {gateCounts[key]}. Total: {GetTotalGateCount()}/{MAX_GATES}");
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
        Debug.Log("Inventory reset to zero");

        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null)
        {
            levelUI.UpdateGateCount(0, 0, 0);
        }
        else
        {
            UIManager.SafeUpdateInventoryDisplay();
        }
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
        Debug.Log("InventoryManager: Synced from cloud data.");
    }
}