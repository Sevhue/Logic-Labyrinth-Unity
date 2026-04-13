using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class SimpleGateCollector : MonoBehaviour
{
    private const float MaxGateCollectDistance = 2.5f;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 6f;
    public float sphereCastRadius = 0.35f;

    [Header("Gate Prefabs (for dropping)")]
    [Tooltip("Assign the same prefabs as the spawner — used when player drops/swaps a gate")]
    public GameObject andGatePrefab;
    public GameObject orGatePrefab;
    public GameObject notGatePrefab;

    // Current targets — only ONE can be active at a time
    private Interactable currentInteractable;
    private InteractiveTable currentTable;
    private CollectibleKey currentKey;
    private TutorialDoor currentDoor;
    private SuccessDoor currentSuccessDoor;
    private CollectibleCandle currentCandle;
    private TruthTableDisplay currentTruthDisplay;
    private Transform currentChapter3QuestionDoor;

    // Cache UI references
    private LevelUIManager _levelUI;
    private UIManager _mainUI;
    private float _uiCacheTimer;
    private float _debugTableTimer;
    private FirstPersonController _playerController;

    void Update()
    {
        if (_playerController == null)
            _playerController = GetComponentInParent<FirstPersonController>();

        // Block interactions while dead so dropped gates cannot be re-collected during death/game-over screen.
        if (_playerController != null && _playerController.IsDead)
        {
            HidePrompt();
            ClearTargets();
            return;
        }

        // Skip everything while SwapGateUI, PuzzleTableController, TruthTableDisplay, or Cutscene is active
        if (SwapGateUI.IsOpen || PuzzleTableController.IsOpen || TruthTableDisplay.IsOpen)
        {
            HidePrompt();
            ClearTargets();
            return;
        }
        if (CutsceneController.IsPlaying || CutsceneController.CameraOnlyMode)
        {
            HidePrompt();
            ClearTargets();
            return;
        }

        // Tick debug timer
        if (_debugTableTimer > 0f) _debugTableTimer -= Time.deltaTime;

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
            Debug.Log($"[SGC] E pressed! gate={currentInteractable != null} table={currentTable != null} key={currentKey != null} door={currentDoor != null} candle={currentCandle != null}");

            if (currentInteractable != null)
            {
                TryCollectGate();
            }
            else if (currentKey != null)
            {
                Debug.Log("[SGC] Collecting key.");
                currentKey.CollectKey();
                currentKey = null;
            }
            else if (currentCandle != null)
            {
                Debug.Log("[SGC] Collecting candle.");
                currentCandle.CollectCandle();
                currentCandle = null;
            }
            else if (currentTable != null)
            {
                Debug.Log($"[SGC] Opening puzzle table: {currentTable.gameObject.name}");
                currentTable.OpenPuzzleInterface();
            }
            else if (currentSuccessDoor != null)
            {
                Debug.Log("[SGC] Interacting with success door.");
                currentSuccessDoor.TryInteract();
            }
            else if (currentTruthDisplay != null)
            {
                Debug.Log("[SGC] Opening truth table display.");
                currentTruthDisplay.OpenDisplay();
            }
            else if (currentChapter3QuestionDoor != null)
            {
                Debug.Log("[SGC] Opening Chapter 3 question display.");
                TruthTableDisplay.OpenChapter3ForDoor(currentChapter3QuestionDoor);
            }
            else if (currentDoor != null)
            {
                Debug.Log("[SGC] Interacting with door.");
                currentDoor.TryInteract();
            }
            else
            {
                // Fallback: Try a direct raycast for tables when SphereCast misses
                if (playerCamera != null)
                {
                    Ray directRay = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                    RaycastHit directHit;
                    if (Physics.Raycast(directRay, out directHit, interactDistance))
                    {
                        CollectibleKey directKey = directHit.collider.GetComponent<CollectibleKey>();
                        if (directKey == null) directKey = directHit.collider.GetComponentInParent<CollectibleKey>();
                        if (directKey != null && !directKey.IsCollected)
                        {
                            directKey.CollectKey();
                        }
                        else
                        {
                            CollectibleCandle directCandle = directHit.collider.GetComponent<CollectibleCandle>();
                            if (directCandle == null) directCandle = directHit.collider.GetComponentInParent<CollectibleCandle>();
                            if (directCandle != null && !directCandle.IsCollected)
                            {
                                directCandle.CollectCandle();
                            }
                            else
                            {
                                SuccessDoor directSuccessDoor = directHit.collider.GetComponent<SuccessDoor>();
                                if (directSuccessDoor == null) directSuccessDoor = directHit.collider.GetComponentInParent<SuccessDoor>();
                                if (directSuccessDoor != null && !directSuccessDoor.IsDoorOpen)
                                {
                                    directSuccessDoor.TryInteract();
                                }
                                else
                                {
                                    TutorialDoor directDoor = directHit.collider.GetComponent<TutorialDoor>();
                                    if (directDoor == null) directDoor = directHit.collider.GetComponentInParent<TutorialDoor>();
                                    if (directDoor != null && !directDoor.IsDoorOpen)
                                    {
                                        directDoor.TryInteract();
                                    }
                                    else
                                    {
                                        InteractiveTable directTable = directHit.collider.GetComponent<InteractiveTable>();
                                        if (directTable == null)
                                            directTable = directHit.collider.GetComponentInParent<InteractiveTable>();
                                        if (directTable != null && !directTable.IsSolved)
                                        {
                                            directTable.OpenPuzzleInterface();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Last fallback: nearest table in front of camera.
                        InteractiveTable[] allTables = FindObjectsByType<InteractiveTable>(FindObjectsSortMode.None);
                        InteractiveTable nearestTable = null;
                        float nearestDist = float.MaxValue;
                        for (int i = 0; i < allTables.Length; i++)
                        {
                            InteractiveTable t = allTables[i];
                            if (t == null || !t.isActiveAndEnabled) continue;

                            float dist = Vector3.Distance(playerCamera.transform.position, t.transform.position);
                            if (dist > interactDistance) continue;

                            Vector3 toTable = (t.transform.position - playerCamera.transform.position).normalized;
                            float dot = Vector3.Dot(playerCamera.transform.forward, toTable);
                            if (dot < 0.2f) continue;

                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearestTable = t;
                            }
                        }

                        if (nearestTable != null && !nearestTable.IsSolved)
                            nearestTable.OpenPuzzleInterface();
                    }
                }
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

    private void ClearTargets()
    {
        currentInteractable = null;
        currentTable = null;
        currentKey = null;
        currentDoor = null;
        currentSuccessDoor = null;
        currentCandle = null;
        currentTruthDisplay = null;
        currentChapter3QuestionDoor = null;
    }

    /// <summary>
    /// Returns true if there is a clear line of sight from the camera to the target.
    /// </summary>
    private bool HasLineOfSight(Vector3 origin, Transform target)
    {
        Vector3 targetPos = target.position;
        Collider targetCol = target.GetComponent<Collider>();
        if (targetCol != null)
            targetPos = targetCol.bounds.center;

        Vector3 dir = targetPos - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit hit;
        if (Physics.Raycast(origin, dir.normalized, out hit, dist))
        {
            if (hit.collider.transform == target ||
                hit.collider.transform.IsChildOf(target) ||
                target.IsChildOf(hit.collider.transform))
            {
                return true;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// Strict visibility check: the first collider hit by the center-screen ray must belong to the target.
    /// Prevents interacting with gates through walls.
    /// </summary>
    private bool IsDirectlyAimable(Transform target)
    {
        if (playerCamera == null || target == null) return false;

        Ray centerRay = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        if (!Physics.Raycast(centerRay, out hit, interactDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider.transform == target ||
               hit.collider.transform.IsChildOf(target) ||
               target.IsChildOf(hit.collider.transform);
    }

    private static float GetGateDistanceFromCamera(Vector3 origin, Interactable gate)
    {
        if (gate == null)
            return float.MaxValue;

        Renderer[] renderers = gate.GetComponentsInChildren<Renderer>(true);
        float minDist = float.MaxValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) continue;

            Vector3 closest = r.bounds.ClosestPoint(origin);
            float dist = Vector3.Distance(origin, closest);
            if (dist < minDist)
                minDist = dist;
        }

        if (minDist < float.MaxValue)
            return minDist;

        return Vector3.Distance(origin, gate.transform.position);
    }

    void HandleInteraction()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        Interactable bestInteractable = null;
        InteractiveTable bestTable = null;
        CollectibleKey bestKey = null;
        CollectibleCandle bestCandle = null;
        TutorialDoor bestDoor = null;
        SuccessDoor bestSuccessDoor = null;

        float bestDistance = float.MaxValue;
        float bestTableDistance = float.MaxValue;
        float bestKeyDistance = float.MaxValue;
        float bestCandleDistance = float.MaxValue;
        float bestDoorDistance = float.MaxValue;
        float bestSuccessDoorDistance = float.MaxValue;
        TruthTableDisplay bestTruthDisplay = null;
        float bestTruthDisplayDistance = float.MaxValue;
        Transform bestChapter3QuestionDoor = null;
        float bestChapter3QuestionDoorDistance = float.MaxValue;

        RaycastHit[] sphereHits = Physics.SphereCastAll(ray, sphereCastRadius, interactDistance);
        for (int i = 0; i < sphereHits.Length; i++)
        {
            Transform hitTransform = sphereHits[i].collider.transform;
            float hitDist = sphereHits[i].distance;

            // ── Check Interactable (gates) ──
            Interactable interactable = sphereHits[i].collider.GetComponent<Interactable>();
            if (interactable == null) interactable = sphereHits[i].collider.GetComponentInParent<Interactable>();

            if (interactable != null)
            {
                float gateObjectDistance = GetGateDistanceFromCamera(ray.origin, interactable);

                if (hitDist == 0f)
                {
                    // Overlap: use dot product and actual distance
                    Vector3 toGate = (interactable.transform.position - ray.origin).normalized;
                    float dot = Vector3.Dot(ray.direction, toGate);
                    float dist = Vector3.Distance(ray.origin, interactable.transform.position);
                    if (dot > 0.75f && dist < 2f && dist < bestDistance
                        && gateObjectDistance <= MaxGateCollectDistance)
                    {
                        bestDistance = dist;
                        bestInteractable = interactable;
                    }
                }
                else if (hitDist < bestDistance
                         && gateObjectDistance <= MaxGateCollectDistance)
                {
                    bestDistance = hitDist;
                    bestInteractable = interactable;
                }
            }

            // ── Check InteractiveTable ──
            InteractiveTable table = sphereHits[i].collider.GetComponent<InteractiveTable>();
            if (table == null) table = sphereHits[i].collider.GetComponentInParent<InteractiveTable>();

            if (table != null && !table.IsSolved)
            {
                if (hitDist == 0f)
                {
                    Vector3 toTable = (table.transform.position - ray.origin).normalized;
                    float dot = Vector3.Dot(ray.direction, toTable);
                    float dist = Vector3.Distance(ray.origin, table.transform.position);
                    if (dot > 0.2f && dist < interactDistance && dist < bestTableDistance)
                    {
                        bestTableDistance = dist;
                        bestTable = table;
                    }
                }
                else if (hitDist < bestTableDistance)
                {
                    bestTableDistance = hitDist;
                    bestTable = table;
                }
            }

            // ── Check CollectibleKey ──
            CollectibleKey keyComp = sphereHits[i].collider.GetComponent<CollectibleKey>();
            if (keyComp == null) keyComp = sphereHits[i].collider.GetComponentInParent<CollectibleKey>();

            if (keyComp != null && !keyComp.IsCollected)
            {
                float dist = hitDist == 0f ? Vector3.Distance(ray.origin, keyComp.transform.position) : hitDist;
                if (dist < bestKeyDistance)
                {
                    bestKeyDistance = dist;
                    bestKey = keyComp;
                }
            }

            // ── Check CollectibleCandle ──
            CollectibleCandle candleComp = sphereHits[i].collider.GetComponent<CollectibleCandle>();
            if (candleComp == null) candleComp = sphereHits[i].collider.GetComponentInParent<CollectibleCandle>();

            if (candleComp != null && !candleComp.IsCollected)
            {
                float dist = hitDist == 0f ? Vector3.Distance(ray.origin, candleComp.transform.position) : hitDist;
                if (dist < bestCandleDistance)
                {
                    bestCandleDistance = dist;
                    bestCandle = candleComp;
                }
            }

            // ── Check TutorialDoor ──
            TutorialDoor doorComp = sphereHits[i].collider.GetComponent<TutorialDoor>();
            if (doorComp == null) doorComp = sphereHits[i].collider.GetComponentInParent<TutorialDoor>();

            if (doorComp != null && !doorComp.IsDoorOpen)
            {
                float dist = hitDist == 0f ? Vector3.Distance(ray.origin, doorComp.transform.position) : hitDist;
                if (dist < bestDoorDistance)
                {
                    bestDoorDistance = dist;
                    bestDoor = doorComp;
                }
            }

            // ── Check SuccessDoor ──
            SuccessDoor successDoorComp = sphereHits[i].collider.GetComponent<SuccessDoor>();
            if (successDoorComp == null) successDoorComp = sphereHits[i].collider.GetComponentInParent<SuccessDoor>();

            if (successDoorComp != null && !successDoorComp.IsDoorOpen)
            {
                float dist = hitDist == 0f ? Vector3.Distance(ray.origin, successDoorComp.transform.position) : hitDist;
                if (dist < bestSuccessDoorDistance)
                {
                    bestSuccessDoorDistance = dist;
                    bestSuccessDoor = successDoorComp;
                }
            }

            // ── Check TruthTableDisplay ──
            TruthTableDisplay truthDisplayComp = sphereHits[i].collider.GetComponent<TruthTableDisplay>();
            if (truthDisplayComp == null) truthDisplayComp = sphereHits[i].collider.GetComponentInParent<TruthTableDisplay>();

            if (truthDisplayComp != null)
            {
                float dist = hitDist == 0f ? Vector3.Distance(ray.origin, truthDisplayComp.transform.position) : hitDist;
                if (dist < bestTruthDisplayDistance)
                {
                    bestTruthDisplayDistance = dist;
                    bestTruthDisplay = truthDisplayComp;
                }
            }

            // ── Check Chapter 3 question doors by name ──
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Chapter3")
            {
                Transform named = sphereHits[i].collider.transform;
                while (named != null)
                {
                    if (!string.IsNullOrEmpty(named.name) && named.name.StartsWith("DoorQuestion"))
                    {
                        float dist = hitDist == 0f ? Vector3.Distance(ray.origin, named.position) : hitDist;
                        if (dist < bestChapter3QuestionDoorDistance)
                        {
                            bestChapter3QuestionDoorDistance = dist;
                            bestChapter3QuestionDoor = named;
                        }
                        break;
                    }

                    if (!string.IsNullOrEmpty(named.name) &&
                        named.name.IndexOf("DoorQuestion", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        float dist = hitDist == 0f ? Vector3.Distance(ray.origin, named.position) : hitDist;
                        if (dist < bestChapter3QuestionDoorDistance)
                        {
                            bestChapter3QuestionDoorDistance = dist;
                            bestChapter3QuestionDoor = named;
                        }
                        break;
                    }

                    named = named.parent;
                }
            }
        }

        // Fallback: if ray/sphere cast failed to identify a table component,
        // pick the nearest visible-ish table in front of the camera.
        if (bestTable == null)
        {
            InteractiveTable[] allTables = FindObjectsByType<InteractiveTable>(FindObjectsSortMode.None);
            float nearest = float.MaxValue;
            for (int i = 0; i < allTables.Length; i++)
            {
                InteractiveTable t = allTables[i];
                if (t == null || !t.isActiveAndEnabled || t.IsSolved) continue;

                float dist = Vector3.Distance(ray.origin, t.transform.position);
                if (dist > interactDistance) continue;

                Vector3 toTable = (t.transform.position - ray.origin).normalized;
                float dot = Vector3.Dot(ray.direction, toTable);
                if (dot < 0.2f) continue;

                if (dist < nearest)
                {
                    nearest = dist;
                    bestTable = t;
                }
            }
        }

        // Chapter3 fallback: detect nearest visible key even if trigger colliders are missed by casts.
        if (bestKey == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Chapter3")
        {
            CollectibleKey[] allKeys = FindObjectsByType<CollectibleKey>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allKeys.Length; i++)
            {
                CollectibleKey key = allKeys[i];
                if (key == null || key.IsCollected) continue;

                float dist = Vector3.Distance(ray.origin, key.transform.position);
                if (dist > interactDistance) continue;

                Vector3 toKey = (key.transform.position - ray.origin).normalized;
                float dot = Vector3.Dot(ray.direction, toKey);
                if (dot < 0.2f) continue;

                if (dist < bestKeyDistance)
                {
                    bestKeyDistance = dist;
                    bestKey = key;
                }
            }
        }

        // Chapter3 fallback: detect Success_Door even if casts miss thin/offset colliders.
        if (bestSuccessDoor == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Chapter3")
        {
            SuccessDoor[] allSuccessDoors = FindObjectsByType<SuccessDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allSuccessDoors.Length; i++)
            {
                SuccessDoor door = allSuccessDoors[i];
                if (door == null || door.IsDoorOpen) continue;

                float dist = Vector3.Distance(ray.origin, door.transform.position);
                if (dist > interactDistance) continue;

                Vector3 toDoor = (door.transform.position - ray.origin).normalized;
                float dot = Vector3.Dot(ray.direction, toDoor);
                if (dot < 0.1f) continue;

                if (dist < bestSuccessDoorDistance)
                {
                    bestSuccessDoorDistance = dist;
                    bestSuccessDoor = door;
                }
            }
        }

        // Chapter3 fallback: detect DoorQuestion1..n by name even if collider hits miss.
        if (bestChapter3QuestionDoor == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Chapter3")
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;

                string n = t.name;
                if (string.IsNullOrEmpty(n)) continue;
                bool isDoorQuestionName =
                    n.Equals("DoorQuestion1", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("DoorQuestion2", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("DoorQuestion3", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("DoorQuestion4", System.StringComparison.OrdinalIgnoreCase) ||
                    n.IndexOf("DoorQuestion", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isDoorQuestionName) continue;

                float dist = Vector3.Distance(ray.origin, t.position);
                if (dist > interactDistance) continue;

                Vector3 toDoor = (t.position - ray.origin).normalized;
                float dot = Vector3.Dot(ray.direction, toDoor);
                if (dot < 0.1f) continue;

                if (dist < bestChapter3QuestionDoorDistance)
                {
                    bestChapter3QuestionDoorDistance = dist;
                    bestChapter3QuestionDoor = t;
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  Update prompts — priority: gate > key > candle > table > doors
        // ═══════════════════════════════════════════════
        if (bestInteractable != null)
        {
            currentInteractable = bestInteractable;
            currentTable = null;
            currentKey = null;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = null;

            string promptText;
            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryFull())
            {
                int total = InventoryManager.Instance.GetTotalGateCount();
                int cap = InventoryManager.Instance.GetCurrentGateCapacity();
                promptText = $"Press E to swap for {bestInteractable.gateType} Gate ({total}/{cap})";
            }
            else
            {
                promptText = bestInteractable.GetInteractionText();
            }
            ShowPrompt(promptText);
        }
        else if (bestKey != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = bestKey;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = null;
            ShowPrompt("Press E to pick up key");
        }
        else if (bestCandle != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = null;
            currentCandle = bestCandle;
            currentDoor = null;
            currentSuccessDoor = null;
            ShowPrompt("Press E to pick up candle");
        }
        else if (bestTable != null)
        {
            currentInteractable = null;
            currentTable = bestTable;
            currentKey = null;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = null;
            ShowPrompt("Press E to open Puzzle Table");
        }
        else if (bestSuccessDoor != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = null;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = bestSuccessDoor;
            currentTruthDisplay = null;
            ShowPrompt("Press E to open");
        }
        else if (bestTruthDisplay != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = null;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = null;
            currentTruthDisplay = bestTruthDisplay;
            currentChapter3QuestionDoor = null;
            ShowPrompt("Press E to view Truth Table");
        }
        else if (bestChapter3QuestionDoor != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = null;
            currentCandle = null;
            currentDoor = null;
            currentSuccessDoor = null;
            currentTruthDisplay = null;
            currentChapter3QuestionDoor = bestChapter3QuestionDoor;
            ShowPrompt("Press E to open door");
        }
        else if (bestDoor != null)
        {
            currentInteractable = null;
            currentTable = null;
            currentKey = null;
            currentCandle = null;
            currentDoor = bestDoor;
            currentSuccessDoor = null;
            currentTruthDisplay = null;
            currentChapter3QuestionDoor = null;
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool showDoorPrompt = activeScene == "Level1" || activeScene.StartsWith("Chapter");

            if (showDoorPrompt)
                ShowPrompt("Press E to open door");
            else
                HidePrompt();
        }
        else
        {
            ClearTargets();
            HidePrompt();
        }
    }

    private void ShowPrompt(string text)
    {
        if (_levelUI == null)
            _levelUI = FindAnyObjectByType<LevelUIManager>();
        if (_mainUI == null)
            _mainUI = FindAnyObjectByType<UIManager>();

        bool shown = false;

        if (_levelUI != null && _levelUI.interactPrompt != null)
        {
            _levelUI.ShowInteractPrompt(text);
            shown = true;
        }

        if (!shown && _mainUI != null)
        {
            _mainUI.ShowInteractPrompt(true, text);
            shown = true;
        }

        if (!shown)
            UIManager.SafeShowInteractPrompt(true, text);
    }

    private void HidePrompt()
    {
        if (_levelUI == null)
            _levelUI = FindAnyObjectByType<LevelUIManager>();
        if (_mainUI == null)
            _mainUI = FindAnyObjectByType<UIManager>();

        bool hidden = false;

        if (_levelUI != null && _levelUI.interactPrompt != null)
        {
            _levelUI.HideInteractPrompt();
            hidden = true;
        }

        if (!hidden && _mainUI != null)
        {
            _mainUI.ShowInteractPrompt(false);
            hidden = true;
        }

        if (!hidden)
            UIManager.SafeShowInteractPrompt(false);
    }

    void TryCollectGate()
    {
        if (currentInteractable == null) return;
        if (playerCamera == null) return;
        if (GetGateDistanceFromCamera(playerCamera.transform.position, currentInteractable) > MaxGateCollectDistance)
        {
            currentInteractable = null;
            HidePrompt();
            return;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryFull())
        {
            Debug.Log("[SimpleGateCollector] Inventory full — showing swap UI.");
            SwapGateUI.ShowSwap(currentInteractable, andGatePrefab, orGatePrefab, notGatePrefab, transform);
            currentInteractable = null;
            return;
        }

        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        currentInteractable.Interact();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGatePickupSound();
        currentInteractable = null;
    }

    void TryDiscardGate()
    {
        if (InventoryManager.Instance == null) return;

        // If the popup is already open, Q is the close key — let SwapGateUI handle it.
        if (SwapGateUI.IsOpen) return;

        Debug.Log("[SimpleGateCollector] Opening discard UI.");
        SwapGateUI.ShowDiscard(andGatePrefab, orGatePrefab, notGatePrefab, transform);
    }

    private string GetSelectedDroppableGateType()
    {
        if (GameInventoryUI.Instance == null)
            return null;

        switch (GameInventoryUI.Instance.GetSelectedItem())
        {
            case GameInventoryUI.ItemType.AND:
                return "AND";
            case GameInventoryUI.ItemType.OR:
                return "OR";
            case GameInventoryUI.ItemType.NOT:
                return "NOT";
            default:
                return null;
        }
    }
}
