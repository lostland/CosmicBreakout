using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 5개 큰 스테이지(지구~다른 은하)와 각 5개 레벨의 설정 데이터를 보관한다.
/// ScriptableObject 없이 코드로 정의해 즉시 참조 가능하게 구성.
/// </summary>
public static class StageDatabase
{
    public const int StageCount = 5;
    public const int LevelsPerStage = 5;

    private static StageData[] _stages;

    public static StageData[] Stages
    {
        get
        {
            if (_stages == null) Build();
            return _stages;
        }
    }

    public static StageData GetStage(int index) => Stages[Mathf.Clamp(index, 0, StageCount - 1)];
    public static LevelData  GetLevel(int stageIndex, int levelIndex)
        => GetStage(stageIndex).Levels[Mathf.Clamp(levelIndex, 0, LevelsPerStage - 1)];

    // ══════════════════════════════════════════════════════════════
    private static void Build()
    {
        _stages = new StageData[StageCount];

        // ── Stage 0: 지구 ──────────────────────────────────────────
        _stages[0] = new StageData
        {
            Name           = "지구",
            SubTitle       = "푸른 요람",
            ThemeId        = StageTheme.Earth,
            PrimaryColor   = new Color(0.18f, 0.52f, 0.89f),
            SecondaryColor = new Color(0.10f, 0.78f, 0.60f),
            BgScrollSpeed  = 18f,
            WaveAmplitude  = 2.5f,
            WaveFrequency  = 0.8f,
            BonusCoinBase  = 120,
            Levels = new LevelData[]
            {
                new LevelData { LevelIndex=0, BrickRows=4, BrickCols=8, BallSpeedMult=1.0f, MaxSpecialBricks=0, AllowedItems=ItemFlags.PaddleGrow|ItemFlags.MultiBall,              BrickPattern=BrickPattern.Simple,    BonusCoinAmount=80  },
                new LevelData { LevelIndex=1, BrickRows=5, BrickCols=8, BallSpeedMult=1.1f, MaxSpecialBricks=3, AllowedItems=ItemFlags.PaddleGrow|ItemFlags.MultiBall|ItemFlags.SlowBall, BrickPattern=BrickPattern.Checkers,  BonusCoinAmount=100 },
                new LevelData { LevelIndex=2, BrickRows=5, BrickCols=9, BallSpeedMult=1.2f, MaxSpecialBricks=5, AllowedItems=ItemFlags.PaddleGrow|ItemFlags.MultiBall|ItemFlags.SlowBall|ItemFlags.CoinMagnet, BrickPattern=BrickPattern.Diamond, BonusCoinAmount=120 },
                new LevelData { LevelIndex=3, BrickRows=6, BrickCols=9, BallSpeedMult=1.3f, MaxSpecialBricks=8, AllowedItems=ItemFlags.All & ~ItemFlags.BlackHole,                  BrickPattern=BrickPattern.Fortress,  BonusCoinAmount=150 },
                new LevelData { LevelIndex=4, BrickRows=7, BrickCols=9, BallSpeedMult=1.4f, MaxSpecialBricks=12,AllowedItems=ItemFlags.All,                                          BrickPattern=BrickPattern.Boss,      BonusCoinAmount=200 },
            }
        };

        // ── Stage 1: 태양계 ────────────────────────────────────────
        _stages[1] = new StageData
        {
            Name           = "태양계",
            SubTitle       = "중력의 영역",
            ThemeId        = StageTheme.SolarSystem,
            PrimaryColor   = new Color(0.98f, 0.65f, 0.12f),
            SecondaryColor = new Color(0.90f, 0.30f, 0.08f),
            BgScrollSpeed  = 22f,
            WaveAmplitude  = 3.0f,
            WaveFrequency  = 0.6f,
            BonusCoinBase  = 200,
            Levels = new LevelData[]
            {
                new LevelData { LevelIndex=0, BrickRows=5, BrickCols=9,  BallSpeedMult=1.2f, MaxSpecialBricks=4,  AllowedItems=ItemFlags.PaddleGrow|ItemFlags.MultiBall|ItemFlags.Explosive, BrickPattern=BrickPattern.Simple,   BonusCoinAmount=120 },
                new LevelData { LevelIndex=1, BrickRows=5, BrickCols=10, BallSpeedMult=1.3f, MaxSpecialBricks=6,  AllowedItems=ItemFlags.PaddleGrow|ItemFlags.MultiBall|ItemFlags.Explosive|ItemFlags.LaserPaddle, BrickPattern=BrickPattern.Orbit, BonusCoinAmount=150 },
                new LevelData { LevelIndex=2, BrickRows=6, BrickCols=10, BallSpeedMult=1.4f, MaxSpecialBricks=8,  AllowedItems=ItemFlags.All & ~ItemFlags.BlackHole,                   BrickPattern=BrickPattern.Asteroid,BonusCoinAmount=180 },
                new LevelData { LevelIndex=3, BrickRows=6, BrickCols=10, BallSpeedMult=1.5f, MaxSpecialBricks=10, AllowedItems=ItemFlags.All,                                           BrickPattern=BrickPattern.Fortress, BonusCoinAmount=220 },
                new LevelData { LevelIndex=4, BrickRows=7, BrickCols=10, BallSpeedMult=1.6f, MaxSpecialBricks=14, AllowedItems=ItemFlags.All,                                           BrickPattern=BrickPattern.Boss,     BonusCoinAmount=300 },
            }
        };

        // ── Stage 2: 가까운 항성계 ────────────────────────────────
        _stages[2] = new StageData
        {
            Name           = "항성계",
            SubTitle       = "낯선 빛의 세계",
            ThemeId        = StageTheme.StarSystem,
            PrimaryColor   = new Color(0.60f, 0.20f, 0.90f),
            SecondaryColor = new Color(0.20f, 0.90f, 0.80f),
            BgScrollSpeed  = 28f,
            WaveAmplitude  = 3.5f,
            WaveFrequency  = 1.0f,
            BonusCoinBase  = 320,
            Levels = new LevelData[]
            {
                new LevelData { LevelIndex=0, BrickRows=6, BrickCols=10, BallSpeedMult=1.4f, MaxSpecialBricks=6,  AllowedItems=ItemFlags.All & ~ItemFlags.BlackHole,  BrickPattern=BrickPattern.Alien,   BonusCoinAmount=180 },
                new LevelData { LevelIndex=1, BrickRows=6, BrickCols=10, BallSpeedMult=1.5f, MaxSpecialBricks=8,  AllowedItems=ItemFlags.All,                          BrickPattern=BrickPattern.Binary,  BonusCoinAmount=210 },
                new LevelData { LevelIndex=2, BrickRows=7, BrickCols=10, BallSpeedMult=1.6f, MaxSpecialBricks=10, AllowedItems=ItemFlags.All,                          BrickPattern=BrickPattern.Nebula,  BonusCoinAmount=250 },
                new LevelData { LevelIndex=3, BrickRows=7, BrickCols=11, BallSpeedMult=1.7f, MaxSpecialBricks=12, AllowedItems=ItemFlags.All,                          BrickPattern=BrickPattern.Fortress,BonusCoinAmount=300 },
                new LevelData { LevelIndex=4, BrickRows=8, BrickCols=11, BallSpeedMult=1.8f, MaxSpecialBricks=16, AllowedItems=ItemFlags.All,                          BrickPattern=BrickPattern.Boss,    BonusCoinAmount=400 },
            }
        };

        // ── Stage 3: 은하 중심 ────────────────────────────────────
        _stages[3] = new StageData
        {
            Name           = "은하 중심",
            SubTitle       = "중력의 심연",
            ThemeId        = StageTheme.GalacticCore,
            PrimaryColor   = new Color(1.0f, 0.90f, 0.20f),
            SecondaryColor = new Color(0.95f, 0.40f, 0.10f),
            BgScrollSpeed  = 35f,
            WaveAmplitude  = 4.5f,
            WaveFrequency  = 1.3f,
            BonusCoinBase  = 500,
            Levels = new LevelData[]
            {
                new LevelData { LevelIndex=0, BrickRows=7, BrickCols=11, BallSpeedMult=1.6f, MaxSpecialBricks=10, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Gravity,  BonusCoinAmount=280 },
                new LevelData { LevelIndex=1, BrickRows=7, BrickCols=11, BallSpeedMult=1.7f, MaxSpecialBricks=12, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Vortex,   BonusCoinAmount=320 },
                new LevelData { LevelIndex=2, BrickRows=8, BrickCols=11, BallSpeedMult=1.8f, MaxSpecialBricks=14, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Fortress, BonusCoinAmount=380 },
                new LevelData { LevelIndex=3, BrickRows=8, BrickCols=12, BallSpeedMult=1.9f, MaxSpecialBricks=16, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Pulsar,   BonusCoinAmount=450 },
                new LevelData { LevelIndex=4, BrickRows=9, BrickCols=12, BallSpeedMult=2.0f, MaxSpecialBricks=20, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Boss,     BonusCoinAmount=600 },
            }
        };

        // ── Stage 4: 다른 은하 ────────────────────────────────────
        _stages[4] = new StageData
        {
            Name           = "다른 은하",
            SubTitle       = "차원의 경계",
            ThemeId        = StageTheme.Extragalactic,
            PrimaryColor   = new Color(0.90f, 0.10f, 0.70f),
            SecondaryColor = new Color(0.10f, 0.60f, 1.0f),
            BgScrollSpeed  = 42f,
            WaveAmplitude  = 5.5f,
            WaveFrequency  = 1.6f,
            BonusCoinBase  = 800,
            Levels = new LevelData[]
            {
                new LevelData { LevelIndex=0, BrickRows=8, BrickCols=12, BallSpeedMult=1.8f, MaxSpecialBricks=12, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Dimensional, BonusCoinAmount=400 },
                new LevelData { LevelIndex=1, BrickRows=8, BrickCols=12, BallSpeedMult=1.9f, MaxSpecialBricks=14, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Warp,        BonusCoinAmount=480 },
                new LevelData { LevelIndex=2, BrickRows=9, BrickCols=12, BallSpeedMult=2.0f, MaxSpecialBricks=16, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Fractal,     BonusCoinAmount=560 },
                new LevelData { LevelIndex=3, BrickRows=9, BrickCols=13, BallSpeedMult=2.1f, MaxSpecialBricks=18, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Fortress,    BonusCoinAmount=650 },
                new LevelData { LevelIndex=4, BrickRows=10,BrickCols=13, BallSpeedMult=2.2f, MaxSpecialBricks=22, AllowedItems=ItemFlags.All, BrickPattern=BrickPattern.Boss,        BonusCoinAmount=900 },
            }
        };
    }
}

