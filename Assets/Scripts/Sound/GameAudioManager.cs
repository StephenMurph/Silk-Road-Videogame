using System.Collections;
using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    public enum MusicState
    {
        None,
        GrasslandTravel,
        DesertTravel,
        GrasslandTown,
        DesertTown,
        Battle,
        GameOver
    }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;

    [Header("Music Clips")]
    [SerializeField] private AudioClip grasslandTravelMusic;
    [SerializeField] private AudioClip desertTravelMusic;
    [SerializeField] private AudioClip grasslandTownMusic;
    [SerializeField] private AudioClip desertTownMusic;
    [SerializeField] private AudioClip BattleMusic;
    [SerializeField] private AudioClip gameOverMusic;

    [Header("Settings")]
    [SerializeField] private float musicVolume = 0.7f;
    [SerializeField] private float crossfadeDuration = 1.5f;
    
    [Header("SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip playerHopLandSfx;

    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private Coroutine crossfadeRoutine;
    public MusicState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!musicSourceA) musicSourceA = gameObject.AddComponent<AudioSource>();
        if (!musicSourceB) musicSourceB = gameObject.AddComponent<AudioSource>();
        
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        SetupMusicSource(musicSourceA);
        SetupMusicSource(musicSourceB);

        activeSource = musicSourceA;
        inactiveSource = musicSourceB;
    }

    private void SetupMusicSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    public void PlayMusic(MusicState state, bool instant = false)
    {
        if (state == CurrentState)
            return;

        AudioClip clip = GetClipForState(state);
        CurrentState = state;

        if (clip == null)
        {
            StopMusic(instant);
            return;
        }

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        if (instant)
        {
            activeSource.Stop();
            activeSource.clip = clip;
            activeSource.volume = musicVolume;
            activeSource.Play();

            inactiveSource.Stop();
            inactiveSource.volume = 0f;
            return;
        }

        crossfadeRoutine = StartCoroutine(CrossfadeToClip(clip));
    }

    public void StopMusic(bool instant = false)
    {
        CurrentState = MusicState.None;

        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        if (instant)
        {
            activeSource.Stop();
            inactiveSource.Stop();
            activeSource.volume = 0f;
            inactiveSource.volume = 0f;
            return;
        }

        crossfadeRoutine = StartCoroutine(FadeOutAll());
    }

    private IEnumerator CrossfadeToClip(AudioClip clip)
    {
        inactiveSource.Stop();
        inactiveSource.clip = clip;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        float startActive = activeSource.volume;
        float t = 0f;

        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / crossfadeDuration);

            activeSource.volume = Mathf.Lerp(startActive, 0f, u);
            inactiveSource.volume = Mathf.Lerp(0f, musicVolume, u);

            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        inactiveSource.volume = musicVolume;

        SwapSources();
        crossfadeRoutine = null;
    }

    private IEnumerator FadeOutAll()
    {
        float startA = musicSourceA.volume;
        float startB = musicSourceB.volume;
        float t = 0f;

        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / crossfadeDuration);

            musicSourceA.volume = Mathf.Lerp(startA, 0f, u);
            musicSourceB.volume = Mathf.Lerp(startB, 0f, u);

            yield return null;
        }

        musicSourceA.Stop();
        musicSourceB.Stop();
        musicSourceA.volume = 0f;
        musicSourceB.volume = 0f;

        crossfadeRoutine = null;
    }

    private void SwapSources()
    {
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
    }

    private AudioClip GetClipForState(MusicState state)
    {
        switch (state)
        {
            case MusicState.GrasslandTravel: return grasslandTravelMusic;
            case MusicState.DesertTravel: return desertTravelMusic;
            case MusicState.GrasslandTown: return grasslandTownMusic;
            case MusicState.DesertTown: return desertTownMusic;
            case MusicState.Battle: return BattleMusic;
            case MusicState.GameOver: return gameOverMusic;
            default: return null;
        }
    }
    
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayHopLand()
    {
        PlaySfx(playerHopLandSfx, 1f);
    }
}