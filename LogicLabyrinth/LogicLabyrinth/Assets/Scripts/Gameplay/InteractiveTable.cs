using UnityEngine;
using System.Linq;

public class InteractiveTable : MonoBehaviour
{
    [Header("Table Settings")]
    public string levelId = "Level1";

    [Header("Visual Feedback")]
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer tableRenderer;

    void Start()
    {
        tableRenderer = GetComponent<Renderer>();
        if (tableRenderer != null)
            originalMaterial = tableRenderer.material;
    }

    void OnMouseEnter()
    {
        if (tableRenderer != null && highlightMaterial != null)
            tableRenderer.material = highlightMaterial;
    }

    void OnMouseExit()
    {
        if (tableRenderer != null && originalMaterial != null)
            tableRenderer.material = originalMaterial;
    }

    void OnMouseDown()
    {
        OpenPuzzleInterface();
    }

    private void OpenPuzzleInterface()
    {
        string playerId = GetCurrentPlayerId();

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("No player logged in!");
            return;
        }

        
        PuzzleVariant assignedPuzzle = GetPuzzleForThisLevel(playerId);

        if (assignedPuzzle != null)
        {
            if (PuzzleUIManager.Instance != null)
            {
                PuzzleUIManager.Instance.OpenPuzzle(assignedPuzzle);
            }
            else
            {
                Debug.LogError("PuzzleUIManager not found!");
            }
        }
        else
        {
            Debug.LogError($"Could not load puzzle for level {levelId}");
        }
    }

    private PuzzleVariant GetPuzzleForThisLevel(string playerId)
    {
        
        if (LevelManager.Instance.currentLevelPuzzle != null)
        {
            return LevelManager.Instance.currentLevelPuzzle;
        }

        
        PuzzleVariant newPuzzle = PuzzleManager.Instance.GetPuzzleForPlayer(levelId, playerId);
        LevelManager.Instance.currentLevelPuzzle = newPuzzle;

        return newPuzzle;
    }

    private string GetCurrentPlayerId()
    {
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player != null) return player.username;
        return "test_player";
    }
}