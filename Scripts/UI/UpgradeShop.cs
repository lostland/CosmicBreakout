using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 코인으로 구매 가능한 영구 업그레이드 상점.
/// 패들 크기, 공 속도, 시작 아이템, 코인 자석 범위, 추가 라이프를 구매한다.
/// </summary>
public class UpgradeShop : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeSlot
    {
        public UpgradeType Type;
        public Button      BuyBtn;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI CostText;
        public TextMeshProUGUI LevelText;
        public Image[]     Stars;        // 현재 레벨 별 표시
    }

    [SerializeField] UpgradeSlot[]   _slots;
    [SerializeField] TextMeshProUGUI _totalCoinText;
    [SerializeField] Button          _closeBtn;

    void Start()
    {
        _closeBtn?.onClick.AddListener(() => gameObject.SetActive(false));
        Refresh();
    }

    void OnEnable() => Refresh();

    private void Refresh()
    {
        var data = SaveManager.Instance?.Data;
        if (data == null) return;

        _totalCoinText?.SetText(data.TotalCoins.ToString("N0"));

        foreach (var slot in _slots)
        {
            var def = UpgradeDatabase.Get(slot.Type);
            if (def == null) continue;

            int current = GetCurrentLevel(slot.Type, data);
            bool maxed  = current >= def.MaxLevel;

            slot.NameText?.SetText(def.Name);
            slot.LevelText?.SetText(maxed ? "MAX" : $"Lv.{current}");

            long cost = maxed ? 0 : def.Costs[current];
            slot.CostText?.SetText(maxed ? "-" : cost.ToString("N0"));
            slot.BuyBtn.interactable = !maxed && data.TotalCoins >= cost;

            // 별 표시
            if (slot.Stars != null)
                for (int i = 0; i < slot.Stars.Length; i++)
                    slot.Stars[i]?.gameObject.SetActive(i < current);

            // 버튼 콜백 (lambda capture)
            UpgradeType capturedType = slot.Type;
            slot.BuyBtn.onClick.RemoveAllListeners();
            slot.BuyBtn.onClick.AddListener(() => OnBuyClicked(capturedType));
        }
    }

    private void OnBuyClicked(UpgradeType type)
    {
        var data = SaveManager.Instance?.Data;
        if (data == null) return;

        var def     = UpgradeDatabase.Get(type);
        int current = GetCurrentLevel(type, data);
        if (current >= def.MaxLevel) return;

        long cost = def.Costs[current];
        if (data.TotalCoins < cost) return;

        // 구매 처리
        data.TotalCoins -= cost;
        SetCurrentLevel(type, current + 1, data);
        SaveManager.Instance.Save();

        AudioManager.Instance?.PlaySFX(SFXType.CoinCollect);
        Refresh();
    }

    private int GetCurrentLevel(UpgradeType type, SaveData data)
    {
        return type switch
        {
            UpgradeType.PaddleSize  => data.UpgradePaddleSize,
            UpgradeType.BallSpeed   => data.UpgradeBallSpeed,
            UpgradeType.StartItems  => data.UpgradeStartItems,
            UpgradeType.CoinMagnet  => data.UpgradeCoinMagnet,
            UpgradeType.ExtraLife   => data.UpgradeExtraLife,
            _                       => 0,
        };
    }

    private void SetCurrentLevel(UpgradeType type, int level, SaveData data)
    {
        switch (type)
        {
            case UpgradeType.PaddleSize: data.UpgradePaddleSize  = level; break;
            case UpgradeType.BallSpeed:  data.UpgradeBallSpeed   = level; break;
            case UpgradeType.StartItems: data.UpgradeStartItems  = level; break;
            case UpgradeType.CoinMagnet: data.UpgradeCoinMagnet  = level; break;
            case UpgradeType.ExtraLife:  data.UpgradeExtraLife   = level; break;
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// 업그레이드 데이터
// ══════════════════════════════════════════════════════════════════

public enum UpgradeType { PaddleSize, BallSpeed, StartItems, CoinMagnet, ExtraLife }

public class UpgradeDef
{
    public UpgradeType Type;
    public string      Name;
    public string      Description;
    public int         MaxLevel;
    public long[]      Costs;       // 각 레벨 구매 비용
}

public static class UpgradeDatabase
{
    private static Dictionary<UpgradeType, UpgradeDef> _map;

    public static UpgradeDef Get(UpgradeType type)
    {
        if (_map == null) Build();
        return _map.TryGetValue(type, out var d) ? d : null;
    }

    public static float GetPaddleSizeBonus(int level)   => level * 0.3f;  // 레벨당 +0.3
    public static float GetBallSpeedMult(int level)     => 1f + level * 0.08f;
    public static int   GetStartItemSlots(int level)    => level;          // 레벨 = 추가 아이템 슬롯
    public static float GetCoinMagnetRadius(int level)  => level * 1.5f;
    public static int   GetExtraLives(int level)        => level;

    private static void Build()
    {
        _map = new Dictionary<UpgradeType, UpgradeDef>
        {
            { UpgradeType.PaddleSize, new UpgradeDef {
                Type=UpgradeType.PaddleSize, Name="패들 강화",
                Description="패들 기본 크기가 커집니다.",
                MaxLevel=3, Costs=new long[]{300, 800, 2000}
            }},
            { UpgradeType.BallSpeed, new UpgradeDef {
                Type=UpgradeType.BallSpeed, Name="공 가속",
                Description="공의 초기 속도가 빨라집니다.",
                MaxLevel=3, Costs=new long[]{400, 1000, 2500}
            }},
            { UpgradeType.StartItems, new UpgradeDef {
                Type=UpgradeType.StartItems, Name="아이템 준비",
                Description="게임 시작 시 아이템을 미리 보유합니다.",
                MaxLevel=3, Costs=new long[]{500, 1200, 3000}
            }},
            { UpgradeType.CoinMagnet, new UpgradeDef {
                Type=UpgradeType.CoinMagnet, Name="코인 자석",
                Description="코인 자동 수집 범위가 넓어집니다.",
                MaxLevel=3, Costs=new long[]{250, 700, 1800}
            }},
            { UpgradeType.ExtraLife, new UpgradeDef {
                Type=UpgradeType.ExtraLife, Name="추가 라이프",
                Description="시작 라이프가 늘어납니다.",
                MaxLevel=2, Costs=new long[]{600, 1500}
            }},
        };
    }
}
