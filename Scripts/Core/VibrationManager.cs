using UnityEngine;

/// <summary>
/// Android 네이티브 진동 피드백.
/// 벽돌 파괴, 라이프 손실, 클리어 등 이벤트에 맞춰 강도 다른 진동을 발동한다.
/// </summary>
public class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static bool _initialized = false;

    private static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
    }
#endif

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
#if UNITY_ANDROID && !UNITY_EDITOR
        Init();
#endif
    }

    private bool IsEnabled => SaveManager.Instance?.Data.VibrationEnabled ?? true;

    // ── 공개 API ─────────────────────────────────────────────────

    /// <summary>가벼운 탁! 느낌 (일반 벽돌 파괴)</summary>
    public void Light() => Vibrate(20);

    /// <summary>중간 진동 (특수 벽돌 파괴, 아이템 획득)</summary>
    public void Medium() => Vibrate(50);

    /// <summary>강한 진동 (라이프 손실, 폭발)</summary>
    public void Heavy() => Vibrate(100);

    /// <summary>클리어 패턴 (짧은 연속 진동)</summary>
    public void ClearPattern()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsEnabled || _vibrator == null) return;
        long[] pattern  = { 0, 40, 60, 40, 60, 80 };
        int[]  amps     = { 0, 80, 0, 120, 0, 200 };
        _vibrator.Call("vibrate", new AndroidJavaObject("android.os.VibrationEffect",
            pattern, amps, -1));
#endif
    }

    private void Vibrate(long ms)
    {
        if (!IsEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_vibrator == null) return;
        // API 26+: VibrationEffect.createOneShot
        try
        {
            using var effect = new AndroidJavaClass("android.os.VibrationEffect");
            var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, -1);
            _vibrator.Call("vibrate", oneShot);
        }
        catch
        {
            _vibrator.Call("vibrate", ms);   // 구형 API 폴백
        }
#elif UNITY_EDITOR
        // 에디터에서 로그로 확인
        Debug.Log($"[Vibration] {ms}ms");
#endif
    }
}
