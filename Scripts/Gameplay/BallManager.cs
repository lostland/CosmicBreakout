using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 게임 내 모든 공(BallController)의 생명주기를 관리한다.
/// 멀티볼 생성, 마지막 볼 손실 처리, 패들 재장착을 담당한다.
/// </summary>
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [SerializeField] BallController   _ballPrefab;
    [SerializeField] PaddleController _paddle;

    public List<BallController> ActiveBalls { get; } = new List<BallController>();

    private bool _waitingToLaunch = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SpawnInitialBall();
    }

    // ═════════════════════════════════════════════════════════════
    // 초기화
    // ═════════════════════════════════════════════════════════════

    private void SpawnInitialBall()
    {
        SpawnBallOnPaddle();
    }

    private BallController SpawnBallOnPaddle()
    {
        var ball = Instantiate(_ballPrefab, _paddle.transform.position + Vector3.up * 0.7f, Quaternion.identity);

        // 스테이지 속도 배율 적용
        float stageMult = 1f;
        if (GameManager.Instance != null)
        {
            var ld = StageDatabase.GetLevel(
                GameManager.Instance.CurrentStageIndex,
                GameManager.Instance.CurrentLevelIndex);
            stageMult = ld.BallSpeedMult;
        }
        ball.StageMult = stageMult;
        ball.AttachToPaddle(_paddle.transform);
        ActiveBalls.Add(ball);
        _waitingToLaunch = true;
        return ball;
    }

    // ═════════════════════════════════════════════════════════════
    // 발사
    // ═════════════════════════════════════════════════════════════

    /// <summary>패들이 터치되었을 때 호출. 대기 중인 공을 발사한다.</summary>
    public void TryLaunch()
    {
        if (!_waitingToLaunch) return;
        _waitingToLaunch = false;

        foreach (var b in ActiveBalls)
        {
            if (b != null && !b.IsLaunched)
            {
                b.Launch(Vector2.up);
                AudioManager.Instance?.PlaySFX(SFXType.BallLaunch);
                return;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 멀티볼
    // ═════════════════════════════════════════════════════════════

    public void SpawnMultiBalls(int count = 2)
    {
        var originals = new List<BallController>(ActiveBalls);
        foreach (var orig in originals)
        {
            if (orig == null || !orig.IsLaunched) continue;
            for (int i = 0; i < count; i++)
            {
                var clone = orig.Clone();
                ActiveBalls.Add(clone);
            }
        }
        AudioManager.Instance?.PlaySFX(SFXType.MultiBall);
    }

    // ═════════════════════════════════════════════════════════════
    // 볼 손실 처리
    // ═════════════════════════════════════════════════════════════

    public void OnBallLost(BallController ball)
    {
        ActiveBalls.Remove(ball);
        Destroy(ball.gameObject);

        // 보호막이 있으면 볼 복구
        if (_paddle.ConsumeShield())
        {
            SpawnBallOnPaddle();
            return;
        }

        // 아직 볼이 남아 있으면 계속 진행
        if (ActiveBalls.Count > 0) return;

        // 모든 볼 손실 → 라이프 감소
        StartCoroutine(HandleLifeLost());
    }

    private IEnumerator HandleLifeLost()
    {
        // 짧은 딜레이 후 처리 (이펙트 재생 시간 확보)
        CameraShake.Instance?.Shake(0.2f, 0.4f);
        AudioManager.Instance?.PlaySFX(SFXType.LifeLost);
        yield return new WaitForSeconds(0.8f);

        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoseLife();

        if (GameManager.Instance.CurrentState == GameManager.GameState.Playing)
        {
            // 라이프 남아 있으면 새 볼 스폰
            yield return new WaitForSeconds(0.5f);
            SpawnBallOnPaddle();
        }
    }
}
