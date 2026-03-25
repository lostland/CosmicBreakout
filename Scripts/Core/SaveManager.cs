using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 게임 진행 데이터를 JSON으로 로컬 저장한다.
/// 코인, 해금 스테이지, 각 스테이지별 레벨 진행도, 업그레이드 데이터를 포함한다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData Data { get; private set; }
    private string _savePath;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _savePath = Path.Combine(Application.persistentDataPath, "save.json");
        Load();
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(_savePath, json);
    }

    public void Load()
    {
        if (File.Exists(_savePath))
        {
            string json = File.ReadAllText(_savePath);
            Data = JsonUtility.FromJson<SaveData>(json);
            if (Data == null)
            {
                Data = new SaveData();
                Save();
                return;
            }
            Data.Normalize();
        }
        else
        {
            Data = new SaveData();
            Save();
        }
    }

    public void DeleteAll()
    {
        Data = new SaveData();
        Save();
    }
}

[Serializable]
public class SaveData
{
    public long TotalCoins      = 0;
    public int  UnlockedStages  = 0;   // 0 = 지구만 해금
    // 각 큰 스테이지(0~4)에서 클리어한 레벨 수 (0~5)
    public int[] StageLevelProgress = new int[5] { 0, 0, 0, 0, 0 };

    // 업그레이드 레벨 (0 = 미구매)
    public int UpgradePaddleSize   = 0;  // 최대 3
    public int UpgradeBallSpeed    = 0;  // 최대 3
    public int UpgradeStartItems   = 0;  // 최대 3: 시작 시 아이템 슬롯 추가
    public int UpgradeCoinMagnet   = 0;  // 최대 3: 코인 자동 수집 범위
    public int UpgradeExtraLife    = 0;  // 최대 2: 시작 라이프 +1

    // 설정
    public bool SoundEnabled     = true;
    public bool VibrationEnabled = true;
    public float MusicVolume     = 0.7f;
    public float SfxVolume       = 1.0f;

    // 통계
    public long TotalBricksDestroyed = 0;
    public long TotalCoinsEver       = 0;
    public int  TotalLevelsCleared   = 0;
    public int  MaxCombo             = 0;

    public int GetLevelProgress(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= StageLevelProgress.Length) return 0;
        return StageLevelProgress[stageIndex];
    }

    public void SetLevelProgress(int stageIndex, int levelCount)
    {
        if (stageIndex < 0 || stageIndex >= StageLevelProgress.Length) return;
        StageLevelProgress[stageIndex] = Mathf.Max(StageLevelProgress[stageIndex], levelCount);
    }

    public bool IsStageUnlocked(int stageIndex) => stageIndex <= UnlockedStages;
    public bool IsLevelUnlocked(int stageIndex, int levelIndex)
    {
        if (!IsStageUnlocked(stageIndex)) return false;
        return levelIndex <= StageLevelProgress[stageIndex];
    }

    public void Normalize()
    {
        if (StageLevelProgress == null || StageLevelProgress.Length != 5)
        {
            int[] normalized = new int[5] { 0, 0, 0, 0, 0 };
            if (StageLevelProgress != null)
            {
                int copyLen = Mathf.Min(StageLevelProgress.Length, normalized.Length);
                Array.Copy(StageLevelProgress, normalized, copyLen);
            }
            StageLevelProgress = normalized;
        }

        UnlockedStages = Mathf.Clamp(UnlockedStages, 0, StageLevelProgress.Length - 1);
        for (int i = 0; i < StageLevelProgress.Length; i++)
            StageLevelProgress[i] = Mathf.Clamp(StageLevelProgress[i], 0, 5);
    }
}
