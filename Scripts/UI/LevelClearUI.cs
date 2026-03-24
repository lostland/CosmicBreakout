using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 레벨 클리어 결과 화면.
/// 코인 집계 연출, 보너스 상자 오픈, 광고 2배 선택 UX를 구현한다.
/// </summary>
public class LevelClearUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject _clearPanel;
    [SerializeField] GameObject _rewardPanel;

    [Header("Coin Summary")]
    [SerializeField] TextMeshProUGUI _sessionCoinText;
    [SerializeField] TextMeshProUGUI _bonusCoinText;
    [SerializeField] TextMeshProUGUI _totalCoinText;
    [SerializeField] TextMeshProUGUI _stageNameText;
    [SerializeField] TextMeshProUGUI _levelNameText;

    [Header("Reward")]
    [SerializeField] RectTransform   _bonusBox;           // 보너스 상자 이미지
    [SerializeField] ParticleSystem  _boxOpenFX;
    [SerializeField] Button          _doubleBtn;          // 광고 2배
    [SerializeField] Button          _claimBtn;           // 그냥 받기
    [SerializeField] TextMeshProUGUI _doubleBtnCoinText;
    [SerializeField] TextMeshProUGUI _claimBtnCoinText;
    [SerializeField] Image           _doubleHighlight;    // 2배 버튼 빛나는 효과

    [Header("Navigation")]
    [SerializeField] Button          _nextLevelBtn;
    [SerializeField] Button          _stageSelectBtn;
    [SerializeField] GameObject      _nextLevelLockedIcon; // 다음 스테이지 없을 때

    [Header("Coin 연출")]
    [SerializeField] int             _clearCoinExplosionCount = 30;

    // ── 내부 ─────────────────────────────────────────────────────
    private long _bonusCoin;
    private bool _rewardClaimed = false;

    // ═════════════════════════════════════════════════════════════
    void OnEnable()
    {
        GameManager.OnLevelClear += ShowClearScreen;
    }
    void OnDisable()
    {
        GameManager.OnLevelClear -= ShowClearScreen;
    }

    // ═════════════════════════════════════════════════════════════
    // 클리어 화면 진입
    // ═════════════════════════════════════════════════════════════

    private void ShowClearScreen()
    {
        StartCoroutine(ClearSequence());
    }

    private IEnumerator ClearSequence()
    {
        // 잠깐 딜레이 후 화면 전환
        yield return new WaitForSecondsRealtime(0.5f);

        _clearPanel?.SetActive(true);
        _rewardPanel?.SetActive(false);

        var gm = GameManager.Instance;
        if (gm == null) yield break;

        // 스테이지/레벨 이름
        var sd = StageDatabase.GetStage(gm.CurrentStageIndex);
        _stageNameText?.SetText(sd.Name);
        _levelNameText?.SetText($"레벨 {gm.CurrentLevelIndex + 1}");

        // 코인 집계 연출
        yield return StartCoroutine(CountUpCoins(gm.SessionCoins));

        // 배경 코인 폭발 연출
        CoinFlyManager.Instance?.PlayClearCoinExplosion(_clearCoinExplosionCount);
        AudioManager.Instance?.PlaySFX(SFXType.LevelClear);

        yield return new WaitForSecondsRealtime(1.2f);

        // 보상 화면
        ShowRewardScreen(gm);
    }

    private IEnumerator CountUpCoins(long target)
    {
        float dur     = Mathf.Clamp(target / 80f, 0.5f, 2.5f);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            long shown = (long)(target * Mathf.SmoothStep(0, 1, elapsed / dur));
            _sessionCoinText?.SetText($"획득 코인  +{shown:N0}");
            yield return null;
        }
        _sessionCoinText?.SetText($"획득 코인  +{target:N0}");
    }

    // ═════════════════════════════════════════════════════════════
    // 보상 화면
    // ═════════════════════════════════════════════════════════════

    private void ShowRewardScreen(GameManager gm)
    {
        _rewardPanel?.SetActive(true);

        var ld     = StageDatabase.GetLevel(gm.CurrentStageIndex, gm.CurrentLevelIndex);
        _bonusCoin = ld.BonusCoinAmount;

        // 보너스 상자 오픈 연출
        StartCoroutine(BoxOpenSequence());

        // 버튼 수치 표시
        long doubleAmount = _bonusCoin * (long)gm.BonusCoinMult;
        _claimBtnCoinText?.SetText($"+{_bonusCoin:N0}");
        _doubleBtnCoinText?.SetText($"+{doubleAmount:N0}");

        // 다음 레벨 버튼 상태
        bool hasNext = !(gm.CurrentLevelIndex == 4 &&
                         gm.CurrentStageIndex >= StageDatabase.StageCount - 1);
        _nextLevelBtn?.gameObject.SetActive(hasNext);
        _nextLevelLockedIcon?.SetActive(!hasNext);

        // 버튼 리스너
        _claimBtn?.onClick.RemoveAllListeners();
        _doubleBtn?.onClick.RemoveAllListeners();
        _nextLevelBtn?.onClick.RemoveAllListeners();
        _stageSelectBtn?.onClick.RemoveAllListeners();

        _claimBtn?.onClick.AddListener(OnClaimClicked);
        _doubleBtn?.onClick.AddListener(OnDoubleClicked);
        _nextLevelBtn?.onClick.AddListener(OnNextLevel);
        _stageSelectBtn?.onClick.AddListener(OnStageSelect);
    }

    private IEnumerator BoxOpenSequence()
    {
        if (_bonusBox == null) yield break;

        // 상자 등장 (아래에서 튀어오름)
        _bonusBox.localScale = Vector3.zero;
        float elapsed = 0f;
        float dur     = 0.4f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float s = Mathf.LerpUnclamped(0f, 1f, EaseOutBack(t));
            _bonusBox.localScale = Vector3.one * s;
            yield return null;
        }
        _bonusBox.localScale = Vector3.one;

        // 흔들림 → 열림 연출
        yield return new WaitForSecondsRealtime(0.3f);
        yield return StartCoroutine(ShakeBox());

        _boxOpenFX?.Play();
        AudioManager.Instance?.PlaySFX(SFXType.CoinBurst);

        // 2배 버튼 빛나기 시작
        StartCoroutine(PulseHighlight());
    }

    private IEnumerator ShakeBox()
    {
        Vector3 origin = _bonusBox.localPosition;
        for (int i = 0; i < 8; i++)
        {
            float x = Mathf.Sin(i * Mathf.PI * 0.7f) * (8 - i) * 2f;
            _bonusBox.localPosition = origin + Vector3.right * x;
            yield return new WaitForSecondsRealtime(0.04f);
        }
        _bonusBox.localPosition = origin;
    }

    private IEnumerator PulseHighlight()
    {
        if (_doubleHighlight == null) yield break;
        float t = 0f;
        while (!_rewardClaimed)
        {
            t += Time.unscaledDeltaTime * 2f;
            float alpha = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f * 0.6f;
            _doubleHighlight.color = new Color(1f, 1f, 0.5f, alpha);
            yield return null;
        }
        _doubleHighlight.color = Color.clear;
    }

    // ═════════════════════════════════════════════════════════════
    // 버튼 콜백
    // ═════════════════════════════════════════════════════════════

    private void OnClaimClicked()
    {
        if (_rewardClaimed) return;
        _rewardClaimed = true;

        // 기본 보너스 코인 적립 (HandleLevelClear에서 이미 session coin은 저장됨)
        var data = SaveManager.Instance?.Data;
        if (data != null)
        {
            data.TotalCoins += _bonusCoin;
            SaveManager.Instance.Save();
        }

        StartCoroutine(ClaimAnimation(_bonusCoin));
        AudioManager.Instance?.PlaySFX(SFXType.CoinCollect);
    }

    private void OnDoubleClicked()
    {
        if (_rewardClaimed) return;
        // 실제 프로젝트에서는 AdsManager.Instance.ShowRewardedAd(onSuccess) 호출
        // 여기서는 바로 2배 처리
        ShowRewardedAdSimulated(() => {
            _rewardClaimed = true;
            GameManager.Instance?.ClaimDoubleReward();
            long doubleAmount = _bonusCoin * (long)(GameManager.Instance?.BonusCoinMult ?? 2f);
            StartCoroutine(ClaimAnimation(doubleAmount, isDouble: true));
        });
    }

    private void ShowRewardedAdSimulated(System.Action onComplete)
    {
        // 실제 구현에서는 Unity Ads / AdMob SDK 호출
        // AdsManager.Instance.ShowRewardedAd("LevelClear", onComplete, onFailed);
        // 데모: 즉시 콜백
        onComplete?.Invoke();
    }

    private IEnumerator ClaimAnimation(long amount, bool isDouble = false)
    {
        // 코인 폭발 연출
        CoinFlyManager.Instance?.PlayClearCoinExplosion(isDouble ? 60 : 30);
        _totalCoinText?.SetText($"총 보너스  +{amount:N0}");

        yield return new WaitForSecondsRealtime(1.5f);

        // 버튼 비활성화
        _claimBtn?.gameObject.SetActive(false);
        _doubleBtn?.gameObject.SetActive(false);

        _bonusCoinText?.SetText($"보너스 코인  +{amount:N0}");
    }

    private void OnNextLevel()
    {
        GameManager.Instance?.NextLevel();
    }

    private void OnStageSelect()
    {
        GameManager.Instance?.ReturnToStageSelect();
    }

    // ═════════════════════════════════════════════════════════════
    // 이징 함수
    // ═════════════════════════════════════════════════════════════

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
