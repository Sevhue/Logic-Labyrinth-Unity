using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour
{
    
    public enum GateType { AND, OR, NOT }

    [Header("Gate Settings")]
    public GateType gateType = GateType.AND;
    public string gateID; 

    void Start()
    {
        
        if (string.IsNullOrEmpty(gateID))
        {
            gateID = $"{gateType}_{gameObject.name}_{transform.position.x}_{transform.position.y}_{transform.position.z}";
        }

        
        StartCoroutine(CheckIfDestroyedAfterDelay());
    }

    private IEnumerator CheckIfDestroyedAfterDelay()
    {
        
        yield return new WaitForSeconds(0.5f);

        if (AccountManager.Instance != null &&
            AccountManager.Instance.GetCurrentPlayer() != null &&
            AccountManager.Instance.GetCurrentPlayer().destroyedGates.Contains(gateID))
        {
            Destroy(gameObject); 
            Debug.Log($"Gate {gateID} was already collected - destroyed on load");
        }
    }

    public void Interact()
    {
        Debug.Log($"COLLECTED {gateType} GATE!");

       
        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();
        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        UIManager mainUI = FindAnyObjectByType<UIManager>();

       
        if (inventory != null)
        {
            inventory.AddGate(gateType.ToString());
            Debug.Log($"Added {gateType} to inventory successfully");
        }
        else
        {
            Debug.LogError("InventoryManager not found during gate collection!");
        }

       
        if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
        {
            AccountManager.Instance.GetCurrentPlayer().destroyedGates.Add(gateID);
            Debug.Log($"Marked gate {gateID} as destroyed");
        }

        
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.SavePlayerProgress();
            Debug.Log($"Immediately saved {gateType} gate collection");
        }

       
        Destroy(gameObject);
        Debug.Log($"Destroyed {gateType} gate object");

        
        if (levelUI != null)
        {
            levelUI.HideInteractPrompt();
            levelUI.ShowCollectionMessage($"Collected {gateType} Gate!", Color.green);
        }
        else if (mainUI != null)
        {
            mainUI.ShowInteractPrompt(false);
        }
        else
        {
            Debug.LogWarning("No UI manager found!");
        }
    }

    public string GetInteractionText()
    {
        return $"Press E to collect {gateType} Gate";
    }
}