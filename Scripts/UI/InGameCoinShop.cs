using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 일시정지 화면 내 인게임 코인 상점.
/// 세션 코인(이번 판 획득 코인)을 소비해 즉시 효과를 구매한다.
/// 영구 업그레이드와 달리 이번 판에만 유효하다.
/// </summary>
public class InGameCoinShop : MonoBehaviour
{
    [System.Serializable]
    public class ShopItem
    {
        public string            Name;
        public string            Description;
        public long              Cost;
        public ItemType          ItemToActivate;
        public Button            BuyBtn;
        public TextMeshProUGUI   CostText;
        public TextMeshProUGUI   DescText;
        public Image             Icon;
        public Color             IconColor;
        [HideInInspector] public bool Purchased;
    }

    [SerializeField] ShopItem[]      _items;
    [SerializeField] TextMeshProUGUI _sessionCoinDisplay;
    [SerializeField] Button          _closeBtn;

    void OnEnable()
    {
        RefreshAll();
        _closeBtn?.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void RefreshAll()
    {
        long coins = GameManager.Instance?.SessionCoins ?? 0;
        _sessionCoinDisplay?.SetText($"보유 코인: {coins:N0}");

        foreach (var item in _items)
        {
            if (item.CostText) item.CostText.text = item.Cost.ToString("N0");
            if (item.DescText) item.DescText.text = item.Description;
            if (item.Icon)     item.Icon.color     = item.IconColor;

            bool canAfford = !item.Purchased && coins >= item.Cost;
            if (item.BuyBtn) item.BuyBtn.interactable = canAfford;

            // 람다 캡쳐를 위한 로컬 복사
            ShopItem captured = item;
            item.BuyBtn?.onClick.RemoveAllListeners();
            item.BuyBtn?.onClick.AddListener(() => OnBuy(captured));
        }
    }

    private void OnBuy(ShopItem item)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.SessionCoins < item.Cost) return;

        // 세션 코인에서 차감 (총 코인에서 차감하지 않음 — 이번 판 코인 사용)
        gm.SpendCoins(item.Cost);
        item.Purchased = true;

        // 아이템 효과 즉시 발동
        ItemManager.Instance?.ActivateItem(item.ItemToActivate);
        AudioManager.Instance?.PlaySFX(SFXType.ItemPickup);
        FloatingTextManager.Instance?.Show(
            $"{item.Name} 발동!", Vector3.zero, 1.5f, new Color(1f, 0.9f, 0.2f), 24);

        RefreshAll();
    }

    // 기본 상품 목록 (Inspector에서 설정하거나 코드로 초기화 가능)
    void Start()
    {
        if (_items == null || _items.Length == 0)
            InitDefaultItems();
    }

    private void InitDefaultItems()
    {
        _items = new ShopItem[]
        {
            new ShopItem { Name="멀티볼",   Description="공 2개 추가",              Cost=80,  ItemToActivate=ItemType.MultiBall,     IconColor=new Color(0.9f,0.9f,0.2f) },
            new ShopItem { Name="관통볼",   Description="벽돌 관통 8초",            Cost=100, ItemToActivate=ItemType.PierceBall,    IconColor=new Color(0.2f,0.9f,1f)   },
            new ShopItem { Name="번개",     Description="랜덤 벽돌 파괴",           Cost=120, ItemToActivate=ItemType.Lightning,     IconColor=new Color(1f,1f,0.2f)     },
            new ShopItem { Name="패들 확장",Description="패들 크기 +50%, 10초",     Cost=60,  ItemToActivate=ItemType.PaddleGrow,    IconColor=new Color(0.2f,0.8f,0.2f) },
            new ShopItem { Name="코인 샤워",Description="코인 대량 획득",           Cost=50,  ItemToActivate=ItemType.CoinShower,    IconColor=new Color(1f,0.85f,0.1f)  },
            new ShopItem { Name="보호막",   Description="볼 1회 손실 방어 15초",    Cost=90,  ItemToActivate=ItemType.Shield,        IconColor=new Color(0.3f,0.5f,1f)   },
            new ShopItem { Name="폭격",     Description="랜덤 위치 10회 폭발",      Cost=150, ItemToActivate=ItemType.Bombardment,   IconColor=new Color(0.8f,0.4f,0.1f) },
            new ShopItem { Name="슬로우",   Description="시간 느리게 6초",          Cost=80,  ItemToActivate=ItemType.SlowMotion,    IconColor=new Color(0.6f,0.9f,1f)   },
        };
    }
}
