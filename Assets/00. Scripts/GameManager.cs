using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 상태 관리 (싱글톤)
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject goalPanel;   // "도착!" 패널

    // MapPiece 동적 등록 지원
    private List<MapPiece> mapPieces = new List<MapPiece>();
    public MapPiece[] AllPieces => mapPieces.ToArray();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (goalPanel) goalPanel.SetActive(false);

        // 씬에 이미 있는 MapPiece 자동 등록
        foreach (var piece in FindObjectsByType<MapPiece>(FindObjectsSortMode.None))
            RegisterMapPiece(piece);
    }

    void Update()
    {
        // R키를 누르면 재시작
        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RegisterMapPiece(MapPiece piece)
    {
        if (piece != null && !mapPieces.Contains(piece))
            mapPieces.Add(piece);
    }

    public void ClearMapPieces()
    {
        mapPieces.Clear();
    }

    public void OnPlayerCrushed()
    {
        // 리스폰
        FindFirstObjectByType<PlayerController>().Respawn();
    }

    /// <summary>플레이어가 Goal 트리거에 진입했을 때 호출</summary>
    public void OnPlayerReachedGoal()
    {
        Debug.Log("Goal Reached!");
        if (goalPanel) goalPanel.SetActive(true);
        Time.timeScale = 0f;   // 일시 정지
    }

    /// <summary>UI 버튼 등에서 재시작 호출</summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}