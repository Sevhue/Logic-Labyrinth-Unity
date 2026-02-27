using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attaches to Door_Success. Works like TutorialDoor but uses its own
/// static key flag (PlayerHasSuccessKey) so it doesn't conflict with
/// the tutorial key/door system.
/// When the door opens it triggers the next-level transition.
/// </summary>
public class SuccessDoor : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color lightColor = new Color(0.3f, 1f, 0.5f, 1f); // Green-gold
    public float pulseSpeed = 2f;
    public float baseLightIntensity = 2f;
    public float baseLightRange = 3f;

    /// <summary>Set by CollectibleKey (KeyType.Success) when the player picks it up.</summary>
    public static bool PlayerHasSuccessKey { get; set; } = false;

    public static bool IsShowingPrompt => false;

    /// <summary>True after the door has been unlocked and opened.</summary>
    public bool IsDoorOpen { get; private set; } = false;

    // State
    private bool isHighlighting = false;
    private List<Light> highlightLights = new List<Light>();
    private GameObject lockedMessageUI;
    private Coroutine pulseCoroutine;
    private Coroutine lockedMessageRoutine;

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

        Debug.Log("[SuccessDoor] Component initialized on " + gameObject.name);
    }

    // ═══════════════════════════════════════════════
    //  INTERACTION (called by SimpleGateCollector)
    // ═══════════════════════════════════════════════

    public void TryInteract()
    {
        if (IsDoorOpen) return;

        if (PlayerHasSuccessKey)
        {
            UnlockDoor();
        }
        else
        {
            ShowLockedMessage();
        }
    }

    // ═══════════════════════════════════════════════
    //  HIGHLIGHT
    // ═══════════════════════════════════════════════

    public void StartHighlight()
    {
        if (isHighlighting) return;
        isHighlighting = true;

        CreateHighlightLights();
        pulseCoroutine = StartCoroutine(PulseLights());
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
            new Vector3(leftX, bottomY, frontZ),
            new Vector3(leftX, midY, frontZ),
            new Vector3(leftX, topY, frontZ),
            new Vector3(midX, topY, frontZ),
            new Vector3(rightX, topY, frontZ),
            new Vector3(rightX, midY, frontZ),
            new Vector3(rightX, bottomY, frontZ),
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
        Debug.Log("[SuccessDoor] Player has the success key! Unlocking door...");
        IsDoorOpen = true;

        // Consume the key
        PlayerHasSuccessKey = false;
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        StopHighlight();
        StartCoroutine(AnimateDoorOpenAndTransition());
    }

    private void ShowLockedMessage()
    {
        if (lockedMessageUI != null) return;
        Debug.Log("[SuccessDoor] Door is locked! Player needs to complete the puzzle first.");
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
        SuccessDoor[] doors = FindObjectsByType<SuccessDoor>(FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].HideLockedMessageImmediate();
        }
    }

    private IEnumerator AnimateDoorOpenAndTransition()
    {
        // Disable ALL colliders so the rotating door doesn't push the player
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        // ── 1. Swing the door open ──
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
        Debug.Log("[SuccessDoor] Door opened!");

        // ── 2. Create full-screen fade overlay ──
        GameObject fadeGO = CreateFadeOverlay();
        CanvasGroup fadeCG = fadeGO.GetComponent<CanvasGroup>();
        fadeCG.alpha = 0f;

        // Keep the overlay alive across scene loads so the new scene fades in from black
        Object.DontDestroyOnLoad(fadeGO);

        // Brief pause so the player sees the open door
        yield return new WaitForSeconds(0.5f);

        // ── 3. Slowly fade the screen to black ──
        float fadeDuration = 2f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (fadeCG != null) fadeCG.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        if (fadeCG != null) fadeCG.alpha = 1f;
        Debug.Log("[SuccessDoor] Screen fully dark.");

        // ── 4. Stop the timer and record the best time ──
        bool recordedViaTimer = false;
        if (LevelTimer.Instance != null)
        {
            float completionTime = LevelTimer.Instance.StopAndRecordTime();
            if (completionTime > 0f)
            {
                recordedViaTimer = true;
                Debug.Log($"[SuccessDoor] Level completed in {LevelTimer.FormatTime(completionTime)}");
            }
        }

        // Fallback: if timer wasn't running/initialized, still record a time for this level.
        if (!recordedViaTimer && AccountManager.Instance != null)
        {
            int sceneLevel = GetSceneLevelNumber();
            if (sceneLevel > 1)
            {
                float fallbackSeconds = Mathf.Max(Time.timeSinceLevelLoad, 0.01f);
                AccountManager.Instance.RecordLevelTime(sceneLevel, fallbackSeconds);
                Debug.Log($"[SuccessDoor] Fallback time record for Level {sceneLevel}: {LevelTimer.FormatTime(fallbackSeconds)}");
            }
        }

        // ── 5. While dark, unlock the next level ──
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.UnlockNextLevel();
            Debug.Log("[SuccessDoor] UnlockNextLevel called on AccountManager.");
        }

        // Show the puzzle-complete panel (visible over the black screen)
        if (UIManager.Instance != null)
            UIManager.Instance.ShowPuzzleComplete();

        // Hold on black for a moment
        yield return new WaitForSeconds(2f);

        // ── 5. Load next level ──
        // Attach a self-fading script to the overlay — it will fade from black → clear
        // once the new scene finishes loading, then destroy itself.
        if (fadeGO != null)
            fadeGO.AddComponent<FadeOverlayAutoDestroy>();

        int sceneLevelForLoad = GetSceneLevelNumber();
        if (LevelManager.Instance != null)
        {
            // Prefer scene-derived next level so direct scene testing still advances correctly
            // (e.g., Level3 -> Level4 even if LevelManager currentLevel was not initialized).
            if (sceneLevelForLoad > 0)
            {
                int targetLevel = sceneLevelForLoad + 1;
                Debug.Log($"[SuccessDoor] Triggering scene-derived transition: Level {sceneLevelForLoad} -> Level {targetLevel}");
                LevelManager.Instance.LoadLevel(targetLevel);
            }
            else
            {
                Debug.Log("[SuccessDoor] Scene level unavailable, falling back to LoadNextLevel().");
                LevelManager.Instance.LoadNextLevel();
            }
        }
        else
        {
            // Fallback: LevelManager may not exist when testing a scene directly.
            int nextLevel = 2;
            if (AccountManager.Instance != null)
            {
                var player = AccountManager.Instance.GetCurrentPlayer();
                if (player != null)
                    nextLevel = player.lastCompletedLevel + 1;
            }
            else
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (sceneName.StartsWith("Level") && int.TryParse(sceneName.Substring(5), out int cur))
                    nextLevel = cur + 1;
            }

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetInventory();

            string nextScene = $"Level{nextLevel}";
            Debug.Log($"[SuccessDoor] LevelManager not found — loading '{nextScene}' via SceneManager.");
            SceneManager.LoadScene(nextScene);
        }
    }

    private int GetSceneLevelNumber()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) &&
            sceneName.StartsWith("Level") &&
            int.TryParse(sceneName.Substring(5), out int parsed))
        {
            return parsed;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════
    //  SCREEN FADE OVERLAY
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Creates a full-screen black overlay Canvas used for the fade-to-dark transition.
    /// Returns the root GameObject which has a CanvasGroup for alpha control.
    /// </summary>
    private GameObject CreateFadeOverlay()
    {
        GameObject fadeGO = new GameObject("SuccessDoor_FadeOverlay");

        // Canvas — on top of everything
        Canvas canvas = fadeGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = fadeGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        fadeGO.AddComponent<GraphicRaycaster>();

        // CanvasGroup for smooth alpha control
        CanvasGroup cg = fadeGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = true;  // block clicks while fading
        cg.interactable = false;

        // Full-screen black image
        GameObject imgGO = new GameObject("BlackOverlay");
        imgGO.transform.SetParent(fadeGO.transform, false);

        RectTransform rt = imgGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = imgGO.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        return fadeGO;
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
        tmp.text = "The door is locked.\nComplete the puzzle to get the key.";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        tmp.fontStyle = FontStyles.Italic;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        yield return new WaitForSeconds(3f);

        float fadeTime = 0.5f;
        float fadeElapsed = 0f;
        CanvasGroup cg = lockedMessageUI.AddComponent<CanvasGroup>();
        while (fadeElapsed < fadeTime)
        {
            fadeElapsed += Time.deltaTime;
            cg.alpha = 1f - (fadeElapsed / fadeTime);
            yield return null;
        }

        Destroy(lockedMessageUI);
        lockedMessageUI = null;
        lockedMessageRoutine = null;
    }

    void OnDestroy()
    {
        foreach (var light in highlightLights)
        {
            if (light != null) Destroy(light.gameObject);
        }
        highlightLights.Clear();
        if (lockedMessageUI != null) Destroy(lockedMessageUI);
    }
}

