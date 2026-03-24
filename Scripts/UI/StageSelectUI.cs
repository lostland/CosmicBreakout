using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 스테이지 선택 화면.
/// 해금된 스테이지만 선택 가능하고, 잠긴 스테이지는 잠금 연출을 표시한다.
/// 마지막 플레이 가능한 스테이지에 포커스를 맞춘다.
/// </summary>
public class StageSelectUI : MonoBehaviour
{
    [Header("Stage Cards")]
    [SerializeField] StageCard[] _stageCards;   // Inspector에서 5개 연결
    [SerializeField] ScrollRect  _scrollRect;

    [Header("Header")]
    [SerializeField] TextMeshProUGUI _totalCoinText;

    [Header("Back")]
    [SerializeField] Button          _backButton;

    void Start()
    {
        _backButton?.onClick.AddListener(() => GameManager.Instance?.ReturnToMain());
        RefreshAll();
    }

    private void RefreshAll()
    {
        var save = SaveManager.Instance?.Data;
        if (save == null) return;

        _totalCoinText?.SetText(save.TotalCoins.ToString("N0"));

        for (int i = 0; i < _stageCards.Length && i < StageDatabase.StageCount; i++)
        {
            bool unlocked = save.IsStageUnlocked(i);
            int  progress = save.GetLevelProgress(i);
            _stageCards[i].Setup(i, unlocked, progress);
        }

        // 마지막 해금 스테이지로 스크롤 포커스
        int focusIdx = Mathf.Clamp(save.UnlockedStages, 0, _stageCards.Length - 1);
        StartCoroutine(ScrollToCard(focusIdx));
    }

    private IEnumerator ScrollToCard(int cardIndex)
    {
        yield return new WaitForEndOfFrame();
        if (_scrollRect == null || _stageCards.Length == 0) yield break;

        float t = cardIndex / (float)(_stageCards.Length - 1);
        _scrollRect.horizontalNormalizedPosition = t;
    }
}

// ══════════════════════════════════════════════════════════════════
// StageCard: 개별 스테이지 카드 UI
// ══════════════════════════════════════════════════════════════════

public class StageCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _subTitleText;
    [SerializeField] TextMeshProUGUI _progressText;
    [SerializeField] Image           _bgImage;
    [SerializeField] Image           _lockOverlay;
    [SerializeField] GameObject      _lockIcon;
    [SerializeField] Button          _selectButton;
    [SerializeField] RectTransform   _cardRoot;

    // 레벨 선택 버튼 (카드 안 5개)
    [SerializeField] LevelButton[]   _levelButtons;

    private int  _stageIndex;
    private bool _unlocked;

    public void Setup(int stageIndex, bool unlocked, int levelProgress)
    {
        _stageIndex = stageIndex;
        _unlocked   = unlocked;

        var sd = StageDatabase.GetStage(stageIndex);
        _nameText?.SetText(sd.Name);
        _subTitleText?.SetText(sd.SubTitle);
        _progressText?.SetText($"{levelProgress} / {StageDatabase.LevelsPerStage} 클리어");

        // 색상
        if (_bgImage) _bgImage.color = Color.Lerp(sd.PrimaryColor, Color.black, 0.5f);

        // 잠금 상태
        _lockOverlay?.gameObject.SetActive(!unlocked);
        _lockIcon?.SetActive(!unlocked);

        // 카드 클릭 (잠금이면 흔들림 연출)
        _selectButton?.onClick.RemoveAllListeners();
        _selectButton?.onClick.AddListener(OnCardClicked);

        // 레벨 버튼 초기화
        for (int i = 0; i < _levelButtons.Length; i++)
        {
            bool levelUnlocked = unlocked && i <= levelProgress;
            bool levelCleared  = unlocked && i < levelProgress;
            _levelButtons[i]?.Setup(stageIndex, i, levelUnlocked, levelCleared);
        }

        // 등장 애니메이션
        StartCoroutine(AppearAnim());
    }

    private void OnCardClicked()
    {
        if (!_unlocked)
        {
            StartCoroutine(LockedShake());
            return;
        }
        // 잠금 해제된 카드는 첫 번째 미클리어 레벨로 바로 이동
        var save = SaveManager.Instance?.Data;
        if (save == null) return;
        int levelIdx = Mathf.Min(save.GetLevelProgress(_stageIndex), StageDatabase.LevelsPerStage - 1);
        GameManager.Instance?.StartLevel(_stageIndex, levelIdx);
    }

    private IEnumerator LockedShake()
    {
        if (_cardRoot == null) yield break;
        AudioManager.Instance?.PlaySFX(SFXType.ShieldBreak);
        Vector3 origin = _cardRoot.localPosition;
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Sin(i * Mathf.PI * 0.75f) * (6 - i) * 4f;
            _cardRoot.localPosition = origin + Vector3.right * x;
            yield return new WaitForSeconds(0.04f);
        }
        _cardRoot.localPosition = origin;
    }

    private IEnumerator AppearAnim()
    {
        if (_cardRoot == null) yield break;
        _cardRoot.localScale = Vector3.zero;
        float elapsed = 0f;
        float dur     = 0.35f;
        yield return new WaitForSeconds(_stageIndex * 0.07f); // 순차 등장
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float s = EaseOutBack(t);
            _cardRoot.localScale = Vector3.one * s;
            yield return null;
        }
        _cardRoot.localScale = Vector3.one;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

// ══════════════════════════════════════════════════════════════════
// LevelButton: 스테이지 카드 내 레벨 선택 버튼
// ══════════════════════════════════════════════════════════════════

public class LevelButton : MonoBehaviour
{
    [SerializeField] Button          _btn;
    [SerializeField] TextMeshProUGUI _label;
    [SerializeField] Image           _star;     // 클리어 별 표시
    [SerializeField] Image           _lockIcon;
    [SerializeField] Color           _unlockedColor = Color.white;
    [SerializeField] Color           _lockedColor   = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    private int _stageIndex;
    private int _levelIndex;

    public void Setup(int stageIndex, int levelIndex, bool unlocked, bool cleared)
    {
        _stageIndex = stageIndex;
        _levelIndex = levelIndex;

        _label?.SetText($"Lv.{levelIndex + 1}");
        _btn.interactable = unlocked;
        _star?.gameObject.SetActive(cleared);
        _lockIcon?.gameObject.SetActive(!unlocked);

        GetComponent<Image>().color = unlocked ? _unlockedColor : _lockedColor;

        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        GameManager.Instance?.StartLevel(_stageIndex, _levelIndex);
    }
}
