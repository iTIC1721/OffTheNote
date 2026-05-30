using UnityEngine;
using TMPro;

public class PauseUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageInfoText;

    void OnEnable()
    {
        if (ProgressManager.Instance == null) return;

        var world = ProgressManager.Instance.CurrentWorld;
        int stageIndex = ProgressManager.Instance.CurrentStageIndex;

        if (world != null)
            stageInfoText.text = $"{world.displayName} - Stage {stageIndex + 1}";
        else
            stageInfoText.text = "";
    }

    public void OnResumeButton() => PauseManager.Instance?.Resume();
    public void OnRestartButton() => PauseManager.Instance?.RestartStage();
    public void OnQuitButton() => PauseManager.Instance?.GoToStageSelect();
}