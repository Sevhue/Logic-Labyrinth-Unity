using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Attach to the key GameObject in the scene.
/// Handles bob/spin animation and the collection logic.
/// Prompt and E-key detection are handled by SimpleGateCollector.
/// </summary>
public class CollectibleKey : MonoBehaviour
{
    public enum KeyType { Tutorial, Success }

    [Header("Key Settings")]
    [Tooltip("Tutorial = opens Door_Tutorial, Success = opens Door_Success (after puzzle)")]
    public KeyType keyType = KeyType.Tutorial;

    [Header("Animation")]
    public float bobSpeed = 1.5f;
    public float bobHeight = 0.15f;

    [Header("Shine Effect (Success Key)")]
    public Color shineColor = new Color(1f, 0.85f, 0.3f, 1f);
    public float shinePulseSpeed = 2.5f;
    public float shineIntensity = 3f;
    public float shineRange = 4f;
    [Tooltip("How long the success-key glow should stay visible after spawn.")]
    public float successGlowDuration = 1f;

    [Header("UI")]
    [Tooltip("Legacy popup message. Keep false to avoid duplicate stacked key messages.")]
    public bool showLegacyPickupPopup = false;

    [Header("Success Key Hint")]
    [SerializeField] private bool showSuccessKeyDoorHint = true;
    [SerializeField] private float successKeyHintDelay = 0.2f;
    [SerializeField] private float successKeyHintVisibleTime = 1.5f;

    /// <summary>True after the player picks up the key.</summary>
    public bool IsCollected { get; private set; } = false;

    // Legacy static flag — no longer needed but kept for compat
    public static bool IsShowingPrompt => false;

