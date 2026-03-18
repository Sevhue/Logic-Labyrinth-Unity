using UnityEngine;
using UnityEngine.Rendering;


/// <summary>
/// Makes the dungeon dark with limited player visibility.
/// Attach to any GameObject in a level scene (e.g. an empty "DungeonLighting" object).
/// 
/// What it does at runtime:
///   1. Sets ambient light to near-black
///   2. Enables exponential-squared fog for natural visibility falloff
///   3. Dims/disables all directional lights in the scene
///   4. Changes the camera background to solid black (no skybox)
///   5. Attaches a Point Light to the player so they can only see a small radius around them
/// </summary>
public class DungeonLightingManager : MonoBehaviour
{
    private struct TorchBaseline
    {
        public float intensity;
        public float range;
    }

    [Header("Labyrinth Preset")]
    [Tooltip("Forces a consistent horror/labyrinth visibility profile across all levels.")]
    public bool forceLabyrinthPreset = true;

    [Header("Player Light Settings")]
    [Tooltip("Use a forward-facing spotlight (horror flashlight style) instead of 360 point light.")]
    public bool useSpotlight = true;

    [Tooltip("How far the player can see (Point Light range in meters)")]
    [Range(2f, 20f)]
    public float playerLightRange = 2.8f;

    [Tooltip("Brightness of the player's light")]
    [Range(0.1f, 5f)]
    public float playerLightIntensity = 2.2f;

    [Tooltip("Color of the player's light (warm torch-like)")]
    public Color playerLightColor = new Color(1f, 0.85f, 0.6f, 1f); // Warm orange

    [Tooltip("Outer angle of the spotlight cone.")]
    [Range(20f, 120f)]
    public float playerSpotAngle = 54f;

    [Tooltip("Inner angle of the spotlight cone.")]
    [Range(10f, 100f)]
    public float playerInnerSpotAngle = 30f;

    [Tooltip("Downward pitch (degrees) so the beam lights the floor in front of the player.")]
    [Range(-20f, 25f)]
    public float playerSpotPitch = 10f;

    [Tooltip("Small helper light to keep nearby floor readable.")]
    public bool useFloorFillLight = true;

    [Range(0.1f, 3f)]
    public float floorFillRange = 2.0f;

    [Range(0.05f, 2f)]
    public float floorFillIntensity = 1.1f;

    [Header("Ambient & Fog")]
    [Tooltip("The ambient color of the dungeon (should be very dark)")]
    public Color ambientColor = new Color(0.03f, 0.03f, 0.05f, 1f); // Near black with slight blue

    [Tooltip("Enable fog for distance-based darkness")]
    public bool enableFog = true;

    [Tooltip("Fog color (should match the darkness)")]
    public Color fogColor = new Color(0.02f, 0.02f, 0.03f, 1f);

    [Tooltip("Fog density — higher = shorter visibility")]
    [Range(0.01f, 0.5f)]
    public float fogDensity = 0.1f;

    [Header("Directional Light")]
    [Tooltip("How much to reduce the directional light (0 = fully off)")]
    [Range(0f, 0.1f)]
    public float directionalLightIntensity = 0f;

    [Header("Torch Lights")]
    [Tooltip("If enabled, every non-player light is disabled unless it looks like a placed torch light.")]
    public bool forceOnlyTorchLights = true;

    [Tooltip("Name keywords used to detect placed torch lights.")]
    public string[] torchNameKeywords = { "torch", "brazi", "sconce", "flame" };

    [Tooltip("Boost multiplier for existing torch/point lights in the scene")]
    [Range(1f, 5f)]
    public float torchLightBoost = 1.0f;

    [Tooltip("Extra range added to torch lights so they punch through fog")]
    [Range(0f, 15f)]
    public float torchExtraRange = 0.3f;

    // Internal references
    private Light playerLight;
    private Light floorFillLight;
    private GameObject playerLightObj;
    private Light[] originalDirectionalLights;
    private float[] originalIntensities;
    private CameraClearFlags originalClearFlags;
    private Color originalBgColor;
    private Camera mainCam;
    private readonly System.Collections.Generic.Dictionary<int, TorchBaseline> torchBaselines =
        new System.Collections.Generic.Dictionary<int, TorchBaseline>();
    private System.Collections.IEnumerator lightRulesWatchdog;

