using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string stageSelectSceneName = "StageSelect";
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float fadeInDuration = 0.5f;

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGMWithFade("menu");
    }

    public void MoveToWorldSelect()
    {
        FadeManager.Instance.FadeAndLoadScene(
            sceneName: stageSelectSceneName,
            fadeOutDuration: fadeOutDuration,
            fadeInDuration: fadeInDuration
        );
    }
}
