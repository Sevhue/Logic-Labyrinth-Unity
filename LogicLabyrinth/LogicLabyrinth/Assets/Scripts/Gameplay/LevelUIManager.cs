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
        // ADDED: Update UI when level loads to show saved inventory
        RefreshUIOnLoad();
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
            UpdateGateCount(
                InventoryManager.Instance.GetGateCount("AND"),
                InventoryManager.Instance.GetGateCount("OR"),
                InventoryManager.Instance.GetGateCount("NOT")
            );
            Debug.Log("UI refreshed with saved inventory on level load");
        }
    }
}