using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 레벨 시작 시 BrickPattern에 따라 벽돌을 배치하고,
/// 폭발/전기 체인, 분열, 클리어 감지를 담당한다.
/// </summary>
public class BrickLayoutManager : MonoBehaviour
{
    public static BrickLayoutManager Instance { get; private set; }

    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] BrickController _brickPrefab;
    [SerializeField] BrickController _bossCorePrefab;
    [SerializeField] GameObject      _explosionChainFX;
    [SerializeField] GameObject      _electricChainFX;
    [SerializeField] GameObject      _splitPiecePrefab;

    [Header("Layout")]
    [SerializeField] float _brickWidth    = 1.1f;
    [SerializeField] float _brickHeight   = 0.5f;
    [SerializeField] float _brickPadX     = 0.08f;
    [SerializeField] float _brickPadY     = 0.08f;
    [SerializeField] float _layoutTopY    = 4.5f;   // 가장 위 행 y 좌표
    [SerializeField] float _layoutCenterX = 0f;

    // ── 상태 ──────────────────────────────────────────────────────
    private List<BrickController> _bricks       = new List<BrickController>();
    private int                   _activeBricks = 0;
    private StageData             _stageData;
    private LevelData             _levelData;

    // 보스 관련
    private BrickController _bossCore;
    private int             _bossPhase = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ═════════════════════════════════════════════════════════════
    // 레이아웃 생성
    // ═════════════════════════════════════════════════════════════

    public void BuildLayout(int stageIndex, int levelIndex)
    {
        ClearAll();
        _stageData = StageDatabase.GetStage(stageIndex);
        _levelData = StageDatabase.GetLevel(stageIndex, levelIndex);

        switch (_levelData.BrickPattern)
        {
            case BrickPattern.Simple:      BuildSimple();      break;
            case BrickPattern.Checkers:    BuildCheckers();    break;
            case BrickPattern.Diamond:     BuildDiamond();     break;
            case BrickPattern.Fortress:    BuildFortress();    break;
            case BrickPattern.Boss:        BuildBoss();        break;
            case BrickPattern.Orbit:       BuildOrbit();       break;
            case BrickPattern.Asteroid:    BuildAsteroid();    break;
            case BrickPattern.Alien:       BuildAlien();       break;
            case BrickPattern.Binary:      BuildBinary();      break;
            case BrickPattern.Nebula:      BuildNebula();      break;
            case BrickPattern.Gravity:     BuildGravity();     break;
            case BrickPattern.Vortex:      BuildVortex();      break;
            case BrickPattern.Pulsar:      BuildPulsar();      break;
            case BrickPattern.Dimensional: BuildDimensional(); break;
            case BrickPattern.Warp:        BuildWarp();        break;
            case BrickPattern.Fractal:     BuildFractal();     break;
            default:                       BuildSimple();      break;
        }

        _activeBricks = _bricks.Count;
    }

    // ── 기본 패턴 ─────────────────────────────────────────────────

    private void BuildSimple()
    {
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
    }

    private void BuildCheckers()
    {
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if ((r + c) % 2 == 0)
                    SpawnBrick(r, c, cols, GetHpForRow(r) + ((r + c) % 3 == 0 ? 1 : 0), GetTypeForRow(r, rows));
    }

    private void BuildDiamond()
    {
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        int mid  = cols / 2;
        for (int r = 0; r < rows; r++)
        {
            int spread = Mathf.RoundToInt(mid * (1f - Mathf.Abs((float)r / rows - 0.5f) * 2f));
            for (int c = mid - spread; c <= mid + spread; c++)
                SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
        }
    }

    private void BuildFortress()
    {
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool edge = (r == 0 || r == rows - 1 || c == 0 || c == cols - 1);
                int hp = edge ? GetHpForRow(r) + 1 : GetHpForRow(r);
                BrickController.BrickType t = edge ? BrickController.BrickType.Armored : GetTypeForRow(r, rows);
                SpawnBrick(r, c, cols, hp, t);
            }
    }

    private void BuildBoss()
    {
        BuildFortress(); // 외벽 + 보스 코어
        // 중앙에 보스 코어 배치
        Vector3 bossPos = new Vector3(_layoutCenterX, _layoutTopY - 2f, 0f);
        if (_bossCorePrefab != null)
        {
            _bossCore = Instantiate(_bossCorePrefab, bossPos, Quaternion.identity, transform);
            _bossCore.Init(BrickController.BrickType.BossCore, 20,
                           _stageData.PrimaryColor,
                           _stageData.WaveAmplitude,
                           _stageData.WaveFrequency, 0f);
            _bricks.Add(_bossCore);
        }
    }

    private void BuildOrbit()
    {
        int cols = _levelData.BrickCols;
        int rows = _levelData.BrickRows;
        // 동심원형 배치
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float dist = Mathf.Sqrt(Mathf.Pow(c - cols/2f, 2) + Mathf.Pow(r - rows/2f, 2));
                if (dist < rows * 0.5f)
                    SpawnBrick(r, c, cols, Mathf.Max(1, Mathf.CeilToInt(dist * 0.4f)), GetTypeForRow(r, rows));
            }
    }

    private void BuildAsteroid()
    {
        // 불규칙 배치로 소행성대 느낌
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (Random.value < 0.65f)
                    SpawnBrick(r, c, cols, Random.Range(1, 3), GetTypeForRow(r, rows));
    }

    private void BuildAlien()
    {
        // 물결 형태
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
        {
            int offset = Mathf.RoundToInt(Mathf.Sin(r * 1.2f) * 2);
            for (int c = 0; c < cols; c++)
            {
                int cc = (c + offset + cols) % cols;
                SpawnBrick(r, cc, cols, GetHpForRow(r), GetTypeForRow(r, rows));
            }
        }
    }

    private void BuildBinary()
    {
        // 이진 패턴 (비트 연산)
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (((r * cols + c) & 3) != 0)
                    SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
    }

    private void BuildNebula()
    {
        // 가우시안 분포 느낌 (중앙 밀집)
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float nx = (c - cols/2f) / (cols/2f);
                float ny = (r - rows/2f) / (rows/2f);
                float d  = nx*nx + ny*ny;
                if (Random.value > d * 0.8f)
                    SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
            }
    }

    private void BuildGravity()
    {
        BuildSimple();
        // 중력장 벽돌을 격자마다 삽입
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 1; r < rows; r += 3)
            for (int c = 1; c < cols; c += 4)
                SpawnBrick(r, c, cols, 2, BrickController.BrickType.GravityWell, overwrite: true);
    }

    private void BuildVortex()
    {
        // 나선형 배치
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float angle = Mathf.Atan2(r - rows/2f, c - cols/2f);
                float dist  = Mathf.Sqrt(Mathf.Pow(c - cols/2f, 2) + Mathf.Pow(r - rows/2f, 2));
                float spiral = (angle + dist * 0.8f) % (2 * Mathf.PI);
                if (spiral < Mathf.PI * 0.9f)
                    SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
            }
    }

    private void BuildPulsar()
    {
        // 방사형 + 전기 벽돌
        BuildOrbit();
        int cols = _levelData.BrickCols;
        int rows = _levelData.BrickRows;
        for (int r = 0; r < rows; r += 2)
            SpawnBrick(r, cols/2, cols, 2, BrickController.BrickType.Electric, overwrite: true);
    }

    private void BuildDimensional()
    {
        // 격자 내 무작위 공백 + 이동 벽돌
        BuildSimple();
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r += 2)
            for (int c = 0; c < cols; c += 3)
                SpawnBrick(r, c, cols, 2, BrickController.BrickType.Moving, overwrite: true);
    }

    private void BuildWarp()
    {
        BuildCheckers();
        // 재생 벽돌 추가
        int cols = _levelData.BrickCols;
        int rows = _levelData.BrickRows;
        for (int r = 1; r < rows; r += 4)
            SpawnBrick(r, cols/2, cols, 3, BrickController.BrickType.Regenerator, overwrite: true);
    }

    private void BuildFractal()
    {
        // 프랙탈 X 패턴
        int rows = _levelData.BrickRows;
        int cols = _levelData.BrickCols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int cr = r % (rows / 2 == 0 ? 2 : rows);
                int cc = c % (cols / 2 == 0 ? 2 : cols);
                if (cr == cc || cr + cc == Mathf.Max(cr, cc))
                    SpawnBrick(r, c, cols, GetHpForRow(r), GetTypeForRow(r, rows));
            }
    }

    // ═════════════════════════════════════════════════════════════
    // 벽돌 생성 헬퍼
    // ═════════════════════════════════════════════════════════════

    private void SpawnBrick(int row, int col, int totalCols, int hp,
                             BrickController.BrickType type = BrickController.BrickType.Normal,
                             bool overwrite = false)
    {
        float stepX  = _brickWidth  + _brickPadX;
        float stepY  = _brickHeight + _brickPadY;
        float startX = _layoutCenterX - (totalCols - 1) * stepX * 0.5f;
        Vector3 pos  = new Vector3(startX + col * stepX, _layoutTopY - row * stepY, 0f);

        // 이미 해당 위치에 벽돌이 있으면 overwrite 처리
        if (overwrite)
        {
            // 기존 벽돌 제거
            var existing = _bricks.Find(b => b != null && Vector3.Distance(b.transform.position, pos) < 0.1f);
            if (existing != null) { _bricks.Remove(existing); Destroy(existing.gameObject); }
        }

        float phase = (row * totalCols + col) * 0.31f;  // 자연스러운 위상 차이
        Color col2  = GetBrickColor(type, row, _levelData.BrickRows);

        BrickController brick = Instantiate(_brickPrefab, pos, Quaternion.identity, transform);
        brick.Init(type, hp, col2, _stageData.WaveAmplitude, _stageData.WaveFrequency, phase);
        _bricks.Add(brick);
    }

    private int GetHpForRow(int row)
    {
        // 위 행일수록 HP 높음 + 레벨에 따라 증가
        int baseHp = Mathf.Max(1, _levelData.BrickRows - row);
        return Mathf.Clamp(baseHp, 1, 4);
    }

    private BrickController.BrickType GetTypeForRow(int row, int totalRows)
    {
        float normalizedRow = (float)row / totalRows;
        int specialCount = 0;
        foreach (var b in _bricks)
            if (b != null && b.Type != BrickController.BrickType.Normal) specialCount++;
        if (specialCount >= _levelData.MaxSpecialBricks)
            return BrickController.BrickType.Normal;

        // 위쪽 행에 특수 벽돌 집중
        if (normalizedRow < 0.2f && Random.value < 0.35f)
        {
            var specials = GetAvailableSpecialTypes();
            if (specials.Count > 0) return specials[Random.Range(0, specials.Count)];
        }
        return BrickController.BrickType.Normal;
    }

    private List<BrickController.BrickType> GetAvailableSpecialTypes()
    {
        var list = new List<BrickController.BrickType>();
        int stage = GameManager.Instance?.CurrentStageIndex ?? 0;
        int level = GameManager.Instance?.CurrentLevelIndex ?? 0;

        // 스테이지/레벨이 높을수록 더 다양한 특수 벽돌 해금
        if (stage >= 0) { list.Add(BrickController.BrickType.Armored); list.Add(BrickController.BrickType.Coin); }
        if (stage >= 0 && level >= 2) list.Add(BrickController.BrickType.Explosive);
        if (stage >= 1) list.Add(BrickController.BrickType.Moving);
        if (stage >= 1 && level >= 1) list.Add(BrickController.BrickType.Shielded);
        if (stage >= 1 && level >= 3) list.Add(BrickController.BrickType.Ice);
        if (stage >= 2) list.Add(BrickController.BrickType.Electric);
        if (stage >= 2 && level >= 1) list.Add(BrickController.BrickType.GravityWell);
        if (stage >= 2 && level >= 2) list.Add(BrickController.BrickType.Splitter);
        if (stage >= 3) list.Add(BrickController.BrickType.Regenerator);
        if (stage >= 3 && level >= 2) list.Add(BrickController.BrickType.Fire);
        return list;
    }

    private Color GetBrickColor(BrickController.BrickType type, int row, int totalRows)
    {
        Color stageColor = Color.Lerp(_stageData.PrimaryColor, _stageData.SecondaryColor,
                                       (float)row / totalRows);
        return type switch
        {
            BrickController.BrickType.Armored    => Color.Lerp(stageColor, Color.gray, 0.5f),
            BrickController.BrickType.Explosive  => Color.Lerp(stageColor, new Color(1f,0.3f,0f), 0.6f),
            BrickController.BrickType.Coin       => Color.Lerp(stageColor, new Color(1f,0.9f,0f), 0.7f),
            BrickController.BrickType.GravityWell=> Color.Lerp(stageColor, Color.black, 0.4f),
            BrickController.BrickType.Electric   => Color.Lerp(stageColor, Color.cyan, 0.5f),
            BrickController.BrickType.Ice        => Color.Lerp(stageColor, Color.blue, 0.5f),
            BrickController.BrickType.Regenerator=> Color.Lerp(stageColor, Color.green, 0.5f),
            _                                    => stageColor,
        };
    }

    // ═════════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═════════════════════════════════════════════════════════════

    public void OnBrickDestroyed(BrickController brick)
    {
        _bricks.Remove(brick);
        _activeBricks = _bricks.FindAll(b => b != null && !b.IsDestroyed).Count;

        if (_activeBricks <= 0)
            GameManager.Instance?.TriggerLevelClear();
    }

    public void TriggerExplosion(Vector3 center, float radius)
    {
        StartCoroutine(ExplosionChain(center, radius));
    }

    private IEnumerator ExplosionChain(Vector3 center, float radius)
    {
        EffectPool.Instance?.Spawn(_explosionChainFX, center, Quaternion.identity);
        AudioManager.Instance?.PlaySFX(SFXType.BrickExplode);
        CameraShake.Instance?.Shake(0.12f, 0.25f);

        var hits = new List<BrickController>(_bricks);
        foreach (var b in hits)
        {
            if (b == null || b.IsDestroyed) continue;
            if (Vector3.Distance(b.transform.position, center) <= radius)
            {
                yield return new WaitForSeconds(0.05f);
                b.TakeDamage(1, isExplosive: true);
            }
        }
    }

    public void TriggerElectricChain(Vector3 center, float radius)
    {
        StartCoroutine(ElectricChain(center, radius));
    }

    private IEnumerator ElectricChain(Vector3 center, float radius)
    {
        EffectPool.Instance?.Spawn(_electricChainFX, center, Quaternion.identity);
        AudioManager.Instance?.PlaySFX(SFXType.BrickElectricHit);

        var hits = new List<BrickController>(_bricks);
        foreach (var b in hits)
        {
            if (b == null || b.IsDestroyed) continue;
            if (Vector3.Distance(b.transform.position, center) <= radius)
            {
                yield return new WaitForSeconds(0.03f);
                b.TakeDamage(1);
                EffectPool.Instance?.Spawn(_electricChainFX, b.transform.position, Quaternion.identity);
            }
        }
    }

    public void SpawnSplitterPieces(Vector3 pos)
    {
        if (_splitPiecePrefab == null) return;
        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.3f, 0.3f), 0, 0);
            var piece = Instantiate(_splitPiecePrefab, pos + offset, Quaternion.identity, transform);
            var bc    = piece.GetComponent<BrickController>();
            if (bc) bc.Init(BrickController.BrickType.Normal, 1, Color.white,
                            _stageData.WaveAmplitude * 0.5f, _stageData.WaveFrequency, Random.value);
            _bricks.Add(bc);
            _activeBricks++;
        }
    }

    public void OnBossCoreDestroyed(BrickController core)
    {
        _bossPhase++;
        if (_bossPhase < 3)
        {
            // 보스 2단계: 벽돌 재소환
            StartCoroutine(BossNextPhase());
        }
        else
        {
            // 진짜 클리어
            GameManager.Instance?.TriggerLevelClear();
        }
    }

    private IEnumerator BossNextPhase()
    {
        CameraShake.Instance?.Shake(0.3f, 0.5f);
        yield return new WaitForSeconds(1.0f);
        // 새 외벽 소환 (간략화)
        BuildFortress();
    }

    public void ClearAll()
    {
        foreach (var b in _bricks)
            if (b != null) Destroy(b.gameObject);
        _bricks.Clear();
        _activeBricks = 0;
        _bossCore  = null;
        _bossPhase = 0;
    }

    public int ActiveBrickCount => _activeBricks;
}