    void Awake()
    {
        if (!forceLabyrinthPreset) return;

        // V1.3 baseline for player light.
        playerLightRange = 6f;
        playerLightIntensity = 1.2f;
        ambientColor = Color.black;       // Pure black — no ambient silhouette
        fogColor = Color.black;           // Fog fades to pure black, matching camera BG
        fogDensity = 0.1f;
        directionalLightIntensity = 0f;
        torchLightBoost = 1.0f;
        torchExtraRange = 0.3f;
    }

    void Start()
    {
        SetupCamera();
        SetupAmbientAndFog();
        DimDirectionalLights();
        ApplySceneLightRules();

        // Keep map blackout enforced even if objects/scripts enable lights later.
        lightRulesWatchdog = EnforceLightRulesLoop();
        StartCoroutine(lightRulesWatchdog);

        StartCoroutine(AttachPlayerLightDelayed());
    }

    private void SetupCamera()
    {
        // Find the main camera and change it to solid black background
        mainCam = Camera.main;
        if (mainCam != null)
        {
            originalClearFlags = mainCam.clearFlags;
            originalBgColor = mainCam.backgroundColor;

            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = Color.black;
            Debug.Log("[DungeonLighting] Camera set to solid black background.");
        }
    }

    /// <summary>
    /// Wait a couple frames for the player to be fully spawned before attaching the light.
    /// </summary>
    private System.Collections.IEnumerator AttachPlayerLightDelayed()
    {
        // Wait for player to be ready (especially important on load game)
        yield return null;
        yield return null;
        yield return null;

        AttachPlayerLight();
    }

