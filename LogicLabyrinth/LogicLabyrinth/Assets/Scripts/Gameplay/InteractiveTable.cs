using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the table object in the scene.
/// When the player interacts (E key via Interactable, or mouse click),
/// it instantiates the UITable prefab and opens the puzzle.
/// </summary>
public class InteractiveTable : MonoBehaviour
{
    [Header("Puzzle UI")]
    [Tooltip("Assign the UITable prefab from Assets/Prefabs/Table/Table/UITable.prefab")]
    public GameObject puzzleUIPrefab;

    [Header("Answer Key")]
    [Tooltip("The correct gate for each box slot, in order (Box1, Box2, Box3, ...)")]
    public GateType[] answerKey = new GateType[] {
        GateType.OR,   // Box1
        GateType.OR,   // Box2
        GateType.OR,   // Box3
        GateType.AND,  // Box4
        GateType.AND   // Box5
    };

    [Header("Settings")]
    public int maxAttempts = 3;

    [Header("Visual Feedback")]
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer tableRenderer;

    // Runtime
    private GameObject puzzleUIInstance;
    private bool isPuzzleOpen;

    void Start()
    {
        tableRenderer = GetComponent<Renderer>();
        if (tableRenderer != null)
            originalMaterial = tableRenderer.material;
    }

    void OnMouseEnter()
    {
        if (tableRenderer != null && highlightMaterial != null && !isPuzzleOpen)
            tableRenderer.material = highlightMaterial;
    }

    void OnMouseExit()
    {
        if (tableRenderer != null && originalMaterial != null && !isPuzzleOpen)
            tableRenderer.material = originalMaterial;
    }

    void OnMouseDown()
    {
        OpenPuzzleInterface();
    }

    /// <summary>
    /// Call this from an Interactable or any other interaction system.
    /// </summary>
    public void OpenPuzzleInterface()
    {
        if (isPuzzleOpen) return;

        if (puzzleUIPrefab == null)
        {
            Debug.LogError("[InteractiveTable] puzzleUIPrefab is not assigned! Assign the UITable prefab in the Inspector.");
            return;
        }

        // Ensure an EventSystem exists (required for UI interactions)
        EnsureEventSystem();

        // Players can always open the table to view the problem.
        // The Submit button requires all slots filled, so no gates = can't submit anyway.

        // Instantiate the puzzle UI
        puzzleUIInstance = Instantiate(puzzleUIPrefab);
        puzzleUIInstance.name = "PuzzleUI_Active";

        // Get the PuzzleTableController (should already be on the prefab)
        PuzzleTableController controller = puzzleUIInstance.GetComponent<PuzzleTableController>();
        if (controller == null)
            controller = puzzleUIInstance.AddComponent<PuzzleTableController>();

        // Set the answer key and attempts BEFORE calling OpenPuzzle
        controller.answerKey = answerKey;
        controller.maxAttempts = maxAttempts;

        // Now explicitly open the puzzle (this sets up slots, palette, controls)
        controller.OpenPuzzle();

        isPuzzleOpen = true;
        Debug.Log("[InteractiveTable] Puzzle opened!");

        // Watch for when the puzzle closes
        StartCoroutine(WatchForPuzzleClose());
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Debug.Log("[InteractiveTable] Created EventSystem for UI interactions.");
        }
    }

    private System.Collections.IEnumerator WatchForPuzzleClose()
    {
        // Wait until the puzzle instance is disabled/destroyed
        while (puzzleUIInstance != null && puzzleUIInstance.activeSelf)
        {
            yield return null;
        }

        isPuzzleOpen = false;
        Debug.Log("[InteractiveTable] Puzzle closed!");

        // Clean up
        if (puzzleUIInstance != null)
        {
            Destroy(puzzleUIInstance);
            puzzleUIInstance = null;
        }

        // Restore table material
        if (tableRenderer != null && originalMaterial != null)
            tableRenderer.material = originalMaterial;
    }
}
