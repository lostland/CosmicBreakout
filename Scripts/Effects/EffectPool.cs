using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 이펙트 프리팹을 오브젝트 풀로 관리한다.
/// Instantiate/Destroy 반복 없이 Activate/Deactivate 재활용.
/// </summary>
public class EffectPool : MonoBehaviour
{
    public static EffectPool Instance { get; private set; }

    [System.Serializable]
    public class PoolEntry
    {
        public GameObject Prefab;
        public int        InitialCount = 10;
    }

    [SerializeField] List<PoolEntry> _entries;

    private Dictionary<GameObject, Queue<GameObject>> _pools
        = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var entry in _entries)
            PreWarm(entry.Prefab, entry.InitialCount);
    }

    private void PreWarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            _pools[prefab].Enqueue(go);
        }
    }

    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;
        if (!_pools.ContainsKey(prefab))
            PreWarm(prefab, 5);

        GameObject go;
        if (_pools[prefab].Count > 0)
        {
            go = _pools[prefab].Dequeue();
        }
        else
        {
            go = Instantiate(prefab, transform);
        }

        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);

        // AutoReturn 컴포넌트가 있으면 자동 반환
        var ar = go.GetComponent<AutoReturn>();
        if (ar != null) ar.Init(prefab, this);

        return go;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;
        instance.SetActive(false);
        instance.transform.SetParent(transform);
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();
        _pools[prefab].Enqueue(instance);
    }
}

/// <summary>파티클이 끝나면 자동으로 풀에 반환하는 컴포넌트</summary>
public class AutoReturn : MonoBehaviour
{
    private GameObject   _prefab;
    private EffectPool   _pool;
    private ParticleSystem _ps;
    private float        _lifetime = 2f;

    public void Init(GameObject prefab, EffectPool pool)
    {
        _prefab   = prefab;
        _pool     = pool;
        _ps       = GetComponent<ParticleSystem>();
        _lifetime = _ps ? _ps.main.duration + _ps.main.startLifetime.constantMax : 2f;
        StartCoroutine(ReturnAfter(_lifetime));
    }

    private IEnumerator ReturnAfter(float t)
    {
        yield return new WaitForSeconds(t);
        _pool?.Return(_prefab, gameObject);
    }

    void OnDisable() { StopAllCoroutines(); }
}

// ══════════════════════════════════════════════════════════════════
// 카메라 쉐이크
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// 카메라에 붙이는 쉐이크 컴포넌트.
/// 타격감 있는 작은 흔들림을 제공한다.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3    _originPos;
    private Coroutine  _shakeCo;

    void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance    = this;
        _originPos  = transform.localPosition;
    }

    /// <summary>
    /// 쉐이크 발동.
    /// intensity: 픽셀 진폭 (0.1 = 매우 작음, 0.5 = 보통)
    /// duration:  초
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - elapsed / duration;   // 선형 감쇠
            float x = Random.Range(-1f, 1f) * intensity * t;
            float y = Random.Range(-1f, 1f) * intensity * t;
            transform.localPosition = _originPos + new Vector3(x, y, 0f);
            yield return null;
        }
        transform.localPosition = _originPos;
    }
}