    private void SetupAmbientAndFog()
    {
        // Set ambient light to pure black so unlit surfaces are invisible
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = 0f;

        // Disable skybox reflections
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        // Setup fog
        RenderSettings.fog = enableFog;
        if (enableFog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        Debug.Log("[DungeonLighting] Ambient and fog configured for dark dungeon.");
    }

    private void DimDirectionalLights()
    {
        // Find all lights in the scene
        Light[] allLights = FindObjectsOfType<Light>();
        var dirLights = new System.Collections.Generic.List<Light>();
        var origIntensities = new System.Collections.Generic.List<float>();

        foreach (Light light in allLights)
        {
            if (light.type == LightType.Directional)
            {
                dirLights.Add(light);
                origIntensities.Add(light.intensity);

                // Dim it way down
                light.intensity = directionalLightIntensity;
                Debug.Log($"[DungeonLighting] Dimmed '{light.gameObject.name}' from {origIntensities[origIntensities.Count - 1]:F2} to {directionalLightIntensity:F3}");
            }
        }

        originalDirectionalLights = dirLights.ToArray();
        originalIntensities = origIntensities.ToArray();
    }

    private void ApplySceneLightRules()
    {
        Light[] allLights = FindObjectsOfType<Light>(true);
        int activeTorchCount = 0;
        int disabledCount = 0;

        foreach (Light light in allLights)
        {
            if (light == null) continue;

            if (IsPlayerOwnedLight(light))
                continue;

            if (light.type == LightType.Directional)
            {
                light.enabled = true;
                light.intensity = directionalLightIntensity;
                continue;
            }

            bool isTorch = IsTorchLight(light);
            bool isGuidanceLight = IsGuidanceLight(light);

            if (forceOnlyTorchLights && !isTorch && !isGuidanceLight)
            {
                if (light.enabled || light.intensity > 0f)
                {
                    light.enabled = false;
                    light.intensity = 0f;
                    disabledCount++;
                }

                continue;
            }

            if (isTorch)
            {
                int id = light.GetInstanceID();
                TorchBaseline baseline;
                if (!torchBaselines.TryGetValue(id, out baseline))
                {
                    baseline = new TorchBaseline
                    {
                        intensity = light.intensity,
                        range = light.range,
                    };
                    torchBaselines[id] = baseline;
                }

                light.enabled = true;
                light.intensity = baseline.intensity * torchLightBoost;
                light.range = baseline.range + torchExtraRange;

                // Make sure torch lights cast shadows for occlusion and atmosphere.
                if (light.shadows == LightShadows.None)
                    light.shadows = LightShadows.Soft;

                activeTorchCount++;
            }
        }

        if (activeTorchCount > 0 || disabledCount > 0)
            Debug.Log($"[DungeonLighting] Light rules enforced. Torches active: {activeTorchCount}, non-torch disabled: {disabledCount}.");
    }

    private bool IsPlayerOwnedLight(Light light)
    {
        if (light == null) return false;

        if (light == playerLight || light == floorFillLight)
            return true;

        Transform t = light.transform;
        if (playerLightObj != null && (t == playerLightObj.transform || t.IsChildOf(playerLightObj.transform)))
            return true;

        Transform root = t.root;
        if (root != null)
        {
            if (root.CompareTag("Player"))
                return true;

            if (root.GetComponentInChildren<CharacterController>() != null)
                return true;
        }

        return false;
    }

    private bool IsTorchLight(Light light)
    {
        if (light == null) return false;

        string path = GetHierarchyPath(light.transform).ToLowerInvariant();

        if (torchNameKeywords != null)
        {
            for (int i = 0; i < torchNameKeywords.Length; i++)
            {
                string key = torchNameKeywords[i];
                if (!string.IsNullOrWhiteSpace(key) && path.Contains(key.ToLowerInvariant()))
                    return true;
            }
        }

        return false;
    }

    // Keep scripted guidance lights (tutorial/success door highlights) alive even in strict torch mode.
    private bool IsGuidanceLight(Light light)
    {
        if (light == null) return false;

        string path = GetHierarchyPath(light.transform).ToLowerInvariant();
        return path.Contains("doorhighlightlight")
            || path.Contains("door_tutorial")
            || path.Contains("door_success")
            || path.Contains("keyshinelight");
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null) return string.Empty;

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private System.Collections.IEnumerator EnforceLightRulesLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.75f);
            ApplySceneLightRules();
        }
    }

    private void OnEnable()
    {
        ApplySceneLightRules();
    }

    private void LateUpdate()
    {
        // If player light was destroyed by scene reload timing, recover it quickly.
        if (playerLight == null && playerLightObj == null)
            AttachPlayerLight();
    }

    private void AttachPlayerLight()
    {
        // Find the actual player object (the one with CharacterController that moves)
        GameObject playerObj = FindPlayerWithCharacterController();
        if (playerObj == null)
        {
            Debug.LogWarning("[DungeonLighting] Could not find player! Will retry...");
            StartCoroutine(RetryAttachLight());
            return;
        }

        // Avoid duplicates
        if (playerLightObj != null) return;

        // V1.3 behavior: point light attached directly to player.
        playerLightObj = new GameObject("PlayerDungeonLight");
        playerLightObj.transform.SetParent(playerObj.transform, false);
        playerLightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        playerLightObj.transform.localRotation = Quaternion.identity;

        // Add player light
        playerLight = playerLightObj.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = playerLightRange;
        playerLight.intensity = playerLightIntensity;
        playerLight.color = playerLightColor;
        playerLight.shadows = LightShadows.None;
        playerLight.renderMode = LightRenderMode.ForcePixel;

        Debug.Log($"[DungeonLighting] Player light attached! Range={playerLightRange}m, Intensity={playerLightIntensity}");
    }

    private void CreateFloorFillLight()
    {
        if (playerLightObj == null || floorFillLight != null) return;

        GameObject fillObj = new GameObject("PlayerFloorFillLight");
        fillObj.transform.SetParent(playerLightObj.transform, false);
        fillObj.transform.localPosition = new Vector3(0f, -0.35f, 0.15f);
        fillObj.transform.localRotation = Quaternion.identity;

        floorFillLight = fillObj.AddComponent<Light>();
        floorFillLight.type = LightType.Point;
        floorFillLight.range = floorFillRange;
        floorFillLight.intensity = floorFillIntensity;
        floorFillLight.color = playerLightColor;
        floorFillLight.shadows = LightShadows.None;
        floorFillLight.renderMode = LightRenderMode.ForcePixel;
    }

    private Camera FindViewCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].isActiveAndEnabled)
                return cams[i];
        }

        return null;
    }

    private System.Collections.IEnumerator RetryAttachLight()
    {
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.5f);
            GameObject playerObj = FindPlayerWithCharacterController();
            if (playerObj != null)
            {
                AttachPlayerLight();
                yield break;
            }
        }
        Debug.LogError("[DungeonLighting] Failed to find player after multiple retries!");
    }

    /// <summary>
    /// Finds the player GameObject that has a CharacterController (the one that actually moves).
    /// </summary>
    private GameObject FindPlayerWithCharacterController()
    {
        // Try PauseMenuController's helper first
        try
        {
            var method = typeof(PauseMenuController).GetMethod("FindPlayerWithCharacterController",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                GameObject result = method.Invoke(null, null) as GameObject;
                if (result != null) return result;
            }
        }
        catch { /* Ignore if PauseMenuController doesn't exist */ }

        // Fallback: find by CharacterController
        CharacterController[] controllers = FindObjectsOfType<CharacterController>();
        foreach (var cc in controllers)
        {
            if (cc.gameObject.CompareTag("Player"))
                return cc.gameObject;
        }

        // Last resort
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>
    /// Call this to increase the player's light radius (e.g. when picking up a torch).
    /// </summary>
    public void IncreasePlayerLightRadius(float additionalRange)
    {
        if (playerLight != null)
        {
            playerLight.range += additionalRange;
            Debug.Log($"[DungeonLighting] Player light range increased to {playerLight.range}m");
        }
    }

    /// <summary>
    /// Call this to set the player's light radius to a specific value.
    /// </summary>
    public void SetPlayerLightRadius(float newRange)
    {
        if (playerLight != null)
        {
            playerLight.range = newRange;
            Debug.Log($"[DungeonLighting] Player light range set to {newRange}m");
        }
    }

    // ═══════════════════════════════════════════════
    //  CANDLE EQUIP — doubles player light
    // ═══════════════════════════════════════════════

    private bool candleEquipped = false;
    private float preCandleRange;
    private float preCandleIntensity;

    /// <summary>
    /// Called by CollectibleCandle to toggle 2× player light when candle is equipped/unequipped.
    /// </summary>
    public void SetCandleEquipped(bool equipped)
    {
        if (playerLight == null)
        {
            Debug.LogWarning("[DungeonLighting] Player light not found for candle toggle.");
            return;
        }

        if (equipped && !candleEquipped)
        {
            candleEquipped = true;
            preCandleRange = playerLight.range;
            preCandleIntensity = playerLight.intensity;

            playerLight.range = preCandleRange * 2f;
            playerLight.intensity = preCandleIntensity * 2f;

            Debug.Log($"[DungeonLighting] Candle equipped! Light: range {preCandleRange:F1}→{playerLight.range:F1}, " +
                      $"intensity {preCandleIntensity:F1}→{playerLight.intensity:F1}");
        }
        else if (!equipped && candleEquipped)
        {
            candleEquipped = false;
            playerLight.range = preCandleRange;
            playerLight.intensity = preCandleIntensity;

            Debug.Log($"[DungeonLighting] Candle unequipped! Light restored: range {playerLight.range:F1}, intensity {playerLight.intensity:F1}");
        }
    }

    void OnDestroy()
    {
        // Restore camera
        if (mainCam != null)
        {
            mainCam.clearFlags = originalClearFlags;
            mainCam.backgroundColor = originalBgColor;
        }

        // Restore directional lights if we're leaving the scene
        if (originalDirectionalLights != null)
        {
            for (int i = 0; i < originalDirectionalLights.Length; i++)
            {
                if (originalDirectionalLights[i] != null)
                {
                    originalDirectionalLights[i].intensity = originalIntensities[i];
                }
            }
        }

        // Clean up fog
        RenderSettings.fog = false;

        // Restore ambient
        RenderSettings.ambientMode = AmbientMode.Skybox;

        if (playerLightObj != null)
        {
            Destroy(playerLightObj);
            floorFillLight = null;
        }

        if (lightRulesWatchdog != null)
        {
            StopCoroutine(lightRulesWatchdog);
            lightRulesWatchdog = null;
        }

        torchBaselines.Clear();
    }
}
