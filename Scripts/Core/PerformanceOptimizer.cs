using UnityEngine;
using System.Collections;

/// <summary>
/// Android 모바일 성능 최적화 관리자.
/// - 목표 프레임레이트 설정 (60fps)
/// - 배터리 절약 모드 (30fps 강제)
/// - 파티클 품질 LOD (저사양 기기 대응)
/// - 텍스처 품질 조절
/// - 메모리 GC 타이밍 제어
/// </summary>
public class PerformanceOptimizer : MonoBehaviour
{
    public static PerformanceOptimizer Instance { get; private set; }

    [Header("Frame Rate")]
    [SerializeField] int  _targetFPS            = 60;
    [SerializeField] bool _batterySaverMode     = false;
    [SerializeField] int  _batterySaverFPS      = 30;

    [Header("Quality")]
    [SerializeField] int  _maxParticlesHigh     = 500;
    [SerializeField] int  _maxParticlesMedium   = 200;
    [SerializeField] int  _maxParticlesLow      = 80;

    [Header("GC")]
    [SerializeField] float _gcInterval          = 30f;  // 씬 전환 외 추가 GC 주기

    public enum QualityTier { High, Medium, Low }
    public QualityTier CurrentTier { get; private set; } = QualityTier.High;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        DetectDeviceTier();
        ApplySettings();
        StartCoroutine(PeriodicGC());
    }

    // ═════════════════════════════════════════════════════════════
    // 기기 성능 감지
    // ═════════════════════════════════════════════════════════════

    private void DetectDeviceTier()
    {
        int ram = SystemInfo.systemMemorySize;        // MB
        int gpuMem = SystemInfo.graphicsMemorySize;  // MB

        if (ram >= 4096 && gpuMem >= 1024)
            CurrentTier = QualityTier.High;
        else if (ram >= 2048 && gpuMem >= 512)
            CurrentTier = QualityTier.Medium;
        else
            CurrentTier = QualityTier.Low;

        Debug.Log($"[Performance] Device tier: {CurrentTier} | RAM: {ram}MB | VRAM: {gpuMem}MB");
    }

    private void ApplySettings()
    {
        // 프레임레이트
        int fps = _batterySaverMode ? _batterySaverFPS : _targetFPS;
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount  = 0;  // vSync 끄고 targetFrameRate 사용

        // 품질 레벨
        switch (CurrentTier)
        {
            case QualityTier.High:
                QualitySettings.SetQualityLevel(2);
                ParticleSystemManager.SetMaxParticles(_maxParticlesHigh);
                break;
            case QualityTier.Medium:
                QualitySettings.SetQualityLevel(1);
                ParticleSystemManager.SetMaxParticles(_maxParticlesMedium);
                break;
            case QualityTier.Low:
                QualitySettings.SetQualityLevel(0);
                QualitySettings.antiAliasing = 0;
                ParticleSystemManager.SetMaxParticles(_maxParticlesLow);
                break;
        }

        // 화면 꺼짐 방지
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Android 멀티터치 활성화
        Input.multiTouchEnabled = true;
    }

    // ═════════════════════════════════════════════════════════════
    // 배터리 절약 모드 토글
    // ═════════════════════════════════════════════════════════════

    public void SetBatterySaverMode(bool on)
    {
        _batterySaverMode           = on;
        Application.targetFrameRate = on ? _batterySaverFPS : _targetFPS;
    }

    // ═════════════════════════════════════════════════════════════
    // 주기적 GC (씬 전환 외 메모리 정리)
    // ═════════════════════════════════════════════════════════════

    private IEnumerator PeriodicGC()
    {
        while (true)
        {
            yield return new WaitForSeconds(_gcInterval);
            // 게임이 일시정지 상태일 때만 GC 실행 (플레이 중 끊김 방지)
            if (GameManager.Instance?.CurrentState == GameManager.GameState.Paused ||
                GameManager.Instance?.CurrentState == GameManager.GameState.MainMenu)
            {
                System.GC.Collect();
                Resources.UnloadUnusedAssets();
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 씬 전환 시 정리 (GameManager에서 호출)
    // ═════════════════════════════════════════════════════════════

    public void OnSceneTransition()
    {
        System.GC.Collect();
        // Resources.UnloadUnusedAssets()는 씬 로드 후 비동기로 처리됨
    }
}

/// <summary>파티클 최대 개수 전역 조절 헬퍼</summary>
public static class ParticleSystemManager
{
    private static int _maxParticles = 500;

    public static void SetMaxParticles(int max)
    {
        _maxParticles = max;
        // 현재 씬의 모든 파티클 시스템에 적용
        var systems = GameObject.FindObjectsOfType<ParticleSystem>();
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (main.maxParticles > max)
                main.maxParticles = max;
        }
    }

    public static int MaxParticles => _maxParticles;
}
