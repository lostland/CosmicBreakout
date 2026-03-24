using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ── SFX 열거형 ────────────────────────────────────────────────────
public enum SFXType
{
    BallLaunch, BallPaddleHit, BallWallHit,
    BrickNormalHit, BrickArmorHit, BrickIceHit, BrickElectricHit,
    BrickDestroy, BrickExplode, BossDestroy,
    CoinCollect, CoinBurst, CoinShower,
    ItemPickup, MultiBall, Laser, Lightning, BlackHole, Bombardment,
    ShieldBreak, LifeLost, LevelClear, GameOver,
}

/// <summary>
/// BGM(스테이지별)과 SFX(풀 기반)를 관리하는 오디오 시스템.
/// AudioSource 컴포넌트를 동적 풀로 운용해 동시 재생에 대응한다.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("BGM")]
    [SerializeField] AudioSource _bgmSource;
    [SerializeField] AudioClip[] _stageBgm;     // 스테이지 0~4 BGM
    [SerializeField] AudioClip   _mainMenuBgm;
    [SerializeField] float       _bgmFadeDur = 1.5f;

    [Header("SFX Pool")]
    [SerializeField] int         _sfxPoolSize = 20;
    [SerializeField] AudioClip[] _sfxClips;    // SFXType 열거형 순서와 일치

    [Header("Volume")]
    [Range(0,1)] [SerializeField] float _musicVolume = 0.7f;
    [Range(0,1)] [SerializeField] float _sfxVolume   = 1.0f;

    private Queue<AudioSource>        _sfxPool    = new Queue<AudioSource>();
    private Dictionary<SFXType, AudioClip> _sfxMap = new Dictionary<SFXType, AudioClip>();
    private int _currentStage = -1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSFXMap();
        PreWarmPool();
        ApplyVolumeSettings();
    }

    void Start()
    {
        // 저장된 볼륨 설정 로드
        var data = SaveManager.Instance?.Data;
        if (data != null)
        {
            _musicVolume = data.MusicVolume;
            _sfxVolume   = data.SfxVolume;
            ApplyVolumeSettings();
        }
    }

    // ═════════════════════════════════════════════════════════════
    // BGM
    // ═════════════════════════════════════════════════════════════

    public void PlayStageBGM(int stageIndex)
    {
        if (stageIndex == _currentStage) return;
        _currentStage = stageIndex;

        AudioClip clip = stageIndex >= 0 && stageIndex < _stageBgm.Length
                         ? _stageBgm[stageIndex]
                         : _mainMenuBgm;
        StartCoroutine(CrossfadeBGM(clip));
    }

    public void PlayMainMenuBGM()
    {
        _currentStage = -1;
        StartCoroutine(CrossfadeBGM(_mainMenuBgm));
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        if (newClip == null) yield break;

        // 페이드 아웃
        float startVol = _bgmSource.volume;
        float elapsed  = 0f;
        while (elapsed < _bgmFadeDur * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (_bgmFadeDur * 0.5f));
            yield return null;
        }

        _bgmSource.Stop();
        _bgmSource.clip = newClip;
        _bgmSource.Play();

        // 페이드 인
        elapsed = 0f;
        while (elapsed < _bgmFadeDur * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            _bgmSource.volume = Mathf.Lerp(0f, _musicVolume, elapsed / (_bgmFadeDur * 0.5f));
            yield return null;
        }
        _bgmSource.volume = _musicVolume;
    }

    // ═════════════════════════════════════════════════════════════
    // SFX
    // ═════════════════════════════════════════════════════════════

    public void PlaySFX(SFXType type)
    {
        if (!_sfxMap.TryGetValue(type, out var clip) || clip == null) return;

        AudioSource src = GetPooledSource();
        src.clip   = clip;
        src.volume = _sfxVolume;
        src.pitch  = 1f + Random.Range(-0.05f, 0.05f);  // 피치 미세 변화로 단조로움 방지
        src.Play();
        StartCoroutine(ReturnToPool(src, clip.length + 0.1f));
    }

    public void PlaySFXAtPitch(SFXType type, float pitch)
    {
        if (!_sfxMap.TryGetValue(type, out var clip) || clip == null) return;
        AudioSource src = GetPooledSource();
        src.clip   = clip;
        src.volume = _sfxVolume;
        src.pitch  = pitch;
        src.Play();
        StartCoroutine(ReturnToPool(src, clip.length / pitch + 0.1f));
    }

    private AudioSource GetPooledSource()
    {
        if (_sfxPool.Count > 0) return _sfxPool.Dequeue();
        return gameObject.AddComponent<AudioSource>();
    }

    private IEnumerator ReturnToPool(AudioSource src, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        src.Stop();
        _sfxPool.Enqueue(src);
    }

    // ═════════════════════════════════════════════════════════════
    // 볼륨 설정
    // ═════════════════════════════════════════════════════════════

    public void SetMusicVolume(float v)
    {
        _musicVolume        = v;
        _bgmSource.volume   = v;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Data.MusicVolume = v;
            SaveManager.Instance.Save();
        }
    }

    public void SetSFXVolume(float v)
    {
        _sfxVolume = v;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Data.SfxVolume = v;
            SaveManager.Instance.Save();
        }
    }

    public void ToggleSound(bool on)
    {
        _bgmSource.mute = !on;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Data.SoundEnabled = on;
            SaveManager.Instance.Save();
        }
    }

    private void ApplyVolumeSettings()
    {
        if (_bgmSource) _bgmSource.volume = _musicVolume;
    }

    // ═════════════════════════════════════════════════════════════
    // 초기화 헬퍼
    // ═════════════════════════════════════════════════════════════

    private void BuildSFXMap()
    {
        // _sfxClips 배열이 SFXType 열거형 순서와 일치한다고 가정
        var values = (SFXType[])System.Enum.GetValues(typeof(SFXType));
        for (int i = 0; i < values.Length && i < _sfxClips.Length; i++)
            _sfxMap[values[i]] = _sfxClips[i];
    }

    private void PreWarmPool()
    {
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            _sfxPool.Enqueue(src);
        }
    }
}
