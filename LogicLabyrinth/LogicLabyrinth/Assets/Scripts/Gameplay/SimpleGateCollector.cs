using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleGateCollector : MonoBehaviour
{
    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 5f;
    public float sphereCastRadius = 0.25f;

    [Header("Gate Prefabs (for dropping)")]
    [Tooltip("Assign the same prefabs as the spawner — used when player drops/swaps a gate")]
    public GameObject andGatePrefab;
    public GameObject orGatePrefab;
    public GameObject notGatePrefab;

    // Current targets
    private Interactable currentInteractable;
    private InteractiveTable currentTable;

    // Cache UI references
    private LevelUIManager _levelUI;
    private UIManager _mainUI;
    private float _uiCacheTimer;

    void Update()
    {
        // Skip everything while SwapGateUI or PuzzleTableController is open
        if (SwapGateUI.IsOpen || PuzzleTableController.IsOpen) return;

        // Re-cache UI references periodically
        _uiCacheTimer -= Time.deltaTime;
        if (_uiCacheTimer <= 0f)
        {
            _levelUI = FindAnyObjectByType<LevelUIManager>();
            _mainUI = FindAnyObjectByType<UIManager>();
            _uiCacheTimer = 2f;
        }

        HandleInteraction();

        // ── E Key ──
        bool ePressed = Input.GetKeyDown(KeyCode.E);
        if (!ePressed && Keyboard.current != null)
            ePressed = Keyboard.current.eKey.wasPressedThisFrame;

        if (ePressed)
        {
            if (currentInteractable != null)
            {
                TryCollectGate();
            }
            else if (currentTable != null)
            {
                currentTable.OpenPuzzleInterface();
            }
        }

        // ── Q Key — Discard ──
        bool qPressed = Input.GetKeyDown(KeyCode.Q);
        if (!qPressed && Keyboard.current != null)
            qPressed = Keyboard.current.qKey.wasPressedThisFrame;

        if (qPressed)
        {
            TryDiscardGate();
        }
    }

    /// <summary>
    /// Returns true if there is a clear line of sight from the camera to the target
    /// (no walls/environment blocking). Ignores the target's own colliders.
    /// </summary>
    private bool HasLineOfSight(Vector3 origin, Transform target)
    {
        Vector3 targetPos = target.position;
        Vector3 dir = targetPos - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true; // Essentially on top of it

        RaycastHit hit;
        // Cast a ray toward the target; if the first thing we hit is NOT the target → wall is blocking
        if (Physics.Raycast(origin, dir.normalized, out hit, dist))
        {
            // Check if we hit the target itself or one of its children
            if (hit.collider.transform == target ||
                hit.collider.transform.IsChildOf(target) ||
                target.IsChildOf(hit.collider.transform))
            {
                return true; // Line of sight is clear
            }
            return false; // Something else (a wall) is in the way
        }
        return true; // Nothing blocking
    }

    void HandleInteraction()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        Interactable bestInteractable = null;
        InteractiveTable bestTable = null;
        float bestDistance = float.MaxValue;
        float bestTableDistance = float.MaxValue;

        RaycastHit[] sphereHits = Physics.SphereCastAll(ray, sphereCastRadius, interactDistance);
        for (int i = 0; i < sphereHits.Length; i++)
        {
            // Overlap case (sphere starts inside collider)
            if (sphereHits[i].distance == 0f && sphereHits[i].point == Vector3.zero)
            {
                // Check for Interactable
                Interactable overlapInteractable = sphereHits[i].collider.GetComponent<Interactable>();
                if (overlapInteractable == null)
                    overlapInteractable = sphereHits[i].collider.GetComponentInParent<Interactable>();

                if (overlapInteractable != null)
                {
                    Vector3 toGate = (overlapInteractable.transform.position - ray.origin).normalized;
                    float dot = Vector3.Dot(ray.direction, toGate);
                    float dist = Vector3.Distance(ray.origin, overlapInteractable.transform.position);

                    // Must be in front, close, AND have line of sight (no walls)
                    if (dot > 0.75f && dist < 2f && dist < bestDistance
                        && HasLineOfSight(ray.origin, overlapInteractable.transform))
                    {
                        bestDistance = dist;
                        bestInteractable = overlapInteractable;
                    }
                }

                // Check for InteractiveTable
                InteractiveTable overlapTable = sphereHits[i].collider.GetComponent<InteractiveTable>();
                if (overlapTable == null)
                    overlapTable = sphereHits[i].collider.GetComponentInParent<InteractiveTable>();

                if (overlapTable != null)
                {
                    Vector3 toTable = (overlapTable.transform.position - ray.origin).normalized;
                    float dot = Vector3.Dot(ray.direction, toTable);
                    float dist = Vector3.Distance(ray.origin, overlapTable.transform.position);

                    if (dot > 0.5f && dist < 3f && dist < bestTableDistance
                        && HasLineOfSight(ray.origin, overlapTable.transform))
                    {
                        bestTableDistance = dist;
                        bestTable = overlapTable;
                    }
                }

                continue;
            }

            // Normal hit — also needs line-of-sight check
            Interactable interactable = sphereHits[i].collider.GetComponent<Interactable>();
            if (interactable == null)
                interactable = sphereHits[i].collider.GetComponentInParent<Interactable>();

            if (interactable != null && sphereHits[i].distance < bestDistance
                && HasLineOfSight(ray.origin, interactable.transform))
            {
                bestDistance = sphereHits[i].distance;
                bestInteractable = interactable;
            }

            InteractiveTable table = sphereHits[i].collider.GetComponent<InteractiveTable>();
            if (table == null)
                table = sphereHits[i].collider.GetComponentInParent<InteractiveTable>();

            if (table != null && sphereHits[i].distance < bestTableDistance
                && HasLineOfSight(ray.origin, table.transform))
            {
                bestTableDistance = sphereHits[i].distance;
                bestTable = table;
            }
        }

        // Update prompts
        if (bestInteractable != null)
        {
            currentInteractable = bestInteractable;
            currentTable = null;

            // Show different prompt based on inventory state
            string promptText;
            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryFull())
            {
                int total = InventoryManager.Instance.GetTotalGateCount();
                promptText = $"Press E to swap for {bestInteractable.gateType} Gate ({total}/{InventoryManager.MAX_GATES})";
            }
            else
            {
                promptText = bestInteractable.GetInteractionText();
            }

            if (_levelUI != null)
                _levelUI.ShowInteractPrompt(promptText);
            else if (_mainUI != null)
                _mainUI.ShowInteractPrompt(true, promptText);
        }
        else if (bestTable != null)
        {
            currentInteractable = null;
            currentTable = bestTable;

            if (_levelUI != null)
                _levelUI.ShowInteractPrompt("Press E to open Puzzle Table");
            else if (_mainUI != null)
                _mainUI.ShowInteractPrompt(true, "Press E to open Puzzle Table");
        }
        else
        {
            currentInteractable = null;
            currentTable = null;

            if (_levelUI != null)
                _levelUI.HideInteractPrompt();
            else if (_mainUI != null)
                _mainUI.ShowInteractPrompt(false);
        }
    }

    void TryCollectGate()
    {
        if (currentInteractable == null) return;

        // Check if inventory is full → show swap UI
        if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryFull())
        {
            Debug.Log("[SimpleGateCollector] Inventory full — showing swap UI.");
            SwapGateUI.ShowSwap(currentInteractable, andGatePrefab, orGatePrefab, notGatePrefab, transform);
            currentInteractable = null; // Clear so we don't re-trigger
            return;
        }

        // Normal collection
        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        currentInteractable.Interact();
        currentInteractable = null;
    }

    void TryDiscardGate()
    {
        // Can't discard if no gates
        if (InventoryManager.Instance == null || InventoryManager.Instance.GetTotalGateCount() == 0)
        {
            Debug.Log("[SimpleGateCollector] No gates to discard.");
            return;
        }

        Debug.Log("[SimpleGateCollector] Opening discard UI.");
        SwapGateUI.ShowDiscard(andGatePrefab, orGatePrefab, notGatePrefab, transform);
    }
}