/// <summary>
/// Attach to a DontDestroyOnLoad fade overlay Canvas.
/// Once the new scene finishes loading it fades from black → clear, then self-destructs.
/// Also has a hard failsafe: if it still exists after <c>maxLifetimeSeconds</c>, it
/// force-destroys itself so the player never gets stuck on a black screen.
/// </summary>
public class FadeOverlayAutoDestroy : MonoBehaviour
{
    [Tooltip("How long (seconds) the fade-in from black takes.")]
    public float fadeInDuration = 1.5f;

    [Tooltip("Hard timeout — overlay is destroyed even if fade hasn't started.")]
    public float maxLifetimeSeconds = 8f;

    private CanvasGroup cg;
    private float spawnTime;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        spawnTime = Time.unscaledTime;
    }

    void Start()
    {
        // When Start runs we are already in the NEW scene — begin fade-in
        StartCoroutine(FadeIn());
    }

    void Update()
    {
        // Hard failsafe: if something went wrong and the overlay is still here, kill it
        if (Time.unscaledTime - spawnTime > maxLifetimeSeconds)
        {
            Debug.LogWarning("[FadeOverlayAutoDestroy] Hard timeout reached — destroying overlay.");
            Destroy(gameObject);
        }
    }

    private IEnumerator FadeIn()
    {
        // Wait one frame so the new scene is fully initialized
        yield return null;

        if (cg == null)
        {
            Destroy(gameObject);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled in case timeScale is 0
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        Debug.Log("[FadeOverlayAutoDestroy] Fade-in complete — destroying overlay.");
        Destroy(gameObject);
    }
}
