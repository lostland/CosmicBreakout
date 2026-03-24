using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Linq;

/// <summary>
/// 전체 게임 상태를 관리하는 핵심 싱글톤.
/// 씬 전환, 게임 상태(플레이/일시정지/클리어/게임오버), 코인/라이프 관리를 담당한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── 게임 상태 ──────────────────────────────────────────────────
    public enum GameState { MainMenu, StageSelect, Playing, Paused, LevelClear, GameOver }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // ── 이벤트 ────────────────────────────────────────────────────
    public static event Action<GameState> OnStateChanged;
    public static event Action<long>      OnCoinsChanged;     // 현재 총 코인
    public static event Action<int>       OnLivesChanged;
    public static event Action<int>       OnComboChanged;
    public static event Action            OnLevelClear;
    public static event Action            OnGameOver;

    // ── 플레이 데이터 ──────────────────────────────────────────────
    [Header("Play Session")]
    public int   CurrentStageIndex  = 0;   // 큰 스테이지 (지구=0 …)
    public int   CurrentLevelIndex  = 0;   // 스테이지 내 레벨 (0~4)
    public int   Lives              = 3;
    public long  SessionCoins       = 0;   // 이번 판에서 모은 코인
    public int   Combo              = 0;
    public float ComboTimer         = 0f;
    public const float COMBO_TIMEOUT = 2.5f;

    [Header("Config")]
    public int MaxLives        = 3;
    public int CoinsPerCombo   = 2;  // 콤보당 추가 코인 배수 기준
    public float BonusCoinMult = 2f; // 광고 보상 배수

    // ── 영구 저장 데이터 ──────────────────────────────────────────
    public long  TotalCoins         => SaveManager.Instance.Data.TotalCoins;
    public int   UnlockedStages     => SaveManager.Instance.Data.UnlockedStages;

    // ── 내부 ───────────────────────────────────────────────────────
    private bool _levelCleared = false;

    // ══════════════════════════════════════════════════════════════
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (CurrentState == GameState.Playing && Combo > 0)
        {
            ComboTimer -= Time.deltaTime;
            if (ComboTimer <= 0f) ResetCombo();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 상태 전환
    // ══════════════════════════════════════════════════════════════

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.LevelClear:
                Time.timeScale = 0f;
                HandleLevelClear();
                break;
            case GameState.GameOver:
                Time.timeScale = 0f;
                OnGameOver?.Invoke();
                break;
        }
    }

    public void StartLevel(int stageIndex, int levelIndex)
    {
        CurrentStageIndex = stageIndex;
        CurrentLevelIndex = levelIndex;
        SessionCoins      = 0;
        Lives             = MaxLives;
        Combo             = 0;
        _levelCleared     = false;

        OnLivesChanged?.Invoke(Lives);
        OnCoinsChanged?.Invoke(SessionCoins);

        ChangeState(GameState.Playing);
        LoadSceneWithFallback("GameScene");
    }

    public void ReturnToMain()
    {
        Time.timeScale = 1f;
        ChangeState(GameState.MainMenu);
        LoadSceneWithFallback("MainMenu");
    }

    public void ReturnToStageSelect()
    {
        Time.timeScale = 1f;
        ChangeState(GameState.StageSelect);
        LoadSceneWithFallback("StageSelect");
    }

    // ══════════════════════════════════════════════════════════════
    // 코인
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 벽돌 파괴, 아이템 등에서 코인을 추가한다.
    /// 콤보에 따라 보너스 배율이 적용된다.
    /// </summary>
    public void AddCoins(long amount, bool applyComboBonus = true)
    {
        long bonus = applyComboBonus ? Mathf.Max(0, Combo - 1) * CoinsPerCombo : 0;
        long total = amount + bonus;
        SessionCoins += total;
        OnCoinsChanged?.Invoke(SessionCoins);

        // 파티클/UI 반응은 CoinUIController가 이벤트를 수신해 처리
    }

    public void SpendCoins(long amount)
    {
        var data = SaveManager.Instance.Data;
        data.TotalCoins = Mathf.Max(0, (int)(data.TotalCoins - amount));
        SaveManager.Instance.Save();
        OnCoinsChanged?.Invoke(data.TotalCoins);
    }

    // ══════════════════════════════════════════════════════════════
    // 콤보
    // ══════════════════════════════════════════════════════════════

    public void AddCombo()
    {
        Combo++;
        ComboTimer = COMBO_TIMEOUT;
        OnComboChanged?.Invoke(Combo);
    }

    public void ResetCombo()
    {
        Combo      = 0;
        ComboTimer = 0f;
        OnComboChanged?.Invoke(0);
    }

    // ══════════════════════════════════════════════════════════════
    // 라이프
    // ══════════════════════════════════════════════════════════════

    public void LoseLife()
    {
        Lives--;
        ResetCombo();
        OnLivesChanged?.Invoke(Lives);

        if (Lives <= 0)
            ChangeState(GameState.GameOver);
    }

    // ══════════════════════════════════════════════════════════════
    // 레벨 클리어
    // ══════════════════════════════════════════════════════════════

    public void TriggerLevelClear()
    {
        if (_levelCleared) return;
        _levelCleared = true;
        OnLevelClear?.Invoke();
        ChangeState(GameState.LevelClear);
    }

    private void HandleLevelClear()
    {
        // 영구 저장: 코인 적립 + 진행도 기록
        var data = SaveManager.Instance.Data;
        data.TotalCoins    += SessionCoins;
        int playedMax       = CurrentStageIndex * 5 + CurrentLevelIndex;
        if (playedMax + 1 > data.UnlockedStages * 5 + data.GetLevelProgress(CurrentStageIndex))
            data.SetLevelProgress(CurrentStageIndex, CurrentLevelIndex + 1);
        // 다음 큰 스테이지 해금
        if (CurrentLevelIndex == 4 && CurrentStageIndex + 1 > data.UnlockedStages)
            data.UnlockedStages = CurrentStageIndex + 1;
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// 광고 보상: 세션 코인 2배 추가 지급 후 저장
    /// </summary>
    public void ClaimDoubleReward()
    {
        var data = SaveManager.Instance.Data;
        data.TotalCoins += (long)(SessionCoins * (BonusCoinMult - 1));
        SaveManager.Instance.Save();
    }

    public void NextLevel()
    {
        int nextLevel = CurrentLevelIndex + 1;
        int nextStage = CurrentStageIndex;
        if (nextLevel >= 5) { nextLevel = 0; nextStage++; }
        if (nextStage >= StageDatabase.StageCount)
        {
            ReturnToStageSelect();
            return;
        }
        StartLevel(nextStage, nextLevel);
    }

    private void LoadSceneWithFallback(string preferredScene)
    {
        bool exists = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
            .Select(SceneUtility.GetScenePathByBuildIndex)
            .Any(p => p.EndsWith(preferredScene + ".unity", StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            SceneManager.LoadScene(preferredScene);
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
