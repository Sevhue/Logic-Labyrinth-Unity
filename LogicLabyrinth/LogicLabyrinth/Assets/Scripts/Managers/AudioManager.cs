using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip backgroundMusic;
    public AudioClip lobbyMusic;
    public AudioClip inGameMusic;
    public AudioClip gatePickupSound;
    public AudioClip puzzleCompleteSound;
    public AudioClip portalSound;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
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
            musicSource.volume = volume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = volume;
        }
    }
}