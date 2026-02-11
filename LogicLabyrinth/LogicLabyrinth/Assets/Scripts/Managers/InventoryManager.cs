using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public TMPro.TMP_Text andGateText;
    public TMPro.TMP_Text orGateText;
    public TMPro.TMP_Text notGateText;

    private Dictionary<string, int> gateCounts = new Dictionary<string, int> { { "AND", 0 }, { "OR", 0 }, { "NOT", 0 } };

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

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

                // Mas safe gamitin ang Coroutine mo para sa cloud saving
                StartCoroutine(SaveAfterFrame(gateType));
            }
            UpdateLocalUI();
        }
    }

    public void UpdateLocalUI()
    {
        // UI Syncing logic
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGateCounts(gateCounts["AND"], gateCounts["OR"], gateCounts["NOT"]);
        }
    }

    public int GetGateCount(string gateType)
    {
        string key = gateType.ToUpper();
        return gateCounts.ContainsKey(key) ? gateCounts[key] : 0;
    }

    // DITO DAPAT NAKALAGAY SA LOOB NG CLASS ANG MGA TO:
    public void ResetInventory()
    {
        gateCounts["AND"] = 0;
        gateCounts["OR"] = 0;
        gateCounts["NOT"] = 0;
        Debug.Log("Inventory reset to zero");

        // Dynamic UI Update
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
            Debug.Log($"Saved {gateType} gate to Firebase after frame");
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