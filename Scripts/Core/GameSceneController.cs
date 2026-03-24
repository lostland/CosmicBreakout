using UnityEngine;
using System.Collections;

/// <summary>
/// GameScene의 루트 초기화 컨트롤러.
/// - 업그레이드 영구 효과 적용 (패들 크기, 볼 속도, 추가 라이프)
/// - 지루함 방지 시스템: 볼이 오래 왔다 갔다 할 때 자동 개입
/// - 스테이지 테마 색상/파티클 적용
/// - 게임 오버/클리어 연결
/// </summary>
public class GameSceneController : MonoBehaviour
{
    public static GameSceneController Instance { get; private set; }

    [Header("References")]
    [SerializeField] BrickLayoutManager _layoutMgr;
    [SerializeField] PaddleController   _paddle;
    [SerializeField] BallManager        _ballMgr;
    [SerializeField] ItemManager        _itemMgr;
    [SerializeField] BackgroundScroller _bgScroller;
    [SerializeField] GameHUD            _hud;
    [SerializeField] LevelClearUI       _clearUI;
    [SerializeField] GameObject         _gameOverPanel;

    [Header("Anti-Bore System")]
    [Tooltip("공이 이 시간 동안 벽돌을 못 맞히면 개입한다")]
    [SerializeField] float _boreDectectionTime = 8f;
    [Tooltip("지루함 방지 개입 시 드롭하는 아이템")]
    [SerializeField] ItemType _antiBoredItem = ItemType.Lightning;

    private float _lastBrickHitTime;
    private bool  _antiBoredTriggered;

    // ═════════════════════════════════════════════════════════════
    void Start()
    {
        Instance = this;
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 1. 레이아웃 생성
        _layoutMgr.BuildLayout(gm.CurrentStageIndex, gm.CurrentLevelIndex);

        // 2. 영구 업그레이드 적용
        ApplyUpgrades();

        // 3. BGM 전환
        AudioManager.Instance?.PlayStageBGM(gm.CurrentStageIndex);

        // 4. 게임 상태 시작
        gm.ChangeState(GameManager.GameState.Playing);

        // 5. 게임오버 패널 구독
        GameManager.OnGameOver += OnGameOver;

        _lastBrickHitTime = Time.time;
        StartCoroutine(AntiBoreWatch());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameManager.OnGameOver -= OnGameOver;
    }

    // ═════════════════════════════════════════════════════════════
    // 업그레이드 적용
    // ═════════════════════════════════════════════════════════════

    private void ApplyUpgrades()
    {
        var data = SaveManager.Instance?.Data;
        if (data == null) return;

        // 패들 크기 업그레이드
        float paddleBonus = UpgradeDatabase.GetPaddleSizeBonus(data.UpgradePaddleSize);
        if (paddleBonus > 0f && _paddle != null)
        {
            var sd = StageDatabase.GetStage(GameManager.Instance.CurrentStageIndex);
            _paddle.SetWidth(2.4f + paddleBonus, instant: true);
        }

        // 볼 속도 업그레이드는 BallController.StageMult에 곱셈으로 반영
        // (BallManager.SpawnBallOnPaddle에서 적용)

        // 추가 라이프
        int extraLives = UpgradeDatabase.GetExtraLives(data.UpgradeExtraLife);
        if (extraLives > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.Lives = Mathf.Min(
                GameManager.Instance.Lives + extraLives,
                GameManager.Instance.MaxLives + extraLives);
        }

        // 시작 아이템 슬롯 (업그레이드 레벨만큼 랜덤 아이템 즉시 지급)
        int startItems = data.UpgradeStartItems;
        for (int i = 0; i < startItems; i++)
        {
            var ld = StageDatabase.GetLevel(
                GameManager.Instance.CurrentStageIndex,
                GameManager.Instance.CurrentLevelIndex);
            _itemMgr?.TryDropItem(Vector3.zero, ld.AllowedItems);
        }

        // 코인 자석 업그레이드: CoinFlyManager의 magnetRadius에 반영
        // (CoinFlyManager 내 _magnetRadius += UpgradeDatabase.GetCoinMagnetRadius)
    }

    // ═════════════════════════════════════════════════════════════
    // 지루함 방지 시스템
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 공이 너무 오래 같은 패턴을 반복하면 번개/아이템으로 개입해
    /// "공이 내려오는 걸 멍하니 보는" 지루함을 방지한다.
    /// </summary>
    private IEnumerator AntiBoreWatch()
    {
        while (GameManager.Instance?.CurrentState == GameManager.GameState.Playing)
        {
            yield return new WaitForSeconds(1f);

            float timeSinceHit = Time.time - _lastBrickHitTime;

            if (timeSinceHit >= _boreDectectionTime && !_antiBoredTriggered)
            {
                _antiBoredTriggered = true;
                TriggerAntiBoredEvent();
            }

            // 벽돌이 맞히면 리셋
            if (timeSinceHit < _boreDectectionTime)
                _antiBoredTriggered = false;
        }
    }

    /// <summary>
    /// 지루함 방지 이벤트: 번개, 코인 샤워, 슬로우모션 중 랜덤 발동.
    /// 플레이어에게 갑작스러운 변화와 긴장감을 준다.
    /// </summary>
    private void TriggerAntiBoredEvent()
    {
        int roll = Random.Range(0, 4);
        switch (roll)
        {
            case 0:
                // 번개 타격으로 일부 벽돌 즉시 파괴
                _itemMgr?.ActivateItem(ItemType.Lightning);
                ShowAntiBoreNotice("⚡ 번개 강타!");
                break;
            case 1:
                // 슬로우모션으로 조준 보조
                _itemMgr?.ActivateItem(ItemType.SlowMotion);
                ShowAntiBoreNotice("⏱ 슬로우 타임!");
                break;
            case 2:
                // 코인 샤워로 보상감 유지
                _itemMgr?.ActivateItem(ItemType.CoinShower);
                ShowAntiBoreNotice("💰 코인 샤워!");
                break;
            case 3:
                // 폭발볼로 돌파구 마련
                _itemMgr?.ActivateItem(ItemType.ExplosiveBall);
                ShowAntiBoreNotice("💥 폭발볼 발동!");
                break;
        }
        _lastBrickHitTime = Time.time; // 개입 후 타이머 리셋
    }

    private void ShowAntiBoreNotice(string msg)
    {
        // FloatingTextManager가 있으면 화면 중앙에 잠깐 텍스트 표시
        FloatingTextManager.Instance?.Show(msg, Vector3.up * 1f, 1.5f, fontSize: 28);
    }

    /// <summary>BrickController가 맞힐 때마다 호출해 타이머 리셋</summary>
    public void NotifyBrickHit()
    {
        _lastBrickHitTime  = Time.time;
        _antiBoredTriggered = false;
    }

    // ═════════════════════════════════════════════════════════════
    // 게임오버
    // ═════════════════════════════════════════════════════════════

    private void OnGameOver()
    {
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        AudioManager.Instance?.PlaySFX(SFXType.GameOver);
        CameraShake.Instance?.Shake(0.25f, 0.5f);
        yield return new WaitForSecondsRealtime(0.8f);
        _gameOverPanel?.SetActive(true);
    }

    // 게임오버 패널 버튼
    public void OnRetry()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.StartLevel(gm.CurrentStageIndex, gm.CurrentLevelIndex);
    }

    public void OnQuitToMenu()
    {
        GameManager.Instance?.ReturnToMain();
    }
}
