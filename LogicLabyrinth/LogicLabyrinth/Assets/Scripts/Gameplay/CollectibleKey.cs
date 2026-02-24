using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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

    /// <summary>True after the player picks up the key.</summary>
    public bool IsCollected { get; private set; } = false;

    // Legacy static flag — no longer needed but kept for compat
    public static bool IsShowingPrompt => false;

    private Vector3 startLocalPos;
    private List<Light> shineLights = new List<Light>();
    private Coroutine shinePulseCoroutine;
    private Coroutine shineSpawnCoroutine;

    void Start()
    {
        startLocalPos = transform.localPosition;

        // Ensure a collider exists so SphereCast can hit this object.
        // Must be a TRIGGER so it doesn't push the player's CharacterController.
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();

        // Use Abs to avoid "BoxCollider does not support negative scale" warnings
        // when the key model has a negative scale on any axis.
        Vector3 ls = transform.lossyScale;
        float sX = Mathf.Abs(ls.x) > 0.0001f ? 0.15f / Mathf.Abs(ls.x) : 0.15f;
        float sY = Mathf.Abs(ls.y) > 0.0001f ? 0.15f / Mathf.Abs(ls.y) : 0.15f;
        float sZ = Mathf.Abs(ls.z) > 0.0001f ? 0.15f / Mathf.Abs(ls.z) : 0.15f;
        bc.size = new Vector3(sX, sY, sZ);
        bc.center = new Vector3(0f, 0.005f, 0f);
        bc.isTrigger = true;

        // Fix negative scale — BoxCollider requires all scale axes to be positive.
        FixNegativeScale();

        StartCoroutine(BobAndSpin());
    }

    void OnEnable()
    {
        // Kick off the shine effect for success keys every time the object is activated
        if (keyType == KeyType.Success)
        {
            shineSpawnCoroutine = StartCoroutine(SpawnShineEffect());
        }
    }

    void OnDisable()
    {
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

        // Stop the shine effect — it will fade during the pickup animation
        CleanupShine();

        // Notify hotbar UI
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.OnKeyCollected();

        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        // Show pickup message
        GameObject msgUI = CreatePickupMessage();

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

        // ── Phase 2: Continuous gentle pulse ──
        shinePulseCoroutine = StartCoroutine(PulseShineLights());
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

    private IEnumerator PulseShineLights()
    {
        float timer = 0f;
        while (!IsCollected)
        {
            timer += Time.deltaTime * shinePulseSpeed;
            float t = (Mathf.Sin(timer) + 1f) / 2f; // 0 → 1 wave

            float intensity = Mathf.Lerp(shineIntensity * 0.4f, shineIntensity, t);
            float range = Mathf.Lerp(shineRange * 0.6f, shineRange, t);

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
    }

    private void CleanupShine()
    {
        if (shinePulseCoroutine != null)
        {
            StopCoroutine(shinePulseCoroutine);
            shinePulseCoroutine = null;
        }
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
        CleanupShine();
    }
}
