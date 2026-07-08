using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public bool IsPaused { get; private set; }

    [SerializeField] private GameObject pausePanel;

    [SerializeField] private string selectSceneName = "StageSelect";
    [SerializeField] private float fadeOutDuration = 0.75f;
    [SerializeField] private float fadeInDuration = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle() { if (IsPaused) Resume(); else Pause(); }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;

        MapPieceSelector.Instance?.StopAllDragging();
        MapPieceSelector.Instance?.EnableDragging(false); // ← 시그니처 변경 필요

        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;

        MapPieceSelector.Instance?.EnableDragging(true);

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void RestartStage()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToStageSelect()
    {
        ProgressManager.Instance?.RequestWorldFocus();
        IsPaused = false;
        Time.timeScale = 1f;

        //SceneManager.LoadScene("StageSelect");
        FadeManager.Instance.FadeAndLoadScene(
            sceneName: selectSceneName,
            fadeOutDuration: fadeOutDuration,
            fadeInDuration: fadeInDuration
        );
    }
}