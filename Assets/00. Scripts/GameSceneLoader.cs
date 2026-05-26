using UnityEngine;

/// <summary>
/// 게임 씬 진입 시 PlayerPrefs에서 선택된 스테이지 파일명을 읽어 로드
/// StageLoader와 함께 같은 오브젝트에 부착
/// </summary>
[RequireComponent(typeof(StageLoader))]
public class GameSceneLoader : MonoBehaviour
{
    [SerializeField] private StageLoadKey key;
    [SerializeField] private string fallbackStageFile = "fallback.json";

    void Awake()
    {
        string file = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetCurrentStageFile()
            : PlayerPrefs.GetString(key.ToString(), fallbackStageFile);

        if (!string.IsNullOrEmpty(file))
            GetComponent<StageLoader>().LoadStage(file);

        // 사용 후 삭제
        //PlayerPrefs.DeleteKey(key.ToString());
    }
}

public enum StageLoadKey
{
    SelectedStageFile,
    PlayTestStageFile
}