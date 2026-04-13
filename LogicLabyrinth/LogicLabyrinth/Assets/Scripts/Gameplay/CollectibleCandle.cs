using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to the candle GameObject in the scene.
/// Handles bob/spin animation and collection logic.
/// Prompt and E-key detection are handled by SimpleGateCollector.
/// When equipped from hotbar, creates a candle model in the player's right hand
/// and doubles the player's light.
/// </summary>
public class CollectibleCandle : MonoBehaviour
{
    private const float MaxUseSeconds = 60f;

    [Header("Animation")]
    public float bobSpeed = 1.2f;
    public float bobHeight = 0.12f;

    [Header("Scene Spawn Rules")]
    public bool alwaysSpawnInLevel1 = true;

    [Header("Candle Light (when equipped)")]
    public Color candleLightColor = new Color(1f, 0.85f, 0.55f, 1f);
    public float candleLightIntensity = 1.5f;
    public float candleLightRange = 4f;

    [Header("Tutorial Shine (Level1)")]
    public bool enableTutorialShineInLevel1 = true;
    public float tutorialShineBaseIntensity = 1.3f;
    public float tutorialShinePulseAmplitude = 0.45f;
    public float tutorialShinePulseSpeed = 2.2f;
    public float tutorialShineRange = 3.8f;
    public float tutorialEmissionBase = 0.8f;
    public float tutorialEmissionPulse = 0.7f;

    /// <summary>True after the player picks up the candle.</summary>
    public bool IsCollected { get; private set; } = false;

    /// <summary>Global static: true when the candle is equipped (active in hand).</summary>
    public static bool IsEquipped { get; set; } = false;

    // ── Saved mesh/material from the original candle FBX (captured on collect) ──
    private static Mesh savedCandleMesh;
    private static Material[] savedCandleMaterials;

    // Runtime references for the equipped candle
    private static GameObject equippedCandleModel;
    private static Light equippedCandleLight;
    private static int resolvedSceneHandle = int.MinValue;
    private static int selectedCandleInstanceId = int.MinValue;
    private static float remainingUseSeconds = 0f;
    private static float lastUsageTickTime = -1f;

    private Vector3 startLocalPos;
    private Light tutorialShineLight;
    private Material[] tutorialMats;
    private Coroutine tutorialShineCoroutine;

    void Start()
    {
        if (!ShouldSpawnInCurrentScene())
        {
            gameObject.SetActive(false);
            return;
        }

        startLocalPos = transform.localPosition;

        // Fix negative scale BEFORE adding collider to avoid
        // "BoxCollider does not support negative scale" warnings.
        FixNegativeScale();

        // Ensure a trigger collider exists so SphereCast can hit this object.
        // Size it based on the mesh renderer bounds (world-space), then convert to local space.
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds wb = rend.bounds;
            Vector3 ls = transform.lossyScale;
            // Use Mathf.Abs to guarantee positive sizes even if a parent has negative scale
            Vector3 localSize = new Vector3(
                Mathf.Abs(ls.x) > 0.0001f ? wb.size.x / Mathf.Abs(ls.x) : 0.1f,
                Mathf.Abs(ls.y) > 0.0001f ? wb.size.y / Mathf.Abs(ls.y) : 0.1f,
                Mathf.Abs(ls.z) > 0.0001f ? wb.size.z / Mathf.Abs(ls.z) : 0.1f
            );
            // Add generous padding so SphereCast can reliably hit it
            localSize *= 3f;
            bc.size = localSize;
            bc.center = transform.InverseTransformPoint(wb.center);
        }
        else
        {
            bc.size = new Vector3(0.02f, 0.02f, 0.02f);
            bc.center = Vector3.zero;
        }
        bc.isTrigger = true;

