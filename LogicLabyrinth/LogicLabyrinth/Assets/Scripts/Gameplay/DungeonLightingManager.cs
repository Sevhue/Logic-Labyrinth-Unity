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
    [Header("Player Light Settings")]
    [Tooltip("How far the player can see (Point Light range in meters)")]
    [Range(2f, 20f)]
    public float playerLightRange = 6f;

    [Tooltip("Brightness of the player's light")]
    [Range(0.1f, 5f)]
    public float playerLightIntensity = 1.2f;

    [Tooltip("Color of the player's light (warm torch-like)")]
    public Color playerLightColor = new Color(1f, 0.85f, 0.6f, 1f); // Warm orange

    [Header("Ambient & Fog")]
    [Tooltip("The ambient color of the dungeon (should be very dark)")]
    public Color ambientColor = new Color(0.02f, 0.02f, 0.04f, 1f); // Near black with slight blue

    [Tooltip("Enable fog for distance-based darkness")]
    public bool enableFog = true;

    [Tooltip("Fog color (should match the darkness)")]
    public Color fogColor = new Color(0.01f, 0.01f, 0.02f, 1f);

    [Tooltip("Fog density — higher = shorter visibility")]
    [Range(0.01f, 0.5f)]
    public float fogDensity = 0.12f;

    [Header("Directional Light")]
    [Tooltip("How much to reduce the directional light (0 = fully off)")]
    [Range(0f, 0.1f)]
    public float directionalLightIntensity = 0.02f;

    // Internal references
    private Light playerLight;
    private GameObject playerLightObj;
    private Light[] originalDirectionalLights;
    private float[] originalIntensities;
    private CameraClearFlags originalClearFlags;
    private Color originalBgColor;
    private Camera mainCam;

    void Start()
    {
        SetupCamera();
        SetupAmbientAndFog();
        DimDirectionalLights();
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
        // Set ambient light to very dark
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = 0.1f;

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

        // Create the point light as a child of the player
        playerLightObj = new GameObject("PlayerDungeonLight");
        playerLightObj.transform.SetParent(playerObj.transform, false);

        // Position it slightly above the player's center (eye level)
        playerLightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        // Add the point light
        playerLight = playerLightObj.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = playerLightRange;
        playerLight.intensity = playerLightIntensity;
        playerLight.color = playerLightColor;
        playerLight.shadows = LightShadows.Soft;
        playerLight.shadowStrength = 0.8f;
        playerLight.renderMode = LightRenderMode.ForcePixel;

        Debug.Log($"[DungeonLighting] Player light attached! Range={playerLightRange}m, Intensity={playerLightIntensity}");
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
        }
    }
}
