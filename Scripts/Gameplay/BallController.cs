using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 공의 이동, 물리 반사, 강화 상태(관통/폭발/속도), 트레일 이펙트를 제어한다.
/// Rigidbody2D + CircleCollider2D 가 붙어 있어야 한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(TrailRenderer))]
public class BallController : MonoBehaviour
{
    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("Base Speed")]
    [SerializeField] float _baseSpeed       = 12f;
    [SerializeField] float _maxSpeed        = 28f;
    [SerializeField] float _minAngleDeg     = 20f;  // 수평 반사 방지 최소 각도

    [Header("Enhance")]
    [SerializeField] float _pierceDuration  = 8f;
    [SerializeField] float _explosiveDuration = 8f;
    [SerializeField] float _speedBoostMult  = 1.5f;
    [SerializeField] float _slowMult        = 0.6f;

    [Header("Trail")]
    [SerializeField] TrailRenderer _trail;
    [SerializeField] Color _normalTrailColor    = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] Color _pierceTrailColor    = new Color(0.2f, 0.8f, 1f, 0.9f);
    [SerializeField] Color _explosiveTrailColor = new Color(1f, 0.4f, 0.1f, 0.9f);

    [Header("FX Prefabs")]
    [SerializeField] GameObject _wallHitFX;
    [SerializeField] GameObject _paddleHitFX;
    [SerializeField] GameObject _explosionFX;
    [SerializeField] GameObject _pierceLineFX;

    // ── 상태 ─────────────────────────────────────────────────────
    public bool  IsLaunched   { get; private set; }
    public bool  IsPierce     { get; private set; }
    public bool  IsExplosive  { get; private set; }
    public float CurrentSpeed => _rb.linearVelocity.magnitude;

    private Rigidbody2D _rb;
    private CircleCollider2D _col;
    private float _speedMult = 1f;
    private bool _attached   = true;     // 패들에 붙어 있는 대기 상태
    private Transform _paddle;
    private Vector3 _attachOffset;
    private Coroutine _pierceCoroutine;
    private Coroutine _explosiveCoroutine;
    private Coroutine _speedCoroutine;

    // ── 스테이지 속도 배율 (GameManager에서 주입) ──────────────────
    public float StageMult { get; set; } = 1f;

    // ═════════════════════════════════════════════════════════════
    void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _col = GetComponent<CircleCollider2D>();
        _rb.gravityScale = 0;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _trail = GetComponent<TrailRenderer>();
        SetTrailColor(_normalTrailColor);
    }

    void Update()
    {
        if (_attached && _paddle != null)
        {
            transform.position = _paddle.position + _attachOffset;
        }

        // 속도 보정: 최소 속도 유지 (물리 감쇠로 멈추는 현상 방지)
        if (IsLaunched && _rb.linearVelocity.sqrMagnitude < 0.1f)
        {
            Launch(Vector2.up);
        }

        // 수평 반사 방지: 거의 수평으로 튀는 경우 각도 보정
        if (IsLaunched) CorrectFlatAngle();
    }

    // ═════════════════════════════════════════════════════════════
    // 발사
    // ═════════════════════════════════════════════════════════════

    public void AttachToPaddle(Transform paddle)
    {
        _paddle       = paddle;
        _attached     = true;
        IsLaunched    = false;
        _rb.linearVelocity  = Vector2.zero;
        _attachOffset = transform.position - paddle.position;
        _attachOffset.y = Mathf.Abs(_attachOffset.y);
    }

    /// <summary>터치/클릭 시 공을 발사한다.</summary>
    public void Launch(Vector2 direction)
    {
        _attached  = false;
        IsLaunched = true;
        float speed = _baseSpeed * _speedMult * StageMult;
        speed = Mathf.Clamp(speed, _baseSpeed * 0.5f, _maxSpeed);
        _rb.linearVelocity = direction.normalized * speed;
    }

    // ═════════════════════════════════════════════════════════════
    // 충돌
    // ═════════════════════════════════════════════════════════════

    void OnCollisionEnter2D(Collision2D col)
    {
        // 패들 충돌
        if (col.gameObject.CompareTag("Paddle"))
        {
            HandlePaddleHit(col);
            SpawnFX(_paddleHitFX, transform.position);
            AudioManager.Instance?.PlaySFX(SFXType.BallPaddleHit);
            return;
        }

        // 벽 충돌
        if (col.gameObject.CompareTag("Wall"))
        {
            SpawnFX(_wallHitFX, col.contacts[0].point);
            AudioManager.Instance?.PlaySFX(SFXType.BallWallHit);
            return;
        }

        // 벽돌 충돌 (관통 모드에서는 물리 반사를 하지 않음)
        if (col.gameObject.CompareTag("Brick"))
        {
            if (IsPierce)
            {
                // 충돌 처리는 BrickController가 처리; 공은 계속 진행
                SpawnFX(_pierceLineFX, col.contacts[0].point);
                return;
            }
        }
    }

    /// <summary>
    /// 패들 위치에 따라 반사 각도를 계산한다.
    /// 패들 중앙에 맞을수록 직각, 끝에 맞을수록 더 비스듬히 반사.
    /// </summary>
    private void HandlePaddleHit(Collision2D col)
    {
        PaddleController paddle = col.gameObject.GetComponent<PaddleController>();
        if (paddle == null) return;

        float hitX    = col.contacts[0].point.x - col.transform.position.x;
        float halfW   = paddle.HalfWidth;
        float t       = Mathf.Clamp(hitX / halfW, -1f, 1f);  // -1 ~ +1

        // 각도: 중앙=90도, 끝=30도 (최소 30도 유지)
        float angle   = Mathf.Lerp(75f, 20f, Mathf.Abs(t));
        float xSign   = Mathf.Sign(t == 0 ? _rb.linearVelocity.x : t);
        float rad     = angle * Mathf.Deg2Rad;
        Vector2 newDir = new Vector2(xSign * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

        float speed = Mathf.Clamp(CurrentSpeed, _baseSpeed * _speedMult, _maxSpeed);
        _rb.linearVelocity = newDir * speed;

        // 강타 효과 (패들 끝)
        if (Mathf.Abs(t) > 0.8f)
            CameraShake.Instance?.Shake(0.08f, 0.15f);
    }

    private void CorrectFlatAngle()
    {
        Vector2 v = _rb.linearVelocity;
        if (v.sqrMagnitude < 0.01f) return;

        float angleDeg = Mathf.Abs(Vector2.Angle(v, Vector2.right));
        // 거의 수평(5도 이내)이면 강제로 각도 보정
        if (angleDeg < _minAngleDeg || angleDeg > 180f - _minAngleDeg)
        {
            float sign  = v.y >= 0 ? 1f : -1f;
            float newY  = Mathf.Sin(_minAngleDeg * Mathf.Deg2Rad) * v.magnitude * sign;
            float newX  = Mathf.Sqrt(Mathf.Max(0f, v.sqrMagnitude - newY * newY)) * Mathf.Sign(v.x);
            _rb.linearVelocity = new Vector2(newX, newY);
        }
    }

    // 화면 아래 이탈
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathZone"))
        {
            OnBallLost();
        }
    }

    private void OnBallLost()
    {
        IsLaunched = false;
        _rb.linearVelocity = Vector2.zero;
        BallManager.Instance?.OnBallLost(this);
    }

    // ═════════════════════════════════════════════════════════════
    // 강화 상태
    // ═════════════════════════════════════════════════════════════

    public void ActivatePierce(float duration = -1f)
    {
        if (_pierceCoroutine != null) StopCoroutine(_pierceCoroutine);
        _pierceCoroutine = StartCoroutine(PierceRoutine(duration < 0 ? _pierceDuration : duration));
    }

    private IEnumerator PierceRoutine(float duration)
    {
        IsPierce = true;
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Ball"), LayerMask.NameToLayer("Brick"), true);
        SetTrailColor(_pierceTrailColor);
        _col.isTrigger = false; // 벽, 패들은 계속 충돌
        yield return new WaitForSeconds(duration);
        IsPierce = false;
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Ball"), LayerMask.NameToLayer("Brick"), false);
        SetTrailColor(_normalTrailColor);
    }

    public void ActivateExplosive(float duration = -1f)
    {
        if (_explosiveCoroutine != null) StopCoroutine(_explosiveCoroutine);
        _explosiveCoroutine = StartCoroutine(ExplosiveRoutine(duration < 0 ? _explosiveDuration : duration));
    }

    private IEnumerator ExplosiveRoutine(float duration)
    {
        IsExplosive = true;
        SetTrailColor(_explosiveTrailColor);
        yield return new WaitForSeconds(duration);
        IsExplosive = false;
        SetTrailColor(_normalTrailColor);
    }

    public void SetSpeedMult(float mult, float duration = 8f)
    {
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(SpeedRoutine(mult, duration));
    }

    private IEnumerator SpeedRoutine(float mult, float duration)
    {
        float old = _speedMult;
        _speedMult = mult;
        // 현재 속도 즉시 반영
        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * (_baseSpeed * _speedMult * StageMult);
        yield return new WaitForSeconds(duration);
        _speedMult = old;
        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * (_baseSpeed * _speedMult * StageMult);
    }

    // ═════════════════════════════════════════════════════════════
    // 유틸
    // ═════════════════════════════════════════════════════════════

    private void SetTrailColor(Color c)
    {
        if (_trail == null) return;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = g;
    }

    private void SpawnFX(GameObject prefab, Vector3 pos)
    {
        if (prefab != null)
            EffectPool.Instance?.Spawn(prefab, pos, Quaternion.identity);
    }

    // 멀티볼 복제 시 사용
    public BallController Clone()
    {
        BallController clone = Instantiate(this, transform.position, Quaternion.identity);
        clone.StageMult  = StageMult;
        clone._speedMult = _speedMult;
        // 복제된 공은 약간 다른 방향으로 발사
        float angle = Random.Range(-30f, 30f);
        Vector2 dir = Quaternion.Euler(0, 0, angle) * _rb.linearVelocity.normalized;
        clone.IsLaunched = true;
        clone._rb.linearVelocity = dir * CurrentSpeed;
        return clone;
    }
}