    private Vector3 startLocalPos;
    private List<Light> shineLights = new List<Light>();
    private Coroutine shineSpawnCoroutine;
    private Coroutine bobRoutine;
    private static readonly HashSet<string> successHintShownByLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        // Apply before physics/collider usage to avoid negative-scale collider warnings.
        FixNegativeScale();
        EnsureTriggerCollider();
    }

    void Start()
    {
        startLocalPos = transform.localPosition;
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider legacyBox = GetComponent<BoxCollider>();
        if (legacyBox != null) Destroy(legacyBox);

        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = 0.15f;
        sc.center = new Vector3(0f, 0.005f, 0f);
        sc.isTrigger = true;
    }

    private void EnsureSuccessDoorExists()
    {
        if (FindAnyObjectByType<SuccessDoor>() != null) return;

        GameObject candidate = GameObject.Find("Door_Success");
        if (candidate == null) candidate = GameObject.Find("Success_Door");

        if (candidate == null)
        {
            // Fallback for misnamed scenes: pick the nearest object containing "door".
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float nearest = float.MaxValue;
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (go == null) continue;
                if (!go.name.ToLowerInvariant().Contains("door")) continue;
                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d < nearest)
                {
                    nearest = d;
                    candidate = go;
                }
            }
        }

        if (candidate != null && candidate.GetComponent<SuccessDoor>() == null)
        {
            candidate.AddComponent<SuccessDoor>();
            Debug.Log($"[CollectibleKey] Auto-added SuccessDoor to '{candidate.name}' for level completion flow.");
        }
    }

    void OnEnable()
    {
        startLocalPos = transform.localPosition;
        if (!IsCollected && bobRoutine == null)
            bobRoutine = StartCoroutine(BobAndSpin());

        if (keyType == KeyType.Success)
            EnsureSuccessDoorExists();

        // Kick off the shine effect for success keys every time the object is activated
        if (keyType == KeyType.Success)
        {
            shineSpawnCoroutine = StartCoroutine(SpawnShineEffect());
        }
    }

    void OnDisable()
    {
        if (bobRoutine != null)
        {
            StopCoroutine(bobRoutine);
            bobRoutine = null;
        }
        CleanupShine();
    }

    /// <summary>
    /// Called by SimpleGateCollector when the player presses E while looking at the key.
    /// </summary>
    public void CollectKey()
    {
        if (IsCollected) return;
        IsCollected = true;

        Debug.Log($"[CollectibleKey] Key collected! Type={keyType}");

        if (keyType == KeyType.Success)
            SuccessDoor.PlayerHasSuccessKey = true;
        else
            TutorialDoor.PlayerHasKey = true;

        // Remove any stale locked-door overlays so key pickup doesn't stack multiple UI banners.
        TutorialDoor.HideAllLockedMessages();
        SuccessDoor.HideAllLockedMessages();

        // Stop the shine effect — it will fade during the pickup animation
        CleanupShine();

        // Notify hotbar UI
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.OnKeyCollected();

        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        if (ShouldShowSuccessDoorHint())
            ShowSuccessDoorHint();

        StartCoroutine(PickupAnimation());
    }

    private bool ShouldShowSuccessDoorHint()
    {
        if (!showSuccessKeyDoorHint || keyType != KeyType.Success) return false;

        string sceneName = SceneManager.GetActiveScene().name;
        bool isLevelOneOrTwo = sceneName.Equals("Level1", StringComparison.OrdinalIgnoreCase) ||
                               sceneName.Equals("Level2", StringComparison.OrdinalIgnoreCase);
        if (!isLevelOneOrTwo) return false;

        if (successHintShownByLevel.Contains(sceneName)) return false;
        successHintShownByLevel.Add(sceneName);
        return true;
    }

    private void ShowSuccessDoorHint()
    {
        GameObject hintUI = new GameObject("SuccessKeyDoorHintMessage");
        Canvas canvas = hintUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 620;

        CanvasScaler scaler = hintUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(hintUI.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.32f, 0.68f);
        panelRT.anchorMax = new Vector2(0.68f, 0.78f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.09f, 0.08f, 0.02f, 0.88f);
        bg.raycastTarget = false;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.35f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(18f, 8f);
        textRT.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Use this key to unlock the exit door.";
        tmp.fontSize = 30;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.62f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        CanvasGroup cg = hintUI.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        SuccessKeyHintAutoFade fader = hintUI.AddComponent<SuccessKeyHintAutoFade>();
        fader.Begin(
            cg,
            Mathf.Max(0f, successKeyHintDelay),
            Mathf.Max(0.1f, successKeyHintVisibleTime),
            0.2f,
            0.35f);
    }

    private IEnumerator PickupAnimation()
    {
        // Legacy popup is optional; inventory UI already shows key collection feedback.
        GameObject msgUI = showLegacyPickupPopup ? CreatePickupMessage() : null;

        // Shrink and float upward
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;
        float duration = 0.6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = startPos + Vector3.up * t * 1.5f;
            transform.localScale = startScale * (1f - t);
            yield return null;
        }

        // Fade out message
        yield return new WaitForSeconds(1.5f);
        if (msgUI != null)
        {
            CanvasGroup cg = msgUI.AddComponent<CanvasGroup>();
            float fadeTime = 0.5f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                cg.alpha = 1f - (fadeElapsed / fadeTime);
                yield return null;
            }
            Destroy(msgUI);
        }

        Destroy(gameObject);
    }

    private GameObject CreatePickupMessage()
    {
        GameObject msgUI = new GameObject("KeyPickupMessage");
        Canvas canvas = msgUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = msgUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(msgUI.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.3f, 0.55f);
        panelRT.anchorMax = new Vector2(0.7f, 0.65f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.02f, 0.85f);
        bg.raycastTarget = false;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Key collected!";
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.9f, 0.5f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return msgUI;
    }

    // ═══════════════════════════════════════════════
    //  SHINE EFFECT
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Creates a dramatic golden glow that bursts outward then settles into a
    /// gentle pulse around the key. Only used for Success keys.
    /// </summary>
    private IEnumerator SpawnShineEffect()
    {
        // Small delay so the key is fully positioned before lights spawn
        yield return null;

        CreateShineLights();

        // ── Phase 1: Burst — lights flash bright then ease down ──
        float burstDuration = 0.6f;
        float burstIntensity = shineIntensity * 3f;
        float burstRange = shineRange * 2f;
        float elapsed = 0f;

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;
            float easeOut = 1f - (t * t); // decelerating ease

            float currentIntensity = Mathf.Lerp(shineIntensity, burstIntensity, easeOut);
            float currentRange = Mathf.Lerp(shineRange, burstRange, easeOut);

            foreach (var light in shineLights)
            {
                if (light != null)
                {
                    light.intensity = currentIntensity;
                    light.range = currentRange;
                }
            }
            yield return null;
        }

        // ── Phase 2: Keep a short pulse window, then turn off ──
        float glowTime = 0f;
        while (glowTime < Mathf.Max(0.05f, successGlowDuration) && !IsCollected)
        {
            glowTime += Time.deltaTime;
            float pulse = (Mathf.Sin(glowTime * shinePulseSpeed * 3f) + 1f) / 2f;
            float intensity = Mathf.Lerp(shineIntensity * 0.5f, shineIntensity, pulse);
            float range = Mathf.Lerp(shineRange * 0.7f, shineRange, pulse);

            foreach (var light in shineLights)
            {
                if (light != null)
                {
                    light.intensity = intensity;
                    light.range = range;
                }
            }
            yield return null;
        }

        // Requested behavior: success-key light shows briefly, then turns off.
        foreach (var light in shineLights)
        {
            if (light != null) Destroy(light.gameObject);
        }
        shineLights.Clear();
    }

    private void CreateShineLights()
    {
        // Clean up any existing lights first
        foreach (var l in shineLights)
            if (l != null) Destroy(l.gameObject);
        shineLights.Clear();

        // Place lights around the key for an even glow
        Vector3[] offsets = new Vector3[]
        {
            new Vector3( 0f,    0.08f,  0f),     // top
            new Vector3( 0f,   -0.04f,  0f),     // bottom
            new Vector3( 0.1f,  0.02f,  0f),     // right
            new Vector3(-0.1f,  0.02f,  0f),     // left
            new Vector3( 0f,    0.02f,  0.1f),   // front
            new Vector3( 0f,    0.02f, -0.1f),   // back
        };

        foreach (var offset in offsets)
        {
            GameObject lightGO = new GameObject("KeyShineLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = offset;

            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = shineColor;
            light.intensity = shineIntensity;
            light.range = shineRange;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;

            shineLights.Add(light);
        }

        Debug.Log("[CollectibleKey] Shine lights created for Success key.");
    }

    private void CleanupShine()
    {
        if (shineSpawnCoroutine != null)
        {
            StopCoroutine(shineSpawnCoroutine);
            shineSpawnCoroutine = null;
        }

        foreach (var light in shineLights)
        {
            if (light != null) Destroy(light.gameObject);
        }
        shineLights.Clear();
    }

    // ═══════════════════════════════════════════════
    //  NEGATIVE-SCALE FIX
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Flips any negative local-scale axes to positive.
    /// BoxCollider does not support negative scale and will log warnings.
    /// </summary>
    private void FixNegativeScale()
    {
        Vector3 s = transform.localScale;
        if (s.x < 0f || s.y < 0f || s.z < 0f)
        {
            transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            Debug.Log($"[CollectibleKey] Fixed negative scale on '{name}': {s} → {transform.localScale}");
        }
    }

    // ═══════════════════════════════════════════════
    //  BOB & SPIN
    // ═══════════════════════════════════════════════

    private IEnumerator BobAndSpin()
    {
        float timer = 0f;
        while (!IsCollected)
        {
            timer += Time.deltaTime;

            float yOffset = Mathf.Sin(timer * bobSpeed) * bobHeight;
            transform.localPosition = startLocalPos + Vector3.up * yOffset;
            transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);

            yield return null;
        }
    }

    void OnDestroy()
    {
        if (bobRoutine != null)
        {
            StopCoroutine(bobRoutine);
            bobRoutine = null;
        }
        CleanupShine();
    }
}

internal sealed class SuccessKeyHintAutoFade : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private float initialDelay;
    private float holdSeconds;
    private float fadeInSeconds;
    private float fadeOutSeconds;

    public void Begin(CanvasGroup targetCanvasGroup, float delay, float hold, float fadeIn, float fadeOut)
    {
        canvasGroup = targetCanvasGroup;
        initialDelay = Mathf.Max(0f, delay);
        holdSeconds = Mathf.Max(0f, hold);
        fadeInSeconds = Mathf.Max(0.01f, fadeIn);
        fadeOutSeconds = Mathf.Max(0.01f, fadeOut);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (canvasGroup == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }

        Destroy(gameObject);
    }
}
