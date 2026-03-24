using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 코인 드롭 → 궤적 → UI 흡수 애니메이션 전체를 관리한다.
/// 코인 자석(Magnet) 아이템, 코인 카운터 흔들림 효과도 담당.
/// </summary>
public class CoinFlyManager : MonoBehaviour
{
    public static CoinFlyManager Instance { get; private set; }

    [Header("Coin Prefab")]
    [SerializeField] CoinFlyParticle _coinPrefab;
    [SerializeField] int             _poolSize      = 80;

    [Header("Fly Settings")]
    [SerializeField] float  _flyDuration   = 0.6f;
    [SerializeField] float  _spreadRadius  = 0.8f;
    [SerializeField] Vector3 _uiTarget;          // 코인 UI 위치 (월드 좌표 또는 Screen → World 변환)

    [Header("Magnet")]
    [SerializeField] float  _magnetRadius  = 5f;
    [SerializeField] float  _magnetSpeed   = 10f;

    private Queue<CoinFlyParticle> _pool = new Queue<CoinFlyParticle>();
    private List<CoinFlyParticle>  _active = new List<CoinFlyParticle>();
    private bool _magnetActive;
    private Coroutine _magnetCoroutine;

    // 코인 카운터 UI 참조
    [SerializeField] RectTransform _coinCounterUI;
    private Coroutine _counterShakeCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        PreWarm();
    }

    void Update()
    {
        if (_magnetActive) PullCoinsTowardsPaddle();
    }

    // ═════════════════════════════════════════════════════════════
    // 풀 관리
    // ═════════════════════════════════════════════════════════════

    private void PreWarm()
    {
        for (int i = 0; i < _poolSize; i++)
        {
            var coin = Instantiate(_coinPrefab, transform);
            coin.gameObject.SetActive(false);
            _pool.Enqueue(coin);
        }
    }

    private CoinFlyParticle Rent(Vector3 pos)
    {
        CoinFlyParticle coin = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_coinPrefab, transform);
        coin.transform.position = pos;
        coin.gameObject.SetActive(true);
        _active.Add(coin);
        return coin;
    }

    public void Return(CoinFlyParticle coin)
    {
        _active.Remove(coin);
        coin.gameObject.SetActive(false);
        _pool.Enqueue(coin);
    }

    // ═════════════════════════════════════════════════════════════
    // 코인 드롭 연출
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// pos 위치에서 count개의 코인을 드롭하고 UI로 흡수되는 연출을 재생한다.
    /// </summary>
    public void SpawnCoinFly(Vector3 pos, int count)
    {
        count = Mathf.Clamp(count, 1, 20);
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = Random.insideUnitCircle * _spreadRadius;
            var coin = Rent(pos + offset);
            StartCoroutine(FlyToUI(coin));
        }
        ShakeCounter();
    }

    private IEnumerator FlyToUI(CoinFlyParticle coin)
    {
        // 1단계: 짧게 튀어오르는 물리감
        Vector3 startPos = coin.transform.position;
        Vector3 arc      = startPos + Vector3.up * Random.Range(0.3f, 1.0f)
                                    + (Vector3)Random.insideUnitCircle * 0.5f;
        float elapsed = 0f;
        float bounceDur = 0.2f;
        while (elapsed < bounceDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDur;
            coin.transform.position = Vector3.Lerp(startPos, arc, t);
            yield return null;
        }

        // 2단계: UI 코인 카운터로 빨려 들어감
        elapsed = 0f;
        Vector3 flyStart = coin.transform.position;
        Vector3 target   = GetUIWorldPos();

        while (elapsed < _flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _flyDuration);
            // 베지어 곡선으로 매끄럽게
            Vector3 ctrl = (flyStart + target) * 0.5f + Vector3.up * 1.5f;
            coin.transform.position = QuadBezier(flyStart, ctrl, target, t);

            // 빨려 들어가면서 스케일 감소
            float scale = Mathf.Lerp(1f, 0.2f, t * t);
            coin.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // 흡수 완료
        Return(coin);
        AudioManager.Instance?.PlaySFX(SFXType.CoinCollect);
        ShakeCounter();
    }

    private Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;
    }

    private Vector3 GetUIWorldPos()
    {
        if (_coinCounterUI != null)
        {
            // RectTransform → 월드 좌표 변환
            Vector3[] corners = new Vector3[4];
            _coinCounterUI.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }
        return _uiTarget;
    }

    // ═════════════════════════════════════════════════════════════
    // 코인 카운터 흔들림
    // ═════════════════════════════════════════════════════════════

    private void ShakeCounter()
    {
        if (_coinCounterUI == null) return;
        if (_counterShakeCo != null) StopCoroutine(_counterShakeCo);
        _counterShakeCo = StartCoroutine(CounterShakeRoutine());
    }

    private IEnumerator CounterShakeRoutine()
    {
        Vector3 origin = _coinCounterUI.localPosition;
        float dur = 0.3f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * 4f;
            _coinCounterUI.localPosition = origin + Vector3.right * shake;
            yield return null;
        }
        _coinCounterUI.localPosition = origin;
    }

    // ═════════════════════════════════════════════════════════════
    // 코인 자석
    // ═════════════════════════════════════════════════════════════

    public void ActivateMagnet(float duration)
    {
        if (_magnetCoroutine != null) StopCoroutine(_magnetCoroutine);
        _magnetCoroutine = StartCoroutine(MagnetRoutine(duration));
    }

    private IEnumerator MagnetRoutine(float duration)
    {
        _magnetActive = true;
        yield return new WaitForSeconds(duration);
        _magnetActive = false;
    }

    private void PullCoinsTowardsPaddle()
    {
        var paddle = FindObjectOfType<PaddleController>();
        if (paddle == null) return;
        Vector3 paddlePos = paddle.transform.position;

        foreach (var coin in _active)
        {
            if (coin == null) continue;
            float dist = Vector3.Distance(coin.transform.position, paddlePos);
            if (dist < _magnetRadius)
            {
                coin.transform.position = Vector3.MoveTowards(
                    coin.transform.position, paddlePos,
                    _magnetSpeed * Time.deltaTime);

                if (dist < 0.5f)
                {
                    StartCoroutine(FlyToUI(coin));
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 클리어 시 코인 폭발 연출
    // ═════════════════════════════════════════════════════════════

    public void PlayClearCoinExplosion(int coinCount)
    {
        StartCoroutine(ClearExplosionRoutine(coinCount));
    }

    private IEnumerator ClearExplosionRoutine(int count)
    {
        AudioManager.Instance?.PlaySFX(SFXType.CoinBurst);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-5f, 5f), Random.Range(-2f, 5f), 0f);
            SpawnCoinFly(pos, 1);
            if (i % 5 == 0)
                yield return new WaitForSeconds(0.05f);
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// CoinFlyParticle: 개별 코인 시각 오브젝트
// ══════════════════════════════════════════════════════════════════

public class CoinFlyParticle : MonoBehaviour
{
    [SerializeField] SpriteRenderer _sprite;
    [SerializeField] Color[]        _colors = {
        new Color(1f, 0.85f, 0.1f),   // 기본 금색
        new Color(1f, 1f,   0.5f),    // 밝은 금색
        new Color(0.9f, 0.6f, 0.1f),  // 어두운 금색
    };

    void OnEnable()
    {
        if (_sprite)
            _sprite.color = _colors[Random.Range(0, _colors.Length)];
        transform.localScale = Vector3.one;
    }
}
