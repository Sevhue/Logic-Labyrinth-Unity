using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Firebase.Database;
using Firebase.Extensions;
using System.Threading.Tasks;


public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public TMPro.TMP_Text andGateText;
    public TMPro.TMP_Text orGateText;
    public TMPro.TMP_Text notGateText;
    private Dictionary<string, int> gateCounts = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);


            gateCounts["AND"] = 0;
            gateCounts["OR"] = 0;
            gateCounts["NOT"] = 0;

            Debug.Log("Inventory Manager Initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddGate(string gateType)
    {
        string upperType = gateType.ToUpper();
        if (!gateCounts.ContainsKey(upperType)) gateCounts[upperType] = 0;

        gateCounts[upperType]++;
        UpdateLocalUI();

        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.SavePlayerProgress();
        }
    }
    void UpdateLocalUI()
    {
        if (andGateText != null) andGateText.text = "AND: " + gateCounts["AND"];
        if (orGateText != null) orGateText.text = "OR: " + gateCounts["OR"];
        if (notGateText != null) notGateText.text = "NOT: " + gateCounts["NOT"];
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateInventoryDisplay();
        }
    }

    public int GetGateCount(string gateType)
    {
        return gateCounts.ContainsKey(gateType) ? gateCounts[gateType] : 0;
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

        // Sa AccountManager mo, dapat tinatawag nito ang Firebase update
        AccountManager.Instance.SavePlayerProgress();
        Debug.Log($"Saved {gateType} gate to Firebase after frame");
    }
    public void SyncFromCloud(int and, int or, int not)
    {
        gateCounts["AND"] = and;
        gateCounts["OR"] = or;
        gateCounts["NOT"] = not;

        // Para mag-update agad yung mga boxes/text sa screen mo
        UpdateLocalUI();
        Debug.Log("Inventory Updated from Cloud!");
    }
}