using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    private const string MusicVolumePrefKey = "LL_MusicVolume";
    private const string SfxVolumePrefKey = "LL_SFXVolume";

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips - Music")]
    public AudioClip backgroundMusic;
    public AudioClip lobbyMusic;
    public AudioClip inGameMusic;

    [Header("Audio Clips - Gameplay SFX")]
    public AudioClip gatePickupSound;
    public AudioClip puzzleCompleteSound;
    public AudioClip portalSound;
    public AudioClip correctAnswerSound;
    public AudioClip unlockDoorSound;
    public AudioClip damageSound;
    public AudioClip drinkSound;

    [Header("Audio Clips - Player SFX")]
    public AudioClip walkSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public AudioClip clickSound;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEditorDirectPlayClipFallbacks();
            EnsureAudioSources();
            LoadSavedVolumes();
            EnsureAudibleDefaultVolumes();
            EnsureAudioListenerExists();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateMusicForScene(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAudioListenerExists();
        UpdateMusicForScene(scene);
    }

    private void UpdateMusicForScene(Scene scene)
    {
        bool isInGameScene = scene.name.StartsWith("Level");
        AudioClip desiredClip = isInGameScene ? inGameMusic : lobbyMusic;

        // Backward-compatible fallback if clips are not assigned yet in Inspector.
        if (desiredClip == null)
            desiredClip = backgroundMusic;

        PlayMusicClip(desiredClip);
    }

    private void PlayMusicClip(AudioClip clip)
    {
        if (musicSource == null || clip == null)
            return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    private void EnsureAudioSources()
    {
        // Auto-wire sources if they were not assigned in Inspector.
        if (musicSource == null || sfxSource == null)
        {
            AudioSource[] existingSources = GetComponents<AudioSource>();
            if (musicSource == null && existingSources.Length > 0)
                musicSource = existingSources[0];
            if (sfxSource == null && existingSources.Length > 1)
                sfxSource = existingSources[1];
        }

        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    private void EnsureAudioListenerExists()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners != null && listeners.Length > 0)
            return;

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras != null && cameras.Length > 0)
                targetCamera = cameras[0];
        }

        if (targetCamera != null)
        {
            AudioListener listener = targetCamera.GetComponent<AudioListener>();
            if (listener == null)
                listener = targetCamera.gameObject.AddComponent<AudioListener>();

            listener.enabled = true;
            Debug.Log($"[AudioManager] Added fallback AudioListener to camera '{targetCamera.name}'.");
        }
    }

    private void EnsureAudibleDefaultVolumes()
    {
        if (musicSource != null && musicSource.volume <= 0.001f && !PlayerPrefs.HasKey(MusicVolumePrefKey))
            musicSource.volume = 1f;

        if (sfxSource != null && sfxSource.volume <= 0.001f && !PlayerPrefs.HasKey(SfxVolumePrefKey))
            sfxSource.volume = 1f;
    }

    private void LoadSavedVolumes()
    {
        if (musicSource != null && PlayerPrefs.HasKey(MusicVolumePrefKey))
            musicSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, 1f));

        if (sfxSource != null && PlayerPrefs.HasKey(SfxVolumePrefKey))
            sfxSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, 1f));
    }

    private void EnsureEditorDirectPlayClipFallbacks()
    {
#if UNITY_EDITOR
        // When pressing Play directly in a level scene, Main scene references may not exist.
        // Auto-find clips by name in the editor so audio remains testable from any scene.
        if (lobbyMusic == null)
            lobbyMusic = FindAudioClipByName("Lobby");
        if (inGameMusic == null)
            inGameMusic = FindAudioClipByName("InGame");

        if (walkSound == null)
            walkSound = FindAudioClipByName("Walk");
        if (runSound == null)
            runSound = FindAudioClipByName("Running");
        if (jumpSound == null)
            jumpSound = FindAudioClipByName("Jumping");
        if (clickSound == null)
            clickSound = FindAudioClipByName("Click");

        if (unlockDoorSound == null)
            unlockDoorSound = FindAudioClipByName("UnlockDoor");
        if (correctAnswerSound == null)
            correctAnswerSound = FindAudioClipByName("CorrectAnswer");
        if (gatePickupSound == null)
            gatePickupSound = FindAudioClipByName("grab") ?? FindAudioClipByName("Grab");
        if (damageSound == null)
            damageSound = FindAudioClipByName("Damage");
        if (drinkSound == null)
            drinkSound = FindAudioClipByName("Drink");
#endif
    }

#if UNITY_EDITOR
    private static AudioClip FindAudioClipByName(string clipBaseName)
    {
        if (string.IsNullOrEmpty(clipBaseName))
            return null;

        string[] guids = AssetDatabase.FindAssets($"{clipBaseName} t:AudioClip", new[] { "Assets" });
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                return clip;
        }

        return null;
    }
#endif

    public void PlayBackgroundMusic()
    {
        UpdateMusicForScene(SceneManager.GetActiveScene());
    }

    public void PlayGatePickupSound()
    {
        PlaySFX(gatePickupSound);
    }

    public void PlayPuzzleCompleteSound()
    {
        PlaySFX(puzzleCompleteSound);
    }

    public void PlayPortalSound()
    {
        PlaySFX(portalSound);
    }

    public void PlayCorrectAnswerSound()
    {
        PlaySFX(correctAnswerSound);
    }

    public void PlayUnlockDoorSound()
    {
        PlaySFX(unlockDoorSound);
    }

    public void PlayDamageSound()
    {
        PlaySFX(damageSound);
    }

    public void PlayDrinkSound()
    {
        PlaySFX(drinkSound);
    }

    public void PlayWalkSound()
    {
        PlaySFX(walkSound);
    }

    public void PlayRunSound()
    {
        PlaySFX(runSound);
    }

    public void PlayJumpSound()
    {
        PlaySFX(jumpSound);
    }

    public void PlayClickSound()
    {
        PlaySFX(clickSound);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            musicSource.volume = clampedVolume;
            PlayerPrefs.SetFloat(MusicVolumePrefKey, clampedVolume);
            PlayerPrefs.Save();
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            sfxSource.volume = clampedVolume;
            PlayerPrefs.SetFloat(SfxVolumePrefKey, clampedVolume);
            PlayerPrefs.Save();
        }
    }

    public float GetMusicVolume()
    {
        return musicSource != null ? musicSource.volume : 0.5f;
    }

    public float GetSFXVolume()
    {
        return sfxSource != null ? sfxSource.volume : 0.5f;
    }
}