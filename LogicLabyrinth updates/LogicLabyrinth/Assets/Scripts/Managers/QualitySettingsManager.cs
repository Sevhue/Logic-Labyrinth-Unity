using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages game quality settings (Low, Medium, High, Ultra).
/// Swaps URP pipeline assets and adjusts QualitySettings at runtime.
/// Persists the player's choice via PlayerPrefs.
/// </summary>
public class QualitySettingsManager : MonoBehaviour
{
    public static QualitySettingsManager Instance { get; private set; }

    public enum QualityPreset { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>Currently active quality preset.</summary>
    public QualityPreset CurrentPreset { get; private set; } = QualityPreset.Medium;

    private const string PREFS_KEY = "GameQualityPreset";

    // Cached URP pipeline assets (loaded from Resources/QualityPipelines/)
    private RenderPipelineAsset lowPipeline;
    private RenderPipelineAsset mediumPipeline;
    private RenderPipelineAsset highPipeline;
    private RenderPipelineAsset ultraPipeline;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPipelineAssets();
        LoadSavedPreset();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Loads the URP pipeline assets from Resources.
    /// </summary>
    private void LoadPipelineAssets()
    {
        lowPipeline    = Resources.Load<RenderPipelineAsset>("QualityPipelines/Low_PipelineAsset");
        mediumPipeline = Resources.Load<RenderPipelineAsset>("QualityPipelines/Medium_PipelineAsset");
        highPipeline   = Resources.Load<RenderPipelineAsset>("QualityPipelines/High_PipelineAsset");
        ultraPipeline  = Resources.Load<RenderPipelineAsset>("QualityPipelines/Ultra_PipelineAsset");

        if (lowPipeline == null)    Debug.LogWarning("[QualitySettings] Low pipeline asset not found!");
        if (mediumPipeline == null) Debug.LogWarning("[QualitySettings] Medium pipeline asset not found!");
        if (highPipeline == null)   Debug.LogWarning("[QualitySettings] High pipeline asset not found!");
        if (ultraPipeline == null)  Debug.LogWarning("[QualitySettings] Ultra pipeline asset not found!");

        Debug.Log("[QualitySettings] Pipeline assets loaded.");
    }

    /// <summary>
    /// Loads the saved quality preset from PlayerPrefs and applies it.
    /// Defaults to Medium if no saved preference exists.
    /// </summary>
    private void LoadSavedPreset()
    {
        int saved = PlayerPrefs.GetInt(PREFS_KEY, (int)QualityPreset.Medium);
        // Clamp to valid range
        saved = Mathf.Clamp(saved, 0, 3);
        CurrentPreset = (QualityPreset)saved;
        ApplyQualityPreset(CurrentPreset);
        Debug.Log($"[QualitySettings] Loaded saved preset: {CurrentPreset}");
    }

    /// <summary>
    /// Sets the quality level and saves the preference.
    /// </summary>
    public void SetQuality(QualityPreset preset)
    {
        CurrentPreset = preset;
        PlayerPrefs.SetInt(PREFS_KEY, (int)preset);
        PlayerPrefs.Save();
        ApplyQualityPreset(preset);
        Debug.Log($"[QualitySettings] Quality set to: {preset}");
    }

    // Shorthand methods for button wiring
    public void SetLow()    => SetQuality(QualityPreset.Low);
    public void SetMedium() => SetQuality(QualityPreset.Medium);
    public void SetHigh()   => SetQuality(QualityPreset.High);
    public void SetUltra()  => SetQuality(QualityPreset.Ultra);

    /// <summary>
    /// Applies the full suite of quality settings for the given preset.
    /// Carefully tuned so Ultra looks great but won't destroy lower-end PCs.
    /// </summary>
    private void ApplyQualityPreset(QualityPreset preset)
    {
        switch (preset)
        {
            case QualityPreset.Low:
                ApplyLow();
                break;
            case QualityPreset.Medium:
                ApplyMedium();
                break;
            case QualityPreset.High:
                ApplyHigh();
                break;
            case QualityPreset.Ultra:
                ApplyUltra();
                break;
        }
    }

