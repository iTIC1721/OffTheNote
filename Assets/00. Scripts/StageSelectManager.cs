using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 스테이지 선택 씬 매니저
/// StreamingAssets 폴더의 JSON 파일 목록을 읽어 버튼으로 표시
/// </summary>
public class StageSelectManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform buttonContainer;   // 버튼들이 들어갈 부모 (ScrollView Content 등)
    [SerializeField] private GameObject stageButtonPrefab; // 버튼 프리팹
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Stage File Settings")]
    [SerializeField] private string stageFilePrefix = "stage_"; // 스테이지 파일 접두사
    [SerializeField] private string stageFileExtension = ".json";

    void Start()
    {
        LoadStageList();
    }

    void LoadStageList()
    {
        string[] files = Directory.GetFiles(
            Application.streamingAssetsPath,
            $"{stageFilePrefix}*{stageFileExtension}");

        // 파일명 정렬
        System.Array.Sort(files);

        foreach (var filePath in files)
        {
            string fileName = Path.GetFileName(filePath);

            // JSON에서 스테이지 이름 읽기
            string stageName = GetStageName(filePath, fileName);

            CreateStageButton(fileName, stageName);
        }
    }

    string GetStageName(string filePath, string fallback)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            StageData data = JsonUtility.FromJson<StageData>(json);
            return string.IsNullOrEmpty(data.stageName) ? fallback : data.stageName;
        }
        catch
        {
            return fallback;
        }
    }

    void CreateStageButton(string fileName, string stageName)
    {
        GameObject btn = Instantiate(stageButtonPrefab, buttonContainer);

        // 버튼 텍스트 설정
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = stageName;
        else
        {
            Text legacy = btn.GetComponentInChildren<Text>();
            if (legacy != null) legacy.text = stageName;
        }

        // 클릭 시 해당 스테이지 로드
        Button button = btn.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => SelectStage(fileName));
    }

    void SelectStage(string fileName)
    {
        // 게임 씬에서 읽어갈 파일명 저장
        PlayerPrefs.SetString(StageLoadKey.SelectedStageFile.ToString(), fileName);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }
}