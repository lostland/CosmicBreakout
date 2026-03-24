using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 위성(Satellite) 아이템: 공 좌우에 미니 위성을 배치해 자동 반사 공격.
/// 드론(Drone) 아이템: 화면 상단을 자율 이동하며 레이저 사격.
/// </summary>
public class SatelliteManager : MonoBehaviour
{
    public static SatelliteManager Instance { get; private set; }

    [Header("Satellite")]
    [SerializeField] GameObject _satellitePrefab;
    [SerializeField] float      _satelliteOrbitRadius = 0.8f;
    [SerializeField] float      _satelliteOrbitSpeed  = 180f;  // deg/s

    [Header("Drone")]
    [SerializeField] GameObject _dronePrefab;
    [SerializeField] float      _droneSpeed           = 4f;
    [SerializeField] float      _droneFireInterval    = 0.6f;
    [SerializeField] GameObject _droneLaserPrefab;

    private List<GameObject>   _satellites = new List<GameObject>();
    private List<GameObject>   _drones     = new List<GameObject>();
    private BallController     _trackedBall;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ═════════════════════════════════════════════════════════════
    // 위성
    // ═════════════════════════════════════════════════════════════

    public void SpawnSatellite(float duration)
    {
        if (_satellitePrefab == null) return;

        // 기존 위성 제거 후 재생성
        ClearSatellites();

        // 주 공 추적
        _trackedBall = BallManager.Instance?.ActiveBalls?.Count > 0
                       ? BallManager.Instance.ActiveBalls[0] : null;

        for (int i = 0; i < 2; i++)
        {
            var sat = Instantiate(_satellitePrefab, transform);
            _satellites.Add(sat);
        }

        StartCoroutine(SatelliteRoutine(duration));
        AudioManager.Instance?.PlaySFX(SFXType.ItemPickup);
    }

    private IEnumerator SatelliteRoutine(float duration)
    {
        float elapsed = 0f;
        float angle   = 0f;

        while (elapsed < duration && _satellites.Count > 0)
        {
            elapsed += Time.deltaTime;
            angle   += _satelliteOrbitSpeed * Time.deltaTime;

            // 추적 대상 갱신
            if (_trackedBall == null || !_trackedBall.IsLaunched)
            {
                var balls = BallManager.Instance?.ActiveBalls;
                _trackedBall = balls != null && balls.Count > 0 ? balls[0] : null;
            }

            Vector3 center = _trackedBall != null
                             ? _trackedBall.transform.position
                             : Vector3.zero;

            for (int i = 0; i < _satellites.Count; i++)
            {
                if (_satellites[i] == null) continue;
                float a = (angle + i * 180f) * Mathf.Deg2Rad;
                _satellites[i].transform.position =
                    center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * _satelliteOrbitRadius;
            }

            yield return null;
        }

        ClearSatellites();
    }

    private void ClearSatellites()
    {
        foreach (var s in _satellites) if (s) Destroy(s);
        _satellites.Clear();
    }

    // ═════════════════════════════════════════════════════════════
    // 드론
    // ═════════════════════════════════════════════════════════════

    public void SpawnDrone(float duration)
    {
        if (_dronePrefab == null) return;
        ClearDrones();

        var drone = Instantiate(_dronePrefab, new Vector3(-6f, 4f, 0f), Quaternion.identity, transform);
        _drones.Add(drone);
        StartCoroutine(DroneRoutine(drone, duration));
    }

    private IEnumerator DroneRoutine(GameObject drone, float duration)
    {
        float elapsed      = 0f;
        float fireTimer    = 0f;
        float dirX         = 1f;
        float leftBound    = -6f;
        float rightBound   =  6f;

        while (elapsed < duration && drone != null)
        {
            elapsed   += Time.deltaTime;
            fireTimer += Time.deltaTime;

            // 좌우 이동
            float newX = drone.transform.position.x + dirX * _droneSpeed * Time.deltaTime;
            if (newX > rightBound)  { newX = rightBound;  dirX = -1f; }
            if (newX < leftBound)   { newX = leftBound;   dirX =  1f; }
            drone.transform.position = new Vector3(newX, drone.transform.position.y, 0f);

            // 가장 가까운 벽돌 향해 레이저 발사
            if (fireTimer >= _droneFireInterval)
            {
                fireTimer = 0f;
                FireDroneLaser(drone.transform.position);
            }

            yield return null;
        }

        if (drone) Destroy(drone);
        _drones.Remove(drone);
    }

    private void FireDroneLaser(Vector3 from)
    {
        if (_droneLaserPrefab == null) return;

        // 가장 가까운 파괴 가능한 벽돌 찾기
        BrickController target  = null;
        float           minDist = float.MaxValue;
        var bricks = FindObjectsOfType<BrickController>();
        foreach (var b in bricks)
        {
            if (b == null || b.IsDestroyed) continue;
            float d = Vector3.Distance(from, b.transform.position);
            if (d < minDist) { minDist = d; target = b; }
        }

        if (target == null) return;

        // 레이저 이펙트 스폰
        var laser = EffectPool.Instance?.Spawn(_droneLaserPrefab, from, Quaternion.identity);
        if (laser != null)
        {
            // 레이저를 목표 방향으로 회전
            Vector3 dir = (target.transform.position - from).normalized;
            laser.transform.up = dir;
        }

        target.TakeDamage(1);
        AudioManager.Instance?.PlaySFX(SFXType.Laser);
    }

    private void ClearDrones()
    {
        foreach (var d in _drones) if (d) Destroy(d);
        _drones.Clear();
    }
}
