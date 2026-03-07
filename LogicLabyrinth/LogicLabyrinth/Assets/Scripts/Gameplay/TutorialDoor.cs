using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Attaches to the Door_Tutorial GameObject to:
/// 1. Show pulsing golden point lights around the door frame (when highlight is started)
/// 2. Handle locked/unlocked interaction when player presses E (called by SimpleGateCollector)
/// Prompt display and E-key detection are handled by SimpleGateCollector.
/// </summary>
public class TutorialDoor : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color lightColor = new Color(1f, 0.8f, 0.3f, 1f);
    public float pulseSpeed = 2f;
    public float baseLightIntensity = 2f;
    public float baseLightRange = 3f;

    // Static key state — set by CollectibleKey when player picks it up
    public static bool PlayerHasKey { get; set; } = false;

    // Legacy static flag — no longer needed but kept for compat
    public static bool IsShowingPrompt => false;

    /// <summary>True after the door has been unlocked and opened.</summary>
    public bool IsDoorOpen { get; private set; } = false;

    // State
    private bool isHighlighting = false;
    private List<Light> highlightLights = new List<Light>();
    private GameObject lockedMessageUI;
    private GameObject runHintUI;
    private Coroutine pulseCoroutine;
    private Coroutine lockedMessageRoutine;
    private Coroutine runHintRoutine;

    [Header("Level 1 Run Hint")]
    [SerializeField] private bool enableRunHint = true;
    [SerializeField] private float runHintDelaySeconds = 4f;
    [SerializeField] private float minWalkDistanceForHint = 1.5f;

    private static bool runHintShownThisSession = false;

    void Start()
    {
        // Ensure there's a trigger BoxCollider for SphereCast detection
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(2f, 4f, 1f);
            bc.center = new Vector3(0f, 1.5f, 0f);
        }
        bc.isTrigger = true;

        Debug.Log("[TutorialDoor] Component initialized on " + gameObject.name);
    }

    // ═══════════════════════════════════════════════
    //  INTERACTION (called by SimpleGateCollector)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Called by SimpleGateCollector when the player presses E while looking at the door.
    /// </summary>
    public void TryInteract()
    {
        if (IsDoorOpen) return;

        if (PlayerHasKey)
        {
            UnlockDoor();
        }
        else
        {
            ShowLockedMessage();
        }
    }

    // ═══════════════════════════════════════════════
    //  HIGHLIGHT (optional visual — started by CutsceneController)
    // ═══════════════════════════════════════════════

    public void StartHighlight()
    {
        if (isHighlighting) return;
        isHighlighting = true;

        CreateHighlightLights();
        pulseCoroutine = StartCoroutine(PulseLights());

        Debug.Log("[TutorialDoor] Highlight started with point lights.");
    }

    public void StopHighlight()
    {
        if (!isHighlighting) return;
        isHighlighting = false;

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        foreach (var light in highlightLights)
        {
            if (light != null) Destroy(light.gameObject);
        }
        highlightLights.Clear();
    }

    private void CreateHighlightLights()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Bounds bounds;
        if (mf != null && mf.sharedMesh != null)
            bounds = mf.sharedMesh.bounds;
        else
            bounds = new Bounds(Vector3.zero, new Vector3(1.5f, 3f, 0.3f));

        float frontZ = bounds.max.z + 0.3f;
        float backZ = bounds.min.z - 0.3f;
        float midX = bounds.center.x;
        float leftX = bounds.min.x - 0.1f;
        float rightX = bounds.max.x + 0.1f;
        float bottomY = bounds.min.y + 0.3f;
        float midY = bounds.center.y;
        float topY = bounds.max.y - 0.1f;

        Vector3[] lightPositions = new Vector3[]
        {
            // Front side
            new Vector3(leftX, bottomY, frontZ),
            new Vector3(leftX, midY, frontZ),
            new Vector3(leftX, topY, frontZ),
            new Vector3(midX, topY, frontZ),
            new Vector3(rightX, topY, frontZ),
            new Vector3(rightX, midY, frontZ),
            new Vector3(rightX, bottomY, frontZ),
            // Back side
            new Vector3(leftX, midY, backZ),
            new Vector3(midX, topY, backZ),
            new Vector3(rightX, midY, backZ),
        };

        foreach (var localPos in lightPositions)
        {
            GameObject lightGO = new GameObject("DoorHighlightLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = localPos;

            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.intensity = baseLightIntensity;
            light.range = baseLightRange;
            light.shadows = LightShadows.None;

            highlightLights.Add(light);
        }
    }

    private IEnumerator PulseLights()
    {
        float timer = 0f;
        while (isHighlighting)
        {
            timer += Time.deltaTime * pulseSpeed;
            float t = (Mathf.Sin(timer) + 1f) / 2f;
            float intensity = Mathf.Lerp(baseLightIntensity * 0.3f, baseLightIntensity, t);
            float range = Mathf.Lerp(baseLightRange * 0.6f, baseLightRange, t);

            foreach (var light in highlightLights)
            {
                if (light != null)
                {
                    light.intensity = intensity;
                    light.range = range;
                }
            }
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════
    //  DOOR STATE
    // ═══════════════════════════════════════════════

    private void UnlockDoor()
    {
        Debug.Log("[TutorialDoor] Player has the key! Unlocking door...");
        IsDoorOpen = true;

        // Consume the key — remove it from inventory and hotbar
        PlayerHasKey = false;
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        StopHighlight();
        StartCoroutine(ShowUnlockMessageAndOpen());
    }

    private void ShowLockedMessage()
    {
        if (lockedMessageUI != null) return;
        Debug.Log("[TutorialDoor] Door is locked! Player needs a key.");
        lockedMessageRoutine = StartCoroutine(ShowLockedMessageRoutine());
    }

    public void HideLockedMessageImmediate()
    {
        if (lockedMessageRoutine != null)
        {
            StopCoroutine(lockedMessageRoutine);
            lockedMessageRoutine = null;
        }
        if (lockedMessageUI != null)
        {
            Destroy(lockedMessageUI);
            lockedMessageUI = null;
        }
    }

    public static void HideAllLockedMessages()
    {
        TutorialDoor[] doors = FindObjectsByType<TutorialDoor>(FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].HideLockedMessageImmediate();
        }
    }

    private IEnumerator ShowUnlockMessageAndOpen()
    {
        yield return StartCoroutine(AnimateDoorOpen());
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStartRunHintFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Fallback: if the player was already overlapping when the door opened,
        // OnTriggerEnter may not run, so keep checking while inside.
        TryStartRunHintFromCollider(other);
    }

    private void TryStartRunHintFromCollider(Collider other)
    {
        if (!ShouldEvaluateRunHint()) return;
        if (!IsPlayerCollider(other)) return;
        if (runHintRoutine != null) return;

        runHintRoutine = StartCoroutine(EvaluateRunHintAfterDelay(other.transform));
    }

    private bool ShouldEvaluateRunHint()
    {
        if (!enableRunHint || runHintShownThisSession) return false;
        if (!IsDoorOpen) return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.name == "Level1";
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        return false;
    }

    private IEnumerator EvaluateRunHintAfterDelay(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            runHintRoutine = null;
            yield break;
        }

        Vector3 startPosition = playerTransform.position;
        float elapsed = 0f;
        bool shiftUsed = false;

        while (elapsed < runHintDelaySeconds)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                shiftUsed = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        runHintRoutine = null;

        if (runHintShownThisSession || shiftUsed || playerTransform == null)
            yield break;

        if (!ShouldEvaluateRunHint())
            yield break;

        float walkedDistance = Vector3.Distance(startPosition, playerTransform.position);
        if (walkedDistance < minWalkDistanceForHint)
            yield break;

        runHintShownThisSession = true;
        StartCoroutine(ShowRunHintRoutine());
    }

    private IEnumerator ShowRunHintRoutine()
    {
        if (runHintUI != null) yield break;

        runHintUI = new GameObject("RunHintMessage");
        Canvas canvas = runHintUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 590;

        CanvasScaler scaler = runHintUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(runHintUI.transform, false);
        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.58f, 0.84f);
        panelRT.anchorMax = new Vector2(0.98f, 0.95f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.05f, 0.02f, 0.9f);
        bg.raycastTarget = false;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.7f, 0.35f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(15f, 8f);
        textRT.offsetMax = new Vector2(-15f, -8f);

        TextMeshProUGUI textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.text = "Hold Shift to run faster.";
        textTMP.fontSize = 20;
        textTMP.alignment = TextAlignmentOptions.Center;
        textTMP.color = new Color(1f, 0.9f, 0.6f, 1f);
        textTMP.fontStyle = FontStyles.Italic;
        textTMP.enableWordWrapping = true;
        textTMP.raycastTarget = false;

        yield return new WaitForSeconds(4f);

        CanvasGroup cg = runHintUI.AddComponent<CanvasGroup>();
        float fadeTime = 0.5f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - (elapsed / fadeTime);
            yield return null;
        }

        Destroy(runHintUI);
        runHintUI = null;
    }

    private IEnumerator AnimateDoorOpen()
    {
        // Disable only solid colliders so the rotating door doesn't push the player.
        // Keep trigger colliders active for tutorial hint detection.
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (col != null && !col.isTrigger)
                col.enabled = false;
        }

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 90f, 0f);
        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.localRotation = endRot;
        Debug.Log("[TutorialDoor] Door opened!");
    }

    private IEnumerator ShowLockedMessageRoutine()
    {
        lockedMessageUI = new GameObject("LockedDoorMessage");
        Canvas canvas = lockedMessageUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = lockedMessageUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(lockedMessageUI.transform, false);
        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.25f, 0.4f);
        panelRT.anchorMax = new Vector2(0.75f, 0.55f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.05f, 0.02f, 0.9f);
        bg.raycastTarget = false;

        Outline outlineComp = panelGO.AddComponent<Outline>();
        outlineComp.effectColor = new Color(0.85f, 0.6f, 0.2f, 0.8f);
        outlineComp.effectDistance = new Vector2(2f, 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "The door is locked. You need a key.";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        tmp.fontStyle = FontStyles.Italic;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        yield return new WaitForSeconds(3f);

        float fadeTime = 0.5f;
        float elapsed = 0f;
        CanvasGroup cg = lockedMessageUI.AddComponent<CanvasGroup>();
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - (elapsed / fadeTime);
            yield return null;
        }

        Destroy(lockedMessageUI);
        lockedMessageUI = null;
        lockedMessageRoutine = null;
    }

    void OnDestroy()
    {
        if (runHintRoutine != null)
            StopCoroutine(runHintRoutine);

        foreach (var light in highlightLights)
        {
            if (light != null) Destroy(light.gameObject);
        }
        highlightLights.Clear();
        if (lockedMessageUI != null) Destroy(lockedMessageUI);
        if (runHintUI != null) Destroy(runHintUI);
    }
}
