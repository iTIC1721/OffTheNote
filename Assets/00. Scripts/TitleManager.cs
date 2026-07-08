using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string stageSelectSceneName = "StageSelect";

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGMWithFade("menu");
    }

    public void MoveToWorldSelect()
    {
        SceneManager.LoadScene(stageSelectSceneName);
    }
}