        TryEnableTutorialShine();
        StartCoroutine(BobAndSpin());
    }

    private bool ShouldSpawnInCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (alwaysSpawnInLevel1 && scene.name == "Level1")
            return true;

        ResolveSceneCandleSelection(scene);
        return selectedCandleInstanceId == GetInstanceID();
    }

    private void ResolveSceneCandleSelection(Scene scene)
    {
        if (resolvedSceneHandle == scene.handle)
            return;

        resolvedSceneHandle = scene.handle;
        selectedCandleInstanceId = int.MinValue;

        CollectibleCandle[] candles = FindObjectsByType<CollectibleCandle>(FindObjectsSortMode.None);
        if (candles == null || candles.Length == 0)
            return;

        int chosenIndex = Random.Range(0, candles.Length);
        selectedCandleInstanceId = candles[chosenIndex].GetInstanceID();
    }

    /// <summary>
    /// Called by SimpleGateCollector when the player presses E while looking at the candle.
    /// </summary>
    public void CollectCandle()
    {
        if (IsCollected) return;
        IsCollected = true;

        Debug.Log("[CollectibleCandle] Candle collected!");
        DisableTutorialShine();

        // ── Save the actual mesh and materials BEFORE destroying the object ──
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
        if (mf != null && mf.sharedMesh != null)
        {
            savedCandleMesh = mf.sharedMesh;
            Debug.Log($"[CollectibleCandle] Saved mesh: {savedCandleMesh.name} ({savedCandleMesh.vertexCount} verts)");
        }
        if (mr != null && mr.sharedMaterials != null && mr.sharedMaterials.Length > 0)
        {
            // Clone materials so they survive the Destroy
            savedCandleMaterials = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mr.sharedMaterials.Length; i++)
            {
                if (mr.sharedMaterials[i] != null)
                    savedCandleMaterials[i] = new Material(mr.sharedMaterials[i]);
            }
            Debug.Log($"[CollectibleCandle] Saved {savedCandleMaterials.Length} material(s)");
        }

        // Track in inventory
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.SetHasCandle(true);

        remainingUseSeconds = MaxUseSeconds;
        lastUsageTickTime = -1f;

        // Notify hotbar UI
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.OnCandleCollected();

        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        // Center candle-collected banner removed — the hotbar notification covers this.
        GameObject msgUI = null;

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

    private void TryEnableTutorialShine()
    {
        if (!enableTutorialShineInLevel1)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Level1")
            return;

        GameObject lightGO = new GameObject("TutorialCandleShine");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        tutorialShineLight = lightGO.AddComponent<Light>();
        tutorialShineLight.type = LightType.Point;
        tutorialShineLight.color = candleLightColor;
        tutorialShineLight.range = tutorialShineRange;
        tutorialShineLight.intensity = tutorialShineBaseIntensity;
        tutorialShineLight.shadows = LightShadows.None;

        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            var mats = new System.Collections.Generic.List<Material>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i].materials == null) continue;
                Material[] rm = rends[i].materials;
                for (int m = 0; m < rm.Length; m++)
                {
                    if (rm[m] != null)
                        mats.Add(rm[m]);
                }
            }
            tutorialMats = mats.ToArray();
        }

        tutorialShineCoroutine = StartCoroutine(PulseTutorialShine());
    }

    private IEnumerator PulseTutorialShine()
    {
        while (!IsCollected)
        {
            float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * tutorialShinePulseSpeed));

            if (tutorialShineLight != null)
            {
                tutorialShineLight.intensity = tutorialShineBaseIntensity + (pulse * tutorialShinePulseAmplitude);
            }

            if (tutorialMats != null)
            {
                Color emission = candleLightColor * (tutorialEmissionBase + (pulse * tutorialEmissionPulse));
                for (int i = 0; i < tutorialMats.Length; i++)
                {
                    Material mat = tutorialMats[i];
                    if (mat == null) continue;
                    if (!mat.HasProperty("_EmissionColor")) continue;

                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
            }

            yield return null;
        }
    }

    private void DisableTutorialShine()
    {
        if (tutorialShineCoroutine != null)
        {
            StopCoroutine(tutorialShineCoroutine);
            tutorialShineCoroutine = null;
        }

        if (tutorialShineLight != null)
        {
            Destroy(tutorialShineLight.gameObject);
            tutorialShineLight = null;
        }
    }

    private GameObject CreatePickupMessage()
    {
        GameObject msgUI = new GameObject("CandlePickupMessage");
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
        tmp.text = "Candle collected!";
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.9f, 0.5f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return msgUI;
    }

    private IEnumerator BobAndSpin()
    {
        float timer = 0f;
        while (!IsCollected)
        {
            timer += Time.deltaTime;
            float yOffset = Mathf.Sin(timer * bobSpeed) * bobHeight;
            transform.localPosition = startLocalPos + Vector3.up * yOffset;
            transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.Self);
            yield return null;
        }
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
            Debug.Log($"[CollectibleCandle] Fixed negative scale on '{name}': {s} → {transform.localScale}");
        }
    }

    // ═══════════════════════════════════════════════
    //  EQUIP / UNEQUIP (static — called by GameInventoryUI)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Creates the actual candle model in the player's right hand and doubles the player light.
    /// </summary>
    public static void Equip()
    {
        if (IsEquipped) return;

        if (remainingUseSeconds <= 0f)
            remainingUseSeconds = MaxUseSeconds;

        IsEquipped = true;
        lastUsageTickTime = Time.time;

        Debug.Log("[CollectibleCandle] Candle equipped!");

        // Find right hand bone
        Transform rightHand = FindRightHandBone();
        if (rightHand != null)
        {
            CreateHandCandle(rightHand);
        }
        else
        {
            Debug.LogWarning("[CollectibleCandle] Could not find right hand bone for candle!");
        }

        // Double the player light
        DoublePlayerLight();
    }

    /// <summary>
    /// Removes the candle from the player's hand and restores normal light.
    /// </summary>
    public static void Unequip()
    {
        if (!IsEquipped) return;
        IsEquipped = false;
        attachedToCamera = false;
        lastUsageTickTime = -1f;

        Debug.Log("[CollectibleCandle] Candle unequipped!");

        // Destroy the hand candle model
        if (equippedCandleModel != null)
        {
            Object.Destroy(equippedCandleModel);
            equippedCandleModel = null;
            equippedCandleLight = null;
        }

        // Restore player light
        RestorePlayerLight();

        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.ClearCandleOverlayText();
    }

    public static void UpdateCandleUsage()
    {
        if (InventoryManager.Instance == null || !InventoryManager.Instance.HasCandle)
        {
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.ClearCandleOverlayText();
            return;
        }

        if (remainingUseSeconds <= 0f)
        {
            ExpireCandle();
            return;
        }

        if (!IsEquipped)
        {
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.ClearCandleOverlayText();
            return;
        }

        float now = Time.time;
        if (lastUsageTickTime < 0f)
            lastUsageTickTime = now;

        float dt = Mathf.Max(0f, now - lastUsageTickTime);
        lastUsageTickTime = now;
        remainingUseSeconds -= dt;

        int display = Mathf.Max(0, Mathf.CeilToInt(remainingUseSeconds));
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.SetCandleOverlayText(display.ToString(), new Color(1f, 0.86f, 0.52f, 1f));

        if (remainingUseSeconds <= 0f)
            ExpireCandle();
    }

    private static void ExpireCandle()
    {
        remainingUseSeconds = 0f;

        if (IsEquipped)
            Unequip();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.SetHasCandle(false);

        if (GameInventoryUI.Instance != null)
        {
            GameInventoryUI.Instance.ClearCandleOverlayText();
            GameInventoryUI.Instance.RefreshFromInventory();
        }
    }

    // True when the candle is parented to the camera anchor (not a skeleton bone)
    private static bool attachedToCamera = false;

    private static Transform FindRightHandBone()
    {
        attachedToCamera = false;

        // Priority order: animated skeleton bones first, then mesh objects
        // "mixamorig:RightHand" is the actual animated bone.
        // "R_hand" / "R_Hand" are skinned mesh renderers (NOT animated bones) — avoid those.
        string[] handBoneNames = { "mixamorig:RightHand", "RightHand", "Hand_R" };

        // Search through FirstPersonArmAnimator first
        if (FirstPersonArmAnimator.Instance != null)
        {
            foreach (string boneName in handBoneNames)
            {
                Transform hand = FindDeepChild(FirstPersonArmAnimator.Instance.transform, boneName);
                if (hand != null)
                {
                    Debug.Log($"[CollectibleCandle] Found hand bone: '{boneName}' under FirstPersonArmAnimator");
                    return hand;
                }
            }
        }

        // Fallback: search from CharacterModel or player root
        GameObject player = GameObject.Find("FirstPersonPlayer");
        if (player != null)
        {
            foreach (string boneName in handBoneNames)
            {
                Transform hand = FindDeepChild(player.transform, boneName);
                if (hand != null)
                {
                    Debug.Log($"[CollectibleCandle] Found hand bone: '{boneName}' under FirstPersonPlayer");
                    return hand;
                }
            }
        }

        // Last resort: search all SkinnedMeshRenderers in scene
        SkinnedMeshRenderer[] smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
        foreach (var smr in smrs)
        {
            foreach (string boneName in handBoneNames)
            {
                Transform hand = FindDeepChild(smr.transform.root, boneName);
                if (hand != null)
                {
                    Debug.Log($"[CollectibleCandle] Found hand bone: '{boneName}' under {smr.transform.root.name}");
                    return hand;
                }
            }
        }

        // ── CAMERA FALLBACK ──────────────────────────────────────────────────
        // No skeleton bone found — attach to the first-person camera instead.
        // This is reliable for FPS games where the body model is separate from the view.
        Camera fpsCam = Camera.main;
        if (fpsCam == null)
        {
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams) { if (c.isActiveAndEnabled) { fpsCam = c; break; } }
        }
        if (fpsCam != null)
        {
            Transform anchor = fpsCam.transform.Find("CandleHandAnchor");
            if (anchor == null)
            {
                var anchorGO = new GameObject("CandleHandAnchor");
                anchorGO.transform.SetParent(fpsCam.transform, false);
                anchor = anchorGO.transform;
            }
            attachedToCamera = true;
            Debug.Log("[CollectibleCandle] No hand bone found — attaching candle to camera anchor.");
            return anchor;
        }

        Debug.LogWarning("[CollectibleCandle] Could not find any right hand bone or camera!");
        return null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        var r = parent.Find(name);
        if (r != null) return r;
        foreach (Transform c in parent)
        {
            r = FindDeepChild(c, name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// Creates the candle in the player's right hand using the ACTUAL candle mesh and material
    /// saved during collection (not primitives).
    /// Adds a CandleHandAdjuster component so you can tweak position/rotation/scale
    /// live in the Inspector during Play mode.
    /// </summary>
    private static void CreateHandCandle(Transform rightHand)
    {
        equippedCandleModel = new GameObject("EquippedCandle");
        equippedCandleModel.transform.SetParent(rightHand, false);

        if (attachedToCamera)
        {
            // Camera-space defaults: lower-right of view, within arm's reach.
            // Press P in Play mode to print current values, then paste them here.
            equippedCandleModel.transform.localPosition = new Vector3(0.22f, -0.28f, 0.45f);
            equippedCandleModel.transform.localRotation = Quaternion.Euler(10f, -15f, 5f);
            equippedCandleModel.transform.localScale = Vector3.one;
        }
        else
        {
            // Bone-space defaults (you can tweak via the adjuster)
            equippedCandleModel.transform.localPosition = new Vector3(0f, 0.06f, 0.04f);
            equippedCandleModel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            equippedCandleModel.transform.localScale = Vector3.one;
        }

        Transform meshChild = null;

        if (savedCandleMesh != null)
        {
            // ── Use the REAL candle mesh ──
            GameObject candleMeshGO = new GameObject("CandleMesh");
            candleMeshGO.transform.SetParent(equippedCandleModel.transform, false);

            candleMeshGO.transform.localScale = new Vector3(0.810f, 1.145f, 0.692f);
            candleMeshGO.transform.localPosition = new Vector3(0.038f, -0.001f, 0.013f);
            candleMeshGO.transform.localRotation = Quaternion.Euler(41.135f, 354.422f, 63.344f);

            meshChild = candleMeshGO.transform;

            MeshFilter mf = candleMeshGO.AddComponent<MeshFilter>();
            mf.sharedMesh = savedCandleMesh;

            MeshRenderer mr = candleMeshGO.AddComponent<MeshRenderer>();
            if (savedCandleMaterials != null && savedCandleMaterials.Length > 0)
            {
                mr.sharedMaterials = savedCandleMaterials;
            }
            else
            {
                Material candleMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                candleMat.SetColor("_BaseColor", new Color(0.91f, 0.91f, 0.91f, 1f));
                candleMat.SetColor("_Color", new Color(0.91f, 0.91f, 0.91f, 1f));
                mr.material = candleMat;
            }

            foreach (var col in candleMeshGO.GetComponentsInChildren<Collider>())
                col.enabled = false;

            Debug.Log($"[CollectibleCandle] Hand candle created with real mesh ({savedCandleMesh.name})");
        }
        else
        {
            Debug.LogWarning("[CollectibleCandle] No saved mesh — creating fallback primitive candle");
            CreateFallbackPrimitiveCandle(equippedCandleModel.transform);
        }

        // ── Create flame light at the top of the candle ──
        float flameY = 0.15f;

        GameObject flameLightGO = new GameObject("FlameLight");
        flameLightGO.transform.SetParent(equippedCandleModel.transform, false);
        flameLightGO.transform.localPosition = new Vector3(0f, flameY, 0f);

        equippedCandleLight = flameLightGO.AddComponent<Light>();
        equippedCandleLight.type = LightType.Point;
        equippedCandleLight.color = new Color(1f, 0.85f, 0.55f, 1f);
        equippedCandleLight.intensity = 1.2f;
        equippedCandleLight.range = 3f;
        equippedCandleLight.shadows = LightShadows.None;

        // Small emissive flame sphere
        GameObject flameSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flameSphere.name = "FlameGlow";
        flameSphere.transform.SetParent(equippedCandleModel.transform, false);
        flameSphere.transform.localPosition = new Vector3(-0.077f, 0.043f, 0.040f);
        flameSphere.transform.localScale = new Vector3(0.015f, 0.025f, 0.015f);

        Collider flameCol = flameSphere.GetComponent<Collider>();
        if (flameCol != null) Object.Destroy(flameCol);

        Renderer flameRenderer = flameSphere.GetComponent<Renderer>();
        if (flameRenderer != null)
        {
            Material flameMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            flameMat.SetColor("_BaseColor", new Color(1f, 0.75f, 0.25f, 1f));
            flameMat.EnableKeyword("_EMISSION");
            flameMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.15f, 1f) * 2.5f);
            flameRenderer.material = flameMat;
        }

        // ── Add the live adjuster so you can tweak transforms in Play mode ──
        CandleHandAdjuster adjuster = equippedCandleModel.AddComponent<CandleHandAdjuster>();
        adjuster.meshChild = meshChild;
        adjuster.flameLightTransform = flameLightGO.transform;
        adjuster.flameGlowTransform = flameSphere.transform;
    }

    /// <summary>
    /// Fallback if the real mesh wasn't captured — creates a simple cylinder candle.
    /// </summary>
    private static void CreateFallbackPrimitiveCandle(Transform parent)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CandleBody";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        body.transform.localScale = new Vector3(0.015f, 0.045f, 0.015f);

        Collider bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Object.Destroy(bodyCol);

        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            Material candleMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            candleMat.SetColor("_BaseColor", new Color(0.91f, 0.91f, 0.91f, 1f));
            candleMat.SetColor("_Color", new Color(0.91f, 0.91f, 0.91f, 1f));
            bodyRenderer.material = candleMat;
        }
    }

    private static void DoublePlayerLight()
    {
        DungeonLightingManager dlm = Object.FindFirstObjectByType<DungeonLightingManager>();
        if (dlm != null)
        {
            dlm.SetCandleEquipped(true);
        }
    }

    private static void RestorePlayerLight()
    {
        DungeonLightingManager dlm = Object.FindFirstObjectByType<DungeonLightingManager>();
        if (dlm != null)
        {
            dlm.SetCandleEquipped(false);
        }
    }
}
