using UnityEngine;
using System.Collections;
using System.Globalization;

public class Interactable : MonoBehaviour
{
    
    public enum GateType { AND, OR, NOT }

    [Header("Gate Settings")]
    public GateType gateType = GateType.AND;
    public string gateID; 

    void Start()
    {
        // Generate a deterministic ID if one wasn't set in the Inspector.
        // Keep the SAME format that was used when the gate was first collected & saved,
        // so existing saves in Firebase still match.
        if (string.IsNullOrEmpty(gateID))
        {
            gateID = $"{gateType}_{gameObject.name}_{transform.position.x}_{transform.position.y}_{transform.position.z}";
        }

        Debug.Log($"[Gate] Start: gateID='{gateID}'");

        // ── Immediate check (frame 0) — no delay ──
        if (IsAlreadyCollected())
        {
            Debug.Log($"[Gate] {gateID} already collected — destroying immediately.");
            Destroy(gameObject);
            return;
        }

        // ── Delayed safety-net — catches edge cases where data arrives late ──
        StartCoroutine(CheckIfDestroyedAfterDelay());
    }

    /// <summary>
    /// Returns true if this gate's ID is in the player's destroyedGates list.
    /// Checks both the current gateID and an invariant-culture variant for robustness.
    /// </summary>
    private bool IsAlreadyCollected()
    {
        if (AccountManager.Instance == null) return false;
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player == null || player.destroyedGates == null) return false;

        if (player.destroyedGates.Contains(gateID))
            return true;

        // Also try an invariant-culture formatted ID in case saved on a different locale
        string invariantID = string.Format(CultureInfo.InvariantCulture,
            "{0}_{1}_{2:F3}_{3:F3}_{4:F3}",
            gateType, gameObject.name,
            transform.position.x, transform.position.y, transform.position.z);

        return player.destroyedGates.Contains(invariantID);
    }

    private IEnumerator CheckIfDestroyedAfterDelay()
    {
        // Wait a couple of frames (not 0.5s) — just enough for late-init managers.
        yield return null;
        yield return null;
        yield return null;

        if (IsAlreadyCollected())
        {
            Debug.Log($"[Gate] {gateID} found in destroyedGates (delayed check) — destroying.");
            Destroy(gameObject);
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
            Debug.Log($"[Gate] Marked gate {gateID} as destroyed (destroyedGates count: {AccountManager.Instance.GetCurrentPlayer().destroyedGates.Count})");
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