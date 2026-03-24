using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 인게임 HUD: 코인, 라이프, 콤보, 활성 아이템 슬롯, 일시정지 버튼.
/// GameManager 이벤트를 구독해 자동 갱신된다.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Coin UI")]
    [SerializeField] TextMeshProUGUI _coinText;
    [SerializeField] RectTransform   _coinIcon;
    [SerializeField] Image           _coinFlashOverlay;   // 대량 획득 시 빛나는 오버레이

    [Header("Lives UI")]
    [SerializeField] Image[]         _lifeIcons;          // 3개 라이프 아이콘
    [SerializeField] Color           _lifeActiveColor  = Color.white;
    [SerializeField] Color           _lifeEmptyColor   = new Color(0.3f, 0.3f, 0.3f, 0.4f);

    [Header("Stage / Level")]
    [SerializeField] TextMeshProUGUI _stageLevelText;

    [Header("Combo")]
    [SerializeField] RectTransform   _comboPanel;
    [SerializeField] TextMeshProUGUI _comboText;
    [SerializeField] Image           _comboTimerBar;

    [Header("Active Items")]
    [SerializeField] RectTransform   _itemSlotContainer;
    [SerializeField] ActiveItemSlot  _itemSlotPrefab;

    [Header("Pause")]
    [SerializeField] Button          _pauseButton;
    [SerializeField] GameObject      _pausePanel;

    // ── 내부 ─────────────────────────────────────────────────────
    private Dictionary<ItemType, ActiveItemSlot> _activeSlots = new Dictionary<ItemType, ActiveItemSlot>();
    private long   _displayedCoins = 0;
    private Coroutine _coinAnimCo;

    // ═════════════════════════════════════════════════════════════
    void OnEnable()
    {
        GameManager.OnCoinsChanged  += UpdateCoin;
        GameManager.OnLivesChanged  += UpdateLives;
        GameManager.OnComboChanged  += UpdateCombo;
        ItemManager.OnItemActivated += OnItemActivated;
        ItemManager.OnItemExpired   += OnItemExpired;
        _pauseButton?.onClick.AddListener(OnPauseClicked);
    }

    void OnDisable()
    {
        GameManager.OnCoinsChanged  -= UpdateCoin;
        GameManager.OnLivesChanged  -= UpdateLives;
        GameManager.OnComboChanged  -= UpdateCombo;
        ItemManager.OnItemActivated -= OnItemActivated;
        ItemManager.OnItemExpired   -= OnItemExpired;
        _pauseButton?.onClick.RemoveListener(OnPauseClicked);
    }

    void Start()
    {
        // 초기값 세팅
        var gm = GameManager.Instance;
        if (gm != null)
        {
            UpdateCoin(gm.SessionCoins);
            UpdateLives(gm.Lives);
            _stageLevelText?.SetText($"STAGE {gm.CurrentStageIndex + 1}  ·  LEVEL {gm.CurrentLevelIndex + 1}");
        }
        SetComboVisible(false);
        _pausePanel?.SetActive(false);
    }

    void Update()
    {
        UpdateComboBar();
    }

    // ═════════════════════════════════════════════════════════════
    // 코인
    // ═════════════════════════════════════════════════════════════

    private void UpdateCoin(long amount)
    {
        if (_coinAnimCo != null) StopCoroutine(_coinAnimCo);
        _coinAnimCo = StartCoroutine(AnimateCoinCount(_displayedCoins, amount));
    }

    private IEnumerator AnimateCoinCount(long from, long to)
    {
        float dur     = Mathf.Clamp((float)(to - from) / 50f, 0.15f, 0.6f);
        float elapsed = 0f;
        long  delta   = to - from;

        // 대량 획득 시 플래시 효과
        if (delta >= 10)
            StartCoroutine(CoinFlash());

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            _displayedCoins = from + (long)(delta * t);
            _coinText?.SetText(_displayedCoins.ToString("N0"));

            // 코인 아이콘 펄스
            float scale = 1f + Mathf.Sin(elapsed * 20f) * 0.05f * (1f - elapsed / dur);
            _coinIcon.localScale = Vector3.one * scale;
            yield return null;
        }

        _displayedCoins = to;
        _coinText?.SetText(to.ToString("N0"));
        _coinIcon.localScale = Vector3.one;
    }

    private IEnumerator CoinFlash()
    {
        if (_coinFlashOverlay == null) yield break;
        Color c = _coinFlashOverlay.color;
        _coinFlashOverlay.color = new Color(c.r, c.g, c.b, 0.7f);
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            _coinFlashOverlay.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.7f, 0f, elapsed / 0.3f));
            yield return null;
        }
        _coinFlashOverlay.color = new Color(c.r, c.g, c.b, 0f);
    }

    // ═════════════════════════════════════════════════════════════
    // 라이프
    // ═════════════════════════════════════════════════════════════

    private void UpdateLives(int lives)
    {
        for (int i = 0; i < _lifeIcons.Length; i++)
        {
            if (_lifeIcons[i] == null) continue;
            bool active = i < lives;
            _lifeIcons[i].color = active ? _lifeActiveColor : _lifeEmptyColor;

            if (!active)
                StartCoroutine(LifeLostAnim(_lifeIcons[i]));
        }
    }

    private IEnumerator LifeLostAnim(Image icon)
    {
        Vector3 origin = icon.rectTransform.localScale;
        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / 0.4f;
            float s  = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            icon.rectTransform.localScale = origin * s;
            yield return null;
        }
        icon.rectTransform.localScale = origin;
    }

    // ═════════════════════════════════════════════════════════════
    // 콤보
    // ═════════════════════════════════════════════════════════════

    private void UpdateCombo(int combo)
    {
        if (combo <= 1)
        {
            SetComboVisible(false);
            return;
        }
        SetComboVisible(true);
        _comboText?.SetText($"x{combo} COMBO");

        // 콤보 숫자 크기 펀치
        StartCoroutine(PunchScale(_comboPanel, 1.25f, 0.15f));
    }

    private void SetComboVisible(bool visible)
    {
        if (_comboPanel) _comboPanel.gameObject.SetActive(visible);
    }

    private void UpdateComboBar()
    {
        if (_comboTimerBar == null || GameManager.Instance == null) return;
        float t = GameManager.Instance.ComboTimer / GameManager.COMBO_TIMEOUT;
        _comboTimerBar.fillAmount = Mathf.Clamp01(t);
    }

    // ═════════════════════════════════════════════════════════════
    // 아이템 슬롯
    // ═════════════════════════════════════════════════════════════

    private void OnItemActivated(ItemType type, float duration)
    {
        if (_activeSlots.ContainsKey(type))
        {
            _activeSlots[type].Refresh(duration);
            return;
        }
        if (_itemSlotPrefab == null) return;
        var slot = Instantiate(_itemSlotPrefab, _itemSlotContainer);
        slot.Init(type, duration);
        _activeSlots[type] = slot;
    }

    private void OnItemExpired(ItemType type)
    {
        if (_activeSlots.TryGetValue(type, out var slot))
        {
            Destroy(slot.gameObject);
            _activeSlots.Remove(type);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 일시정지
    // ═════════════════════════════════════════════════════════════

    private void OnPauseClicked()
    {
        if (GameManager.Instance == null) return;
        bool pausing = GameManager.Instance.CurrentState == GameManager.GameState.Playing;
        GameManager.Instance.ChangeState(
            pausing ? GameManager.GameState.Paused : GameManager.GameState.Playing);
        _pausePanel?.SetActive(pausing);
    }

    public void OnResumeClicked()
    {
        GameManager.Instance?.ChangeState(GameManager.GameState.Playing);
        _pausePanel?.SetActive(false);
    }

    public void OnQuitToMenu()
    {
        GameManager.Instance?.ReturnToMain();
    }

    // ═════════════════════════════════════════════════════════════
    // 유틸
    // ═════════════════════════════════════════════════════════════

    private IEnumerator PunchScale(RectTransform rt, float peakScale, float duration)
    {
        Vector3 origin  = rt.localScale;
        float   elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float s = Mathf.Lerp(peakScale, 1f, t);
            rt.localScale = origin * s;
            yield return null;
        }
        rt.localScale = origin;
    }
}

// ══════════════════════════════════════════════════════════════════
// 활성 아이템 슬롯 UI
// ══════════════════════════════════════════════════════════════════

public class ActiveItemSlot : MonoBehaviour
{
    [SerializeField] Image            _icon;
    [SerializeField] Image            _timerRing;
    [SerializeField] TextMeshProUGUI  _nameText;

    private float _totalDuration;
    private float _remaining;

    public void Init(ItemType type, float duration)
    {
        ItemDef def = ItemDatabase.Get(type);
        if (def == null) return;

        if (_icon)     _icon.color  = def.Color;
        if (_nameText) _nameText.text = def.Icon;
        _totalDuration = duration;
        _remaining     = duration;
    }

    public void Refresh(float duration)
    {
        _remaining = duration;
    }

    void Update()
    {
        if (_totalDuration <= 0f) return;
        _remaining -= Time.deltaTime;
        if (_timerRing)
            _timerRing.fillAmount = Mathf.Clamp01(_remaining / _totalDuration);
        if (_remaining <= 0f)
            gameObject.SetActive(false);
    }
}