    // ==============================
    //  LOW  — battery saver / weak hardware
    // ==============================
    private void ApplyLow()
    {
        // URP pipeline
        SwapPipeline(lowPipeline);

        // Shadows
        QualitySettings.shadows           = ShadowQuality.Disable;
        QualitySettings.shadowResolution  = ShadowResolution.Low;
        QualitySettings.shadowDistance     = 20f;
        QualitySettings.shadowCascades    = 1;

        // Textures & LOD
        QualitySettings.globalTextureMipmapLimit = 1; // Half-resolution textures
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.Disable;
        QualitySettings.lodBias                  = 0.5f;

        // Effects
        QualitySettings.softParticles           = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.billboardsFaceCameraPosition = false;
        QualitySettings.antiAliasing             = 0; // None

        // Performance
        QualitySettings.vSyncCount            = 0;
        QualitySettings.particleRaycastBudget = 64;
        QualitySettings.asyncUploadTimeSlice  = 1;
        QualitySettings.asyncUploadBufferSize = 4;
        QualitySettings.skinWeights           = SkinWeights.TwoBones;
        QualitySettings.streamingMipmapsActive = true;
        QualitySettings.streamingMipmapsMemoryBudget = 256;

        Application.targetFrameRate = 60;
    }

    // ==============================
    //  MEDIUM  — balanced (default)
    // ==============================
    private void ApplyMedium()
    {
        SwapPipeline(mediumPipeline);

        QualitySettings.shadows           = ShadowQuality.HardOnly;
        QualitySettings.shadowResolution  = ShadowResolution.Medium;
        QualitySettings.shadowDistance     = 40f;
        QualitySettings.shadowCascades    = 2;

        QualitySettings.globalTextureMipmapLimit = 0; // Full resolution
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.Enable;
        QualitySettings.lodBias                  = 1.0f;

        QualitySettings.softParticles           = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.antiAliasing             = 0;

        QualitySettings.vSyncCount            = 1;
        QualitySettings.particleRaycastBudget = 256;
        QualitySettings.asyncUploadTimeSlice  = 2;
        QualitySettings.asyncUploadBufferSize = 16;
        QualitySettings.skinWeights           = SkinWeights.TwoBones;
        QualitySettings.streamingMipmapsActive = false;

        Application.targetFrameRate = -1; // Unlimited (VSync controls it)
    }

    // ==============================
    //  HIGH  — pretty and smooth
    // ==============================
    private void ApplyHigh()
    {
        SwapPipeline(highPipeline);

        QualitySettings.shadows           = ShadowQuality.All; // Soft shadows
        QualitySettings.shadowResolution  = ShadowResolution.High;
        QualitySettings.shadowDistance     = 70f;
        QualitySettings.shadowCascades    = 2;

        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.ForceEnable;
        QualitySettings.lodBias                  = 1.5f;

        QualitySettings.softParticles           = true;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.antiAliasing             = 2; // 2x MSAA

        QualitySettings.vSyncCount            = 1;
        QualitySettings.particleRaycastBudget = 512;
        QualitySettings.asyncUploadTimeSlice  = 4;
        QualitySettings.asyncUploadBufferSize = 32;
        QualitySettings.skinWeights           = SkinWeights.FourBones;
        QualitySettings.streamingMipmapsActive = false;

        Application.targetFrameRate = -1;
    }

    // ==============================
    //  ULTRA  — best visuals (capped to stay safe)
    // ==============================
    private void ApplyUltra()
    {
        SwapPipeline(ultraPipeline);

        QualitySettings.shadows           = ShadowQuality.All;
        QualitySettings.shadowResolution  = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance     = 120f;    // Generous but capped — not 500+
        QualitySettings.shadowCascades    = 4;

        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.ForceEnable;
        QualitySettings.lodBias                  = 2.0f;

        QualitySettings.softParticles           = true;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.antiAliasing             = 4; // 4x MSAA

        QualitySettings.vSyncCount            = 1;
        QualitySettings.particleRaycastBudget = 1024;
        QualitySettings.asyncUploadTimeSlice  = 4;
        QualitySettings.asyncUploadBufferSize = 64;
        QualitySettings.skinWeights           = SkinWeights.FourBones;
        QualitySettings.streamingMipmapsActive = false;

        // Cap at 120 FPS even on Ultra to prevent GPU from going wild
        Application.targetFrameRate = 120;
    }

    /// <summary>
    /// Swaps the active render pipeline asset.
    /// </summary>
    private void SwapPipeline(RenderPipelineAsset pipelineAsset)
    {
        if (pipelineAsset == null)
        {
            Debug.LogWarning("[QualitySettings] Pipeline asset is null — skipping swap.");
            return;
        }

        // Set the per-quality-level render pipeline override
        QualitySettings.renderPipeline = pipelineAsset;
        Debug.Log($"[QualitySettings] Render pipeline set to: {pipelineAsset.name}");
    }
}
