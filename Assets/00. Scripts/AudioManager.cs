using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    #region Inspector Fields
    [Header("Audio Libraries")]
    [SerializeField] private AudioLibrary bgmLibrary;
    [SerializeField] private AudioLibrary sfxLibrary;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterMixerParam = "MasterVolume";
    [SerializeField] private string bgmMixerParam = "BGMVolume";
    [SerializeField] private string sfxMixerParam = "SFXVolume";
    [Space]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("볼륨 초기값 (0 ~ 1)")]
    [Range(0f, 1f)][SerializeField] private float defaultMasterVolume = 1.0f;
    [Range(0f, 1f)][SerializeField] private float defaultBGMVolume = 0.7f;
    [Range(0f, 1f)][SerializeField] private float defaultSFXVolume = 1.0f;

    [Header("SFX 오브젝트 풀")]
    [SerializeField] private int sfxPoolSize = 10;
    #endregion

    #region Private Fields
    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    private bool _isMasterMuted;
    private bool _isBGMMuted;
    private bool _isSFXMuted;

    private List<AudioSource> _sfxPool = new List<AudioSource>();
    private Coroutine _bgmFadeCoroutine;

    // PlayerPrefs 키
    private const string KEY_MASTER_VOLUME = "MasterVolume";
    private const string KEY_BGM_VOLUME = "BGMVolume";
    private const string KEY_SFX_VOLUME = "SFXVolume";
    private const string KEY_MASTER_MUTE = "MasterMute";
    private const string KEY_BGM_MUTE = "BGMMute";
    private const string KEY_SFX_MUTE = "SFXMute";
    #endregion

    #region Properties

    // ── Master ──────────────────────────────────────────────────────────

    /// <summary>마스터 볼륨 (0~1). BGM·SFX AudioGroup의 상위에서 곱해집니다.</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Mathf.Clamp01(value);
            ApplyMixerVolume(masterMixerParam, _masterVolume, _isMasterMuted);
            SaveSettings();
        }
    }

    public bool IsMasterMuted
    {
        get => _isMasterMuted;
        set
        {
            _isMasterMuted = value;
            ApplyMixerVolume(masterMixerParam, _masterVolume, _isMasterMuted);
            SaveSettings();
        }
    }

    // ── BGM ─────────────────────────────────────────────────────────────

    /// <summary>BGM 볼륨 (0~1).</summary>
    public float BGMVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            ApplyMixerVolume(bgmMixerParam, _bgmVolume, _isBGMMuted);
            SaveSettings();
        }
    }

    public bool IsBGMMuted
    {
        get => _isBGMMuted;
        set
        {
            _isBGMMuted = value;
            ApplyMixerVolume(bgmMixerParam, _bgmVolume, _isBGMMuted);
            SaveSettings();
        }
    }

    // ── SFX ─────────────────────────────────────────────────────────────

    /// <summary>SFX 볼륨 (0~1).</summary>
    public float SFXVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp01(value);
            ApplyMixerVolume(sfxMixerParam, _sfxVolume, _isSFXMuted);
            SaveSettings();
        }
    }

    public bool IsSFXMuted
    {
        get => _isSFXMuted;
        set
        {
            _isSFXMuted = value;
            ApplyMixerVolume(sfxMixerParam, _sfxVolume, _isSFXMuted);
            SaveSettings();
        }
    }

    // ── Defaults (설정 UI 초기화 버튼용) ───────────────────────────────────
    /// <summary>인스펙터에 설정된 마스터 볼륨 기본값</summary>
    public float DefaultMasterVolume => defaultMasterVolume;

    /// <summary>인스펙터에 설정된 BGM 볼륨 기본값</summary>
    public float DefaultBGMVolume => defaultBGMVolume;

    /// <summary>인스펙터에 설정된 SFX 볼륨 기본값</summary>
    public float DefaultSFXVolume => defaultSFXVolume;
    #endregion

    #region Initialization
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    private void Initialize()
    {
        if (audioMixer == null)
            Debug.LogError("[AudioManager] AudioMixer가 할당되지 않았습니다. Inspector에서 연결해 주세요.");

        // BGM AudioSource 자동 생성
        if (bgmSource == null)
        {
            GameObject bgmGo = new GameObject("BGM_Source");
            bgmGo.transform.SetParent(transform);
            bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;
        }

        // SFX AudioSource 자동 생성
        if (sfxSource == null)
        {
            GameObject sfxGo = new GameObject("SFX_Source");
            sfxGo.transform.SetParent(transform);
            sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
        }

        // SFX 오브젝트 풀 생성
        for (int i = 0; i < sfxPoolSize; i++)
            CreatePooledSFXSource();

        LoadSettings();
    }

    private AudioSource CreatePooledSFXSource()
    {
        GameObject go = new GameObject($"SFX_Pool_{_sfxPool.Count}");
        go.transform.SetParent(transform);
        AudioSource src = go.AddComponent<AudioSource>();
        src.loop = false;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = sfxMixerGroup;
        _sfxPool.Add(src);
        return src;
    }
    #endregion

    #region BGM Methods
    /// <summary>BGM을 즉시 재생합니다.</summary>
    public void PlayBGM(string key)
    {
        AudioClip clip = bgmLibrary.Get(key)?.clip;
        PlayBGM(clip);
    }
    public void PlayBGM(string key, bool loop)
    {
        AudioClip clip = bgmLibrary.Get(key)?.clip;
        PlayBGM(clip, loop);
    }

    private void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null) { Debug.LogWarning("[AudioManager] PlayBGM: clip이 null입니다."); return; }

        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    /// <summary>페이드 크로스와 함께 BGM을 전환합니다.</summary>
    public void PlayBGMWithFade(string key, float fadeDuration = 1.0f, bool loop = true, bool forceRestart = false)
    {
        AudioClip clip = bgmLibrary.Get(key)?.clip;
        PlayBGMWithFade(clip, fadeDuration, loop, forceRestart);
    }

    private void PlayBGMWithFade(AudioClip clip, float fadeDuration = 1.0f, bool loop = true, bool forceRestart = false)
    {
        if (!forceRestart && CurrentBGM == clip) return;

        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = StartCoroutine(FadeBGM(clip, fadeDuration, loop));
    }

    /// <summary>BGM을 정지합니다.</summary>
    public void StopBGM(float fadeDuration = 0f)
    {
        if (fadeDuration > 0f)
        {
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeOutBGM(fadeDuration));
        }
        else bgmSource.Stop();
    }

    /// <summary>BGM을 일시정지/재개합니다.</summary>
    public void PauseBGM(bool pause)
    {
        if (pause) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    private IEnumerator FadeBGM(AudioClip newClip, float duration, bool loop)
    {
        float half = duration * 0.5f;

        yield return StartCoroutine(TweenMixerVolume(bgmMixerParam, _bgmVolume, 0f, half));

        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.Play();

        float target = _isBGMMuted ? 0f : _bgmVolume;
        yield return StartCoroutine(TweenMixerVolume(bgmMixerParam, 0f, target, half));
    }

    private IEnumerator FadeOutBGM(float duration)
    {
        yield return StartCoroutine(TweenMixerVolume(bgmMixerParam, _bgmVolume, 0f, duration));
        bgmSource.Stop();
        // 다음 재생을 위해 볼륨 복구
        ApplyMixerVolume(bgmMixerParam, _bgmVolume, _isBGMMuted);
    }

    /// <summary>AudioMixer 파라미터를 선형 보간합니다.</summary>
    private IEnumerator TweenMixerVolume(string param, float fromLinear, float toLinear, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float linear = Mathf.Lerp(fromLinear, toLinear, timer / duration);
            SetMixerDB(param, linear);
            yield return null;
        }
        SetMixerDB(param, toLinear);
    }
    #endregion

    #region SFX Methods
    /// <summary>SFX를 한 번 재생합니다.</summary>
    public void PlaySFX(string key)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFX(clip);
    }

    public void PlaySFX(string key, float volumeScale)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFX(clip, volumeScale);
    }

    private void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null) { Debug.LogWarning("[AudioManager] PlaySFX: clip이 null입니다."); return; }
        if (_isSFXMuted || _isMasterMuted) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    public void PlaySFXPooled(string key)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFXPooled(clip);
    }

    public void PlaySFXPooled(string key, float volumeScale)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFXPooled(clip, volumeScale);
    }

    /// <summary>SFX를 풀에서 꺼내 재생합니다. (동시 다발 재생에 적합)</summary>
    private void PlaySFXPooled(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null) { Debug.LogWarning("[AudioManager] PlaySFXPooled: clip이 null입니다."); return; }
        if (_isSFXMuted || _isMasterMuted) return;

        AudioSource src = GetAvailablePoolSource();
        src.clip = clip;
        src.volume = volumeScale;
        src.Play();
    }

    public void PlaySFXAtPoint(string key, Vector3 position)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFXAtPoint(clip, position);
    }

    public void PlaySFXAtPoint(string key, Vector3 position, float volumeScale)
    {
        AudioClip clip = sfxLibrary.Get(key)?.clip;
        PlaySFXAtPoint(clip, position, volumeScale);
    }

    /// <summary>3D 위치에서 SFX를 재생합니다.</summary>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1.0f)
    {
        if (clip == null || _isSFXMuted || _isMasterMuted) return;
        AudioSource.PlayClipAtPoint(clip, position, volumeScale);
    }

    private AudioSource GetAvailablePoolSource()
    {
        foreach (var src in _sfxPool)
            if (!src.isPlaying) return src;

        Debug.LogWarning("[AudioManager] SFX 풀 초과 — 새 AudioSource를 생성합니다.");
        return CreatePooledSFXSource();
    }
    #endregion

    #region Volume Apply (AudioMixer)
    /// <summary>볼륨(0~1)과 뮤트 상태를 반영해 Mixer 파라미터를 설정합니다.</summary>
    private void ApplyMixerVolume(string param, float linearVolume, bool muted)
    {
        SetMixerDB(param, muted ? 0f : linearVolume);
    }

    /// <summary>선형 볼륨(0~1)을 dB로 변환해 AudioMixer에 적용합니다.</summary>
    private void SetMixerDB(string param, float linearVolume)
    {
        if (audioMixer == null) return;
        float dB = linearVolume > 0.0001f ? Mathf.Log10(linearVolume) * 20f : -80f;
        audioMixer.SetFloat(param, dB);
    }

    private void ApplyAllVolumes()
    {
        ApplyMixerVolume(masterMixerParam, _masterVolume, _isMasterMuted);
        ApplyMixerVolume(bgmMixerParam, _bgmVolume, _isBGMMuted);
        ApplyMixerVolume(sfxMixerParam, _sfxVolume, _isSFXMuted);
    }
    #endregion

    #region Settings Persistence
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, _masterVolume);
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, _bgmVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, _sfxVolume);
        PlayerPrefs.SetInt(KEY_MASTER_MUTE, _isMasterMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_BGM_MUTE, _isBGMMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SFX_MUTE, _isSFXMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        _masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, defaultMasterVolume);
        _bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, defaultBGMVolume);
        _sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, defaultSFXVolume);
        _isMasterMuted = PlayerPrefs.GetInt(KEY_MASTER_MUTE, 0) == 1;
        _isBGMMuted = PlayerPrefs.GetInt(KEY_BGM_MUTE, 0) == 1;
        _isSFXMuted = PlayerPrefs.GetInt(KEY_SFX_MUTE, 0) == 1;

        ApplyAllVolumes();
    }
    #endregion

    #region Utility
    /// <summary>현재 재생 중인 BGM 클립을 반환합니다.</summary>
    public AudioClip CurrentBGM => bgmSource.clip;

    /// <summary>BGM 재생 여부를 반환합니다.</summary>
    public bool IsBGMPlaying => bgmSource.isPlaying;

    /// <summary>Master·BGM·SFX 볼륨을 모두 기본값으로 초기화합니다.</summary>
    public void ResetToDefault()
    {
        _masterVolume = defaultMasterVolume;
        _bgmVolume = defaultBGMVolume;
        _sfxVolume = defaultSFXVolume;
        _isMasterMuted = false;
        _isBGMMuted = false;
        _isSFXMuted = false;
        ApplyAllVolumes();
        SaveSettings();
    }
    #endregion
}