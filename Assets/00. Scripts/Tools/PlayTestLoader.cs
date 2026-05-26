using UnityEngine;

/// <summary>
/// 테스트 씬 시작 시 Stage Editor에서 저장한 임시 스테이지를 자동 로드
/// 테스트 씬의 StageLoader 오브젝트에 함께 부착
/// </summary>
[RequireComponent(typeof(StageLoader))]
public class PlayTestLoader : MonoBehaviour
{
    void Awake()
    {
        string file = PlayerPrefs.GetString("PlayTestStageFile", "");
        if (!string.IsNullOrEmpty(file))
        {
            GetComponent<StageLoader>().LoadStage(file);
            // 한 번 쓰고 지움
            PlayerPrefs.DeleteKey("PlayTestStageFile");
        }
    }
}