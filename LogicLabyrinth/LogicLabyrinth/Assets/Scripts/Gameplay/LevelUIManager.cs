using UnityEngine;
using TMPro;
using System.Collections;

public class LevelUIManager : MonoBehaviour
{
    [Header("Game UI")]
    public TextMeshProUGUI gateCountText;
    public TextMeshProUGUI interactPrompt;

    public static LevelUIManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Bootstrap the GameInventoryUI if it doesn't already exist
        EnsureGameInventoryUI();

        // Bootstrap the LevelTimer if LevelManager hasn't created it yet
        EnsureLevelTimer();

        // ADDED: Update UI when level loads to show saved inventory
        RefreshUIOnLoad();
    }

    /// <summary>
    /// Creates a LevelTimer if one doesn't exist yet.
    /// Ensures the timer is available even when testing a scene directly without LevelManager.
    /// </summary>
    void EnsureLevelTimer()
    {
        if (LevelTimer.Instance == null)
        {
            GameObject timerGO = new GameObject("LevelTimer");
            timerGO.AddComponent<LevelTimer>();
            Debug.Log("LevelUIManager: Created LevelTimer singleton.");
        }
    }

    /// <summary>
    /// Creates a GameInventoryUI if one doesn't exist yet.
    /// The GameInventoryUI builds its own visual hierarchy in code.
    /// </summary>
    void EnsureGameInventoryUI()
    {
        if (GameInventoryUI.Instance == null)
        {
            GameObject inventoryUIGO = new GameObject("GameInventoryUI");
            inventoryUIGO.AddComponent<GameInventoryUI>();
            Debug.Log("LevelUIManager: Created GameInventoryUI");
        }
    }

    public void UpdateGateCount(int andCount, int orCount, int notCount)
    {
        if (gateCountText != null)
            gateCountText.text = $"AND: {andCount} | OR: {orCount} | NOT: {notCount}";
    }

    public void ShowInteractPrompt(string text = "")
    {
        if (interactPrompt != null)
        {
            interactPrompt.text = text;
            interactPrompt.gameObject.SetActive(true);
        }
    }

    public void HideInteractPrompt()
    {
        if (interactPrompt != null)
            interactPrompt.gameObject.SetActive(false);
    }

    public void ShowCollectionMessage(string message, Color color)
    {
        Debug.Log($"Collection: {message}");
        // You can add visual feedback here later
    }

    // ADDED THIS METHOD: Refresh UI when level loads
    void RefreshUIOnLoad()
    {
        // Wait a frame for everything to initialize
        StartCoroutine(RefreshUICoroutine());
    }

    // ADDED THIS METHOD: Coroutine to refresh UI
    IEnumerator RefreshUICoroutine()
    {
        yield return new WaitForSeconds(0.1f);

        if (InventoryManager.Instance != null)
        {
            int andCount = InventoryManager.Instance.GetGateCount("AND");
            int orCount = InventoryManager.Instance.GetGateCount("OR");
            int notCount = InventoryManager.Instance.GetGateCount("NOT");

            UpdateGateCount(andCount, orCount, notCount);

            // Also refresh the new GameInventoryUI bar
            if (GameInventoryUI.Instance != null)
            {
                GameInventoryUI.Instance.UpdateCounts(andCount, orCount, notCount);
            }

            Debug.Log("UI refreshed with saved inventory on level load");
        }
    }
}