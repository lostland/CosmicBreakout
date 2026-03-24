using UnityEngine;
using System;
// Unity Ads 패키지: Package Manager > Unity Ads SDK 설치 후 아래 using 활성화
// using UnityEngine.Advertisements;

/// <summary>
/// 광고 SDK를 감싸는 래퍼.
/// 현재는 Unity Ads를 기준으로 구조를 잡되, AdMob으로 교체 가능하도록 인터페이스를 분리.
///
/// 사용 방법:
///   AdsManager.Instance.ShowRewardedAd("LevelClear",
///       onSuccess: () => GameManager.Instance.ClaimDoubleReward(),
///       onFailed:  () => Debug.Log("광고 실패 또는 스킵"));
/// </summary>
public class AdsManager : MonoBehaviour /*, IUnityAdsLoadListener, IUnityAdsShowListener */
{
    public static AdsManager Instance { get; private set; }

    [Header("Unity Ads Settings")]
    [SerializeField] string _androidGameId  = "YOUR_ANDROID_GAME_ID";
    [SerializeField] string _iosGameId      = "YOUR_IOS_GAME_ID";
    [SerializeField] bool   _testMode       = true;

    [Header("Ad Unit IDs")]
    [SerializeField] string _rewardedAdId   = "Rewarded_Android";
    [SerializeField] string _interstitialId = "Interstitial_Android";

    private Action _onRewardSuccess;
    private Action _onRewardFailed;
    private bool   _isRewardedLoaded = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAds();
    }

    // ═════════════════════════════════════════════════════════════
    // 초기화
    // ═════════════════════════════════════════════════════════════

    private void InitializeAds()
    {
        // Unity Ads 사용 시:
        // string gameId = Application.platform == RuntimePlatform.Android ? _androidGameId : _iosGameId;
        // Advertisement.Initialize(gameId, _testMode, this);

        // AdMob 사용 시:
        // MobileAds.Initialize(initStatus => { LoadRewardedAd(); });

        Debug.Log("[Ads] AdsManager initialized (stub mode)");
        _isRewardedLoaded = true; // 스텁: 항상 준비된 것으로 처리
    }

    private void LoadRewardedAd()
    {
        // Unity Ads:
        // Advertisement.Load(_rewardedAdId, this);

        // AdMob:
        // RewardedAd.Load("ca-app-pub-xxxxx/xxxxx", new AdRequest(), (ad, error) => { ... });

        _isRewardedLoaded = true;
    }

    // ═════════════════════════════════════════════════════════════
    // 광고 표시
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 보상형 광고를 표시한다.
    /// </summary>
    /// <param name="placement">광고 배치 ID (로그/분석 용도)</param>
    /// <param name="onSuccess">시청 완료 콜백</param>
    /// <param name="onFailed">스킵/실패 콜백</param>
    public void ShowRewardedAd(string placement, Action onSuccess, Action onFailed = null)
    {
        _onRewardSuccess = onSuccess;
        _onRewardFailed  = onFailed;

        if (!_isRewardedLoaded)
        {
            Debug.LogWarning("[Ads] Rewarded ad not loaded yet.");
            onFailed?.Invoke();
            LoadRewardedAd();
            return;
        }

        // Unity Ads:
        // Advertisement.Show(_rewardedAdId, this);

        // AdMob:
        // _rewardedAd.Show(reward => { OnUnityAdsShowComplete(...); });

        // ─ 스텁: 실제 광고 SDK 없이 테스트 ─────────────────────────
#if UNITY_EDITOR
        Debug.Log($"[Ads] [STUB] Rewarded ad shown for: {placement}");
        SimulateAdResult(true);
#else
        // 실제 기기에서는 SDK 호출
        SimulateAdResult(true);
#endif
    }

    /// <summary>전면 광고 (스테이지 선택 화면 등 자연스러운 타이밍에만 표시)</summary>
    public void ShowInterstitialAd(Action onClosed = null)
    {
#if UNITY_EDITOR
        Debug.Log("[Ads] [STUB] Interstitial ad shown.");
        onClosed?.Invoke();
#else
        // Advertisement.Show(_interstitialId, this);
        onClosed?.Invoke();
#endif
    }

    // ═════════════════════════════════════════════════════════════
    // 콜백 (Unity Ads IUnityAdsShowListener 구현부)
    // ═════════════════════════════════════════════════════════════

    // public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState state)
    // {
    //     if (state == UnityAdsShowCompletionState.COMPLETED)
    //         HandleRewardSuccess();
    //     else
    //         HandleRewardFailed();
    //     LoadRewardedAd(); // 다음 광고 미리 로드
    // }

    // public void OnUnityAdsFailedToLoad(string id, UnityAdsLoadError err, string msg) { ... }
    // public void OnUnityAdsAdLoaded(string id) { _isRewardedLoaded = true; }
    // public void OnUnityAdsShowFailure(string id, UnityAdsShowError err, string msg) { HandleRewardFailed(); }
    // public void OnUnityAdsShowStart(string id) { }
    // public void OnUnityAdsShowClick(string id) { }

    private void HandleRewardSuccess()
    {
        _isRewardedLoaded = false;
        _onRewardSuccess?.Invoke();
        _onRewardSuccess = null;
        _onRewardFailed  = null;
    }

    private void HandleRewardFailed()
    {
        _isRewardedLoaded = false;
        _onRewardFailed?.Invoke();
        _onRewardSuccess = null;
        _onRewardFailed  = null;
    }

    private void SimulateAdResult(bool success)
    {
        // 실제 광고 없이 테스트 시 바로 콜백 호출
        if (success) HandleRewardSuccess();
        else         HandleRewardFailed();
    }

    public bool IsRewardedAdReady => _isRewardedLoaded;
}
