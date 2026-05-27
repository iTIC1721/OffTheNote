using UnityEngine;

/// <summary>
/// 현재 선택된 월드/스테이지 진행 상태 관리 (싱글톤)
/// DontDestroyOnLoad로 씬 전환 시에도 유지
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    public WorldData CurrentWorld { get; private set; }
    public int CurrentStageIndex { get; private set; }

    // 씬 전환 후 WorldSelectManager가 소비할 포커싱 인덱스
    // -1 이면 요청 없음
    private int _pendingWorldFocusIndex = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartWorld(WorldData world, int stageIndex = 0)
    {
        CurrentWorld = world;
        CurrentStageIndex = stageIndex;
    }

    public string GetCurrentStageFile()
    {
        if (CurrentWorld == null ||
            CurrentStageIndex >= CurrentWorld.stageFiles.Count) return "";
        return CurrentWorld.stageFiles[CurrentStageIndex];
    }

    /// <summary>
    /// 현재 스테이지 클리어.
    /// 다음 스테이지가 있으면 인덱스 증가 후 true,
    /// 마지막 스테이지였으면 false 반환
    /// </summary>
    public bool ClearCurrentStage()
    {
        if (CurrentWorld == null) return false;

        int nextIndex = CurrentStageIndex + 1;

        // 해금 저장
        int unlocked = GetUnlockedCount(CurrentWorld.worldId);
        if (nextIndex >= unlocked)
            SaveUnlockedCount(CurrentWorld.worldId, nextIndex + 1);

        if (nextIndex < CurrentWorld.stageFiles.Count)
        {
            CurrentStageIndex = nextIndex;
            return true;
        }

        // 월드 마지막 스테이지 클리어
        // 다음 월드가 존재하면 포커싱 요청 예약
        WorldListData worldList = WorldSelectManager.Instance?.WorldList;
        if (worldList != null)
        {
            int currentWorldIndex = worldList.worlds.FindIndex(
                w => w.worldId == CurrentWorld.worldId);

            int nextWorldIndex = currentWorldIndex + 1;
            if (nextWorldIndex < worldList.worlds.Count)
                _pendingWorldFocusIndex = nextWorldIndex;
        }

        return false;
    }

    /// <summary>
    /// WorldSelectManager가 Start()에서 호출.
    /// 예약된 포커싱 인덱스를 반환하고 즉시 초기화합니다(1회 소비).
    /// 예약이 없으면 -1 반환.
    /// </summary>
    public int ConsumeNextWorldFocus()
    {
        int index = _pendingWorldFocusIndex;
        _pendingWorldFocusIndex = -1;
        return index;
    }

    public int GetUnlockedCount(string worldId)
    {
        // 이 월드가 열려있는지 먼저 확인
        if (!IsWorldUnlocked(worldId))
            return 0;

        return PlayerPrefs.GetInt($"unlock_{worldId}", 1);
    }

    public bool IsWorldUnlocked(string worldId)
    {
        WorldListData worldList = WorldSelectManager.Instance?.WorldList;
        if (worldList == null) return true;

        var worlds = worldList.worlds;
        int worldIndex = worlds.FindIndex(w => w.worldId == worldId);

        if (worldIndex <= 0) return true; // 첫 번째 월드는 항상 열려있음

        // 이전 월드를 모두 클리어했는지 확인
        var prevWorld = worlds[worldIndex - 1];
        int prevCleared = Mathf.Max(0, GetUnlockedCount(prevWorld.worldId) - 1);
        return prevCleared >= prevWorld.stageFiles.Count;
    }

    void SaveUnlockedCount(string worldId, int count)
    {
        PlayerPrefs.SetInt($"unlock_{worldId}", count);
        PlayerPrefs.Save();
    }
}