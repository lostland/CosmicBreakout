using UnityEngine;
using System.Collections;

/// <summary>
/// 터치/마우스 드래그로 패들을 제어한다.
/// 패들 길이 변화(아이템), 레이저 발사, 보호막 표시를 담당한다.
/// </summary>
public class PaddleController : MonoBehaviour
{
    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] float _moveSmoothing = 18f;   // 이동 보간 속도
    [SerializeField] float _leftBound     = -7.5f;
    [SerializeField] float _rightBound    =  7.5f;

    [Header("Size")]
    [SerializeField] float _baseWidth     = 2.4f;
    [SerializeField] float _minWidth      = 1.2f;
    [SerializeField] float _maxWidth      = 4.8f;
    [SerializeField] float _sizeChangeDur = 0.25f; // 크기 변화 트위닝 시간

    [Header("Laser")]
    [SerializeField] GameObject _laserPrefab;
    [SerializeField] Transform  _laserSpawnL;
    [SerializeField] Transform  _laserSpawnR;
    [SerializeField] float      _laserInterval = 0.4f;

    [Header("Shield")]
    [SerializeField] GameObject _shieldObject;

    [Header("FX")]
    [SerializeField] ParticleSystem _growFX;
    [SerializeField] ParticleSystem _shrinkFX;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────
    public float HalfWidth => transform.localScale.x * _baseWidth * 0.5f;
    public bool  HasShield { get; private set; }

    // ── 내부 ─────────────────────────────────────────────────────
    private float  _targetX;
    private float  _touchStartX;
    private float  _paddleStartX;
    private bool   _isDragging;
    private Camera _cam;
    private Coroutine _laserCoroutine;
    private Coroutine _sizeCoroutine;
    private float  _currentWidth;

    // ═════════════════════════════════════════════════════════════
    void Awake()
    {
        _cam          = Camera.main;
        _targetX      = transform.position.x;
        _currentWidth = _baseWidth;
        SetWidth(_baseWidth, instant: true);
    }

    void Update()
    {
        HandleInput();
        SmoothMove();
    }

    // ═════════════════════════════════════════════════════════════
    // 입력 처리
    // ═════════════════════════════════════════════════════════════

    private void HandleInput()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _touchStartX  = GetWorldX(Input.mousePosition);
            _paddleStartX = transform.position.x;
            _isDragging   = true;
            TryLaunchBall();
        }
        if (Input.GetMouseButton(0) && _isDragging)
        {
            float delta = GetWorldX(Input.mousePosition) - _touchStartX;
            _targetX    = Mathf.Clamp(_paddleStartX + delta, _leftBound, _rightBound);
        }
        if (Input.GetMouseButtonUp(0)) _isDragging = false;
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);

        switch (t.phase)
        {
            case TouchPhase.Began:
                _touchStartX  = GetWorldX(t.position);
                _paddleStartX = transform.position.x;
                _isDragging   = true;
                TryLaunchBall();
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (_isDragging)
                {
                    float delta = GetWorldX(t.position) - _touchStartX;
                    _targetX    = Mathf.Clamp(_paddleStartX + delta, _leftBound, _rightBound);
                }
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                _isDragging = false;
                break;
        }
    }

    private float GetWorldX(Vector3 screenPos)
    {
        return _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -_cam.transform.position.z)).x;
    }

    private void TryLaunchBall()
    {
        BallManager.Instance?.TryLaunch();
    }

    private void SmoothMove()
    {
        float newX = Mathf.Lerp(transform.position.x, _targetX, Time.deltaTime * _moveSmoothing);
        transform.position = new Vector3(newX, transform.position.y, 0f);
    }

    // ═════════════════════════════════════════════════════════════
    // 아이템 효과: 크기 변화
    // ═════════════════════════════════════════════════════════════

    public void SetWidth(float width, bool instant = false)
    {
        width = Mathf.Clamp(width, _minWidth, _maxWidth);
        if (_sizeCoroutine != null) StopCoroutine(_sizeCoroutine);

        if (instant)
        {
            _currentWidth = width;
            ApplyWidth(width);
        }
        else
        {
            _sizeCoroutine = StartCoroutine(SizeRoutine(width));
        }
    }

    public void GrowPaddle(float amount = 0.8f, float duration = 10f)
    {
        float newW = Mathf.Min(_currentWidth + amount, _maxWidth);
        SetWidth(newW);
        _growFX?.Play();
        StartCoroutine(RevertSizeAfter(newW, _currentWidth, duration));
    }

    public void ShrinkPaddle(float amount = 0.8f, float duration = 8f)
    {
        float newW = Mathf.Max(_currentWidth - amount, _minWidth);
        SetWidth(newW);
        _shrinkFX?.Play();
        StartCoroutine(RevertSizeAfter(newW, _currentWidth, duration));
    }

    private IEnumerator SizeRoutine(float targetWidth)
    {
        float startW = _currentWidth;
        float elapsed = 0f;
        while (elapsed < _sizeChangeDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _sizeChangeDur;
            // 오버슈트 느낌의 이징
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            ApplyWidth(Mathf.Lerp(startW, targetWidth, eased));
            yield return null;
        }
        _currentWidth = targetWidth;
        ApplyWidth(targetWidth);
    }

    private IEnumerator RevertSizeAfter(float fromW, float toW, float delay)
    {
        yield return new WaitForSeconds(delay);
        SetWidth(toW);
    }

    private void ApplyWidth(float width)
    {
        float scale = width / _baseWidth;
        transform.localScale = new Vector3(scale, transform.localScale.y, 1f);
    }

    // ═════════════════════════════════════════════════════════════
    // 아이템 효과: 레이저
    // ═════════════════════════════════════════════════════════════

    public void ActivateLaser(float duration = 10f)
    {
        if (_laserCoroutine != null) StopCoroutine(_laserCoroutine);
        _laserCoroutine = StartCoroutine(LaserRoutine(duration));
    }

    private IEnumerator LaserRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            FireLaser();
            yield return new WaitForSeconds(_laserInterval);
            elapsed += _laserInterval;
        }
    }

    private void FireLaser()
    {
        if (_laserPrefab == null) return;
        if (_laserSpawnL) EffectPool.Instance?.Spawn(_laserPrefab, _laserSpawnL.position, Quaternion.identity);
        if (_laserSpawnR) EffectPool.Instance?.Spawn(_laserPrefab, _laserSpawnR.position, Quaternion.identity);
        AudioManager.Instance?.PlaySFX(SFXType.Laser);
    }

    // ═════════════════════════════════════════════════════════════
    // 아이템 효과: 보호막
    // ═════════════════════════════════════════════════════════════

    public void ActivateShield(float duration = 15f)
    {
        HasShield = true;
        if (_shieldObject) _shieldObject.SetActive(true);
        StartCoroutine(ShieldRoutine(duration));
    }

    private IEnumerator ShieldRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        HasShield = false;
        if (_shieldObject) _shieldObject.SetActive(false);
    }

    /// <summary>보호막이 있으면 공이 패들 아래로 내려가도 한 번 막아준다.</summary>
    public bool ConsumeShield()
    {
        if (!HasShield) return false;
        HasShield = false;
        if (_shieldObject) _shieldObject.SetActive(false);
        AudioManager.Instance?.PlaySFX(SFXType.ShieldBreak);
        return true;
    }
}
