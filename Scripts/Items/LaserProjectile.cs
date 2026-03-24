using UnityEngine;

/// <summary>
/// 레이저 투사체. 생성 방향으로 고속 이동하며 벽돌에 닿으면 데미지를 주고 사라진다.
/// 패들 레이저와 드론 레이저 모두 이 컴포넌트를 공유한다.
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    [SerializeField] float _speed      = 20f;
    [SerializeField] int   _damage     = 1;
    [SerializeField] bool  _pierce     = false;   // 관통 여부
    [SerializeField] float _lifetime   = 3f;

    private float _elapsed;
    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        _elapsed = 0f;
        if (_rb)
        {
            _rb.gravityScale = 0f;
            _rb.velocity     = transform.up * _speed;
        }
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _lifetime)
            ReturnToPool();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        BrickController brick = other.GetComponent<BrickController>();
        if (brick != null && !brick.IsDestroyed)
        {
            brick.TakeDamage(_damage);
            if (!_pierce) ReturnToPool();
            return;
        }

        // 천장/벽에 닿으면 사라짐
        if (HasTag(other.gameObject, "Wall") || HasTag(other.gameObject, "Ceiling"))
            ReturnToPool();
    }

    private bool HasTag(GameObject obj, string tagName)
    {
        return obj != null && obj.tag == tagName;
    }

    private void ReturnToPool()
    {
        // AutoReturn이 없으면 직접 비활성화
        gameObject.SetActive(false);
    }
}
