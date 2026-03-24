using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ══════════════════════════════════════════════════════════════════
// ItemDrop: 화면에서 낙하하는 아이템 오브젝트
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// 벽돌 파괴 시 드롭되어 낙하하는 아이템 캡슐.
/// 패들에 닿으면 효과를 발동하고 사라진다.
/// </summary>
public class ItemDrop : MonoBehaviour
{
    [SerializeField] SpriteRenderer _sprite;
    [SerializeField] TMPro.TextMeshPro _label;
    [SerializeField] float _fallSpeed = 3.5f;
    [SerializeField] float _lifetime  = 12f;

    public ItemType Type { get; private set; }
    private float _elapsed;

    public void Init(ItemType type)
    {
        Type = type;
        var def = ItemDatabase.Get(type);
        if (_sprite) _sprite.color  = def.Color;
        if (_label)  _label.text    = def.Icon;
        _elapsed = 0f;
    }

    void Update()
    {
        transform.Translate(Vector3.down * _fallSpeed * Time.deltaTime);
        _elapsed += Time.deltaTime;
        if (_elapsed >= _lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Paddle"))
        {
            ItemManager.Instance?.ActivateItem(Type);
            AudioManager.Instance?.PlaySFX(SFXType.ItemPickup);
            EffectPool.Instance?.Spawn(
                ItemDatabase.Get(Type).PickupFX,
                transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
        else if (other.CompareTag("DeathZone"))
        {
            Destroy(gameObject);
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// ItemManager: 아이템 효과 발동 및 지속시간 관리
// ══════════════════════════════════════════════════════════════════

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    [SerializeField] ItemDrop   _itemDropPrefab;
    [SerializeField] Transform  _itemContainer;
    [SerializeField] float      _dropChanceBase = 0.12f;

    // 활성 아이템 UI에 알리기 위한 이벤트
    public static event System.Action<ItemType, float> OnItemActivated;  // type, duration
    public static event System.Action<ItemType>        OnItemExpired;

    private PaddleController _paddle;
    private BallManager      _ballMgr;

    private Dictionary<ItemType, Coroutine> _activeEffects = new Dictionary<ItemType, Coroutine>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _paddle  = FindObjectOfType<PaddleController>();
        _ballMgr = BallManager.Instance;
    }

    // ═════════════════════════════════════════════════════════════
    // 드롭 생성
    // ═════════════════════════════════════════════════════════════

    public void TryDropItem(Vector3 pos, ItemFlags allowed)
    {
        if (Random.value > _dropChanceBase) return;

        // 허용된 아이템 중 랜덤 선택
        var candidates = GetCandidates(allowed);
        if (candidates.Count == 0) return;

        ItemType picked = candidates[Random.Range(0, candidates.Count)];
        SpawnDrop(pos, picked);
    }

    private List<ItemType> GetCandidates(ItemFlags allowed)
    {
        var list = new List<ItemType>();
        foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
        {
            ItemDef def = ItemDatabase.Get(t);
            if ((def.Flag & allowed) != 0)
                for (int w = 0; w < def.Weight; w++) list.Add(t); // 가중치
        }
        return list;
    }

    private void SpawnDrop(Vector3 pos, ItemType type)
    {
        var drop = Instantiate(_itemDropPrefab, pos, Quaternion.identity, _itemContainer);
        drop.Init(type);
    }

    // ═════════════════════════════════════════════════════════════
    // 아이템 효과 발동
    // ═════════════════════════════════════════════════════════════

    public void ActivateItem(ItemType type)
    {
        ItemDef def = ItemDatabase.Get(type);

        // 이미 활성 중이면 지속시간 연장
        if (_activeEffects.ContainsKey(type) && _activeEffects[type] != null)
        {
            StopCoroutine(_activeEffects[type]);
            _activeEffects.Remove(type);
        }

        ApplyEffect(type);
        OnItemActivated?.Invoke(type, def.Duration);

        if (def.Duration > 0f)
        {
            var co = StartCoroutine(ExpireAfter(type, def.Duration));
            _activeEffects[type] = co;
        }
    }

    private void ApplyEffect(ItemType type)
    {
        switch (type)
        {
            // 패들 관련
            case ItemType.PaddleGrow:
                _paddle?.GrowPaddle(0.8f, 10f);
                break;
            case ItemType.PaddleShrink:
                _paddle?.ShrinkPaddle(0.6f, 6f);
                break;
            case ItemType.LaserPaddle:
                _paddle?.ActivateLaser(10f);
                break;
            case ItemType.Shield:
                _paddle?.ActivateShield(15f);
                break;

            // 볼 관련
            case ItemType.MultiBall:
                _ballMgr?.SpawnMultiBalls(2);
                break;
            case ItemType.FastBall:
                foreach (var b in _ballMgr?.ActiveBalls ?? new List<BallController>())
                    b.SetSpeedMult(1.5f, 8f);
                break;
            case ItemType.SlowBall:
                foreach (var b in _ballMgr?.ActiveBalls ?? new List<BallController>())
                    b.SetSpeedMult(0.6f, 8f);
                break;
            case ItemType.PierceBall:
                foreach (var b in _ballMgr?.ActiveBalls ?? new List<BallController>())
                    b.ActivatePierce(8f);
                break;
            case ItemType.ExplosiveBall:
                foreach (var b in _ballMgr?.ActiveBalls ?? new List<BallController>())
                    b.ActivateExplosive(8f);
                break;

            // 코인 관련
            case ItemType.CoinMagnet:
                CoinFlyManager.Instance?.ActivateMagnet(12f);
                break;
            case ItemType.CoinShower:
                StartCoroutine(CoinShowerRoutine());
                break;

            // 공격 관련
            case ItemType.Drone:
                SatelliteManager.Instance?.SpawnDrone(10f);
                break;
            case ItemType.SlowMotion:
                StartCoroutine(SlowMotionRoutine(6f));
                break;
            case ItemType.Lightning:
                StartCoroutine(LightningRoutine());
                break;
            case ItemType.BlackHole:
                StartCoroutine(BlackHoleRoutine());
                break;
            case ItemType.Bombardment:
                StartCoroutine(BombardmentRoutine());
                break;
            case ItemType.Satellite:
                SatelliteManager.Instance?.SpawnSatellite(12f);
                break;
        }
    }

    private IEnumerator ExpireAfter(ItemType type, float duration)
    {
        yield return new WaitForSeconds(duration);
        _activeEffects.Remove(type);
        OnItemExpired?.Invoke(type);
    }

    // ── 특수 루틴 ─────────────────────────────────────────────────

    private IEnumerator CoinShowerRoutine()
    {
        AudioManager.Instance?.PlaySFX(SFXType.CoinShower);
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-7f, 7f), 6f, 0f);
            CoinFlyManager.Instance?.SpawnCoinFly(pos, Random.Range(3, 8));
            GameManager.Instance?.AddCoins(Random.Range(3, 8), false);
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator SlowMotionRoutine(float duration)
    {
        Time.timeScale   = 0.4f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale   = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator LightningRoutine()
    {
        AudioManager.Instance?.PlaySFX(SFXType.Lightning);
        CameraShake.Instance?.Shake(0.15f, 0.3f);

        var bricks = FindObjectsOfType<BrickController>();
        // 랜덤하게 5~8개 벽돌에 번개 타격
        int count = Mathf.Min(Random.Range(5, 9), bricks.Length);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, bricks.Length);
            if (bricks[idx] != null && !bricks[idx].IsDestroyed)
            {
                bricks[idx].TakeDamage(2);
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private IEnumerator BlackHoleRoutine()
    {
        // 화면 중앙에 블랙홀 생성 → 주변 벽돌 흡수 데미지
        Vector3 center  = Vector3.zero;
        float   radius  = 3.5f;
        float   elapsed = 0f;
        float   duration = 5f;

        AudioManager.Instance?.PlaySFX(SFXType.BlackHole);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var bricks = FindObjectsOfType<BrickController>();
            foreach (var b in bricks)
            {
                if (b == null || b.IsDestroyed) continue;
                if (Vector3.Distance(b.transform.position, center) < radius)
                    b.TakeDamage(1);
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator BombardmentRoutine()
    {
        AudioManager.Instance?.PlaySFX(SFXType.Bombardment);
        for (int i = 0; i < 10; i++)
        {
            // 랜덤 위치에 폭발
            Vector3 target = new Vector3(Random.Range(-6f, 6f), Random.Range(1f, 5f), 0f);
            BrickLayoutManager.Instance?.TriggerExplosion(target, 1.2f);
            yield return new WaitForSeconds(0.25f);
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// 아이템 데이터베이스
// ══════════════════════════════════════════════════════════════════

public enum ItemType
{
    PaddleGrow, PaddleShrink, MultiBall, FastBall, SlowBall,
    PierceBall, ExplosiveBall, CoinMagnet, CoinShower,
    Drone, LaserPaddle, Shield, SlowMotion,
    Lightning, BlackHole, Bombardment, Satellite
}

[System.Serializable]
public class ItemDef
{
    public ItemType  Type;
    public string    Name;
    public string    Icon;    // 텍스트 아이콘
    public Color     Color;
    public float     Duration;    // 0 = 즉시 효과
    public ItemFlags Flag;
    public int       Weight = 1;  // 드롭 가중치
    public GameObject PickupFX;
}

public static class ItemDatabase
{
    private static Dictionary<ItemType, ItemDef> _map;

    public static ItemDef Get(ItemType type)
    {
        if (_map == null) Build();
        return _map.TryGetValue(type, out var def) ? def : null;
    }

    private static void Build()
    {
        _map = new Dictionary<ItemType, ItemDef>
        {
            { ItemType.PaddleGrow,    new ItemDef { Type=ItemType.PaddleGrow,    Name="패들 확장",    Icon="⬛", Color=new Color(0.2f,0.8f,0.2f), Duration=10f, Flag=ItemFlags.PaddleGrow,   Weight=4 }},
            { ItemType.PaddleShrink,  new ItemDef { Type=ItemType.PaddleShrink,  Name="패들 축소",    Icon="▬",  Color=new Color(0.8f,0.2f,0.2f), Duration=8f,  Flag=ItemFlags.PaddleShrink, Weight=2 }},
            { ItemType.MultiBall,     new ItemDef { Type=ItemType.MultiBall,     Name="멀티볼",       Icon="⚫", Color=new Color(0.9f,0.9f,0.2f), Duration=0f,  Flag=ItemFlags.MultiBall,    Weight=3 }},
            { ItemType.FastBall,      new ItemDef { Type=ItemType.FastBall,      Name="빠른 공",      Icon="▶",  Color=new Color(1f,0.5f,0.1f),   Duration=8f,  Flag=ItemFlags.FastBall,     Weight=3 }},
            { ItemType.SlowBall,      new ItemDef { Type=ItemType.SlowBall,      Name="느린 공",      Icon="◀",  Color=new Color(0.4f,0.6f,1f),   Duration=8f,  Flag=ItemFlags.SlowBall,     Weight=3 }},
            { ItemType.PierceBall,    new ItemDef { Type=ItemType.PierceBall,    Name="관통볼",       Icon="→",  Color=new Color(0.2f,0.9f,1f),   Duration=8f,  Flag=ItemFlags.PierceBall,   Weight=2 }},
            { ItemType.ExplosiveBall, new ItemDef { Type=ItemType.ExplosiveBall, Name="폭발볼",       Icon="💥", Color=new Color(1f,0.3f,0.1f),   Duration=8f,  Flag=ItemFlags.Explosive,    Weight=2 }},
            { ItemType.CoinMagnet,    new ItemDef { Type=ItemType.CoinMagnet,    Name="코인 자석",    Icon="🧲", Color=new Color(1f,0.85f,0.1f),  Duration=12f, Flag=ItemFlags.CoinMagnet,   Weight=3 }},
            { ItemType.CoinShower,    new ItemDef { Type=ItemType.CoinShower,    Name="코인 샤워",    Icon="$",  Color=new Color(1f,0.9f,0.2f),   Duration=0f,  Flag=ItemFlags.CoinMagnet,   Weight=2 }},
            { ItemType.Drone,         new ItemDef { Type=ItemType.Drone,         Name="드론",         Icon="◈",  Color=new Color(0.7f,0.7f,0.9f), Duration=10f, Flag=ItemFlags.Drone,        Weight=2 }},
            { ItemType.LaserPaddle,   new ItemDef { Type=ItemType.LaserPaddle,   Name="레이저",       Icon="⚡", Color=new Color(0.9f,0.1f,0.9f), Duration=10f, Flag=ItemFlags.LaserPaddle,  Weight=2 }},
            { ItemType.Shield,        new ItemDef { Type=ItemType.Shield,        Name="보호막",       Icon="🛡",  Color=new Color(0.3f,0.5f,1f),   Duration=15f, Flag=ItemFlags.Shield,       Weight=2 }},
            { ItemType.SlowMotion,    new ItemDef { Type=ItemType.SlowMotion,    Name="슬로우",       Icon="⏱",  Color=new Color(0.6f,0.9f,1f),   Duration=6f,  Flag=ItemFlags.SlowMotion,   Weight=1 }},
            { ItemType.Lightning,     new ItemDef { Type=ItemType.Lightning,     Name="번개",         Icon="⚡", Color=new Color(1f,1f,0.2f),     Duration=0f,  Flag=ItemFlags.Lightning,    Weight=1 }},
            { ItemType.BlackHole,     new ItemDef { Type=ItemType.BlackHole,     Name="블랙홀",       Icon="●",  Color=new Color(0.1f,0f,0.2f),   Duration=5f,  Flag=ItemFlags.BlackHole,    Weight=1 }},
            { ItemType.Bombardment,   new ItemDef { Type=ItemType.Bombardment,   Name="폭격",         Icon="✦",  Color=new Color(0.8f,0.4f,0.1f), Duration=0f,  Flag=ItemFlags.Bombardment,  Weight=1 }},
            { ItemType.Satellite,     new ItemDef { Type=ItemType.Satellite,     Name="위성",         Icon="◎",  Color=new Color(0.7f,0.9f,0.7f), Duration=12f, Flag=ItemFlags.Satellite,    Weight=2 }},
        };
    }
}
