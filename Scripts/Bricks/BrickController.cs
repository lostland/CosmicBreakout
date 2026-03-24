using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 개별 벽돌의 HP, 타입별 특수 동작, 웨이브 애니메이션, 파괴 이펙트를 처리한다.
/// BrickLayoutManager가 생성 시 Init()으로 초기화한다.
/// </summary>
public class BrickController : MonoBehaviour
{
    // ── 벽돌 타입 ─────────────────────────────────────────────────
    public enum BrickType
    {
        Normal,         // 기본 (1타)
        Armored,        // 장갑 (2~4타)
        Explosive,      // 폭발 (파괴 시 인접 범위 폭발)
        Splitter,       // 분열 (파괴 시 작은 벽돌 2개)
        Shielded,       // 실드 (특정 조건에서만 파괴)
        GravityWell,    // 중력장 (공 궤도 왜곡)
        Regenerator,    // 재생 (주변 벽돌 HP 회복)
        Moving,         // 이동 (좌우로 천천히 이동)
        BossCore,       // 보스 코어 (단계별 약점 노출)
        Coin,           // 코인 벽돌 (파괴 시 대량 코인)
        Ice,            // 얼음 (공 속도 감소)
        Electric,       // 전기 (파괴 시 인접 전기 데미지)
        Fire,           // 화염 (주변 정상 벽돌에 도트 데미지)
    }

    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("Type & HP")]
    [SerializeField] BrickType  _type        = BrickType.Normal;
    [SerializeField] int        _maxHp       = 1;
    [SerializeField] SpriteRenderer _sprite;
    [SerializeField] Sprite[]   _hpSprites;      // HP별 스프라이트

    [Header("Colors")]
    [SerializeField] Color      _normalColor = Color.white;
    [SerializeField] Color      _hitFlash    = Color.white;

    [Header("Wave Animation")]
    [SerializeField] float _waveAmplitude    = 0f;  // BrickLayoutManager에서 주입
    [SerializeField] float _waveFrequency    = 0f;
    [SerializeField] float _wavePhaseOffset  = 0f;  // 자연스러운 위상 차이

    [Header("FX Prefabs")]
    [SerializeField] GameObject _destroyFX;
    [SerializeField] GameObject _hitFX;
    [SerializeField] GameObject _coinDropFX;

    [Header("Coin")]
    [SerializeField] int _minCoinDrop  = 1;
    [SerializeField] int _maxCoinDrop  = 3;
    [SerializeField] float _coinDropChance = 0.35f;   // 35% 확률로 코인 드롭

    // ── 내부 상태 ─────────────────────────────────────────────────
    public  int      CurrentHp      { get; private set; }
    public  bool     IsDestroyed    { get; private set; }
    public  BrickType Type          => _type;

    private Vector3  _basePosition;   // 생성 시 원래 위치
    private Coroutine _hitFlashCo;
    private float    _elapsedTime;
    private bool     _waveEnabled = true;
    private bool     _shieldIntact = false;

    // 중력장 전용
    public  float GravityRadius    = 2.5f;
    public  float GravityForce     = 3.0f;

    // 이동 벽돌 전용
    private float _moveDirX = 1f;
    private float _moveSpeed = 0.8f;
    private float _moveRange = 2.0f;
    private float _moveOriginX;

    // 재생 벽돌 전용
    private float _regenTimer    = 0f;
    private float _regenInterval = 4.0f;
    private float _regenRadius   = 2.5f;

    // ═════════════════════════════════════════════════════════════
    // 초기화
    // ═════════════════════════════════════════════════════════════

    public void Init(BrickType type, int hp, Color color, float waveAmp, float waveFreq, float phaseOffset)
    {
        _type          = type;
        _maxHp         = hp;
        CurrentHp      = hp;
        _normalColor   = color;
        _waveAmplitude = waveAmp;
        _waveFrequency = waveFreq;
        _wavePhaseOffset = phaseOffset;
        _basePosition  = transform.position;
        _moveOriginX   = transform.position.x;
        _shieldIntact  = (type == BrickType.Shielded);

        if (_sprite) _sprite.color = color;
        UpdateSprite();

        // 이동 벽돌 방향 랜덤
        if (type == BrickType.Moving)
            _moveDirX = Random.value > 0.5f ? 1f : -1f;
    }

    // ═════════════════════════════════════════════════════════════
    // 업데이트: 웨이브 + 이동 + 재생
    // ═════════════════════════════════════════════════════════════

    void Update()
    {
        if (IsDestroyed) return;
        _elapsedTime += Time.deltaTime;

        // 웨이브 애니메이션 (플레이에 지장 없는 미세 움직임)
        if (_waveEnabled && _waveAmplitude > 0f)
        {
            float offsetY = Mathf.Sin(_elapsedTime * _waveFrequency + _wavePhaseOffset) * _waveAmplitude;
            // 충돌체 위치와 렌더 위치를 분리하지 않고, 전체 transform을 미세하게 이동
            // (실제로 벽돌 크기의 20% 미만으로 제한)
            transform.position = _basePosition + Vector3.up * offsetY * 0.01f;
        }

        // 이동 벽돌
        if (_type == BrickType.Moving)
        {
            float newX = transform.position.x + _moveDirX * _moveSpeed * Time.deltaTime;
            if (Mathf.Abs(newX - _moveOriginX) > _moveRange) _moveDirX *= -1f;
            _basePosition = new Vector3(newX, _basePosition.y, _basePosition.z);
        }

        // 중력장: 공에 영향
        if (_type == BrickType.GravityWell)
            ApplyGravityToNearbyBalls();

        // 재생 벽돌
        if (_type == BrickType.Regenerator)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= _regenInterval)
            {
                _regenTimer = 0f;
                RegenerateNeighbors();
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 데미지 처리
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 벽돌에 데미지를 준다.
    /// isPierce = 관통볼, isExplosive = 폭발볼
    /// </summary>
    public void TakeDamage(int dmg, bool isPierce = false, bool isExplosive = false)
    {
        if (IsDestroyed) return;

        // 실드 벽돌: 관통볼이 아니면 1회 무적
        if (_type == BrickType.Shielded && _shieldIntact && !isPierce)
        {
            _shieldIntact = false;
            ShowShieldBreakFX();
            return;
        }

        GameSceneController.Instance?.NotifyBrickHit();

        CurrentHp -= dmg;
        GameManager.Instance?.AddCombo();
        AudioManager.Instance?.PlaySFX(GetHitSFX());

        if (CurrentHp <= 0)
        {
            DestroyBrick(isExplosive);
        }
        else
        {
            UpdateSprite();
            StartHitFlash();
            CameraShake.Instance?.Shake(0.03f, 0.08f);
        }
    }

    private void DestroyBrick(bool isExplosive)
    {
        IsDestroyed = true;
        _waveEnabled = false;

        // 코인 드롭
        HandleCoinDrop();

        // 타입별 특수 동작
        HandleDestroyEffect(isExplosive);

        // 이펙트
        SpawnFX(_destroyFX, transform.position);
        AudioManager.Instance?.PlaySFX(GetDestroySFX());
        CameraShake.Instance?.Shake(0.05f, 0.12f);

        // 통계
        var save = SaveManager.Instance?.Data;
        if (save != null) save.TotalBricksDestroyed++;

        // 레이아웃 매니저에 알림
        BrickLayoutManager.Instance?.OnBrickDestroyed(this);

        gameObject.SetActive(false);
    }

    private void HandleCoinDrop()
    {
        int coinAmt = 0;

        if (_type == BrickType.Coin)
        {
            // 코인 벽돌: 무조건 대량 드롭
            coinAmt = Random.Range(8, 16);
        }
        else if (Random.value < _coinDropChance)
        {
            coinAmt = Random.Range(_minCoinDrop, _maxCoinDrop + 1);
        }

        if (coinAmt > 0)
        {
            GameManager.Instance?.AddCoins(coinAmt);
            // 시각 연출: 코인 드롭 파티클
            SpawnFX(_coinDropFX, transform.position);
            CoinFlyManager.Instance?.SpawnCoinFly(transform.position, coinAmt);
        }
    }

    private void HandleDestroyEffect(bool isExplosive)
    {
        switch (_type)
        {
            case BrickType.Explosive:
                BrickLayoutManager.Instance?.TriggerExplosion(transform.position, 2.0f);
                break;

            case BrickType.Splitter:
                BrickLayoutManager.Instance?.SpawnSplitterPieces(transform.position);
                break;

            case BrickType.Electric:
                BrickLayoutManager.Instance?.TriggerElectricChain(transform.position, 1.5f);
                break;

            case BrickType.Fire:
                // 파괴 전까지 틱 데미지를 줬던 것이 멈춤
                break;

            case BrickType.BossCore:
                BrickLayoutManager.Instance?.OnBossCoreDestroyed(this);
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 중력장 / 재생
    // ═════════════════════════════════════════════════════════════

    private void ApplyGravityToNearbyBalls()
    {
        var balls = BallManager.Instance?.ActiveBalls;
        if (balls == null) return;

        foreach (var ball in balls)
        {
            if (ball == null) continue;
            Vector2 dir    = (Vector2)(transform.position - ball.transform.position);
            float   distSq = dir.sqrMagnitude;
            if (distSq < GravityRadius * GravityRadius)
            {
                float force = GravityForce / Mathf.Max(distSq, 0.1f);
                Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
                if (rb) rb.AddForce(dir.normalized * force * Time.deltaTime, ForceMode2D.Force);
            }
        }
    }

    private void RegenerateNeighbors()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, _regenRadius);
        foreach (var col in cols)
        {
            BrickController brick = col.GetComponent<BrickController>();
            if (brick != null && brick != this && !brick.IsDestroyed && brick.CurrentHp < brick._maxHp)
            {
                brick.CurrentHp = Mathf.Min(brick.CurrentHp + 1, brick._maxHp);
                brick.UpdateSprite();
                EffectPool.Instance?.Spawn(_hitFX, brick.transform.position, Quaternion.identity);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 시각 보조
    // ═════════════════════════════════════════════════════════════

    private void UpdateSprite()
    {
        if (_sprite == null) return;
        if (_hpSprites != null && _hpSprites.Length > 0)
        {
            int idx = Mathf.Clamp(_maxHp - CurrentHp, 0, _hpSprites.Length - 1);
            _sprite.sprite = _hpSprites[idx];
        }
        // HP 비율에 따라 밝기 조절
        float brightness = 0.5f + 0.5f * ((float)CurrentHp / _maxHp);
        _sprite.color = _normalColor * brightness;
    }

    private void StartHitFlash()
    {
        if (_hitFlashCo != null) StopCoroutine(_hitFlashCo);
        _hitFlashCo = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _sprite.color = _hitFlash;
        yield return new WaitForSeconds(0.05f);
        UpdateSprite();
    }

    private void ShowShieldBreakFX()
    {
        // 실드 벽돌의 보호막 깨짐 연출
        SpawnFX(_hitFX, transform.position);
        AudioManager.Instance?.PlaySFX(SFXType.ShieldBreak);
    }

    private void SpawnFX(GameObject prefab, Vector3 pos)
    {
        if (prefab != null)
            EffectPool.Instance?.Spawn(prefab, pos, Quaternion.identity);
    }

    private SFXType GetHitSFX()
    {
        return _type switch
        {
            BrickType.Armored   => SFXType.BrickArmorHit,
            BrickType.Ice       => SFXType.BrickIceHit,
            BrickType.Electric  => SFXType.BrickElectricHit,
            _                   => SFXType.BrickNormalHit,
        };
    }

    private SFXType GetDestroySFX()
    {
        return _type switch
        {
            BrickType.Explosive => SFXType.BrickExplode,
            BrickType.Coin      => SFXType.CoinBurst,
            BrickType.BossCore  => SFXType.BossDestroy,
            _                   => SFXType.BrickDestroy,
        };
    }
}