// ── 데이터 구조체 ────────────────────────────────────────────────

[System.Serializable]
public class StageData
{
    public string     Name;
    public string     SubTitle;
    public StageTheme ThemeId;
    public Color      PrimaryColor;
    public Color      SecondaryColor;
    public float      BgScrollSpeed;   // 배경 스크롤 속도 (px/s)
    public float      WaveAmplitude;   // 벽돌 웨이브 진폭 (pixel)
    public float      WaveFrequency;   // 벽돌 웨이브 주기
    public long       BonusCoinBase;   // 레벨 클리어 기본 보너스
    public LevelData[] Levels;
}

[System.Serializable]
public class LevelData
{
    public int         LevelIndex;
    public int         BrickRows;
    public int         BrickCols;
    public float       BallSpeedMult;
    public int         MaxSpecialBricks;
    public ItemFlags   AllowedItems;
    public BrickPattern BrickPattern;
    public long        BonusCoinAmount;
}

// ── 열거형 ────────────────────────────────────────────────────────

public enum StageTheme { Earth, SolarSystem, StarSystem, GalacticCore, Extragalactic }

public enum BrickPattern
{
    Simple, Checkers, Diamond, Fortress, Boss,
    Orbit, Asteroid, Alien, Binary, Nebula,
    Gravity, Vortex, Pulsar, Dimensional, Warp, Fractal
}

[System.Flags]
public enum ItemFlags
{
    None        = 0,
    PaddleGrow  = 1 << 0,
    PaddleShrink= 1 << 1,
    MultiBall   = 1 << 2,
    FastBall    = 1 << 3,
    SlowBall    = 1 << 4,
    PierceBall  = 1 << 5,
    Explosive   = 1 << 6,
    CoinMagnet  = 1 << 7,
    Drone       = 1 << 8,
    LaserPaddle = 1 << 9,
    Shield      = 1 << 10,
    SlowMotion  = 1 << 11,
    Lightning   = 1 << 12,
    BlackHole   = 1 << 13,
    Bombardment = 1 << 14,
    Satellite   = 1 << 15,
    All         = ~0,
}
