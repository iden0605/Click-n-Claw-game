using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists across scenes.
/// Handles background music and one-shot SFX.
///
/// ── Scene setup ──
///   1. Add AudioManager to a GameObject in the MainMenu scene.
///   2. Add TWO AudioSource components on the same GameObject.
///   3. Assign the first AudioSource to Music Source, the second to SFX Source.
///   4. On the Music AudioSource: check Loop, set Volume ~0.5, uncheck Play On Awake.
///   5. On the SFX AudioSource: uncheck Loop, uncheck Play On Awake.
///   6. Drag all clips from Assets/Audio into the matching fields below.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    public AudioClip bgmLobby;
    public AudioClip bgmMain;

    [Header("SFX")]
    public AudioClip sfxEnemyHit;
    public AudioClip sfxPlayerHpLoss;  // optional — leave empty until you have the clip
    public AudioClip sfxWaveStart;     // optional — leave empty until you have the clip
    public AudioClip sfxEvolve;        // optional — leave empty until you have the clip

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private const string PrefMusic = "vol_music";
    private const string PrefSFX   = "vol_sfx";

    public float MusicVolume { get; private set; } = 1f;
    public float SFXVolume   { get; private set; } = 1f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Apply saved volumes after AudioSources are ready
        EnsureSources();
        MusicVolume = PlayerPrefs.GetFloat(PrefMusic, 1f);
        SFXVolume   = PlayerPrefs.GetFloat(PrefSFX,   1f);
        if (musicSource != null) musicSource.volume = MusicVolume;
        if (sfxSource   != null) sfxSource.volume   = SFXVolume;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Switch music automatically when a scene loads.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
            PlayMusic(bgmLobby);
        else if (scene.name == "Main")
            PlayMusic(bgmMain);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        EnsureSources();
        if (musicSource == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        EnsureSources();
        if (sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PauseMusic()  { EnsureSources(); musicSource?.Pause(); }
    public void ResumeMusic() { EnsureSources(); musicSource?.UnPause(); }

    /// <summary>
    /// Re-acquires AudioSource references if the originals were destroyed
    /// (can happen when the scene that owned them unloads before DontDestroyOnLoad kicks in).
    /// </summary>
    void EnsureSources()
    {
        if (musicSource == null || sfxSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length >= 1 && musicSource == null) musicSource = sources[0];
            if (sources.Length >= 2 && sfxSource   == null) sfxSource   = sources[1];
        }
    }

    public void SetMusicVolume(float v)
    {
        MusicVolume = Mathf.Clamp01(v);
        EnsureSources();
        if (musicSource != null) musicSource.volume = MusicVolume;
        PlayerPrefs.SetFloat(PrefMusic, MusicVolume);
    }

    public void SetSFXVolume(float v)
    {
        SFXVolume = Mathf.Clamp01(v);
        EnsureSources();
        if (sfxSource != null) sfxSource.volume = SFXVolume;
        PlayerPrefs.SetFloat(PrefSFX, SFXVolume);
    }
}
