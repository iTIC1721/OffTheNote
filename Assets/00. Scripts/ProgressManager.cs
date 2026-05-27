using UnityEngine;

/// <summary>
/// 현재 선택된 월드/스테이지 진행 상태 관리 (싱글톤)
/// DontDestroyOnLoad로 씬 전환 시에도 유지
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private WorldListData worldList;
    public WorldListData WorldList => worldList;

    public WorldData CurrentWorld { get; private set; }
    public int CurrentStageIndex { get; private set; }

    // 씬 전환 후 WorldSelectManager가 소비할 포커싱 정보
    // index = -1 이면 요청 없음, wasLocked = 이 클리어로 처음 해금됐는지
    private int _pendingWorldFocusIndex = -1;
    private bool _pendingWorldWasLocked = false;

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

        // 월드 마지막 스테이지 클리어 판단
        bool willUnlockNextWorld = false;
        int pendingNextWorldIndex = -1;

        Debug.Log($"[PM] ClearCurrentStage: world={CurrentWorld.worldId}, stageIndex={CurrentStageIndex}, nextIndex={nextIndex}, totalStages={CurrentWorld.stageFiles.Count}");
        Debug.Log($"[PM] worldListData={(worldList == null ? "NULL" : worldList.name)}");

        if (nextIndex >= CurrentWorld.stageFiles.Count && worldList != null)
        {
            int currentWorldIndex = worldList.worlds.FindIndex(
                w => w.worldId == CurrentWorld.worldId);

            int nextWorldIndex = currentWorldIndex + 1;
            Debug.Log($"[PM] 마지막 스테이지 클리어. currentWorldIndex={currentWorldIndex}, nextWorldIndex={nextWorldIndex}, worldCount={worldList.worlds.Count}");
            if (nextWorldIndex < worldList.worlds.Count)
            {
                pendingNextWorldIndex = nextWorldIndex;
                // 저장 전 현재 상태로 해금 여부 확인
                willUnlockNextWorld = !IsWorldUnlocked(worldList.worlds[nextWorldIndex].worldId);
                Debug.Log($"[PM] 다음 월드={worldList.worlds[nextWorldIndex].worldId}, alreadyUnlocked={!willUnlockNextWorld}, willUnlock={willUnlockNextWorld}");
            }
        }

        // 해금 저장
        int unlocked = GetUnlockedCount(CurrentWorld.worldId);
        if (nextIndex >= unlocked)
            SaveUnlockedCount(CurrentWorld.worldId, nextIndex + 1);

        if (nextIndex < CurrentWorld.stageFiles.Count)
        {
            CurrentStageIndex = nextIndex;
            return true;
        }

        // 포커싱 예약
        Debug.Log($"[PM] 포커싱 예약: pendingNextWorldIndex={pendingNextWorldIndex}, wasLocked={willUnlockNextWorld}");
        if (pendingNextWorldIndex >= 0)
        {
            _pendingWorldFocusIndex = pendingNextWorldIndex;
            _pendingWorldWasLocked = willUnlockNextWorld;
        }

        return false;
    }

    /// <summary>
    /// WorldSelectManager가 Start()에서 호출.
    /// 예약된 포커싱 인덱스와 해금 여부를 반환하고 즉시 초기화합니다(1회 소비).
    /// 예약이 없으면 index = -1 반환.
    /// </summary>
    /// <param name="wasLocked">이번 클리어로 처음 해금된 월드이면 true</param>
    public int ConsumeNextWorldFocus(out bool wasLocked)
    {
        int index = _pendingWorldFocusIndex;
        wasLocked = _pendingWorldWasLocked;

        Debug.Log($"[PM] ConsumeNextWorldFocus: index={index}, wasLocked={wasLocked}");

        _pendingWorldFocusIndex = -1;
        _pendingWorldWasLocked = false;
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

    /// <summary>
    /// 모든 월드/스테이지 해금 데이터를 초기화합니다.
    /// </summary>
    public void ResetAllProgress()
    {
        if (worldList != null)
        {
            foreach (var world in worldList.worlds)
                PlayerPrefs.DeleteKey($"unlock_{world.worldId}");
        }
        else
        {
            // WorldSelectManager가 없는 씬(GameScene 등)에서는 전체 삭제
            PlayerPrefs.DeleteAll();
        }

        PlayerPrefs.Save();
        Debug.Log("[ProgressManager] 모든 진행 데이터가 초기화되었습니다.");
    }
}