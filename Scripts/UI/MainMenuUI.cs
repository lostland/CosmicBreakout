using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 메인 화면 UI.
/// 우주 배경 애니메이션, 코인 표시, 시작/스테이지 선택 버튼, 설정 패널.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] Button _startBtn;
    [SerializeField] Button _stageSelectBtn;
    [SerializeField] Button _settingsBtn;

    [Header("Info")]
    [SerializeField] TextMeshProUGUI _coinText;
    [SerializeField] TextMeshProUGUI _lastStageText;    // "계속하기: 지구 Lv.3"
    [SerializeField] RectTransform   _lastStageHighlight;

    [Header("Settings Panel")]
    [SerializeField] GameObject      _settingsPanel;
    [SerializeField] Slider          _musicSlider;
    [SerializeField] Slider          _sfxSlider;
    [SerializeField] Toggle          _soundToggle;
    [SerializeField] Toggle          _vibToggle;
    [SerializeField] Button          _settingsCloseBtn;

    [Header("Star Particles")]
    [SerializeField] ParticleSystem  _starParticles;
    [SerializeField] ParticleSystem  _nebulaParticles;

    void Start()
    {
        AudioManager.Instance?.PlayMainMenuBGM();

        _startBtn?.onClick.AddListener(OnStartClicked);
        _stageSelectBtn?.onClick.AddListener(OnStageSelectClicked);
        _settingsBtn?.onClick.AddListener(() => _settingsPanel?.SetActive(true));
        _settingsCloseBtn?.onClick.AddListener(() => _settingsPanel?.SetActive(false));
        _settingsPanel?.SetActive(false);

        LoadSettings();
        RefreshCoinDisplay();
        RefreshLastStage();
        StartCoroutine(PulseLastStage());
    }

    // ═════════════════════════════════════════════════════════════
    // 버튼
    // ═════════════════════════════════════════════════════════════

    private void OnStartClicked()
    {
        // 마지막으로 플레이 가능한 스테이지/레벨로 바로 시작
        var save = SaveManager.Instance?.Data;
        if (save == null) return;

        int stage = save.UnlockedStages;
        int level = Mathf.Min(save.GetLevelProgress(stage), StageDatabase.LevelsPerStage - 1);
        GameManager.Instance?.StartLevel(stage, level);
    }

    private void OnStageSelectClicked()
    {
        GameManager.Instance?.ReturnToStageSelect();
    }

    // ═════════════════════════════════════════════════════════════
    // UI 갱신
    // ═════════════════════════════════════════════════════════════

    private void RefreshCoinDisplay()
    {
        long coins = SaveManager.Instance?.Data.TotalCoins ?? 0;
        _coinText?.SetText(coins.ToString("N0"));
    }

    private void RefreshLastStage()
    {
        var save = SaveManager.Instance?.Data;
        if (save == null) return;

        int stageIdx = save.UnlockedStages;
        int levelIdx = save.GetLevelProgress(stageIdx);
        var sd = StageDatabase.GetStage(stageIdx);
        _lastStageText?.SetText($"계속하기: {sd.Name}  ·  레벨 {levelIdx + 1}");
    }

    private IEnumerator PulseLastStage()
    {
        if (_lastStageHighlight == null) yield break;
        while (true)
        {
            yield return StartCoroutine(ScalePulse(_lastStageHighlight, 1.04f, 0.8f));
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator ScalePulse(RectTransform rt, float peak, float duration)
    {
        Vector3 origin = rt.localScale;
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(origin, origin * peak, elapsed / half);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(origin * peak, origin, elapsed / half);
            yield return null;
        }
        rt.localScale = origin;
    }

    // ═════════════════════════════════════════════════════════════
    // 설정
    // ═════════════════════════════════════════════════════════════

    private void LoadSettings()
    {
        var data = SaveManager.Instance?.Data;
        if (data == null) return;

        if (_musicSlider) { _musicSlider.value = data.MusicVolume; _musicSlider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume); }
        if (_sfxSlider)   { _sfxSlider.value   = data.SfxVolume;   _sfxSlider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume); }
        if (_soundToggle) { _soundToggle.isOn   = data.SoundEnabled; _soundToggle.onValueChanged.AddListener(AudioManager.Instance.ToggleSound); }
        if (_vibToggle)   { _vibToggle.isOn     = data.VibrationEnabled; _vibToggle.onValueChanged.AddListener(v => {
            if (SaveManager.Instance != null) {
                SaveManager.Instance.Data.VibrationEnabled = v;
                SaveManager.Instance.Save();
            }
        }); }
    }
}
